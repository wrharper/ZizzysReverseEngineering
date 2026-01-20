using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Builds a Control Flow Graph from a disassembly listing.
    /// Identifies block boundaries via control flow instructions.
    /// </summary>
    public static class BasicBlockBuilder
    {
        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------
        /// <summary>
        /// Build CFG from disassembly. Identifies blocks and control flow edges.
        /// </summary>
        public static ControlFlowGraph BuildCFG(List<Instruction> disassembly, ulong entryPointAddress)
        {
            if (disassembly == null || disassembly.Count == 0)
                throw new ArgumentException("Disassembly cannot be null or empty.", nameof(disassembly));

            var cfg = new ControlFlowGraph();
            var blockStarts = new HashSet<ulong> { entryPointAddress };

            // Step 1: Identify all block boundaries
            IdentifyBlockBoundaries(disassembly, blockStarts);

            // Step 2: Create blocks
            var blocks = CreateBlocks(disassembly, blockStarts);

            // Step 3: Add blocks to CFG
            foreach (var block in blocks)
            {
                if (block.StartAddress == entryPointAddress)
                    block.IsEntryPoint = true;

                cfg.AddBlock(block);
            }

            // Step 4: Identify control flow edges
            ConnectBlocks(disassembly, cfg, blocks);

            return cfg;
        }

        // ---------------------------------------------------------
        //  BLOCK BOUNDARY IDENTIFICATION
        // ---------------------------------------------------------
        private static void IdentifyBlockBoundaries(List<Instruction> disassembly, HashSet<ulong> blockStarts)
        {
            for (int i = 0; i < disassembly.Count; i++)
            {
                var ins = disassembly[i];

                // Every instruction after a control flow terminator is a block start
                if (IsControlFlowTerminator(ins) && i + 1 < disassembly.Count)
                {
                    blockStarts.Add(disassembly[i + 1].Address);
                }

                // Jump targets are block starts
                if (IsJump(ins))
                {
                    var target = GetJumpTarget(ins, disassembly);
                    if (target.HasValue)
                        blockStarts.Add(target.Value);
                }

                // Call targets might be function entry points (could be blocks if we track them)
                // For now, we don't split on calls unless they're part of a jump
            }
        }

        private static List<BasicBlock> CreateBlocks(List<Instruction> disassembly, HashSet<ulong> blockStarts)
        {
            var blocks = new List<BasicBlock>();
            var sortedStarts = blockStarts.OrderBy(a => a).ToList();

            for (int i = 0; i < sortedStarts.Count; i++)
            {
                var blockStart = sortedStarts[i];
                var blockEnd = i + 1 < sortedStarts.Count ? sortedStarts[i + 1] - 1 : ulong.MaxValue;

                // Find instruction indices
                var startIdx = disassembly.FindIndex(ins => ins.Address == blockStart);
                if (startIdx < 0)
                    continue;

                var endIdx = startIdx;
                for (int j = startIdx; j < disassembly.Count && disassembly[j].Address <= blockEnd; j++)
                {
                    endIdx = j;

                    // Stop if we hit a terminator
                    if (IsControlFlowTerminator(disassembly[j]) && j < disassembly.Count - 1)
                        break;
                }

                var block = new BasicBlock(blockStart, startIdx)
                {
                    EndAddress = disassembly[endIdx].EndAddress,
                    EndInstructionIndex = endIdx
                };

                blocks.Add(block);
            }

            return blocks;
        }

        private static void ConnectBlocks(List<Instruction> disassembly, ControlFlowGraph cfg, List<BasicBlock> blocks)
        {
            var blockMap = blocks.ToDictionary(b => b.StartAddress);

            foreach (var block in blocks)
            {
                var lastInstruction = disassembly[block.EndInstructionIndex];

                // Unconditional jump: single successor (target)
                if (IsUnconditionalJump(lastInstruction))
                {
                    var target = GetJumpTarget(lastInstruction, disassembly);
                    if (target.HasValue && blockMap.TryGetValue(target.Value, out var targetBlock))
                    {
                        block.Successors.Add(target.Value);
                        targetBlock.Predecessors.Add(block.StartAddress);
                    }
                }
                // Conditional jump: two successors (fall-through + target)
                else if (IsConditionalJump(lastInstruction))
                {
                    var target = GetJumpTarget(lastInstruction, disassembly);

                    // Fall-through successor
                    if (block.EndInstructionIndex + 1 < disassembly.Count)
                    {
                        var fallThrough = disassembly[block.EndInstructionIndex + 1].Address;
                        if (blockMap.TryGetValue(fallThrough, out var ftBlock))
                        {
                            block.Successors.Add(fallThrough);
                            ftBlock.Predecessors.Add(block.StartAddress);
                        }
                    }

                    // Jump target successor
                    if (target.HasValue && blockMap.TryGetValue(target.Value, out var targetBlock))
                    {
                        block.Successors.Add(target.Value);
                        targetBlock.Predecessors.Add(block.StartAddress);
                    }
                }
                // Normal flow: fall-through to next block
                else if (!IsTerminator(lastInstruction) && block.EndInstructionIndex + 1 < disassembly.Count)
                {
                    var nextAddr = disassembly[block.EndInstructionIndex + 1].Address;
                    if (blockMap.TryGetValue(nextAddr, out var nextBlock))
                    {
                        block.Successors.Add(nextAddr);
                        nextBlock.Predecessors.Add(block.StartAddress);
                    }
                }
            }
        }

        // ---------------------------------------------------------
        //  INSTRUCTION CLASSIFICATION
        // ---------------------------------------------------------
        private static bool IsControlFlowTerminator(Instruction ins)
        {
            if (ins.Raw == null)
                return false;

            var code = ins.Raw.Value.Code;
            return code == Code.Ret || code == Code.Jmp || IsConditionalJump(ins);
        }

        private static bool IsTerminator(Instruction ins)
        {
            if (ins.Raw == null)
                return false;

            var code = ins.Raw.Value.Code;
            return code == Code.Ret;
        }

        private static bool IsJump(Instruction ins)
        {
            if (ins.Raw == null)
                return false;

            var code = ins.Raw.Value.Code;
            return code == Code.Jmp || IsConditionalJump(ins);
        }

        private static bool IsUnconditionalJump(Instruction ins)
        {
            if (ins.Raw == null)
                return false;

            return ins.Raw.Value.Code == Code.Jmp;
        }

        private static bool IsConditionalJump(Instruction ins)
        {
            if (ins.Raw == null)
                return false;

            var code = ins.Raw.Value.Code;
            return code >= Code.Jo && code <= Code.Jg; // Conditional branch range in Iced
        }

        // ---------------------------------------------------------
        //  JUMP TARGET RESOLUTION
        // ---------------------------------------------------------
        private static ulong? GetJumpTarget(Instruction ins, List<Instruction> disassembly)
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
    }
}
