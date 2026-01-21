using Iced.Intel;
using System.Collections.Generic;

namespace ReverseEngineering.Core
{
    public class Instruction
    {
        // ---------------------------------------------------------
        //  CORE DATA
        // ---------------------------------------------------------

        /// <summary>
        /// Virtual address (IP) of the instruction.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// File offset inside the binary (computed during decode).
        /// </summary>
        public int FileOffset { get; set; }

        /// <summary>
        /// RVA = Address - ImageBase.
        /// </summary>
        public uint RVA { get; set; }

        /// <summary>
        /// Index of the section this instruction belongs to.
        /// </summary>
        public int SectionIndex { get; set; }

        /// <summary>
        /// Name of the section this instruction belongs to (e.g., ".text", ".code").
        /// </summary>
        public string? SectionName { get; set; }

        // ---------------------------------------------------------
        //  DISASSEMBLY TEXT
        // ---------------------------------------------------------

        public string Mnemonic { get; set; } = string.Empty;
        public string Operands { get; set; } = string.Empty;

        // ---------------------------------------------------------
        //  RAW BYTES
        // ---------------------------------------------------------

        public int Length { get; set; }
        public byte[] Bytes { get; set; } = [];

        // ⭐ NEW: Required for incremental disassembly
        public ulong EndAddress => Address + (ulong)Length;

        // ---------------------------------------------------------
        //  RAW ICED INSTRUCTION
        // ---------------------------------------------------------

        /// <summary>
        /// The original Iced.Intel instruction object.
        /// Useful for reformatting, operand analysis, CFG, etc.
        /// </summary>
        public Iced.Intel.Instruction? Raw { get; set; }

        // ---------------------------------------------------------
        //  FLAGS (for future CFG / UI highlighting)
        // ---------------------------------------------------------

        public bool IsCall { get; set; }
        public bool IsJump { get; set; }
        public bool IsConditionalJump { get; set; }
        public bool IsReturn { get; set; }
        public bool IsNop { get; set; }

        // ---------------------------------------------------------
        //  ANALYSIS METADATA (Phase 2)
        // ---------------------------------------------------------

        /// <summary>
        /// Function this instruction belongs to (if any).
        /// </summary>
        public ulong? FunctionAddress { get; set; }

        /// <summary>
        /// Basic block this instruction belongs to.
        /// </summary>
        public ulong? BasicBlockAddress { get; set; }

        /// <summary>
        /// Cross-references FROM this instruction.
        /// </summary>
        public List<Analysis.CrossReference> XRefsFrom { get; set; } = [];

        /// <summary>
        /// Symbol name at this address (if any).
        /// </summary>
        public string? SymbolName { get; set; }

        /// <summary>
        /// User annotation for this instruction.
        /// </summary>
        public string? Annotation { get; set; }

        /// <summary>
        /// Whether this instruction has been patched.
        /// </summary>
        public bool IsPatched { get; set; }

        // ---------------------------------------------------------
        //  OPERAND ANALYSIS (RIP-relative, etc.)
        // ---------------------------------------------------------

        /// <summary>
        /// If this instruction has RIP-relative addressing, the resolved address.
        /// Used for data references, string references, etc.
        /// </summary>
        public ulong? RIPRelativeTarget { get; set; }

        /// <summary>
        /// Type of operand (if traceable): "Data", "String", "Import", etc.
        /// </summary>
        public string? OperandType { get; set; }

        /// <summary>
        /// Helper to get display string for RIP-relative target
        /// </summary>
        public string GetRIPRelativeDisplay()
        {
            if (!RIPRelativeTarget.HasValue)
                return "";

            var addr = RIPRelativeTarget.Value;
            var label = OperandType switch
            {
                "String" => "string",
                "Data" => "data",
                "Import" => "import",
                _ => $"0x{addr:X}"
            };

            return $"{label} @ 0x{addr:X}";
        }
    }
}