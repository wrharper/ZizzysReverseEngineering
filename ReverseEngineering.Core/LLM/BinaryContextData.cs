#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Represents static binary metadata and patch summary for LLM system prompt.
    /// No dynamic analysis or function/xref/symbol/string/pattern data included.
    /// </summary>
    public class BinaryContextData
    {
        public SystemContextData SCD { get; set; } = new SystemContextData();
        // --------------------------------------------------------- 
        //  BINARY METADATA (static info only)
        // --------------------------------------------------------- 
        public string BinaryPath { get; set; } = string.Empty;
        public string BinaryName => System.IO.Path.GetFileName(BinaryPath);
        public string BinaryFormat { get; set; } = "Unknown"; // PE, ELF, Mach-O
        public bool Is64Bit { get; set; }
        public uint ImageBase { get; set; }
        public uint ImageSize { get; set; }
        public uint EntryPoint { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // --------------------------------------------------------- 
        //  BINARY CONTENT SUMMARY (no full bytes)
        // --------------------------------------------------------- 
        public int TotalBytes { get; set; }
        public int ModifiedBytes { get; set; } // Count of edited bytes
        public List<(uint offset, byte original, byte current)> RecentPatches { get; set; } = [];
    }
}
