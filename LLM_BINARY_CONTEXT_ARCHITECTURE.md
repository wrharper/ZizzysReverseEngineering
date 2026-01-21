# LLM System Architecture - Binary Context Design

## Overview

The LLM system is designed to provide an AI assistant that understands the complete binary analysis context. Instead of analyzing individual instructions in isolation, the AI now has full awareness of:

- Binary metadata (PE format, architecture, entry point)
- Analysis results (discovered functions, cross-references, symbols)
- Patches and modifications
- Function call chains and data flow

This allows the AI to provide better suggestions for reverse engineering tasks like finding patches, identifying patterns, and understanding binary behavior.

## Architecture Components

### 1. BinaryContextData (Data Container)
**File**: `ReverseEngineering.Core/LLM/BinaryContextData.cs`

Holds all information about the binary in a lightweight format suitable for LLM consumption:

```
BinaryContextData
├── Binary Metadata
│   ├── BinaryPath, BinaryFormat (PE/ELF/Mach-O)
│   ├── Is64Bit (architecture)
│   ├── ImageBase, ImageSize, EntryPoint
│   └── LastUpdated (timestamp)
├── Binary Content Summary
│   ├── TotalBytes
│   ├── ModifiedBytes (count)
│   └── RecentPatches (list of byte changes)
└── Analysis Data
    ├── Functions (50 most significant)
    ├── CrossReferences (30 most referenced)
    └── Symbols (50 most important)
```

**Summary Types** (lightweight for context):
- `FunctionSummary`: Address, name, size, block count, called addresses
- `CrossReferenceSummary`: From address, to address, reference type
- `SymbolSummary`: Address, name, type (function/variable/import/export)

### 2. BinaryContextGenerator (Data Pipeline)
**File**: `ReverseEngineering.Core/LLM/BinaryContextGenerator.cs`

Converts CoreEngine analysis data into LLM-friendly format and generates system prompts:

```csharp
public class BinaryContextGenerator
{
    // Convert CoreEngine → BinaryContextData
    public BinaryContextData GenerateContext()

    // Generate LLM system prompt from context
    public string GenerateSystemPrompt(BinaryContextData context)

    // Generate update notification when context changes
    public string GenerateContextUpdatePrompt(BinaryContextData previous, BinaryContextData current)
}
```

**Key Features**:
- Extracts only top N items (50 functions, 30 xrefs, 50 symbols) to keep prompt manageable
- System prompt ~2-3KB, includes all analysis results
- Update prompt (~500 bytes) notifies AI of changes since last context

### 3. LLMSession (Conversation Management)
**File**: `ReverseEngineering.Core/LLM/LLMSession.cs`

Manages a conversation with maintained context:

```csharp
public class LLMSession
{
    // Update context with current binary state
    public void UpdateContext()

    // Send query with current binary context as system prompt
    public async Task<string> QueryAsync(string userQuery)

    // Get conversation history
    public IReadOnlyList<ChatMessage> GetHistory()
}
```

**Behavior**:
- On initialization: `UpdateContext()` generates initial binary context
- On query: Sends full binary context as system prompt to LocalLLMClient
- On binary changes: Detects changes via context comparison, adds update to history
- Maintains message history for future multi-turn conversations

### 4. Enhanced LLMAnalyzer
**File**: `ReverseEngineering.Core/LLM/LLMAnalyzer.cs`

Updated to support both legacy (single-query) and context-aware (session-based) modes:

```csharp
public class LLMAnalyzer
{
    // New: Session-based API
    public LLMSession CreateSession()
    public LLMSession GetOrCreateSession()
    public async Task<string> QueryWithContextAsync(string query)

    // Legacy: Direct query methods (still supported)
    public async Task<string> ExplainInstructionAsync(Instruction inst)
    public async Task<string> GeneratePseudocodeAsync(List<Instruction> instrs)
    // ... more legacy methods
}
```

## Data Flow

### On Binary Load
```
FormMain
  ↓ (binary loaded)
CoreEngine.LoadFile()
  ↓ (analysis runs)
AnalysisController.RunAnalysisAsync()
  ↓ (analysis complete)
BinaryContextGenerator.GenerateContext()
  ↓ (context created)
LLMSession.UpdateContext()
```

