using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Represents complete binary + analysis context for LLM as system prompt
    /// Updated whenever binary changes (bytes, patches, analysis)
    /// </summary>
    public class BinaryContextData
    {
        // ---------------------------------------------------------
        //  BINARY METADATA
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
        //  BINARY CONTENT SUMMARY (not full bytes - too large)
        // ---------------------------------------------------------
        public int TotalBytes { get; set; }
        public int ModifiedBytes { get; set; } // Count of edited bytes
        public List<(uint offset, byte original, byte current)> RecentPatches { get; set; } = [];

        // ---------------------------------------------------------
        //  ANALYSIS DATA - FUNCTIONS & CONTROL FLOW
        // ---------------------------------------------------------
        public int TotalFunctions { get; set; }
        public List<FunctionSummary> Functions { get; set; } = [];
        public List<CallChainSummary> TopCallChains { get; set; } = []; // Most complex call paths
        
        // ---------------------------------------------------------
        //  ANALYSIS DATA - CROSS-REFERENCES & DATA FLOW
        // ---------------------------------------------------------
        public int TotalCrossReferences { get; set; }
        public List<CrossReferenceSummary> CrossReferences { get; set; } = [];
        public int CodeToCodeRefs { get; set; } // CALL, JMP, conditional branches
        public int CodeToDataRefs { get; set; } // MOV, LEA, indirect references
        
        // ---------------------------------------------------------
        //  ANALYSIS DATA - SYMBOLS & IMPORTS
        // ---------------------------------------------------------
        public int TotalSymbols { get; set; }
        public List<SymbolSummary> Symbols { get; set; } = [];
        public List<SymbolSummary> ImportedFunctions { get; set; } = [];
        public List<SymbolSummary> ExportedFunctions { get; set; } = [];

        // ---------------------------------------------------------
        //  ANALYSIS DATA - STRINGS & DATA SECTIONS
        // ---------------------------------------------------------
        public int TotalStringsFound { get; set; }
        public List<StringReferenceSummary> Strings { get; set; } = [];

        // ---------------------------------------------------------
        //  ANALYSIS DATA - DETECTED PATTERNS
        // ---------------------------------------------------------
        public List<PatternDetectionSummary> DetectedPatterns { get; set; } = []; // Encryption, compression, etc.

        // ---------------------------------------------------------
        //  NOTES & CONTEXT
        // ---------------------------------------------------------
        public string? UserNotes { get; set; }
        public List<string> AnalysisNotes { get; set; } = [];

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Binary: {BinaryName} ({BinaryFormat})");
            sb.AppendLine($"Arch: {(Is64Bit ? "x64" : "x86")}");
            sb.AppendLine($"Size: {TotalBytes} bytes (Modified: {ModifiedBytes})");
            sb.AppendLine($"Functions: {TotalFunctions}, CrossRefs: {TotalCrossReferences}, Symbols: {TotalSymbols}");
            if (ModifiedBytes > 0)
            {
                sb.AppendLine($"Recent patches: {RecentPatches.Count}");
            }
            return sb.ToString();
        }
    }

    // ---------------------------------------------------------
    //  SUMMARY TYPES (lightweight for context)
    // ---------------------------------------------------------
    public class FunctionSummary
    {
        public ulong Address { get; set; }
        public string? Name { get; set; }
        public int Size { get; set; }
        public int BlockCount { get; set; }
        public int InstructionCount { get; set; }
        public List<ulong> CalledAddresses { get; set; } = [];
        public List<ulong> CalledByAddresses { get; set; } = [];
        public int XRefCount { get; set; } // Total references to this function
        public bool IsEntryPoint { get; set; }
        public bool IsImported { get; set; }

        public override string ToString() => $"{Name ?? $"0x{Address:X}"} @ 0x{Address:X} ({Size} bytes, {BlockCount} blocks, {XRefCount} xrefs)";
    }

    public class CallChainSummary
    {
        public List<ulong> Chain { get; set; } = []; // Sequence of function addresses
        public int Depth { get; set; }

        public override string ToString()
        {
            var chain = string.Join(" → ", Chain.Select(a => $"0x{a:X}"));
            return $"{chain} (depth: {Depth})";
        }
    }

    public class CrossReferenceSummary
    {
        public ulong From { get; set; }
        public ulong To { get; set; }
        public string RefType { get; set; } = string.Empty; // call, jmp, lea, mov, etc. - MUST be explicitly set
        public string? Description { get; set; }

        public override string ToString() => $"0x{From:X} → 0x{To:X} ({RefType})";
    }

    public class SymbolSummary
    {
        public ulong Address { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SymbolType { get; set; } = "function"; // function, variable, import, export
        public bool IsImport { get; set; }
        public bool IsExport { get; set; }
        public string? Section { get; set; }
        public uint Size { get; set; }
        public string? SourceDLL { get; set; } // For imports

        public override string ToString() => $"{Name} @ 0x{Address:X} ({SymbolType})";
    }

    public class StringReferenceSummary
    {
        public ulong Address { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<ulong> ReferencedFrom { get; set; } = []; // Addresses that reference this string
        public bool IsUnicode { get; set; }

        public override string ToString() => $"\"{Content}\" @ 0x{Address:X} ({ReferencedFrom.Count} references)";
    }

    public class PatternDetectionSummary
    {
        public string PatternName { get; set; } = string.Empty; // "encryption", "compression", "checksum", etc.
        public ulong Address { get; set; }
        public float Confidence { get; set; } // 0.0-1.0
        public string? Description { get; set; }

        public override string ToString() => $"{PatternName} @ 0x{Address:X} ({(int)(Confidence * 100)}% confidence)";
    }
}
