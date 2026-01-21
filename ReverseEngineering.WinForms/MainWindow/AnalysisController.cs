using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ReverseEngineering.Core;
using ReverseEngineering.Core.AILogs;
using ReverseEngineering.Core.LLM;
using ReverseEngineering.WinForms.GraphView;
using ReverseEngineering.WinForms.SymbolView;
using ReverseEngineering.WinForms.StringView;
using ReverseEngineering.WinForms.LLM;

namespace ReverseEngineering.WinForms.MainWindow
{
    /// <summary>
    /// Controller to manage analysis views and update them when analysis completes.
    /// Integrates LM Studio for AI-powered analysis.
    /// </summary>
    public class AnalysisController
    {
        private readonly CoreEngine _core;
        private readonly SymbolTreeControl? _symbolTree;
        private readonly GraphControl? _graphControl;
        private readonly StringsControl? _stringsControl;
        private readonly LocalLLMClient? _llmClient;
        private readonly LLMPane? _llmPane;
        private readonly LLMAnalyzer? _llmAnalyzer;
        private readonly AILogsManager? _aiLogs;
        private CancellationTokenSource? _analysisCts;

        public event Action? AnalysisStarted;
        public event Action? AnalysisCompleted;

        public AnalysisController(
            CoreEngine core,
            SymbolTreeControl? symbolTree = null,
            GraphControl? graphControl = null,
            LocalLLMClient? llmClient = null,
            LLMPane? llmPane = null,
            AILogsManager? aiLogs = null,
            StringsControl? stringsControl = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _symbolTree = symbolTree;
            _graphControl = graphControl;
            _stringsControl = stringsControl;
            _llmClient = llmClient;
            _llmPane = llmPane;
            _llmAnalyzer = llmClient != null ? new LLMAnalyzer(llmClient, core) : null;
            _aiLogs = aiLogs;

            // Wire up LLM chat interface
            if (_llmPane != null)
            {
                _llmPane.UserQuery += OnUserLLMQuery;
            }
        }

        // ---------------------------------------------------------
        //  LLM CHAT EVENT HANDLER
        // ---------------------------------------------------------
        private async void OnUserLLMQuery(object? sender, QueryEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Query))
                return;

            await QueryLLMAsync(e.Query);
        }

        // ---------------------------------------------------------
        //  ANALYSIS EXECUTION
        // ---------------------------------------------------------
        /// <summary>
        /// Run analysis asynchronously.
        /// </summary>
        public async Task RunAnalysisAsync()
        {
            _analysisCts?.Cancel();
            _analysisCts = new CancellationTokenSource();
            var token = _analysisCts.Token;

            AnalysisStarted?.Invoke();

            try
            {
                await Task.Run(() => _core.RunAnalysis(), token);

                // Update UI
                if (!token.IsCancellationRequested)
                {
                    Logger.Info("UI", "Updating views...");
                    UpdateViews();
                    Logger.Info("UI", "✓ Views updated and displayed");
                    AnalysisCompleted?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Analysis", "Analysis was cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error("Analysis", "Analysis failed", ex);
            }
        }

        /// <summary>
        /// Cancel ongoing analysis.
        /// </summary>
        public void CancelAnalysis()
        {
            _analysisCts?.Cancel();
        }

        // ---------------------------------------------------------
        //  VIEW UPDATES
        // ---------------------------------------------------------
        private void UpdateViews()
        {
            UpdateSymbolTree();
            UpdateGraphView();
            UpdateStringsView();
        }

        private void UpdateSymbolTree()
        {
            if (_symbolTree == null)
                return;

            try
            {
                _symbolTree.PopulateFromAnalysis();
                Logger.Info("UI", $"  → Symbol Tree: {_core.Symbols.Count} symbols");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisController", "Failed to update symbol tree", ex);
            }
        }

        private void UpdateStringsView()
        {
            if (_stringsControl == null)
                return;

            try
            {
                _stringsControl.PopulateFromAnalysis();
                Logger.Info("UI", $"  → Strings: {_core.Strings.Count} strings");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisController", "Failed to update strings view", ex);
            }
        }

        private void UpdateGraphView()
        {
            if (_graphControl == null)
            {
                Logger.Debug("UI", "GraphControl is null");
                return;
            }
            
            if (_core.CFG == null)
            {
                Logger.Debug("UI", "CFG is null");
                return;
            }
            
            if (_core.CFG.Blocks.Count == 0)
            {
                Logger.Debug("UI", "CFG has no blocks");
                return;
            }

            try
            {
                _graphControl.DisplayCFG(_core.CFG);
                Logger.Info("UI", $"  → CFG: {_core.CFG.Blocks.Count} blocks, {_core.Functions.Count} functions");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisController", "Failed to update graph", ex);
            }
        }

        // ---------------------------------------------------------
        //  MANUAL NAVIGATION
        // ---------------------------------------------------------
        /// <summary>
        /// Display CFG for a specific function.
        /// </summary>
        public void ShowFunctionCFG(ulong functionAddress)
        {
            var func = _core.FindFunctionAtAddress(functionAddress);
            if (func?.CFG != null && _graphControl != null)
            {
                _graphControl.DisplayCFG(func.CFG);
            }
        }

        // ---------------------------------------------------------
        //  LLM CHAT (Interactive RE Analysis - Master Level Tool)
        // ---------------------------------------------------------
        /// <summary>
        /// Send a user query to the LLM with full binary context and stream response.
        /// The LLM can read the binary and make patches upon request.
        /// Response is displayed in real-time as chunks arrive.
        /// </summary>
        public async Task QueryLLMAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmAnalyzer == null)
                return;

            var timer = Stopwatch.StartNew();
            _llmPane.SetAnalyzing("Waiting for response...");

            try
            {
                var response = await _llmAnalyzer.QueryWithContextAsync(userQuery, cancellationToken);
                timer.Stop();

                // Log operation
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "LLMChat",
                        Prompt = userQuery,
                        AIOutput = response,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayResponse(response);
            }
            catch (Exception ex)
            {
                timer.Stop();

                // Log failure
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "LLMChat",
                        Prompt = userQuery,
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayError($"Error: {ex.Message}");
            }
        }
    }
}
