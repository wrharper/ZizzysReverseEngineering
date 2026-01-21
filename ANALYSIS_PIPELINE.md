# Analysis Data Pipeline - LLM Context System

## Overview

The analysis data pipeline transforms the CoreEngine's binary analysis results into a comprehensive system prompt that provides an AI assistant with full context of the binary being analyzed.

**Architecture**: `CoreEngine` → `Analysis` → `BinaryContextGenerator` → `BinaryContextData` → `LLMSession` → `AI Assistant`

---

## Data Flow

### 1. CoreEngine Analysis (Pure Reverse Engineering)

The `CoreEngine` runs independent analysis on the loaded binary:

```
LoadFile(binary.exe)
  ↓
Disassembler.DecodePE()  →  List<Instruction> with metadata
  ↓
RunAnalysis()  →  {
  - BasicBlockBuilder     → ControlFlowGraph (CFG)
  - FunctionFinder        → List<Function>
  - CrossReferenceEngine  → Dictionary<address, List<XRef>>
  - SymbolResolver        → Dictionary<address, Symbol>
}
```

**Available Analysis Data**:
- `Disassembly`: Complete instruction list with mnemonics, operands, addresses
- `Functions`: Discovered functions with CFG, entry point, size, import/export flags
- `CFG`: Control flow graph with basic blocks and edges
- `CrossReferences`: Code→Code (calls, jumps), Code→Data (loads)
- `Symbols`: Imported/exported functions, data symbols
- `HexBuffer`: Current binary state + modified bytes tracking

---

### 2. BinaryContextGenerator - Data Extraction

`BinaryContextGenerator.GenerateContext()` extracts all available analysis data and produces a structured `BinaryContextData` object:

#### A. Binary Metadata
```csharp
// Extracted from CoreEngine
BinaryPath              // File path
BinaryFormat            // PE (x64), PE (x86)
Is64Bit                 // Architecture
ImageBase               // Base address
EntryPoint              // Program entry point
TotalBytes              // Binary size
ModifiedBytes           // Count of edited bytes
RecentPatches           // Last 20 patch tuples (offset, original, new)
```

#### B. Function Analysis
```csharp
ExtractFunctionAnalysis()
  ├─ TotalFunctions        // Count of discovered functions
  ├─ Functions (top 50)    // Detailed summaries for most-complex functions
  │   ├─ Address
  │   ├─ Name
  │   ├─ Size (bytes)
  │   ├─ BlockCount        // Number of basic blocks
  │   ├─ InstructionCount
  │   ├─ CalledAddresses   // Functions this one calls (up to 10)
  │   ├─ CalledByAddresses // Functions that call this one (up to 10)
  │   ├─ XRefCount         // Cross-references to this function
  │   ├─ IsEntryPoint      // True if program entry point
  │   └─ IsImported        // True if imported API
  └─ TopCallChains (up to 10)
      └─ CallChainSummary  // Call path: f1 → f2 → f3 → f4
          ├─ Chain         // List<ulong> of function addresses
          └─ Depth         // Depth of call chain
```

#### C. Cross-Reference Analysis
```csharp
ExtractCrossReferenceAnalysis()
  ├─ TotalCrossReferences  // Total xrefs in binary
  ├─ CodeToCodeRefs        // Count of CALL/JMP operations
  ├─ CodeToDataRefs        // Count of data accesses (MOV, LEA)
  └─ CrossReferences (top 30)
      └─ CrossReferenceSummary
          ├─ From          // Source address
          ├─ To            // Target address
          ├─ RefType       // "call", "jmp", "mov", "lea", etc.
          └─ Description   // Human-readable type (e.g., "Function call")
```

#### D. Symbol Analysis
```csharp
ExtractSymbolAnalysis()
  ├─ TotalSymbols
  ├─ ImportedFunctions (up to 20)
  │   ├─ Address
  │   ├─ Name
  │   ├─ SymbolType
  │   ├─ SourceDLL        // Which DLL this import comes from
  │   ├─ Section          // .text, .data, .idata
  │   └─ Size
  ├─ ExportedFunctions (up to 20)
  │   └─ (same structure)
  └─ Symbols (all, up to 50)
      └─ (same structure)
```

