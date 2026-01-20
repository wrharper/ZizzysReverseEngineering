using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Represents a basic block: a sequence of instructions with single entry and exit.
    /// </summary>
    public class BasicBlock
    {
        public ulong StartAddress { get; set; }
        public ulong EndAddress { get; set; }
        public int StartInstructionIndex { get; set; }
        public int EndInstructionIndex { get; set; }

        // ---------------------------------------------------------
        //  CONTROL FLOW
        // ---------------------------------------------------------
        /// <summary>
        /// Successors (edges to blocks that follow this one).
        /// </summary>
        public List<ulong> Successors { get; set; } = [];

        /// <summary>
        /// Predecessors (edges from blocks that precede this one).
        /// </summary>
        public List<ulong> Predecessors { get; set; } = [];

        // ---------------------------------------------------------
        //  METADATA
        // ---------------------------------------------------------
        public bool IsEntryPoint { get; set; }
        public bool IsExitPoint { get; set; }
        public string? FunctionName { get; set; }
        public ulong? ParentFunctionAddress { get; set; }

        // ---------------------------------------------------------
        //  INITIALIZATION
        // ---------------------------------------------------------
        public BasicBlock(ulong startAddress, int startInstructionIndex)
        {
            StartAddress = startAddress;
            StartInstructionIndex = startInstructionIndex;
        }

        // ---------------------------------------------------------
        //  PROPERTIES
        // ---------------------------------------------------------
        public int InstructionCount => EndInstructionIndex - StartInstructionIndex + 1;

        public override string ToString() => $"Block @ 0x{StartAddress:X}: [{StartInstructionIndex}, {EndInstructionIndex}] ({InstructionCount} instrs)";
    }
}