### On User Query
```
User: "Find where this function is called from"
  ↓
LLMSession.QueryAsync(query)
  ↓ (generate system prompt)
BinaryContextGenerator.GenerateSystemPrompt(context)
  ↓ (send to LLM with context)
LocalLLMClient.ChatAsync(query, systemPrompt)
  ↓ (AI responds with full awareness)
Response: "Function is called at 0x1234, 0x5678..."
```

### On Binary Modification
```
HexEditor.OnPatchApplied()
  ↓ (bytes changed)
HexBuffer notifies CoreEngine
  ↓ (analysis updates)
CoreEngine re-analyzes affected regions
  ↓ (analysis complete)
LLMSession.UpdateContext()
  ↓ (detects change)
Adds update to session history
  ↓ (AI stays up to date)
LLMSession context now reflects changes
```

## System Prompt Example

When user queries the AI, it receives this system prompt (~2-3 KB):

```
You are an expert reverse engineer analyzing a binary executable.
Your task is to help understand and modify this binary code.

=== BINARY INFORMATION ===
File: myapp.exe
Format: PE (x64)
Architecture: x86-64
Image Base: 0x140000000
Entry Point: 0x140001000
Binary Size: 524288 bytes

=== ANALYSIS RESULTS ===
Discovered Functions: 127
Major Functions:
  main @ 0x140001000 (3456 bytes, 12 blocks)
  WndProc @ 0x140002000 (2048 bytes, 8 blocks)
  ... and 125 more

Cross-References Found: 456
Sample Cross-References:
  0x140001050 → 0x140002000 (call)
  ... and more

Symbols Resolved: 89
Key Symbols:
  Imports:
    kernel32.CreateWindowA @ 0x00007FFE (import)
    ntdll.RtlAllocateHeap @ 0x00007FFF (import)
  Exports:
    (none found)

=== YOUR ROLE ===
• Analyze assembly code patterns and identify their purpose
• Suggest locations for patches (NOP, JMP redirects, calls)
• Explain functionality of discovered functions
...
```

## Integration with UI

### AnalysisController Updates
- After analysis completes, notifies LLM system
- LLMSession automatically syncs context

### LLMPane Updates
- Displays LLM session responses
- Shows conversation history
- Updates when context changes

### MainMenuController
- Can start new LLM session from menu
- Persists session ID in settings for resuming

## Benefits

1. **Full Context Awareness**: AI understands entire binary structure, not just single instructions
2. **Smart Suggestions**: AI can identify patterns across functions, not just local code
3. **Consistent State**: Context automatically updates when binary changes
4. **Efficient**: Only top N items in context (keeps prompt manageable)
5. **Conversation Support**: Multiple queries maintain context and history
6. **Backward Compatible**: Legacy single-query methods still work

## Future Enhancements

1. **Byte-Level Context**: Could include actual byte snippets for specific addresses
2. **Streaming Responses**: Stream LLM responses for large outputs
3. **Pattern Library**: AI could build learned patterns across sessions
4. **Diff Context**: Show before/after for patches (binary diffs)
5. **Persistent Sessions**: Save/load conversation history between app restarts
6. **Multi-Query Analysis**: AI could suggest follow-up queries

## Usage Example

```csharp
// In AnalysisController or similar
var llmClient = new LocalLLMClient("http://localhost:1234");
var analyzer = new LLMAnalyzer(llmClient, coreEngine);

// Create new session with binary context
var session = analyzer.CreateSession();

// Query 1: Get function overview
var overview = await session.QueryAsync("What does the main function do?");
Console.WriteLine(overview);

// Query 2: AI maintains context from Query 1
var suggestions = await session.QueryAsync("Where should I patch to bypass this check?");
Console.WriteLine(suggestions);

// Check if context changed (binary was modified)
session.UpdateContext(); // Detects changes automatically

// Query 3: AI has updated context
var followUp = await session.QueryAsync("Is this still the best patch point?");
Console.WriteLine(followUp);
```

## Files Added

- `ReverseEngineering.Core/LLM/BinaryContextData.cs` - Data structures
- `ReverseEngineering.Core/LLM/BinaryContextGenerator.cs` - Context generation
- `ReverseEngineering.Core/LLM/LLMSession.cs` - Session management

## Files Modified

- `ReverseEngineering.Core/LLM/LLMAnalyzer.cs` - Added session API

## Build & Test

```bash
# All tests pass
dotnet test
# Result: 11/11 passing

# Build succeeds
dotnet build
# Result: 0 errors
```
