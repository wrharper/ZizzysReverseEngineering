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
            Logger.Debug("FunctionFinder", "Adding entry point...");
            AddEntryPoint(disassembly, engine, functions);

            // Step 2: Add exports
            if (includeExports)
            {
                Logger.Debug("FunctionFinder", "Finding exported functions...");
                AddExportedFunctions(disassembly, engine, functions);
            }

            // Step 3: Add imported functions
            if (includeImports)
            {
                Logger.Debug("FunctionFinder", "Finding imported functions...");
                AddImportedFunctions(disassembly, engine, functions);
            }

            // Step 4: Find prologue-based functions
            if (includePrologues)
            {
                Logger.Debug("FunctionFinder", "Finding prologue-based functions...");
                FindPrologueFunctions(disassembly, functions);
                Logger.Debug("FunctionFinder", $"Found {functions.Count} functions so far");
            }

            // Step 5: Recursively find functions via call graph
            if (includeCallGraph)
            {
                Logger.Debug("FunctionFinder", "Finding call graph functions...");
                FindViaCallGraph(disassembly, functions);
                Logger.Debug("FunctionFinder", $"Found {functions.Count} total functions");
            }

            // Step 6: Build CFG for each function (limit to first 500 to avoid timeout)
            Logger.Debug("FunctionFinder", "Building CFG for each function...");
            int cfgCount = 0;
            int cfgLimit = Math.Min(500, functions.Count);  // Limit to avoid timeout on binaries with many false positive functions
            int cfgIndex = 0;
            var cfgSw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var func in functions.Values.Take(cfgLimit))
            {
                try
                {
                    func.CFG = BasicBlockBuilder.BuildCFG(disassembly, func.Address);
                    cfgCount++;
                }
                catch
                {
                    // Silently fail if CFG build fails for this function
                }
                
                cfgIndex++;
                if (cfgIndex % 10 == 0 || cfgIndex == cfgLimit)
                {
                    Logger.Debug("FunctionFinder", $"  CFG progress: {cfgIndex}/{cfgLimit} ({cfgSw.ElapsedMilliseconds}ms)");
                }
            }
            cfgSw.Stop();
            Logger.Debug("FunctionFinder", $"Built CFG for {cfgCount}/{cfgLimit} functions ({cfgSw.ElapsedMilliseconds}ms total)");
            Logger.Info("FunctionFinder", $"CFG building complete: {cfgCount} CFGs built, {cfgLimit} total functions");

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
            // Optimization: sample every Nth instruction to avoid O(N²) behavior on large binaries
            // Most prologues start with PUSH or SUB, which aren't super common
            int sampleRate = disassembly.Count > 10000 ? 4 : 1;  // Every 4th instruction for large binaries
            int prologuesFound = 0;
            
            for (int i = 0; i < disassembly.Count; i += sampleRate)
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
                        prologuesFound++;
                    }
                }
            }
            
            Logger.Debug("FunctionFinder", $"Prologue search: sampled {disassembly.Count / sampleRate} instructions, found {prologuesFound} prologues");
        }

        private static void FindViaCallGraph(List<Instruction> disassembly, Dictionary<ulong, Function> functions)
        {
            var callTargets = new HashSet<ulong>();

            // Collect all CALL targets
            foreach (var ins in disassembly)
            {
                if (ins.Raw == null)
                    continue;

                var mnemonic = ins.Raw.Value.Mnemonic;
                if (mnemonic == Mnemonic.Call)
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

            var mnemonic = ins.Raw.Value.Mnemonic;

            // STRICT prologue patterns to reduce false positives:
            // Only match VERY clear function starts
            
            // Pattern 1: PUSH RBP; MOV RBP, RSP (frame pointer setup)
            if (mnemonic == Mnemonic.Push && index + 1 < disassembly.Count)
            {
                var raw = ins.Raw.Value;
                // Must be PUSH RBP or PUSH RDI (callee-saved registers)
                bool isPushCalleeSaved = raw.Op0Register == Register.RBP || 
                                        raw.Op0Register == Register.RBX ||
                                        raw.Op0Register == Register.RDI ||
                                        raw.Op0Register == Register.RSI;
                if (!isPushCalleeSaved)
                    return false;

                var next = disassembly[index + 1];
                if (next.Raw != null && next.Raw.Value.Mnemonic == Mnemonic.Mov)
                    return true; // Likely "PUSH Reg; MOV ..." 
            }

            // Pattern 2: Only match SUB RSP with reasonable immediate (stack allocation for locals)
            if (mnemonic == Mnemonic.Sub)
            {
                var raw = ins.Raw.Value;
                // Must be SUB RSP, imm (and imm should be reasonable, e.g., 0x10-0x1000)
                if (raw.Op0Register == Register.RSP && raw.OpCount > 1)
                {
                    try
                    {
                        var imm = raw.Immediate64;
                        // Only match if stack allocation is between 16 bytes and 64KB (reasonable)
                        if (imm >= 0x10 && imm <= 0x10000)
                            return true;
                    }
                    catch { }
                }
            }

            // Pattern 3: MOV RBP, RSP only if it immediately follows PUSH RBP
            if (mnemonic == Mnemonic.Mov && index > 0)
            {
                var raw = ins.Raw.Value;
                if (raw.Op0Register == Register.RBP && raw.Op1Register == Register.RSP)
                {
                    var prev = disassembly[index - 1];
                    if (prev.Raw != null && prev.Raw.Value.Mnemonic == Mnemonic.Push)
                        return true;
                }
            }

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
                if (ins.Raw != null && ins.Raw.Value.Mnemonic == Mnemonic.Ret)
                    break;

                if (size > MaxFunctionSize)
                    break;
            }

            return size;
        }
    }
}
