# API Reference - Analysis Data Pipeline

## BinaryContextData

Container for all binary analysis information.

### Properties

#### Binary Metadata
```csharp
public string BinaryPath { get; set; }              // File path
public string BinaryName { get; }                   // File name only (computed)
public string BinaryFormat { get; set; }            // "PE (x64)", "PE (x86)"
public bool Is64Bit { get; set; }                   // Architecture flag
public uint ImageBase { get; set; }                 // Base address
public uint ImageSize { get; set; }                 // Aligned size
public uint EntryPoint { get; set; }                // Program entry point
public DateTime LastUpdated { get; set; }           // Update timestamp
```

#### Binary Content
```csharp
public int TotalBytes { get; set; }                 // Total binary size
public int ModifiedBytes { get; set; }              // Count of patched bytes
public List<(uint offset, byte original, byte current)> RecentPatches  // Last 20 patches
```

#### Function Analysis
```csharp
public int TotalFunctions { get; set; }             // Total discovered functions
public List<FunctionSummary> Functions { get; set; } // Top 50 by complexity
public List<CallChainSummary> TopCallChains { get; set; } // Top 10 execution paths
```

#### Cross-Reference Analysis
```csharp
public int TotalCrossReferences { get; set; }       // Total xrefs
public List<CrossReferenceSummary> CrossReferences { get; set; } // Top 30
public int CodeToCodeRefs { get; set; }             // CALL/JMP count
public int CodeToDataRefs { get; set; }             // MOV/LEA count
```

#### Symbol Analysis
```csharp
public int TotalSymbols { get; set; }               // Total symbols
public List<SymbolSummary> ImportedFunctions { get; set; } // Imports (up to 20)
public List<SymbolSummary> ExportedFunctions { get; set; } // Exports (up to 20)
public List<SymbolSummary> Symbols { get; set; }    // All symbols (up to 50)
```

#### String Analysis
```csharp
public int TotalStringsFound { get; set; }          // Total strings in binary
public List<StringReferenceSummary> Strings { get; set; } // First 50 strings
```

#### Pattern Detection
```csharp
public List<PatternDetectionSummary> DetectedPatterns { get; set; } // Detected patterns
```

---

## FunctionSummary

Represents a discovered function with relationships.

### Properties
```csharp
public ulong Address { get; set; }                  // Function start address
public string? Name { get; set; }                   // Function name or null
public int Size { get; set; }                       // Function size in bytes
public int BlockCount { get; set; }                 // Basic block count
public int InstructionCount { get; set; }           // Total instructions
public List<ulong> CalledAddresses { get; set; }    // Functions this calls (up to 10)
public List<ulong> CalledByAddresses { get; set; }  // Functions that call this (up to 10)
public int XRefCount { get; set; }                  // Cross-reference count
public bool IsEntryPoint { get; set; }              // True if program entry
public bool IsImported { get; set; }                // True if imported API
```

---

## CallChainSummary

Represents an execution path through functions.

### Properties
```csharp
public List<ulong> Chain { get; set; }              // Function addresses in order
public int Depth { get; set; }                      // Depth of call chain
```

---

## CrossReferenceSummary

Represents a cross-reference between code/data.

### Properties
```csharp
public ulong From { get; set; }                     // Source address
public ulong To { get; set; }                       // Target address
public string RefType { get; set; }                 // "call", "jmp", "mov", etc.
public string Description { get; set; }             // Human-readable type
```

---

## SymbolSummary

Represents an imported, exported, or general symbol.

### Properties
```csharp
public ulong Address { get; set; }                  // Symbol address
public string Name { get; set; }                    // Symbol name
public string SymbolType { get; set; }              // "Function", "Data", etc.
public bool IsImport { get; set; }                  // True if imported
public bool IsExport { get; set; }                  // True if exported
public string? Section { get; set; }                // ".text", ".data", etc.
public int Size { get; set; }                       // Symbol size
public string? SourceDLL { get; set; }              // DLL name for imports
```

---

## StringReferenceSummary

Represents a discovered string in the binary.

### Properties
```csharp
public ulong Address { get; set; }                  // String location in binary
public string Content { get; set; }                 // Actual string content
public bool IsUnicode { get; set; }                 // True if wide-char string
```

