using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReverseEngineering.Core;
using ReverseEngineering.Core.Analysis;

#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Provides curated prompts and response parsing for RE analysis with LM Studio
    /// Works with binary context sessions for full awareness of analysis state
    /// </summary>
    public class LLMAnalyzer
    {
        private readonly LocalLLMClient _client;
        private readonly CoreEngine? _engine;
        private readonly BinaryContextGenerator? _contextGenerator;
        private LLMSession? _currentSession;

        private const string RE_SYSTEM_PROMPT = "You are an expert in reverse engineering x86/x64 assembly code. " +
            "Provide concise, technical analysis focusing on function behavior, data flow, and control flow. " +
            "Keep responses brief (1-3 sentences).";

        public LLMAnalyzer(LocalLLMClient client, CoreEngine? engine = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _engine = engine;
            _contextGenerator = engine != null ? new BinaryContextGenerator(engine) : null!;
        }

        // ---------------------------------------------------------
        //  SESSION MANAGEMENT
        // ---------------------------------------------------------

        /// <summary>
        /// Start new LLM session with binary context
        /// </summary>
        public LLMSession CreateSession()
        {
            if (_engine == null || _contextGenerator == null)
                throw new InvalidOperationException("Engine not available for context-aware analysis. Initialize with CoreEngine.");

            _currentSession = new LLMSession(_client, _contextGenerator);
            _currentSession.UpdateContext();
            return _currentSession;
        }

        /// <summary>
        /// Get current session (or create if none exists)
        /// </summary>
        public LLMSession GetOrCreateSession()
        {
            if (_currentSession == null)
                return CreateSession();
            
            _currentSession.UpdateContext(); // Ensure context is current
            return _currentSession;
        }

        /// <summary>
        /// Send query through current session
        /// </summary>
        public async Task<string> QueryWithContextAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            var session = GetOrCreateSession();
            return await session.QueryAsync(query, cancellationToken);
        }

        // ---------------------------------------------------------
        //  LEGACY METHODS (for backward compatibility)
        // ---------------------------------------------------------

        /// <summary>
        /// Explain what an instruction does
        /// </summary>
        public async Task<string> ExplainInstructionAsync(
            Instruction instruction,
            CancellationToken cancellationToken = default)
        {
            if (instruction == null) return string.Empty;

            var prompt = $"Explain this x86-64 instruction: {instruction.Mnemonic} {instruction.Operands}";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Generate pseudocode for a sequence of instructions
        /// </summary>
        public async Task<string> GeneratePseudocodeAsync(
            List<Instruction> instructions,
            ulong functionStart,
            CancellationToken cancellationToken = default)
        {
            if (instructions == null || instructions.Count == 0) return string.Empty;

            var asmBuilder = new StringBuilder();
            asmBuilder.AppendLine($"; Function at {functionStart:X}");
            
            foreach (var ins in instructions)
            {
                asmBuilder.AppendLine($"{ins.Address:X}  {ins.Mnemonic} {ins.Operands}");
                if (ins.Address - functionStart > 100) break; // Limit to first 100 bytes for analysis
            }

            var prompt = $"Generate C pseudocode for this function:\n\n{asmBuilder}\n\nPseudocode:";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Identify potential function type/signature
        /// </summary>
        public async Task<string> IdentifyFunctionSignatureAsync(
            List<Instruction> instructions,
            ulong functionStart,
            CancellationToken cancellationToken = default)
        {
            if (instructions == null || instructions.Count == 0) return string.Empty;

            var asmBuilder = new StringBuilder();
            asmBuilder.AppendLine("; First 10 instructions of function");
            
            for (int i = 0; i < Math.Min(10, instructions.Count); i++)
            {
                var ins = instructions[i];
                asmBuilder.AppendLine($"{ins.Address:X}  {ins.Mnemonic} {ins.Operands}");
            }

            var prompt = $"Analyze this function prologue and suggest its signature (return type, parameters):\n\n{asmBuilder}\n\nSignature:";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Detect common patterns (e.g., encryption, compression, checksums)
        /// </summary>
        public async Task<string> DetectPatternAsync(
            List<Instruction> instructions,
            ulong functionStart,
            CancellationToken cancellationToken = default)
        {
            if (instructions == null || instructions.Count == 0) return string.Empty;

            var asmBuilder = new StringBuilder();
            int limit = Math.Min(20, instructions.Count);
            
            for (int i = 0; i < limit; i++)
            {
                var ins = instructions[i];
                asmBuilder.AppendLine($"{ins.Address:X}  {ins.Mnemonic} {ins.Operands}");
            }

            var prompt = $"Identify any cryptographic, compression, or algorithmic patterns in this code:\n\n{asmBuilder}\n\nPattern:";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Suggest variable names based on register usage and operands
        /// </summary>
        public async Task<string> SuggestVariableNamesAsync(
            List<Instruction> instructions,
            ulong functionStart,
            CancellationToken cancellationToken = default)
        {
            if (instructions == null || instructions.Count == 0) return string.Empty;

            var asmBuilder = new StringBuilder();
            int limit = Math.Min(15, instructions.Count);
            
            for (int i = 0; i < limit; i++)
            {
                var ins = instructions[i];
                asmBuilder.AppendLine($"{ins.Address:X}  {ins.Mnemonic} {ins.Operands}");
            }

            var prompt = $"Suggest meaningful variable names for registers and memory locations in this code:\n\n{asmBuilder}\n\nSuggested names:";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Analyze control flow and branching logic
        /// </summary>
        public async Task<string> AnalyzeControlFlowAsync(
            List<Instruction> instructions,
            ulong functionStart,
            CancellationToken cancellationToken = default)
        {
            if (instructions == null || instructions.Count == 0) return string.Empty;

            var branches = new List<string>();
            
            foreach (var ins in instructions)
            {
                if (ins.Mnemonic.StartsWith("j") || ins.Mnemonic == "call")
                {
                    branches.Add($"{ins.Address:X}  {ins.Mnemonic} {ins.Operands}");
                }
            }

            if (branches.Count == 0) return "No branch instructions found.";

            var prompt = $"Explain the control flow logic of these branch instructions:\n\n{string.Join("\n", branches)}\n\nAnalysis:";
            return await _client.ChatAsync(prompt, RE_SYSTEM_PROMPT, cancellationToken);
        }

        /// <summary>
        /// Ask a general RE question
        /// </summary>
        public async Task<string> AskQuestionAsync(
            string question,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question)) return string.Empty;
            
            return await _client.ChatAsync(question, RE_SYSTEM_PROMPT, cancellationToken);
        }
    }
}
