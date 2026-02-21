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
        private const int MaxMemoryMessages = 10;

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

        string systemPromptCache = "";

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

            if (_llmPane != null)
            {
                _llmPane.UserQuery += OnUserLLMStreamQuery;
            }
        }

        private void OnUserLLMStreamQuery(object? s, LLM.QueryEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Query))
                _ = QueryLLMStreamingAsync(e.Query);
        }

        private void OnUserLLMQuery(object? s, LLM.QueryEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Query))
                _ = QueryLLMAsync(e.Query);
        }

        private void UpdateSymbolTree()
        {
            if (_symbolTree == null) return;

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
            if (_stringsControl == null) return;

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
            if (_graphControl == null || _core.CFG == null || _core.CFG.Blocks.Count == 0)
                return;

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

        public void ShowFunctionCFG(ulong addr)
        {
            var f = _core.FindFunctionAtAddress(addr);
            if (f?.CFG != null && _graphControl != null)
                _graphControl.DisplayCFG(f.CFG);
        }

        public async Task QueryLLMStreamingAsync(string q, CancellationToken ct = default)
        {
            if (_llmPane == null || _llmClient == null || _core == null)
                return;

            var sw = Stopwatch.StartNew();

            try
            {
                if (systemPromptCache == "")
                {
                    bool sendPE = true, sendBytes = true, sendDisasm = true;

                    var cg = new BinaryContextGenerator(_core);
                    var ctx = cg.GenerateContext(new SystemContextData
                    {
                        SendPE = sendPE,
                        SendBytes = sendBytes,
                        SendDisasm = sendDisasm,
                        DisasmView = _disasmView,
                        HexEditor = _hexEditor,
                        PEInfoControl = _peInfoControl
                    });

                    systemPromptCache = BinaryContextGenerator.GenerateSystemPrompt(ctx);
                }

                var msgs = new List<object>
                {
                    new { role = "system", content = systemPromptCache }
                };

                msgs.AddRange(_chatHistory);
                msgs.Add(new { role = "user", content = q });

                var full = new StringBuilder();

                _llmPane.StartStreamingResponse();

                await _llmClient.StreamChatAsync(
                    msgs,
                    c =>
                    {
                        full.Append(c);
                        _llmPane.AppendStreamedChunk(c);
                    },
                    ct);

                _llmPane.FinishStreamingResponse();

                string resp = full.ToString();

                _chatHistory.Add(new { role = "user", content = q });
                _chatHistory.Add(new { role = "assistant", content = resp });

                while (_chatHistory.Count > MaxMemoryMessages)
                    _chatHistory.RemoveAt(0);

                sw.Stop();

                _aiLogs?.SaveLogEntry(new AILogEntry
                {
                    Operation = "LLMChat",
                    Prompt = q,
                    AIOutput = resp,
                    Status = "Success",
                    DurationMs = sw.ElapsedMilliseconds
                });

                _llmPane.DisplayResponse(resp);
            }
            catch (Exception ex)
            {
                sw.Stop();

                _aiLogs?.SaveLogEntry(new AILogEntry
                {
                    Operation = "LLMChat",
                    Prompt = q,
                    AIOutput = $"Error: {ex.Message}",
                    Status = "Error",
                    DurationMs = sw.ElapsedMilliseconds
                });

                _llmPane.DisplayError($"Error: {ex.Message}");
            }
        }

        public async Task QueryLLMAsync(string q, CancellationToken ct = default)
        {
            if (_llmPane == null || _llmClient == null || _core == null)
                return;

            var sw = Stopwatch.StartNew();

            try
            {
                if (systemPromptCache == "")
                {
                    var cg = new BinaryContextGenerator(_core);
                    var ctx = cg.GenerateContext(new SystemContextData
                    {
                        SendPE = true,
                        SendBytes = true,
                        SendDisasm = true,
                        DisasmView = _disasmView,
                        HexEditor = _hexEditor,
                        PEInfoControl = _peInfoControl
                    });

                    systemPromptCache = BinaryContextGenerator.GenerateSystemPrompt(ctx);
                }

                string resp = await _llmClient.ChatAsync(q, systemPromptCache, ct);

                sw.Stop();

                _aiLogs?.SaveLogEntry(new AILogEntry
                {
                    Operation = "LLMChat",
                    Prompt = q,
                    AIOutput = resp,
                    Status = "Success",
                    DurationMs = sw.ElapsedMilliseconds
                });

                _llmPane.DisplayResponse(resp);
            }
            catch (Exception ex)
            {
                sw.Stop();

                _aiLogs?.SaveLogEntry(new AILogEntry
                {
                    Operation = "LLMChat",
                    Prompt = q,
                    AIOutput = $"Error: {ex.Message}",
                    Status = "Error",
                    DurationMs = sw.ElapsedMilliseconds
                });

                _llmPane.DisplayError($"Error: {ex.Message}");
            }
        }

        public async Task RunAnalysisAsync()
        {
            _analysisCts?.Cancel();
            _analysisCts = new CancellationTokenSource();
            var t = _analysisCts.Token;

            AnalysisStarted?.Invoke();

            try
            {
                await Task.Run(() => _core.RunAnalysis(), t);

                if (!t.IsCancellationRequested)
                {
                    UpdateSymbolTree();
                    UpdateGraphView();
                    UpdateStringsView();

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