---

## PatternDetectionSummary

Represents a detected pattern in the binary.

### Properties
```csharp
public string PatternName { get; set; }             // "XOR operations", "Loop operations"
public ulong Address { get; set; }                  // First occurrence address
public float Confidence { get; set; }               // 0.0 - 1.0 confidence
public string Description { get; set; }             // Pattern details
```

---

## BinaryContextGenerator

Transforms CoreEngine analysis into AI-friendly context.

### Constructor
```csharp
public BinaryContextGenerator(CoreEngine engine)
{
    // Initialize with CoreEngine reference
}
```

### Methods

#### GenerateContext()
Extracts all binary analysis data.
```csharp
public BinaryContextData GenerateContext()
{
    // Returns complete context with all analysis data
    // ~50-100ms execution time
}
```

#### GenerateSystemPrompt()
Creates AI system prompt from context.
```csharp
public string GenerateSystemPrompt(BinaryContextData context)
{
    // Returns 2-3 KB formatted prompt
    // Includes all metadata, functions, xrefs, symbols, strings, patterns
}
```

#### GenerateContextUpdatePrompt()
Notifies AI of changes since last context.
```csharp
public string GenerateContextUpdatePrompt(
    BinaryContextData previous, 
    BinaryContextData current)
{
    // Returns update summary for AI
    // Sent as context update in next query
}
```

### Private Methods
```csharp
private void ExtractFunctionAnalysis(BinaryContextData context)
    // Populates Functions, TopCallChains
private void ExtractCrossReferenceAnalysis(BinaryContextData context)
    // Populates CrossReferences, CodeToCodeRefs, CodeToDataRefs
private void ExtractSymbolAnalysis(BinaryContextData context)
    // Populates Symbols, ImportedFunctions, ExportedFunctions
private void ExtractStringAnalysis(BinaryContextData context)
    // Populates Strings collection
private void ExtractPatternAnalysis(BinaryContextData context)
    // Populates DetectedPatterns
private void ExtractTopCallChains(BinaryContextData context)
    // Computes call paths from functions
private void TraceCallChain(ulong currentAddr, List<ulong> chain, HashSet<ulong> visited, int maxDepth)
    // Recursively traces call paths
private string GetXrefDescription(string refType)
    // Returns human-readable xref type
private static string FormatSize(long bytes)
    // Formats bytes to KB/MB/GB
```

---

## LLMSession

Maintains conversation with automatic context awareness.

### Constructor
```csharp
public LLMSession(CoreEngine engine, LLMAnalyzer analyzer)
{
    // Initialize with engine and analyzer
}
```

### Properties
```csharp
public BinaryContextData CurrentContext { get; }    // Current analysis context
public bool IsContextStale { get; }                  // True if binary has changed
```

### Methods

#### UpdateContext()
Re-generates context from CoreEngine.
```csharp
public void UpdateContext()
{
    // Re-extracts all analysis data
    // Detects changes from previous context
    // ~30-50ms execution time
}
```

#### QueryAsync()
Sends query with full system prompt context.
```csharp
public async Task<string> QueryAsync(string userQuery)
{
    // Returns AI response
    // First query: ~2-3 seconds (includes context generation)
    // Subsequent: ~1-2 seconds
    // Includes full system prompt with all analysis
}
```

#### GetHistory()
Retrieves conversation history.
```csharp
public List<(string role, string content)> GetHistory()
{
    // Returns list of all messages
    // Format: ("system"/"user"/"assistant", message content)
}
```

#### GetCurrentContext()
Accesses current binary context.
```csharp
public BinaryContextData GetCurrentContext()
{
    // Returns latest BinaryContextData
}
```

---

## LLMAnalyzer (Enhanced)

### Existing Methods
```csharp
public async Task<string> ExplainInstructionAsync(Instruction instr)
    // Legacy: Explains single instruction
public async Task<string> GeneratePseudocodeAsync(ulong functionAddress)
    // Legacy: Generates pseudocode
public async Task<string> AnalyzePatternAsync(string bytePattern)
    // Legacy: Analyzes byte pattern
```