#### E. String Analysis
```csharp
ExtractStringAnalysis()
  ├─ TotalStringsFound    // Count of all strings
  └─ Strings (first 50)
      └─ StringReferenceSummary
          ├─ Address      // Where string is located in binary
          ├─ Content      // ASCII string value
          └─ IsUnicode    // True if wide-char string
```

#### F. Pattern Detection
```csharp
ExtractPatternAnalysis()
  └─ DetectedPatterns     // Identified patterns in code
      └─ PatternDetectionSummary
          ├─ PatternName  // "XOR operations", "Loop operations"
          ├─ Address      // First occurrence
          ├─ Confidence   // 0.0-1.0 confidence score
          └─ Description  // Details (e.g., "Found 47 XOR instructions")
```

---

### 3. System Prompt Generation

`BinaryContextGenerator.GenerateSystemPrompt(context)` converts `BinaryContextData` into a human-readable system prompt (~2-3 KB):

```
═══ BINARY METADATA ═══
File: program.exe
Format: PE (x64)
Image Base: 0x140000000
Entry Point: 0x140001000
Total Size: 1.23 MB (1289856 bytes)

═══ ANALYSIS SUMMARY ═══
Functions: 247
Cross-References: 1,243 (Code→Code: 892, Code→Data: 351)
Symbols: 356 (Imports: 42, Exports: 15)
Strings: 128
Patterns: 2 detected

═══ KEY FUNCTIONS ═══
• 0x140001000: _start | 512 bytes | 47 instructions | 3 blocks
  | called-by:1 | calls:3 | xref:5

• 0x140002500: main | 2048 bytes | 156 instructions | 12 blocks
  | called-by:1 | calls:8 | xref:12

... and 15 more ...

═══ IMPORTED FUNCTIONS ═══
From kernel32.dll:
  • 0x140010000: CreateFileA
  • 0x140010008: WriteFile
  • 0x140010010: ReadFile

From msvcrt.dll:
  • 0x140010100: malloc
  • 0x140010108: free

═══ CALL CHAINS ═══
• 0x140001000 → 0x140002500 → 0x140003000 → 0x140004500 → ... (total depth: 5)

═══ KEY CROSS-REFERENCES ═══
• 0x140001000 → 0x140002500 (call: Function call)
• 0x140002510 → 0x140010000 (call: Imported API)
• 0x140003200 → 0x140006000 (jmp: Unconditional jump)

═══ DISCOVERED STRINGS ═══
• 0x140008000: "C:\\Windows\\System32\\notepad.exe"
• 0x140008040: "Error: Could not open file"
• 0x140008080: "Usage: program.exe <filename>"

═══ DETECTED PATTERNS ═══
• XOR operations @ 0x140002100
  Confidence: 60%
  Found 47 XOR instructions - possible encryption/obfuscation

═══ YOUR CAPABILITIES ═══
✓ Analyze code patterns and assembly logic
✓ Suggest patch locations (NOP, jumps, calls)
✓ Explain function behavior
✓ Identify API usage
✓ Locate string references
✓ Find encryption patterns
```

---

### 4. LLMSession - Conversation Management

`LLMSession` maintains the conversation with automatic context updates:

```csharp
var session = new LLMSession(_engine, _analyzer);

// Send query with full system prompt context
var response = await session.QueryAsync("What does the function at 0x140001000 do?");

// When binary changes (bytes edited):
session.UpdateContext();

// LLMSession automatically:
// 1. Detects changes since last context
// 2. Generates updated BinaryContextData
// 3. Includes in next system prompt as context update
```

---

### 5. AI Assistant Analysis

The AI assistant receives:

