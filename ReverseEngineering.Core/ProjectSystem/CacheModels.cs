using System;
using System.Collections.Generic;

#nullable enable

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// Models for SQLite cache entities
    /// </summary>

    /// <summary>
    /// Represents a cached symbol (function, import, export, label)
    /// </summary>
    public class CachedSymbol
    {
        public long SymbolID { get; set; }
        public string BinaryHash { get; set; } = "";
        public ulong Address { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";  // "FUNCTION", "IMPORT", "EXPORT", "LABEL"
        public int Size { get; set; }
        public string Section { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents a cached string literal or data
    /// </summary>
    public class CachedString
    {
        public long StringID { get; set; }
        public string BinaryHash { get; set; } = "";
        public ulong Address { get; set; }
        public string StringValue { get; set; } = "";
        public string Type { get; set; } = "";  // "ASCII", "UNICODE", "REFERENCE"
        public byte[]? References { get; set; }  // Cross-references (packed)
    }

    /// <summary>
    /// Represents a cached cross-reference (code→code, code→data)
    /// </summary>
    public class CachedCrossReference
    {
        public long XRefID { get; set; }
        public string BinaryHash { get; set; } = "";
        public ulong SourceAddress { get; set; }
        public ulong TargetAddress { get; set; }
        public string RefType { get; set; } = "";  // "CALL", "JMP", "DATA_READ", "DATA_WRITE"
        public string Context { get; set; } = "";  // Instruction context
    }

    /// <summary>
    /// Represents a cached instruction range (disassembly cache)
    /// </summary>
    public class CachedInstructionRange
    {
        public string AddressRange { get; set; } = "";  // "0x1000-0x2000"
        public string BinaryHash { get; set; } = "";
        public byte[]? Instructions { get; set; }  // Compressed instruction list
        public string InBasicBlock { get; set; } = "";  // Function/block context
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents a cached byte range (for byte-level caching)
    /// </summary>
    public class CachedByteRange
    {
        public long RangeID { get; set; }
        public string BinaryHash { get; set; } = "";
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public byte[]? Data { get; set; }
        public byte[]? Compressed { get; set; }  // Optional ZSTD compressed version
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents a cached pattern (trainer analysis)
    /// </summary>
    public class CachedPattern
    {
        public long PatternID { get; set; }
        public string BinaryHash { get; set; } = "";
        public ulong Address { get; set; }
        public string Signature { get; set; } = "";  // "prologue_x64", "loop_x86", etc.
        public byte[]? InstructionBytes { get; set; }  // Compressed pattern
        public byte[]? Embedding { get; set; }  // Vector for similarity search
        public string ControlFlowSummary { get; set; } = "";  // JSON: blocks, branches
        public byte[]? References { get; set; }  // Function callers/callees (packed)
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents cache metadata (version, statistics)
    /// </summary>
    public class CacheMetadata
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Cache statistics summary
    /// </summary>
    public class CacheStats
    {
        public long SymbolCount { get; set; }
        public long StringCount { get; set; }
        public long CrossRefCount { get; set; }
        public long PatternCount { get; set; }
        public long CacheHits { get; set; }
        public long TotalQueries { get; set; }
        public long DatabaseSizeKB { get; set; }
    }
}
