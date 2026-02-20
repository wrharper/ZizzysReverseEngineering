using ReverseEngineering.Core;
using ReverseEngineering.Core.AILogs;
using ReverseEngineering.Core.LLM;
using System.Diagnostics;
using System.Text;

namespace ReverseEngineering.WinForms.MainWindow
{

    public class AnalysisController
    {
        private readonly List<object> _chatHistory = new();
        private const int MaxMemoryMessages = 10; // keep last 10 turns
        private readonly CoreEngine _core;
        private readonly SymbolView.SymbolTreeControl? _symbolTree;
        private readonly GraphView.GraphControl? _graphControl;
        private readonly StringView.StringsControl? _stringsControl;
        private readonly LocalLLMClient? _llmClient;
        private readonly LLM.LLMPane? _llmPane;
        private readonly AILogsManager? _aiLogs;
        private CancellationTokenSource? _analysisCts;
        HexEditor.HexEditorControl? _hexEditor;
        DisassemblyControl? _disasmView;
        PEInfoControl? _peInfoControl;

        public event Action? AnalysisStarted;
        public event Action? AnalysisCompleted;

        public AnalysisController(
            CoreEngine core,
            SymbolView.SymbolTreeControl? symbolTree = null,
            GraphView.GraphControl? graphControl = null,
            LocalLLMClient? llmClient = null,
            LLM.LLMPane? llmPane = null,
            AILogsManager? aiLogs = null,
            StringView.StringsControl? stringsControl = null,
            HexEditor.HexEditorControl? hexEditor = null,
            DisassemblyControl? disasmView = null,
            PEInfoControl? peInfoControl = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _symbolTree = symbolTree;
            _graphControl = graphControl;
            _stringsControl = stringsControl;
            _llmClient = llmClient;
            _llmPane = llmPane;
            _aiLogs = aiLogs;
            _hexEditor = hexEditor;
            _disasmView = disasmView;
            _peInfoControl = peInfoControl;

            // Wire up LLM chat interface
            if (_llmPane != null)
            {
                //_llmPane.UserQuery += OnUserLLMQuery;
                _llmPane.UserQuery += OnUserLLMStreamQuery;
            }
        }