**System Prompt** (first message):
- Complete binary metadata
- All discovered functions and their relationships
- Cross-references showing data flow
- Imported/exported APIs
- Detected strings
- Identified patterns
- Recent patches

**Then the user query** (e.g., "Where should I patch to skip this validation?")

The AI:
1. References the provided binary analysis
2. Suggests specific addresses (0xADDRESS format)
3. Provides byte sequences for patches
4. Explains the binary structure
5. Maintains context across messages

---

## Example Usage

### Loading Binary and Creating Session

```csharp
// Load binary
var engine = new CoreEngine();
engine.LoadFile("program.exe");
engine.RunAnalysis();

// Create LLM client and analyzer
var llmClient = new LocalLLMClient("localhost", 1234);
var analyzer = new LLMAnalyzer(llmClient);

// Create session with full context
var session = new LLMSession(engine, analyzer);

// First query includes full system prompt context
var analysis = await session.QueryAsync("What is the entry point doing?");
Console.WriteLine(analysis);
```

### Context Update on Binary Modification

```csharp
// User edits binary in hex editor
engine.HexBuffer.WriteByte(0x1000, 0x90);  // NOP out instruction

// Update session context
session.UpdateContext();

// Next query includes context update
var followUp = await session.QueryAsync("What does that NOP do?");
```

---

## Performance Characteristics

| Operation | Time | Size |
|-----------|------|------|
| GenerateContext() | ~50-100ms | - |
| System Prompt | ~2-3 KB | Single prompt |
| TopCallChains | ~20-30ms | N/A |
| ExtractStrings | ~50-100ms | Depends on binary size |
| UpdateContext() | ~30-50ms | Full re-extract |

**Optimization Notes**:
- Context generation caches function relationships
- String extraction uses simple scanning (not regex)
- Pattern detection is heuristic-based
- Call chains are limited to depth 5 to avoid explosion

---

## Data Completeness

### What's Included
✅ All discovered functions with relationships  
✅ Complete cross-reference map  
✅ All imported/exported APIs  
✅ ASCII and Unicode strings  
✅ Pattern detection hints  
✅ All patches applied  
✅ Function call chains (execution paths)  
✅ Binary metadata and layout  

### What's Not Included
❌ Full disassembly (too large, AI can ask for specific functions)  
❌ Full hex dump (too large, AI can ask for specific offsets)  
❌ CFG visual representation (sent as structured data instead)  
❌ All strings (limited to first 50, AI can ask for more)  
❌ All functions (limited to top 50 by complexity)  

### On-Demand (AI Can Request)
- Disassembly of specific function: `"Show disassembly at 0x140001000"`
- Hex dump of region: `"Show bytes at 0x140002000 for 128 bytes"`
- More strings: `"List all strings matching pattern 'error'"`
- Pattern search: `"Find all XOR operations"`

---

## Integration Points

### Controllers
- `AnalysisController`: Runs analysis, triggers context generation
- `HexEditorController`: Detects byte changes, updates session
- `DisassemblyController`: Selects instructions, sends to AI

### UI Components
- `LLMPane`: Displays AI responses
- `SymbolTreeControl`: Shows discovered functions (from context)
- `SearchDialog`: Can query AI about search results

### Session Management
```csharp
// Available in AnalysisController or any handler
var analyzer = new LLMAnalyzer(llmClient);
var session = analyzer.GetOrCreateSession(_engine);  // Get/create

// Always has current context
await session.QueryAsync("User question here");

// Tracks conversation history
var history = session.GetHistory();
foreach (var msg in history)
    Console.WriteLine($"{msg.Role}: {msg.Content}");
```

---

## Future Enhancements

1. **Byte-level context**: Send specific bytes around cursor
2. **Decompiler integration**: Include pseudo-code in prompt
3. **Vulnerability patterns**: Pre-trained pattern library
4. **Performance profiling**: Show hot functions in context
5. **Taint analysis**: Track data flow from user input
6. **Memory layout visualization**: Show stack/heap structure