### New Session-Based Methods
```csharp
public LLMSession CreateSession(CoreEngine engine)
{
    // Creates new LLMSession
    // Returns fresh session
}

public LLMSession? GetOrCreateSession(CoreEngine engine)
{
    // Returns existing session or creates new one
    // Returns null if creation fails
}

public async Task<string> QueryWithContextAsync(CoreEngine engine, string question)
{
    // High-level: Query with auto-managed session
    // Handles context updates automatically
    // Returns AI response with full context
}
```

---

## Usage Examples

### Create and Query
```csharp
var generator = new BinaryContextGenerator(engine);
var context = generator.GenerateContext();
var prompt = generator.GenerateSystemPrompt(context);

// Or use LLMSession for conversation
var session = new LLMSession(engine, analyzer);
var response = await session.QueryAsync("What does main do?");
```

### Get or Create Session
```csharp
var analyzer = new LLMAnalyzer(llmClient);
var session = analyzer.GetOrCreateSession(engine);

// Multiple queries with same context
var q1 = await session.QueryAsync("Analyze the entry point");
var q2 = await session.QueryAsync("What APIs are used?");
var q3 = await session.QueryAsync("How does data flow?");

// All three queries have full binary context
```

### Update Context on Modification
```csharp
// User edits binary
engine.HexBuffer.WriteByte(0x1000, 0x90);

// Update session context
session.UpdateContext();

// Next query includes the modification in context
var response = await session.QueryAsync("What's the effect?");
// AI knows about the patch
```

### Access Binary Context Directly
```csharp
var context = session.GetCurrentContext();

Console.WriteLine($"Functions: {context.TotalFunctions}");
Console.WriteLine($"Entry point: 0x{context.EntryPoint:X}");

foreach (var func in context.Functions.Take(5))
{
    Console.WriteLine($"  {func.Name} @ 0x{func.Address:X} ({func.Size} bytes)");
}
```

---

## Data Volume Expectations

### Small Binary (1 MB)
- Functions: 50-100
- Strings: 50-200
- XRefs: 200-500
- System Prompt: ~1.5 KB
- GenerateContext: ~30ms

### Medium Binary (10 MB)
- Functions: 100-300
- Strings: 200-1000
- XRefs: 500-2000
- System Prompt: ~2.5 KB
- GenerateContext: ~50ms

### Large Binary (50+ MB)
- Functions: 300-1000+
- Strings: 1000+
- XRefs: 2000+
- System Prompt: ~3 KB (capped at top entries)
- GenerateContext: ~100ms

---

## Performance Tuning

### Context Caching
```csharp
// Session caches context - don't regenerate unnecessarily
var context = session.GetCurrentContext();  // No I/O
var context2 = session.GetCurrentContext(); // Same object
```

### Lazy Context Update
```csharp
// Only update when binary actually changed
if (session.IsContextStale)
    session.UpdateContext();
```

### Limit Context Size
```csharp
// Generator automatically limits:
// - Functions: Top 50 by complexity
// - Strings: First 50
// - XRefs: Top 30
// - Symbols: Top 50
// Result: Always ~2-3 KB prompt regardless of binary size
```

---

## Error Handling

### Connection Errors
```csharp
try
{
    var response = await session.QueryAsync(question);
}
catch (HttpRequestException)
{
    // LM Studio not running or connection failed
    Console.WriteLine("LM Studio unreachable at localhost:1234");
}
```

### Timeout Errors
```csharp
catch (TimeoutException)
{
    // AI response took too long
    Console.WriteLine("Query timeout - try shorter questions");
}
```

### General Errors
```csharp
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // Log and handle gracefully
}
```

---

## Thread Safety

- ✅ `BinaryContextData`: Read-only after generation
- ✅ `BinaryContextGenerator`: Stateless, thread-safe
- ✅ `LLMSession`: NOT thread-safe (use one per CoreEngine)
- ❌ `CoreEngine`: NOT thread-safe (modifications must be serialized)

---

## Memory Footprint

- `BinaryContextData`: ~50-100 KB (mostly strings + xrefs)
- `LLMSession`: ~50-100 KB (history + cached context)
- Total per binary: ~100-200 KB
- Multiple sessions: Create new for each binary

---