        private void OnUserLLMStreamQuery(object? sender, LLM.QueryEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Query))
                return;
            _ = QueryLLMStreamingAsync(e.Query);
        }
        private void OnUserLLMQuery(object? sender, LLM.QueryEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Query))
                return;
            _ = QueryLLMAsync(e.Query);
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
        string systemPromptCache = "";
        public async Task QueryLLMStreamingAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmClient == null || _core == null)
                return;

            var timer = Stopwatch.StartNew();
            Logger.Info("AI", $"[LLM] Query started: '{userQuery}'");

            try
            {
                // Build system prompt if needed
                if (systemPromptCache == "")
                {
                    bool sendAll = false, sendPE = false, sendBytes = false, sendDisasm = false;

                    var mainMenu = Application.OpenForms[0]?.Controls.OfType<MenuStrip>().FirstOrDefault();
                    var menuController = mainMenu?.FindForm()?.Controls.OfType<MainMenuController>().FirstOrDefault();
                    MainMenuController? sendMenuController = _llmPane.Parent?.Controls.OfType<MainMenuController>().FirstOrDefault()
                        ?? _llmPane.FindForm()?.Controls.OfType<MainMenuController>().FirstOrDefault();

                    if (sendMenuController != null)
                    {
                        sendAll = sendMenuController.SendAllEnabled;
                        sendPE = sendMenuController.SendPEInfoEnabled;
                        sendBytes = sendMenuController.SendBytesEnabled;
                        sendDisasm = sendMenuController.SendDisassemblyEnabled;
                    }
                    else
                    {
                        sendAll = false;
                        sendPE = sendBytes = sendDisasm = true;
                    }

                    var contextGenerator = new ReverseEngineering.Core.LLM.BinaryContextGenerator(_core);
                    var context = contextGenerator.GenerateContext(new SystemContextData
                    {
                        SendPE = sendPE,
                        SendBytes = sendBytes,
                        SendDisasm = sendDisasm,
                        DisasmView = _disasmView,
                        HexEditor = _hexEditor,
                        PEInfoControl = _peInfoControl
                    });

                    systemPromptCache = BinaryContextGenerator.GenerateSystemPrompt(context);
                }

                // -------------------------
                // BUILD MESSAGE LIST (MEMORY)
                // -------------------------
                var messages = new List<object>
        {
            new { role = "system", content = systemPromptCache }
        };

                // Add rolling memory
                messages.AddRange(_chatHistory);

                // Add the new user message
                messages.Add(new { role = "user", content = userQuery });

                // -------------------------
                // STREAMING MODE
                // -------------------------
                var fullResponse = new StringBuilder();

                // Tell UI we are starting a streamed response
                _llmPane.StartStreamingResponse();

                await _llmClient.StreamChatAsync(
                    messages,
                    chunk =>
                    {
                        fullResponse.Append(chunk);
                        _llmPane.AppendStreamedChunk(chunk);
                    },
                    cancellationToken
                );

                // Finish UI streaming
                _llmPane.FinishStreamingResponse();

                string aiResponse = fullResponse.ToString();

                // -------------------------
                // UPDATE MEMORY
                // -------------------------
                _chatHistory.Add(new { role = "user", content = userQuery });
                _chatHistory.Add(new { role = "assistant", content = aiResponse });

                // Trim memory
                while (_chatHistory.Count > MaxMemoryMessages)
                    _chatHistory.RemoveAt(0);

                // -------------------------
                // LOGGING
                // -------------------------
                timer.Stop();
                Logger.Info("AI", $"[LLM] Query finished in {timer.ElapsedMilliseconds} ms");

                if (_aiLogs != null)
                {
                    _aiLogs.SaveLogEntry(new AILogEntry
                    {
                        Operation = "LLMChat",
                        Prompt = userQuery,
                        AIOutput = aiResponse,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    });
                }

                // You already streamed it, but DisplayResponse adds the final newline + resets UI
                _llmPane.DisplayResponse(aiResponse);
            }
            catch (Exception ex)
            {
                timer.Stop();

                if (_aiLogs != null)
                {
                    _aiLogs.SaveLogEntry(new AILogEntry
                    {
                        Operation = "LLMChat",
                        Prompt = userQuery,
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    });
                }

                _llmPane.DisplayError($"Error: {ex.Message}");
            }
        }
        public async Task QueryLLMAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmClient == null || _core == null)
                return;
            var timer = Stopwatch.StartNew();
            Logger.Info("AI", $"[LLM] Query started: '{userQuery}'");

            try
            {

                // --- Build user prompt (only visible UI data, not full context) ---
                var mainMenu = Application.OpenForms[0]?.Controls.OfType<MenuStrip>().FirstOrDefault();
                var menuController = mainMenu?.FindForm()?.Controls.OfType<MainMenuController>().FirstOrDefault();
                MainMenuController? sendMenuController = null;
                if (_llmPane.Parent != null)
                {
                    sendMenuController = _llmPane.Parent.Controls.OfType<MainMenuController>().FirstOrDefault();
                }
                if (sendMenuController == null && _llmPane.FindForm() is Form form)
                {
                    sendMenuController = form.Controls.OfType<MainMenuController>().FirstOrDefault();
                }

                if (systemPromptCache == "")
                {
                    // If we can get the menu controller, use its state
                    bool sendAll = false, sendPE = false, sendBytes = false, sendDisasm = false;
                    if (sendMenuController != null)
                    {
                        sendAll = sendMenuController.SendAllEnabled;
                        sendPE = sendMenuController.SendPEInfoEnabled;
                        sendBytes = sendMenuController.SendBytesEnabled;
                        sendDisasm = sendMenuController.SendDisassemblyEnabled;
                    }
                    else
                    {
                        // Default: all on
                        sendAll = false;
                        sendPE = sendBytes = sendDisasm = true;
                    }

                    // --- Build system prompt from current binary context ---
                    var contextGenerator = new ReverseEngineering.Core.LLM.BinaryContextGenerator(_core);
                    var context = contextGenerator.GenerateContext(new SystemContextData
                    { 
                        SendPE = sendPE,
                        SendBytes = sendBytes,
                        SendDisasm = sendDisasm,
                        DisasmView = _disasmView,
                        HexEditor = _hexEditor,
                        PEInfoControl = _peInfoControl
                    });
                    systemPromptCache = BinaryContextGenerator.GenerateSystemPrompt(context);
                }

                // --- Send to LLM as chat (system prompt + user prompt) ---
                string aiResponse = await _llmClient.ChatAsync(userQuery, systemPromptCache, cancellationToken);

                timer.Stop();
                Logger.Info("AI", $"[LLM] Query finished in {timer.ElapsedMilliseconds} ms");
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "LLMChat",
                        Prompt = userQuery,
                        AIOutput = aiResponse,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }
                _llmPane.DisplayResponse(aiResponse);
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
                    UpdateSymbolTree();
                    UpdateGraphView();
                    UpdateStringsView();
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
    }
}
