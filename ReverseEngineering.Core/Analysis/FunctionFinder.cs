using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Represents a discovered function.
    /// </summary>
    public class Function
    {
        public ulong Address { get; set; }
        public string? Name { get; set; }
        public string? Source { get; set; } // "export", "import", "entry", "prologue", "call"
        public int InstructionCount { get; set; }
        public bool IsImported { get; set; }
        public bool IsExported { get; set; }
        public bool IsEntryPoint { get; set; }
        public ControlFlowGraph? CFG { get; set; }

        public override string ToString() => $"Function @ 0x{Address:X}: {Name ?? "unnamed"} ({InstructionCount} instrs, src={Source})";
    }

    /// <summary>
    /// Discovers functions in a binary via multiple heuristics.
    /// - Entry points (PE entry point)
    /// - Exports (from export table)
    /// - Imports (from import table)
    /// - Prologues (pattern matching: PUSH RBP, MOV RSP, SUB RSP, etc.)
    /// - Call graph (recursively analyze CALL targets)
    /// </summary>
    public static class FunctionFinder
    {
        private const int MaxFunctionSize = 0x10000; // Heuristic: functions > 64KB are suspicious

        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------
        public static List<Function> FindFunctions(
            List<Instruction> disassembly,
            CoreEngine engine,
            bool includeExports = true,
            bool includeImports = true,
            bool includePrologues = true,
            bool includeCallGraph = true)
        {
            var functions = new Dictionary<ulong, Function>();

            // Step 1: Add entry point
            AddEntryPoint(disassembly, engine, functions);

            // Step 2: Add exports
            if (includeExports)
                AddExportedFunctions(disassembly, engine, functions);

            // Step 3: Add imported functions
            if (includeImports)
                AddImportedFunctions(disassembly, engine, functions);

            // Step 4: Find prologue-based functions
            if (includePrologues)
                FindPrologueFunctions(disassembly, functions);

            // Step 5: Recursively find functions via call graph
            if (includeCallGraph)
                FindViaCallGraph(disassembly, functions);

            // Step 6: Build CFG for each function
            foreach (var func in functions.Values)
            {
                try
                {
                    func.CFG = BasicBlockBuilder.BuildCFG(disassembly, func.Address);
                }
                catch
                {
                    // Silently fail if CFG build fails for this function
                }
            }

            return functions.Values.OrderBy(f => f.Address).ToList();
        }

        // ---------------------------------------------------------
        //  FUNCTION DISCOVERY STRATEGIES
        // ---------------------------------------------------------
        private static void AddEntryPoint(List<Instruction> disassembly, CoreEngine engine, Dictionary<ulong, Function> functions)
        {
            // Entry point is typically the first executable instruction
            if (disassembly.Count > 0)
            {
                var entryAddr = disassembly[0].Address;
                if (!functions.ContainsKey(entryAddr))
                {
                    functions[entryAddr] = new Function
                    {
                        Address = entryAddr,
                        Name = "_entry",
                        Source = "entry",
                        IsEntryPoint = true
                    };
                }
            }
        }

        private static void AddExportedFunctions(List<Instruction> disassembly, CoreEngine engine, Dictionary<ulong, Function> functions)
        {
            // TODO: Extract from PE export table (would require PE parser additions)
            // For now, this is a placeholder
        }

        private static void AddImportedFunctions(List<Instruction> disassembly, CoreEngine engine, Dictionary<ulong, Function> functions)
        {
            // TODO: Extract from PE import table (would require PE parser additions)
            // For now, this is a placeholder
        }

        private static void FindPrologueFunctions(List<Instruction> disassembly, Dictionary<ulong, Function> functions)
        {
            for (int i = 0; i < disassembly.Count; i++)
            {
                var ins = disassembly[i];

                // Check for common function prologues
                if (MatchesPrologue(disassembly, i))
                {
                    if (!functions.ContainsKey(ins.Address))
                    {
                        functions[ins.Address] = new Function
                        {
                            Address = ins.Address,
                            Source = "prologue"
                        };
                    }
                }
            }
        }

        private static void FindViaCallGraph(List<Instruction> disassembly, Dictionary<ulong, Function> functions)
        {
            var callTargets = new HashSet<ulong>();

            // Collect all CALL targets
            foreach (var ins in disassembly)
            {
                if (ins.Raw == null)
                    continue;

                var code = ins.Raw.Value.Code;
                if (code == Code.Call || code == Code.Lcall)
                {
                    var target = GetCallTarget(ins);
                    if (target.HasValue)
                        callTargets.Add(target.Value);
                }
            }

            // Add discovered call targets
            foreach (var target in callTargets)
            {
                if (!functions.ContainsKey(target))
                {
                    functions[target] = new Function
                    {
                        Address = target,
                        Source = "call_target"
                    };
                }
            }
        }

        // ---------------------------------------------------------
        //  PROLOGUE MATCHING
        // ---------------------------------------------------------
        private static bool MatchesPrologue(List<Instruction> disassembly, int index)
        {
            if (index < 0 || index >= disassembly.Count)
                return false;

            var ins = disassembly[index];
            if (ins.Raw == null)
                return false;

            var code = ins.Raw.Value.Code;

            // Common x86/x64 prologue patterns:
            // 1. PUSH RBP / MOV RBP, RSP
            if (code == Code.Push_r64 && index + 1 < disassembly.Count)
            {
                var next = disassembly[index + 1];
                if (next.Raw?.Value.Code == Code.Mov_r64_r64)
                    return true; // Likely "PUSH RBP; MOV RBP, RSP"
            }

            // 2. SUB RSP, imm
            if (code == Code.Sub_r64_imm32 || code == Code.Sub_r64_imm8)
                return true;

            // 3. MOV RBP, RSP
            if (code == Code.Mov_r64_r64)
            {
                var raw = ins.Raw.Value;
                // Check if it's MOV RBP, RSP
                if (raw.Op0Register == Register.RBP && raw.Op1Register == Register.RSP)
                    return true;
            }

            // 4. XOR EAX, EAX (common in x64 ABI for return value)
            if (code == Code.Xor_r32_r32)
                return true;

            return false;
        }

        // ---------------------------------------------------------
        //  TARGET RESOLUTION
        // ---------------------------------------------------------
        private static ulong? GetCallTarget(Instruction ins)
        {
            if (ins.Raw == null)
                return null;

            var raw = ins.Raw.Value;
            if (raw.OpCount == 0)
                return null;

            var op = raw.Op0Kind;
            if (op == OpKind.NearBranch64)
                return raw.NearBranch64;
            if (op == OpKind.NearBranch32)
                return raw.NearBranch32;

            return null;
        }

        // ---------------------------------------------------------
        //  FUNCTION SIZE CALCULATION
        // ---------------------------------------------------------
        public static int CalculateFunctionSize(List<Instruction> disassembly, ulong functionAddress)
        {
            var startIdx = disassembly.FindIndex(i => i.Address == functionAddress);
            if (startIdx < 0)
                return 0;

            int size = 0;
            for (int i = startIdx; i < disassembly.Count; i++)
            {
                var ins = disassembly[i];
                size += ins.Length;

                // Stop at obvious function boundaries
                if (ins.Raw?.Value.Code == Code.Ret)
                    break;

                if (size > MaxFunctionSize)
                    break;
            }

            return size;
        }
    }
}
