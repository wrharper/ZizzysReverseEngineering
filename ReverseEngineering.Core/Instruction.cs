using Iced.Intel;

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

        // ---------------------------------------------------------
        //  DISASSEMBLY TEXT
        // ---------------------------------------------------------

        public string Mnemonic { get; set; } = string.Empty;
        public string Operands { get; set; } = string.Empty;

        // ---------------------------------------------------------
        //  RAW BYTES
        // ---------------------------------------------------------

        public int Length { get; set; }
        public byte[] Bytes { get; set; } = System.Array.Empty<byte>();

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
    }
}