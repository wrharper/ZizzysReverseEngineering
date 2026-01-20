using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ReverseEngineering.Core;
using ReverseEngineering.Core.AILogs;
using ReverseEngineering.Core.LLM;
using ReverseEngineering.WinForms.GraphView;
using ReverseEngineering.WinForms.SymbolView;
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
            AILogsManager? aiLogs = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _symbolTree = symbolTree;
            _graphControl = graphControl;
            _llmClient = llmClient;
            _llmPane = llmPane;
            _llmAnalyzer = llmClient != null ? new LLMAnalyzer(llmClient) : null;
            _aiLogs = aiLogs;
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
                    UpdateViews();
                    AnalysisCompleted?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // Analysis was cancelled
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
        }

        private void UpdateSymbolTree()
        {
            if (_symbolTree == null)
                return;

            try
            {
                _symbolTree.PopulateFromAnalysis();
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisController", "Failed to update symbol tree", ex);
            }
        }

        private void UpdateGraphView()
        {
            if (_graphControl == null || _core.CFG == null)
                return;

            try
            {
                _graphControl.DisplayCFG(_core.CFG);
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisController", "Failed to update graph view", ex);
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
        //  LLM ANALYSIS (LM Studio Integration)
        // ---------------------------------------------------------
        /// <summary>
        /// Explain a single instruction using LM Studio.
        /// </summary>
        public async Task ExplainInstructionAsync(int instructionIndex, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmAnalyzer == null)
                return;

            if (instructionIndex < 0 || instructionIndex >= _core.Disassembly.Count)
                return;

            var instruction = _core.Disassembly[instructionIndex];
            var timer = Stopwatch.StartNew();

            _llmPane.SetAnalyzing($"Explaining {instruction.Mnemonic}");

            try
            {
                var prompt = $"Explain this x86-64 instruction: {instruction.Mnemonic} {instruction.OpStr}";
                var explanation = await _llmAnalyzer.ExplainInstructionAsync(instruction, cancellationToken);
                
                timer.Stop();

                // Log operation
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "InstructionExplanation",
                        Prompt = prompt,
                        AIOutput = explanation,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayResult("Instruction Explanation", explanation);
            }
            catch (Exception ex)
            {
                timer.Stop();

                // Log failure
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "InstructionExplanation",
                        Prompt = $"Explain instruction at {instruction.Address:X8}",
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayError($"Failed to explain instruction: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate pseudocode for a function using LM Studio.
        /// </summary>
        public async Task GeneratePseudocodeAsync(ulong functionAddress, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmAnalyzer == null)
                return;

            var func = _core.FindFunctionAtAddress(functionAddress);
            if (func == null)
            {
                _llmPane.DisplayError("Function not found");
                return;
            }

            var timer = Stopwatch.StartNew();
            _llmPane.SetAnalyzing($"Generating pseudocode for {func.Name}");

            try
            {
                // Get first 20 instructions of function for analysis
                var startIdx = _core.OffsetToInstructionIndex(_core.AddressToOffset(functionAddress));
                if (startIdx < 0) return;

                var instructions = _core.Disassembly.GetRange(startIdx, Math.Min(20, _core.Disassembly.Count - startIdx));
                var pseudocode = await _llmAnalyzer.GeneratePseudocodeAsync(instructions, functionAddress, cancellationToken);
                
                timer.Stop();

                // Log operation
                if (_aiLogs != null)
                {
                    var prompt = $"Generate pseudocode for {func.Name} (0x{functionAddress:X8})";
                    var logEntry = new AILogEntry
                    {
                        Operation = "PseudocodeGeneration",
                        Prompt = prompt,
                        AIOutput = pseudocode,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayResult($"Pseudocode: {func.Name}", pseudocode);
            }
            catch (Exception ex)
            {
                timer.Stop();

                // Log failure
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "PseudocodeGeneration",
                        Prompt = $"Generate pseudocode for {func.Name}",
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayError($"Failed to generate pseudocode: {ex.Message}");
            }
        }

        /// <summary>
        /// Identify function signature using LM Studio.
        /// </summary>
        public async Task IdentifyFunctionSignatureAsync(ulong functionAddress, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmAnalyzer == null)
                return;

            var func = _core.FindFunctionAtAddress(functionAddress);
            if (func == null)
            {
                _llmPane.DisplayError("Function not found");
                return;
            }

            var timer = Stopwatch.StartNew();
            _llmPane.SetAnalyzing($"Analyzing signature for {func.Name}");

            try
            {
                var startIdx = _core.OffsetToInstructionIndex(_core.AddressToOffset(functionAddress));
                if (startIdx < 0) return;

                var instructions = _core.Disassembly.GetRange(startIdx, Math.Min(10, _core.Disassembly.Count - startIdx));
                var signature = await _llmAnalyzer.IdentifyFunctionSignatureAsync(instructions, functionAddress, cancellationToken);
                
                timer.Stop();

                // Log operation
                if (_aiLogs != null)
                {
                    var prompt = $"Identify function signature for {func.Name} (0x{functionAddress:X8})";
                    var logEntry = new AILogEntry
                    {
                        Operation = "FunctionSignatureIdentification",
                        Prompt = prompt,
                        AIOutput = signature,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayResult($"Function Signature: {func.Name}", signature);
            }
            catch (Exception ex)
            {
                timer.Stop();

                // Log failure
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "FunctionSignatureIdentification",
                        Prompt = $"Identify signature for {func.Name}",
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayError($"Failed to identify signature: {ex.Message}");
            }
        }

        /// <summary>
        /// Detect patterns in a function using LM Studio.
        /// </summary>
        public async Task DetectPatternAsync(ulong functionAddress, CancellationToken cancellationToken = default)
        {
            if (_llmPane == null || _llmAnalyzer == null)
                return;

            var func = _core.FindFunctionAtAddress(functionAddress);
            if (func == null)
            {
                _llmPane.DisplayError("Function not found");
                return;
            }

            var timer = Stopwatch.StartNew();
            _llmPane.SetAnalyzing($"Detecting patterns in {func.Name}");

            try
            {
                var startIdx = _core.OffsetToInstructionIndex(_core.AddressToOffset(functionAddress));
                if (startIdx < 0) return;

                var instructions = _core.Disassembly.GetRange(startIdx, Math.Min(30, _core.Disassembly.Count - startIdx));
                var pattern = await _llmAnalyzer.DetectPatternAsync(instructions, functionAddress, cancellationToken);
                
                timer.Stop();

                // Log operation
                if (_aiLogs != null)
                {
                    var prompt = $"Detect patterns in {func.Name} (0x{functionAddress:X8})";
                    var logEntry = new AILogEntry
                    {
                        Operation = "PatternDetection",
                        Prompt = prompt,
                        AIOutput = pattern,
                        Status = "Success",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayResult($"Detected Patterns: {func.Name}", pattern);
            }
            catch (Exception ex)
            {
                timer.Stop();

                // Log failure
                if (_aiLogs != null)
                {
                    var logEntry = new AILogEntry
                    {
                        Operation = "PatternDetection",
                        Prompt = $"Detect patterns in {func.Name}",
                        AIOutput = $"Error: {ex.Message}",
                        Status = "Error",
                        DurationMs = timer.ElapsedMilliseconds
                    };
                    _aiLogs.SaveLogEntry(logEntry);
                }

                _llmPane.DisplayError($"Failed to detect patterns: {ex.Message}");
            }
        }

        /// <summary>
        /// Get selected instruction index from disassembly controller (placeholder - implement in DisassemblyController).
        /// </summary>
        private int GetSelectedInstructionIndex() => -1;

        /// <summary>
        /// Get selected instruction address from disassembly controller (placeholder - implement in DisassemblyController).
        /// </summary>
        private ulong GetSelectedInstructionAddress() => 0;
    }
}
