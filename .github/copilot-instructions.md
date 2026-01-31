# AI Coding Agent Instructions

## Quick Facts & Recent Updates

- **Status**: Production-ready (Phases 1-5 complete, Phase 6 debugger integration complete)
- **Framework**: .NET 10 Windows Forms
- **Architecture**: Strict separation between Core (logic) and WinForms (UI)
- **Build**: `dotnet build` | **Run**: `dotnet run --project ReverseEngineering.WinForms`
- **Test**: `dotnet test` (xUnit, minimal test coverage)

### Recent Changes (Phase 7 - Dynamic Token Management & Smart Trainer Detection)
- ✅ **LM Studio Integration**: Auto-detects actual server model and context window on app startup
- ✅ **Dynamic Token Baseline**: No more hardcoded defaults—reads actual context from `/api/v1/models` (e.g., 131,072 tokens)
- ✅ **Token Estimation**: CoreEngine calculates binary cost (raw bytes + disassembly) automatically
- ✅ **Trainer Necessity Detection**: Auto-flags when binary analysis exceeds 70% of available context
- ✅ **Smart Cache Management**: Cache loading decisions based on remaining token budget
- ✅ **Server-Controlled Settings**: Model, temperature, streaming all controlled by LM Studio (removed from app settings)
- ✅ **Token Budget Visualization**: AI Stats tab shows real token usage and warnings

---

## CRITICAL: Dynamic Token Architecture (NEW - Phase 7)

### How It Works

1. **App Startup** (FormMain.Load):
   - Connects to LM Studio `/v1/models` → gets actual loaded model name
   - Queries `/api/v1/models` → extracts `max_context_length` (e.g., 131,072 tokens)
   - Stores in `LocalLLMClient._maxContextTokens`
   - Displays in status bar: "✓ LM Studio connected | Model: openai/gpt-oss-120b | Context: 131072 tokens"

2. **Binary Analysis** (CoreEngine.RunAnalysis):
   - Estimates token cost:
     - Raw binary: `HexBuffer.Bytes.Length * 0.5` (binary is ~50% as dense as text)
     - Disassembly: `Disassembly.Count * 4` (avg 4 tokens per instruction)
     - Total: raw + disassembly
   - Calculates threshold: 70% of (usable context = max * 80% with 20% output reserve)
   - Logs estimates to UI: `📊 Token Estimate: Raw=X + Disasm=Y = Total tokens`

3. **Trainer Necessity** (CoreEngine.IsTrainerNeeded):
   - Returns TRUE if: `estimatedTokens > (maxContext * 0.8 * 0.7)`
   - Logs recommendation: `⚠️ TRAINER RECOMMENDED: Binary analysis exceeds 70% threshold. Use Trainer Phase 1...`
   - Cache system uses this to decide: full analysis or compressed patterns?

4. **SQL Cache Intelligence**:
   - Stores binary analysis with token metadata
   - On reload: checks if cached analysis fits in remaining token budget
   - If not: suggests Trainer Phase 1 for compression
   - If yes: loads cache immediately (no re-analysis)

### Token Math Example (Your Model: 131,072 tokens)

```
Available context:        131,072 tokens
Output reserve (20%):      26,214 tokens (reserved for model output)
Usable for input:         104,858 tokens
Trainer threshold (70%):   73,401 tokens (70% of usable)

For a 50MB binary:
  Raw bytes:     50MB * 0.5 ≈ 25,600 tokens
  1000 instructions * 4  ≈ 4,000 tokens
  Total:                   29,600 tokens
  Status: ✓ Fits easily (29.6K < 73.4K threshold)
  
For a 300MB binary:
  Raw bytes:    300MB * 0.5 ≈ 153,600 tokens
  5000 instructions * 4   ≈ 20,000 tokens
  Total:                   173,600 tokens
  Status: ⚠️ EXCEEDS threshold (173.6K > 73.4K)
  Action: TRAINER RECOMMENDED - Use Phase 1 to compress
```

### Changing Models Changes Everything

If you load a **different model in LM Studio** (e.g., smaller 4K context):
- App detects it immediately on startup
- Token estimates recalculate automatically
- Trainer recommendations update dynamically
- Cache loading strategy adjusts
- Status bar shows new context window

**No settings to change. No restart needed. Everything adapts.**

---

## Critical Concept: Address Modes

### File Offset Mode
- **When**: No disassembly loaded
- **Hex Editor Shows**: File offsets (0x0, 0x1000, etc. = raw binary positions)
- **Navigation**: Menu > Navigate > Go to File Offset
- **Use Case**: Direct binary editing before analysis

### Virtual Address Mode  
- **When**: Disassembly loaded (after File > Open analysis completes)
- **Hex Editor Shows**: Virtual addresses (0x400000, 0x401000, etc. = process memory addresses)
- **Navigation**: Menu > Navigate > Go to Virtual Address
- **Use Case**: Navigate to crash location from debugger output

### Why Two Modes?
- PE sections load at different offsets in memory vs in the file
- ASLR randomizes base addresses on each run
- File offset = static position in binary (always works)
- Virtual address = dynamic position in running process (varies per execution)

---

## Paradigm Shift: From Hardcoded to Intelligent Token Management (NEW - Phase 7)

### The Problem That Got Solved

**Before Phase 7:**
- Token budget was hardcoded (always 4,096, regardless of actual model)
- Cache system didn't know how many tokens it could safely use
- Large binaries (>50MB) would either crash the AI or exceed context unpredictably
- No way to know when Trainer compression was needed
- User settings exposed LLM parameters that should be server-controlled

**After Phase 7:**
- Token budget detected automatically from LM Studio server at app startup
- Real context window used for ALL calculations (131,072 for user's model, not 4,096)
- System auto-calculates whether full analysis fits or trainer compression is needed
- Cache loading respects token budget (won't load if would exceed 70% threshold)
- Settings UI simplified to reflect reality: server controls model parameters

### What Changed in Coding/Usage

**For Developers**:
- **Before**: Had to guess at token limits, hardcoded fallbacks, brittle heuristics
- **After**: Call `_llmClient.GetMaxTokens()` and `_core.IsTrainerNeeded()`, let system decide
- **Example**: Check binary size before analyzing:
  ```csharp
  int maxTokens = _llmClient.GetMaxTokens();
  var (raw, disasm, total, _, needsTrainer) = _core.GetTokenEstimate(maxTokens);
  
  if (needsTrainer)
      Logger.Warn("⚠️ Binary too large for full analysis, use Trainer Phase 1");
  else
      Logger.Info("✓ Binary fits in context, proceeding with full analysis");
  ```

**For End Users**:
- Status bar shows actual model name and context: `"Model: gpt-oss-120b | Context: 131072 tokens"`
- After analysis, logs show token estimates: `"📊 Token Estimate: Raw=262144 + Disasm=4000 = 266144 tokens"`
- Trainer recommendations appear automatically when binary exceeds 70% threshold
- AI stats tab will display real-time token usage (in progress)
- Settings simplified: no more Model/Temperature/Streaming options (server controls)

### Why This Matters

**Token Economy**:
- 50MB binary → 29,600 tokens (fits easily, cache loads instantly)
- 300MB binary → 173,600 tokens (exceeds threshold, Trainer Phase 1 compresses to ~500 tokens/query)
- System now makes intelligent decisions automatically instead of guessing

**Flexibility**:
- Switch LM Studio to smaller model (e.g., 4K context)?
  - App detects it on startup
  - Trainer recommendations update instantly
  - Cache gracefully degrades (patterns instead of full analysis)
  - No manual config needed
- Switch to larger model (131K context)?
  - Same automatic detection
  - Thresholds recalculate
  - More binaries fit without trainer compression
  - Everything adapts seamlessly

**Reliability**:
- No more "mysterious context exceeded" errors
- System knows exactly what fits before attempting analysis
- Token usage visible and predictable
- Crashes due to token limits become impossible

---

## System Architecture

### Three-Layer Design

```
ReverseEngineering.WinForms (UI Layer)
  ├─ Controllers (6x): 
  │  ├─ MainMenuController (File/Edit/View menus, Navigate, Debug)
  │  ├─ DisassemblyController (asm editing, sync with hex)
  │  ├─ HexEditorController (hex editing, sync with asm)
  │  ├─ AnalysisController (run analysis, update tree/graph)
  │  ├─ ThemeMenuController (dark/light toggle)
  │  └─ SearchController (find patterns)
  ├─ Views: DisassemblyControl, HexEditor, GraphControl, SymbolTreeControl, LLMPane, DebugLogControl
  ├─ Debug: AdvancedWindowsDebugger, ExternalDebugger, WindowsDebugger
  └─ Models: Theme, ThemeManager

ReverseEngineering.Core (Business Logic)
  ├─ Core: CoreEngine, Disassembler, HexBuffer, PatchEngine, Instruction, SearchManager, Logger
  ├─ Analysis: BasicBlockBuilder, ControlFlowGraph, FunctionFinder, CrossReferenceEngine, SymbolResolver, PatternMatcher
  ├─ ProjectSystem: ProjectModel, ProjectSerializer, ProjectManager, UndoRedoManager, AnnotationStore, SettingsManager
  ├─ LLM: LocalLLMClient, LLMAnalyzer
  └─ AILogs: AILogsManager

ReverseEngineering.Tests (xUnit)
  └─ Debug: Aion2DebugTest (test against real Aion2.exe binary)
```

### Typical User Workflow

```
1. File > Open (binary) → CoreEngine.LoadFile()
   ├─ Hex Editor shows FILE OFFSETS
   └─ Analysis begins...
   
2. Analysis completes (functions, xrefs found)
   ├─ Hex Editor switches to VIRTUAL ADDRESSES
   ├─ Navigate menu enabled for both VA and file offset
   └─ Disassembly/Hex/Analysis views synchronized
   
3. Debug > Run Binary (crash captured)
   ├─ AdvancedWindowsDebugger attaches with DEBUG_PROCESS
   ├─ Captures: Exception code, crash VA, access type
   └─ VA stored in DebugLogControl for navigation
   
4. Navigate > Go to Virtual Address (or crash pre-populated)
   ├─ Hex editor scrolls to crash location
   ├─ Shows bytes at crash address in disassembly context
   └─ Ready for inspection/patching
   
5. Hex Editor > Edit bytes or ASM (patching)
   ├─ Undo/Redo tracked automatically
   └─ File > Save Project (saves patches)
```

---

## Core Components

### `CoreEngine` (Central Orchestrator) - [ReverseEngineering.Core/CoreEngine.cs]
- **`LoadFile(path)`**: PE parse, detect x86/x64, build full disassembly
- **`AddressToOffset(va)`**: Convert virtual address → file offset (binary search)
- **`OffsetToAddress(offset)`**: Convert file offset → virtual address  
- **`RebuildInstructionAtOffset(offset)`**: Fast incremental re-disassemble after byte edits
- **`RunAnalysis()`**: Execute full analysis pipeline, then log token estimates
- **`IsTrainerNeeded(maxContextTokens)`**: Returns TRUE if binary exceeds 70% of available context
  - Used by ProjectManager to decide: full analysis or Trainer Phase 1?
- **`GetTokenEstimate(maxContextTokens)`**: Returns tuple (rawTokens, disassemblyTokens, totalTokens, maxContext, trainerNeeded)
  - `rawTokens = HexBuffer.Bytes.Length * 0.5` (binary density ~50% of text)
  - `disasmTokens = Disassembly.Count * 4` (avg 4 tokens per instruction)
  - `usableContext = maxContextTokens * 0.8` (20% reserve for output)
  - `threshold = usableContext * 0.7` (70% marks trainer necessity)
  - Called by cache system to decide what to load
- **Public State**: `Disassembly`, `HexBuffer`, `Functions`, `CFG`, `Is64Bit`

**Key Pattern**: 
- Disassembly list ordered by FileOffset, enables binary search for fast lookups
- `_addressToIndex` dict maps (Address → Disassembly index)
- Both methods run in O(log n) with pre-built cache
- **NEW**: Token estimates enable intelligent cache/trainer decisions

### `HexBuffer` (Mutable Binary + Change Tracking) - [ReverseEngineering.Core/HexBuffer.cs]
- **`WriteByte(offset, value)`**: Edit single byte, flag as modified
- **`WriteBytes(offset, bytes)`**: Bulk edit
- **`GetModifiedBytes()`**: Iterator of (offset, original, current) for export
- **Events**: `ByteChanged`, `BytesChanged`

### `DebugLogControl` (Debugger Integration) - [ReverseEngineering.WinForms/DebugLogControl.cs]
- **`RunBinaryWithDebuggerAsync(binaryPath, CoreEngine)`**: Launch AdvancedWindowsDebugger
- **`GetLastCrashVirtualAddress()`**: Returns stored crash VA for menu pre-population
- **Public Method**: Exposes crash VA to MainMenuController for navigation dialog
- **Events**: `RunRequested` for manual triggering

### `HexEditorControl` (Dual-Mode Address Display) - [ReverseEngineering.WinForms/HexEditor/HexEditorControl.cs]
- **`GoToAddress(ulong va)`**: Navigate to virtual address (uses CoreEngine mapping)
- **`GoToFileOffset(int offset)`**: Navigate to file offset directly (bypasses VA conversion)
- **Auto-Mode Switch**: 
  - No CoreEngine = shows file offsets
  - With CoreEngine = shows virtual addresses from `OffsetToAddress()`
- **Address Column Logic**: 
  - Renderer checks `_core` to determine display mode
  - Cached lookups via `_addressCache` (O(1) after first hit)

### `AdvancedWindowsDebugger` (Windows Debug API) - [ReverseEngineering.WinForms/Debug/AdvancedWindowsDebugger.cs]
- **`DebugBinaryAsync(binaryPath, callback)`**: Attach debugger, catch exceptions
- **Debug Event Handling**: 
  - Uses `fixed byte[528]` for proper unmanaged DEBUG_EVENT marshaling
  - Manually parses exception data from union buffer at byte offsets
  - Extracts: Exception code, Crash Address (VA), Access type
- **Output**: String with format `"EXCEPTION_TYPE @ 0xVA [ACCESS @ 0xFault]"`
- **Critical Fix**: Changed from managed `byte[]` to `fixed byte[]` for reliable struct marshaling

---

## Navigate Menu (NEW - Phase 6)

**Location**: Menu > Navigate (in existing Navigate menu)
**Options**:
1. **Go to Address...** (existing, Ctrl+G) - Smart dialog adapts to current mode
2. **[Separator]**
3. **Go to File Offset** - Direct file offset navigation (input as hex)
4. **Go to Virtual Address** - Virtual address navigation (input as hex, pre-populates with crash VA if available)

**Implementation**:
- Both options always visible (not conditional)
- File offset works even when viewing virtual addresses
- Virtual address option populates with crash VA from debugger (helps quick navigation to crashes)

---

## Debug Workflow

### Crash Capture Pipeline
```
1. Debug > Run Binary
   ↓
2. AdvancedWindowsDebugger.DebugBinaryAsync()
   - CreateProcessW() with DEBUG_PROCESS flag
   - WaitForDebugEvent() loop
   - Parse EXCEPTION_DEBUG_EVENT from unmanaged buffer
   ↓
3. Crash info: Exception code, VA, faulting address stored
   ↓
4. DebugLogControl.RunBinaryWithDebuggerAsync()
   - Regex extract VA: @"0x([0-9A-Fa-f]{1,16})"
   - Store in _lastCrashVirtualAddress
   ↓
5. Navigate > Go to Virtual Address (pre-populated with crash VA)
   ↓
6. HexEditorControl.GoToAddress() navigates to crash
```

### File Offset Access for Debugger
- Crash captured as Virtual Address (from running process)
- To navigate in hex editor with disassembly loaded:
  - **Option 1**: Menu > Navigate > Go to Virtual Address (direct VA)
  - **Option 2**: Menu > Navigate > Go to File Offset (if you know FO equivalent)
  - Hex editor handles conversion internally

---

## Common Tasks

### Navigate to a Crash Location
1. Load binary (File > Open) - wait for analysis
2. Debug > Run Binary - trigger crash in running binary
3. Observe crash VA in Debug Log (cyan text: `[File Offset] 0x...`)
4. Menu > Navigate > Go to Virtual Address (auto-populated or paste crash VA)
5. Hex editor scrolls to crash address, shows surrounding bytes

### Navigate by File Offset (Before Analysis)
1. Load binary (File > Open) - analysis in progress
2. Menu > Navigate > Go to File Offset
3. Enter hex offset (e.g., 0x1000 = 4096 bytes into file)
4. Hex editor shows file offset column

### Navigate by Virtual Address (After Analysis)
1. Complete workflow above (analysis done, VA mode active)
2. Menu > Navigate > Go to Virtual Address
3. Enter hex VA (e.g., 0x400000 = image base)
4. Hex editor shows VA column with proper mapping

### Edit Bytes & Patch
1. Navigate to location (either method above)
2. Click hex byte to edit
3. Changes tracked in HexBuffer.Modified[]
4. File > Save Project (preserves patches)
5. Undo/Redo available (Edit menu or Ctrl+Z/Ctrl+Y)

---

## Performance Optimizations (Phase 1)

- **Binary Search**: O(log n) address lookups instead of O(n) linear scan
- **Address Cache**: `_addressCache` dict for hex editor (prevents repeated binary searches)
- **Debounce**: 80ms delay on ASM editing before re-assembly (prevents lag during rapid typing)
- **Lazy Analysis**: CFG/xrefs built on-demand, not automatically on load

---

## Trainer Architecture for Large Files (Phase 7 - Design)

### Purpose
**Trainer is essential for binaries > 200MB** where token economy becomes critical. Instead of sending raw bytes/disassembly to AI (~68M+ tokens), trainer compresses binary analysis into pattern descriptors (~500 tokens per query).

### How It Works: Three Phases

**Phase 1: Local Analysis (Training)**
- Binary loaded → Local pattern extraction (NO AI COST)
- Extract: instruction patterns, prologues, loops, control flow, cross-references
- Generate embeddings for semantic similarity
- Store in SQLite cache keyed by binary hash
- One-time cost: ~100M tokens (local analysis, not sent to AI)

**Phase 2: AI Query (Compressed)**
- User asks: "What does function at 0x1000 do?"
- System retrieves pattern descriptor (~500 tokens max):
  - Pattern signature (e.g., "prologue_x64_frame_setup")
  - Control flow summary (blocks, branches)
  - Function calls (callees, callers)
  - Data cross-references
- Send ONLY descriptor to AI, not raw bytes
- AI responds with context-aware analysis
- Cost per query: ~500 tokens (68M+ reduction!)

**Phase 3: Semantic Search (Pattern Matching)**
- User asks: "Find all functions similar to this"
- Embeddings enable similarity search WITHOUT sending binary to AI
- Return grouped results with match percentages
- Cost: 0 tokens (local embedding search)

### Token Math
```
Without Trainer:
  Raw bytes:     34M tokens
  Disassembly:   34M tokens
  Total:         68M+ tokens per binary (IMPOSSIBLE)

With Trainer:
  Phase 1 (one-time, local): ~100M tokens spent locally
  Phase 2 (per query):       ~500 tokens to AI
  Phase 3 (per search):      0 tokens (local embeddings)
  Cost per query:            136,000x reduction (68M → 500 tokens)
```

---

## SQL Cache System (Phase 7 - Design)

### Purpose
**Automated cache management** with project-based database organization. Each project gets its own SQLite database with organized tables for fast lookups.

### Database Organization

**Location**: `AppData/ZizzysReverseEngineering/Cache/[ProjectName].db`

**Tables per Database**:

```sql
-- Pattern index (from Phase 1 trainer analysis)
CREATE TABLE Patterns (
  PatternID INTEGER PRIMARY KEY,
  BinaryHash TEXT NOT NULL,           -- SHA256 of binary
  Address INTEGER NOT NULL,           -- Virtual address or offset
  Signature TEXT,                     -- "prologue_x64", "loop_x86", etc.
  InstructionBytes BLOB,              -- Compressed pattern bytes
  Embedding BLOB,                     -- Vector for similarity search
  ControlFlowSummary TEXT,            -- JSON: blocks, branches
  References BLOB,                    -- Function callers/callees (packed)
  CreatedAt DATETIME,
  UNIQUE(BinaryHash, Address)
);

-- Disassembly cache (parsed instructions for fast retrieval)
CREATE TABLE Disassembly (
  AddressRange TEXT PRIMARY KEY,      -- "0x1000-0x2000"
  BinaryHash TEXT,
  Instructions BLOB,                  -- Compressed Iced instruction list
  InBasicBlock TEXT,                  -- Function/block context
  CreatedAt DATETIME
);

-- String literals and data
CREATE TABLE Strings (
  StringID INTEGER PRIMARY KEY,
  BinaryHash TEXT,
  Address INTEGER,
  StringValue TEXT,
  Type TEXT,                          -- "ASCII", "UNICODE", "REFERENCE"
  References BLOB,                    -- Cross-references (packed)
  UNIQUE(BinaryHash, Address)
);

-- Symbols (functions, imports, exports)
CREATE TABLE Symbols (
  SymbolID INTEGER PRIMARY KEY,
  BinaryHash TEXT,
  Address INTEGER,
  Name TEXT,
  Type TEXT,                          -- "FUNCTION", "IMPORT", "EXPORT", "LABEL"
  Size INTEGER,
  Section TEXT,
  CreatedAt DATETIME,
  UNIQUE(BinaryHash, Address)
);

-- Cross-references (code→code, code→data relationships)
CREATE TABLE CrossReferences (
  XRefID INTEGER PRIMARY KEY,
  BinaryHash TEXT,
  SourceAddress INTEGER,
  TargetAddress INTEGER,
  RefType TEXT,                       -- "CALL", "JMP", "DATA_READ", "DATA_WRITE"
  Context TEXT,                       -- Instruction context (MOV RAX, ..., etc)
  UNIQUE(BinaryHash, SourceAddress, TargetAddress, RefType)
);

-- Bytes cache (raw binary sections for editing)
CREATE TABLE ByteRanges (
  RangeID INTEGER PRIMARY KEY,
  BinaryHash TEXT,
  StartOffset INTEGER,
  EndOffset INTEGER,
  Data BLOB,                          -- Uncompressed bytes
  Compressed BLOB,                    -- Optional ZSTD compressed version
  CreatedAt DATETIME,
  UNIQUE(BinaryHash, StartOffset, EndOffset)
);

-- Metadata (cache statistics and management)
CREATE TABLE CacheMetadata (
  Key TEXT PRIMARY KEY,
  Value TEXT,
  LastUpdated DATETIME
);
-- Example keys: "TotalPatterns", "TotalSymbols", "CacheVersion", "BinaryVersion"
```

### Cache Lifecycle

**On Project Load**:
1. Get project name and binary hash
2. Check if `AppData/.../Cache/[ProjectName].db` exists
3. If no: create new database
4. If yes: validate BinaryHash matches (stale cache detection)
5. Populate cache tables during analysis phase

**During Analysis**:
1. As functions are discovered → insert into `Symbols` table
2. As xrefs are found → insert into `CrossReferences` table
3. As strings are extracted → insert into `Strings` table
4. If Trainer enabled: run Phase 1 analysis → populate `Patterns` table with embeddings

**On Query**:
1. Cache hit: retrieve from SQL (O(1) lookup)
2. Cache miss: compute + store in SQL for next time
3. AI gets pattern descriptor from SQL (never raw bytes)

**Auto-Cleanup**:
- Track access count per row
- On project close: remove unused entries (no queries last 30 days)
- On project load: validate all cache entries (checksums, refs valid)

### Integration with Existing Systems

**CoreEngine Integration**:
```csharp
// In CoreEngine.cs
public class CoreEngine
{
    private CacheManager _cache;  // NEW: SQL cache system
    
    public void LoadFile(string path)
    {
        // ... existing code ...
        
        // NEW: Try to load from cache first
        string projectHash = ComputeHash(path);
        if (_cache.TryLoadCached(projectHash, out var cachedData))
        {
            Disassembly = cachedData.Disassembly;
            Functions = cachedData.Functions;
            CrossReferences = cachedData.CrossReferences;
            // ... restore other state ...
            return;  // Fast path: skip analysis, use cache
        }
        
        // Cache miss: continue with normal analysis
        RunAnalysis();
        
        // NEW: Store in cache for next time
        _cache.StoreAnalysisResults(projectHash, this);
    }
}
```

**LLMAnalyzer Integration**:
```csharp
// In LLMAnalyzer.cs
public async Task<string> ExplainInstructionAsync(Instruction instr)
{
    // NEW: Check cache for pattern descriptor (no raw bytes)
    var descriptor = _cache.GetPatternDescriptor(instr.Address);
    if (descriptor != null)
    {
        // Use compact descriptor instead of raw instruction
        return await _llmClient.CompleteAsync(
            $"Explain this pattern: {descriptor}"
        );
    }
    
    // Fallback: use raw instruction (existing behavior)
    return await _llmClient.CompleteAsync(
        $"Explain: {instr.Mnemonic} {instr.Operands}"
    );
}
```

**ProjectManager Integration**:
```csharp
// In ProjectManager.cs
public void OpenProject(string projectPath)
{
    var project = ProjectSerializer.LoadProject(projectPath);
    
    // NEW: Auto-initialize cache based on project name
    string dbPath = Path.Combine(
        CacheDir, 
        $"{Path.GetFileNameWithoutExtension(projectPath)}.db"
    );
    _cache = new CacheManager(dbPath);
    
    // Load binary and populate cache
    _core.LoadFile(project.BinaryPath);
}

public void CloseProject()
{
    // NEW: Auto-cleanup unused cache entries
    _cache?.Cleanup();
    _cache?.Dispose();
}
```

---

## Implementation Roadmap

### Priority 1: SQL Cache Foundation (Week 1-2)
- [x] Create `CacheManager` class
- [x] Implement SQLite table schema
- [x] Add cache write operations (insert/update)
- [x] Add cache read operations (select/query)
- [x] Integrate with CoreEngine (store symbols, xrefs, strings)
- [ ] Test cache hits on project reload

### Priority 2: Trainer Phase 1 - Local Analysis (Week 2-3)
- [ ] Create `TrainerAnalyzer` class
- [ ] Extract instruction patterns (prologues, common idioms)
- [ ] Build control flow summaries (block counts, branches)
- [ ] Generate embeddings for patterns (use simple cosine similarity first)
- [ ] Store patterns in `Patterns` table
- [ ] Update AIStatsControl to show trainer progress

### Priority 3: Trainer Phase 2 - AI Integration (Week 3-4)
- [ ] Modify `LLMAnalyzer` to use pattern descriptors
- [ ] Change LocalLLMClient to respect token limits (20% reserve)
- [ ] Test with real LLM (compare raw vs descriptor responses)
- [ ] Measure token savings on large binaries

### Priority 4: Trainer Phase 3 - Semantic Search (Week 4-5)
- [ ] Implement embedding-based similarity search
- [ ] Add search UI in SearchDialog
- [ ] Test multi-pattern matching (find all similar functions)
- [ ] Zero-token cost verification

### Priority 5: Auto-Cleanup & Optimization (Week 5-6)
- [ ] Implement cache validation on load
- [ ] Add access tracking (query counts)
- [ ] Auto-remove stale entries (30+ day threshold)
- [ ] Compression for large data (ZSTD)
- [ ] Performance profiling (cache miss rates, query speed)

---

## When to Use What

| Scenario | Use | Benefit |
|----------|-----|---------|
| Binary fits in context (< 70% threshold) | Full analysis + SQL cache | Complete information for AI queries |
| Binary doesn't fit (> 70% threshold) | Trainer Phase 1 + patterns | 98%+ token savings, AI patterns available |
| Model context unknown | Auto-detect from `/api/v1/models` | Always correct context, no guessing |
| Switch LM Studio models | Auto-recalculate on app startup | Trainer recommendations update instantly |
| New binary, first load | Check token budget first | Decide: full analysis or trainer compression |
| Same binary, reload | Load from cache (if budget allows) | Instant load (99% cache hit rate) |
| Cache too large for model | Load trainer patterns instead | Graceful degradation to pattern-based AI |
| User asks about function | Use cached analysis if available | Instant response, no AI cost if patterns exist |
| Find similar functions | Trainer embeddings (local search) | Instant results, zero AI tokens needed |

---

## Database File Example

```
AppData/ZizzysReverseEngineering/Cache/
  ├─ Aion2.db                     (project: Aion2.exe, 135MB)
  │  ├─ Patterns table: 15,847 rows (functions indexed with embeddings)
  │  ├─ Symbols table: 3,521 rows (imports, exports, discovered funcs)
  │  ├─ CrossReferences: 42,102 rows (call graph + data xrefs)
  │  ├─ Strings: 8,456 rows (ASCII + UNICODE strings)
  │  ├─ Disassembly: 128 rows (pre-parsed instruction ranges)
  │  └─ CacheMetadata: version, binary hash, last updated
  │
  └─ malware_sample.db            (project: malware.exe, 8MB)
     ├─ Patterns: 342 rows
     ├─ Symbols: 89 rows
     └─ ... (smaller project, fewer tables)
```

---

## Byte Caching & Large File Optimization

### Current Approach (HexBuffer)
- **`_bytes`**: Full binary loaded into memory (managed byte array)
- **`Modified[]`**: Sparse bitset tracking changed bytes (only flags, not full copies)
- **Efficiency**: Works well for binaries up to 200MB on modern systems

### Issue with Larger Files (>200MB)
- Full in-memory load becomes impractical
- Context window limitations for AI analysis (can't paste entire hex dumps)
- Analysis queries on large sections require external tools
- **Token Cost Crisis**: A 135MB binary = ~34M tokens for raw bytes + ~34M+ tokens for disassembly = **68M+ tokens total**. Impossible to cache in AI context.

### Future Optimization Strategies

**Strategy 1: SQLite Index (Local Storage)**
```csharp
// Cache byte ranges in SQLite for O(1) lookup
// Structure: addressRange | bytes | function | basicBlock
CREATE TABLE ByteIndex (
  StartVA INTEGER,
  EndVA INTEGER,
  Bytes BLOB,
  FunctionName TEXT,
  InBasicBlock INTEGER
);
```
- **Benefits**: Fast queries, offline analysis, searchable
- **Drawback**: Solves local storage, but doesn't help AI token usage
- **Use Case**: When you need bytes locally without re-disassembling

**Strategy 2: Byte Chunking (Low-memory)**
```csharp
// Load 64KB chunks on-demand
// Store chunk map: address → FileOffset
private Dictionary<ulong, byte[]> _chunkCache;  // Lazy-loaded 64KB chunks
```
- **Benefits**: Minimal memory footprint
- **Drawback**: Still requires loading raw bytes for AI analysis
- **Use Case**: Memory-constrained environments

**Strategy 3: AI Context Optimization with Trainer (REQUIRED FOR AI)**
- **Train embedding model on byte patterns** (ONYX, similar tools)
- **Store pattern signatures instead of raw bytes**
- **Query format**: "What does function at 0xA0 do?" → AI receives compressed pattern signature, not raw hex
- **Token Savings**: 68M+ tokens → ~100K tokens (680x+ reduction!)
- **How It Works**:
  1. Trainer learns to recognize: instruction patterns, control flow patterns, common idioms
  2. Binary gets compressed to: "pattern ID + offsets" instead of raw bytes + disassembly
  3. AI receives compressed representation: understands context without token bloat
  4. Can ask questions: "Find all functions matching pattern X" without loading entire binary

### Why Trainer Is Essential for AI Integration
- **Without trainer**: Can't use AI on files > ~50MB (context explodes)
- **With trainer**: Can analyze 500MB+ files with ~100K token budget
- **Trade-off**: One-time training cost vs. unlimited analysis queries

### How Trainer Architecture Works (Zero Token Cost for Queries)

**Phase 1: Training (One-time, Local, No AI)**
```
Binary (135MB) 
  ↓
Local Pattern Analysis (no tokens spent):
  - Extract instruction patterns (prologue, loops, common idioms)
  - Map control flow (block boundaries, branch targets)
  - Find cross-references (callers, callees)
  - Generate embeddings for semantic similarity
  ↓
Store in Local Database:
  CREATE TABLE Patterns (
    PatternID INT,
    Signature TEXT,          -- "prologue_x64_frame_setup"
    InstructionRange BLOB,   -- Compressed pattern bytes
    Embedding BLOB,          -- Vector for semantic search
    ControlFlowSummary TEXT, -- "3 basic blocks, 2 branches"
    References INT[]         -- Function callers/callees
  );
```

**Phase 2: Query (Cheap, Token-Efficient)**
```
User Question: "What does the function at 0xA0 do?"
  ↓
System (Local):
  1. Look up pattern at 0xA0 in database
  2. Retrieve: signature + control flow + references
  3. Don't load raw bytes/disassembly
  ↓
Send to AI (only ~500 tokens):
  "Pattern: prologue_x64_frame_setup
   Flow: 3 basic blocks, 2 conditional branches
   Calls: malloc(), memcpy()
   Called by: init(), cleanup()
   Context: Standard Windows x64 function"
  ↓
AI responds without token bloat
```

**Phase 3: Semantic Search (Pattern Matching)**
```
User Question: "Find all functions similar to this pattern"
  ↓
System (Local):
  1. Generate embedding for query pattern
  2. Search embedding database (instant, local)
  3. Return top N matches with similarity scores
  ↓
Send to AI (only matching summaries):
  "Found 47 similar functions:
   - Function 0x1000: 92% match (prologue_x64_frame_setup)
   - Function 0x2000: 89% match (prologue_x64_frame_setup)
   ..."
  ↓
AI can reason about the group without seeing raw binary
```

**Token Breakdown:**
- Training: ~100M tokens (one-time, local, worth it)
- Per Query: ~500 tokens (pattern descriptor only)
- Savings: 68M → 0.5M tokens per binary analysis (136,000x reduction!)

---

### When to Implement Each Strategy
- **SQLite**: Binary size > 500MB AND need offline local queries
- **Byte Chunking**: RAM < 2GB AND binary > 200MB
- **Trainer (CRITICAL)**: Want to use AI + binary > 50MB (or any large-scale analysis)

---

## Cache Strategy with Token Awareness (NEW - Phase 7)

### Decision Tree: What to Load

Every time a project loads, the system now uses this intelligent cascade:

```
Project Load
  ↓
1. Check if cached analysis exists?
   ├─ YES: Get token estimate from cache metadata
   │        ├─ Available context > estimated tokens?
   │        │  ├─ YES: Load cache immediately ✓ (skip analysis, instant load)
   │        │  └─ NO: Skip cache, go to step 2
   │        │
   │        └─ Cache has Trainer patterns?
   │           └─ Use patterns for AI queries (0 token cost!) ✓
   │
   ├─ NO: Go to step 2
   └─ MISS: Go to step 2

2. Available token budget?
   ├─ > 90% of threshold: Run FULL analysis (all functions, CFG, xrefs) ✓
   ├─ 50-90%: Run LIMITED analysis (functions + symbols only, skip CFG)
   └─ < 50%: Queue for Trainer Phase 1 extraction (no full analysis yet)

3. After analysis:
   ├─ Store in cache with token metadata
   ├─ If trainer needed: Run Phase 1 → generate patterns + embeddings
   └─ Future loads: Check cache FIRST (99% will hit cache, most efficient)
```

### Example Workflows

**Scenario 1: First Load of 50MB Binary (131K context model)**
```
1. Cache miss (new binary)
2. Token estimate: 29,600 tokens → fits easily (29.6K < 73.4K threshold)
3. Run FULL analysis (functions, CFG, xrefs)
4. Store in cache with: metadata, token estimates, analysis results
5. Result: Ready for AI queries ✓
```

**Scenario 2: Reload Same 50MB Binary**
```
1. Cache hit (same binary, same hash)
2. Cached tokens: 29,600 → still fits (no new analysis needed)
3. Load from cache (instant, skip analysis entirely)
4. Cache contains patterns if Trainer Phase 1 ran
5. Result: Ready to go ✓
```

**Scenario 3: First Load of 300MB Binary (131K context model)**
```
1. Cache miss (new binary)
2. Token estimate: 173,600 tokens → EXCEEDS threshold (173.6K > 73.4K)
3. Log warning: ⚠️ TRAINER RECOMMENDED
4. Run LIMITED analysis (symbols + imports only, ~15K tokens)
5. Queue Trainer Phase 1 (extracts 10K+ patterns from instructions)
6. Store in cache: limited analysis + patterns/embeddings
7. For AI queries: Use patterns (500 tokens) instead of full analysis (173K) ✓
8. Result: 98.7% token savings!
```

**Scenario 4: Switch to Smaller Model (4K context)**
```
1. App starts with new model loaded in LM Studio
2. Auto-detects: 4,096 tokens (not 131,072)
3. Token threshold drops: 70% of 3,276 = 2,293 tokens
4. Previously cached 50MB binary now DOESN'T fit
5. Action: Auto-load Trainer patterns from cache instead
6. AI can now reason about binary using compressed patterns (within budget)
7. Result: Graceful degradation ✓
```

### Implementation in CoreEngine

```csharp
public class CoreEngine
{
    // Exposed for cache decisions
    public bool IsTrainerNeeded(int maxContextTokens)
    {
        var (rawTokens, disasmTokens, totalTokens, _, needed) = GetTokenEstimate(maxContextTokens);
        return needed;
    }
    
    public (int rawTokens, int disasmTokens, int totalTokens, int maxContext, bool trainerNeeded) GetTokenEstimate(int maxContextTokens)
    {
        int rawTokens = (int)(HexBuffer.Bytes.Length * 0.5);
        int disasmTokens = Disassembly.Count * 4;
        int totalTokens = rawTokens + disasmTokens;
        
        int usableContext = (int)(maxContextTokens * 0.8);  // 20% reserve for output
        int threshold = (int)(usableContext * 0.7);        // 70% threshold for trainer
        
        bool needsTrainer = totalTokens > threshold;
        
        return (rawTokens, disasmTokens, totalTokens, maxContextTokens, needsTrainer);
    }
    
    // Post-analysis logging
    public void LogTokenEstimates(int maxContextTokens)
    {
        var (raw, disasm, total, max, trainer) = GetTokenEstimate(maxContextTokens);
        
        Logger.Log($"📊 Token Estimate: Raw={raw} + Disasm={disasm} = {total} tokens");
        Logger.Log($"📊 Available Context: {max} tokens (usable: {(int)(max * 0.8)} with 20% output reserve)");
        
        int threshold = (int)(max * 0.8 * 0.7);
        if (trainer)
        {
            Logger.Warn($"⚠️ TRAINER RECOMMENDED: Binary analysis exceeds 70% of available context ({total}/{threshold} threshold)");
            Logger.Info("💡 Use Trainer Phase 1 to compress analysis into embeddings and patterns for AI queries");
        }
        else
        {
            Logger.Info($"✓ Analysis fits comfortably ({total}/{threshold} threshold)");
        }
    }
}
```

### ProjectManager Integration

```csharp
public async Task<bool> OpenProjectAsync(string projectPath)
{
    var project = ProjectSerializer.LoadProject(projectPath);
    
    // NEW: Get available token budget from LLM client
    int maxTokens = await _llmClient.GetModelContextLengthAsync();
    
    // NEW: Check cache before running analysis
    string dbPath = Path.Combine(CacheDir, $"{project.Name}.db");
    var cache = new CacheManager(dbPath);
    
    if (cache.TryLoadCached(project.BinaryHash))
    {
        // Cache hit: check if it still fits in budget
        var cachedTokenEstimate = cache.GetTokenEstimate();
        if (cachedTokenEstimate <= (maxTokens * 0.8 * 0.7))
        {
            // Still fits! Load from cache
            _core.LoadFromCacheData(cache.GetAnalysisResults());
            Logger.Info("✓ Loaded from cache (no re-analysis needed)");
            return true;
        }
        else
        {
            // Budget changed (smaller model loaded): load patterns instead
            _core.LoadFromPatterns(cache.GetPatterns());
            Logger.Warn("⚠️ Cache too large for current model, loaded patterns instead");
            return true;
        }
    }
    
    // Cache miss: decide what analysis to run
    _core.LoadFile(project.BinaryPath);
    
    if (_core.IsTrainerNeeded(maxTokens))
    {
        // Too big for full analysis: run Trainer Phase 1
        Logger.Info("⏳ Running Trainer Phase 1 (pattern extraction)...");
        await _core.RunTrainerPhase1Async();
        cache.StorePatterns(_core.GetPatterns());
    }
    else
    {
        // Fits: run full analysis
        Logger.Info("⏳ Running full analysis...");
        _core.RunAnalysis();
        cache.StoreAnalysisResults(_core);
    }
    
    return true;
}
```

---

## External Dependencies

- **Iced 1.21.0**: x86/x64 disassembler
- **Keystone.Net**: x86/x64 assembler (wraps Keystone.dll)
- **.NET 10 WinForms**: Native Windows UI
- **xUnit**: Test framework
- **Windows Debug API**: Kernel32.dll (CreateProcessW, WaitForDebugEvent, etc.)

---

## File Organization

### ReverseEngineering.Core/
- `CoreEngine.cs` - Central orchestrator
- `HexBuffer.cs` - Mutable binary + change tracking
- `Disassembler.cs` - PE parsing + instruction decode
- `Analysis/` - CFG, functions, xrefs, symbols
- `ProjectSystem/` - Save/load, undo/redo
- `LLM/` - AI integration

### ReverseEngineering.WinForms/
- `MainWindow/` - Menu controller (entry point for user actions)
- `HexEditor/` - Hex viewer + renderer (dual mode)
- `DisassemblyControl.cs` - ASM view
- `DebugLogControl.cs` - Debug output + crash storage
- `Debug/` - Debugger implementations (Advanced, External, Basic)
- `GraphView/` - CFG visualization
- `SymbolView/` - Function/symbol tree

### Navigation Tips
1. **User clicks menu** → MainMenuController handles event
2. **Core logic needed** → MainMenuController calls CoreEngine methods
3. **UI update needed** → MainMenuController updates controls (HexEditor, DisassemblyControl, etc.)
4. **Complex operation** → Controller spawns AnalysisController or DebugLogControl task

---

## Recent Bug Fixes

- ✅ Fixed DEBUG_EVENT marshaling: `fixed byte[528]` instead of managed `byte[]`
- ✅ Fixed hex editor buffer null check in GoToFileOffset()
- ✅ Fixed menu handler: GoToFileOffset calls `GoToFileOffset()` not `GoToAddress()`
- ✅ Fixed address mode logic: Both file offset and VA navigation work simultaneously

---

## Analysis Pipeline (Phase 2 - Implemented)

All components live in `ReverseEngineering.Core.Analysis/` namespace.

### BasicBlockBuilder
- Input: `List<Instruction>`, entry point address
- Identifies block boundaries (JMP, RET, conditional branches, CALL)
- Output: `ControlFlowGraph` with predecessors/successors

### FunctionFinder
- Discovers functions via: PE entry point, exported symbols, prologues, call graph
- Output: `List<Function>` (each with own CFG)

### CrossReferenceEngine
- Tracks code→code (JMP/CALL), code→data (MOV RIP-relative), data→code
- Output: `Dictionary<ulong, List<CrossReference>>`

### SymbolResolver
- Collects symbols from: imports, exports, discovered functions, user annotations
- Output: `Dictionary<ulong, Symbol>` (fast address→name lookup)

### PatternMatcher
- Byte patterns: `"55 8B ?? C3"` (wildcards with `??`)
- Instruction predicates: Match by mnemonic, operand type, etc.

---

## LLM Integration (Phase 4 - Complete)

### LocalLLMClient
- Wraps LM Studio HTTP API (default `localhost:1234`)
- Methods: `ExplainInstructionAsync()`, `GeneratePseudocodeAsync()`, `AnalyzePatternAsync()`
- **TODO**: Query model's `context_length` from LM Studio API to dynamically adjust token limits
  - LM Studio exposes model metadata including max context window
  - Should call `/v1/models` endpoint and parse `context_length` from active model
  - Use this to set `_maxContextTokens` and adjust query compression accordingly

### Proposed Implementation
```csharp
// Add to LocalLLMClient.cs
private int _maxContextTokens = 4096;  // Default fallback
private int _currentTokenUsage = 0;

public async Task<int> GetModelContextLengthAsync()
{
    try
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
        var json = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);
        foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var id = model.GetProperty("id").GetString();
            if (id == Model && model.TryGetProperty("context_length", out var ctxLen))
            {
                _maxContextTokens = ctxLen.GetInt32();
                return _maxContextTokens;
            }
        }
    }
    catch { }
    
    return _maxContextTokens;
}

// Get current context usage as percentage
public float GetContextUsagePercent()
{
    if (_maxContextTokens <= 0)
        return 0f;
    return (_currentTokenUsage / (float)_maxContextTokens) * 100f;
}

// Estimate tokens in text (rough: 1 token ≈ 4 chars)
private int EstimateTokens(string text)
{
    return (text?.Length ?? 0) / 4;
}

// Use in CompleteAsync to track usage and reserve 20% for response
public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
{
    int maxOutput = _maxContextTokens / 5;  // Reserve 20% for output
    int maxInput = _maxContextTokens - maxOutput;
    
    // Estimate token usage
    int promptTokens = EstimateTokens(prompt);
    if (promptTokens > maxInput)
    {
        // Truncate prompt
        int safeChars = maxInput * 4;
        prompt = prompt.Substring(0, Math.Min(safeChars, prompt.Length));
        promptTokens = maxInput;
    }
    
    _currentTokenUsage = promptTokens;
    
    // Warn if approaching limit
    if (GetContextUsagePercent() > 80f)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[WARNING] Context usage at {GetContextUsagePercent():F1}% " +
            $"({_currentTokenUsage}/{_maxContextTokens} tokens)");
    }
    
    // ... rest of implementation
}
```

**Usage in UI:**
```csharp
// In AnalysisController or LLMPane
float usage = _llmClient.GetContextUsagePercent();
statusLabel.Text = $"Context: {usage:F1}% ({_llmClient.GetCurrentTokens()}/{_llmClient.GetMaxTokens()} tokens)";

// Change color based on usage
if (usage > 80)
    statusLabel.ForeColor = Color.Red;    // Danger
else if (usage > 60)
    statusLabel.ForeColor = Color.Orange; // Warning
else
    statusLabel.ForeColor = Color.Green;  // OK
```

---
- Wrapper around LocalLLMClient with domain-specific prompts
- Used by AnalysisController to populate LLMPane

### AILogsManager
- Logs all LLM queries/responses to `AppData/ZizzysReverseEngineering/AILogs/`

---

## UI Controller Patterns

### Event Suppression During Sync
```csharp
_suppressEvents = true;
// ... make changes ...
_suppressEvents = false;
```

### Async Debouncing
```csharp
_asmToHexCts?.Cancel();
_asmToHexCts = new CancellationTokenSource();
await Task.Delay(80, _asmToHexCts.Token);  // Debounce rapid typing
```

### Controllers
1. **AnalysisController**: Run analysis async, update SymbolTree + GraphControl, LLM integration
2. **DisassemblyController**: Sync disassembly ↔ hex, handle ASM editing → assemble → patch
3. **HexEditorController**: Sync hex ↔ disassembly
4. **MainMenuController**: File/Edit/Navigate/Debug menus
5. **ThemeMenuController**: Dark/Light theme toggle

---

## Project System (Phase 5 - Complete)

### ProjectModel / ProjectSerializer
- **Serializes**: Binary path, patches applied, view state, theme, annotations
- **Format**: JSON (human-readable)
- **Important**: Projects store **absolute paths**

### AnnotationStore
- Per-project user annotations: function names, symbols, comments
- Integrated with SymbolResolver
- Persisted in project JSON

### SettingsManager
- Persistent app-level settings: theme, font, layout, auto-analyze flag
- Location: `AppData/ZizzysReverseEngineering/settings.json`

### Logger
- File + in-memory logging with timestamps
- Location: `AppData/ZizzysReverseEngineering/logs/YYYY-MM-DD.log`

---

## Build & Test

### Build
```bash
dotnet build                    # All projects
dotnet build ReverseEngineering.Core/ReverseEngineering.Core.csproj  # Core only
```

### Run
```bash
dotnet run --project ReverseEngineering.WinForms
```

### Test
```bash
dotnet test
```

---

## Common Developer Tasks

### Add a Disassembly Feature
1. Edit `Disassembler.DecodePE()` or add utilities to `CoreEngine`
2. If new instruction metadata: extend `Instruction` class
3. Update consuming controllers

### Fix Hex/ASM Sync Lag
1. Check `DisassemblyController.OnLineEdited()`
2. Increase `Task.Delay(ms)` value (default 80ms)

### Export Patch Format
1. Modify `PatchExporter` static methods in Core
2. Serialize via `HexBuffer.GetModifiedBytes()`
3. Update `ProjectSerializer` if format changes

### Use LLM Analysis
```csharp
var client = new LocalLLMClient("localhost", 1234);
var analyzer = new LLMAnalyzer(client);
var explanation = await analyzer.ExplainInstructionAsync(instruction);
```

### Search for Patterns
```csharp
var patterns = PatternMatcher.FindBytePattern(buffer, "55 8B ?? C3");
```

### Track Edits with Undo/Redo
```csharp
_core.ApplyPatch(offset, newBytes, "NOP out call");
_core.UndoRedo.Undo();
_core.UndoRedo.Redo();
```





### CrossReferenceEngine
- Tracks code→code (JMP/CALL), code→data (MOV RIP-relative), data→code
- Output: `Dictionary<ulong, List<CrossReference>>`

### SymbolResolver
- Collects symbols from: imports, exports, discovered functions, user annotations
- Output: `Dictionary<ulong, Symbol>` (fast address→name lookup)

### PatternMatcher
- Byte patterns: `"55 8B ?? C3"` (wildcards with `??`)
- Instruction predicates: Match by mnemonic, operand type, etc.
- Built-in: x64 prologues, NOPs, stack adjustments

---

## LLM Integration (Phase 4 - Complete)

### LocalLLMClient
- Wraps LM Studio HTTP API (default `localhost:1234`)
- Methods: `ExplainInstructionAsync()`, `GeneratePseudocodeAsync()`, `AnalyzePatternAsync()`
- Configurable model, temperature, max tokens

### LLMAnalyzer
- Wrapper around LocalLLMClient with domain-specific prompts
- Methods: `ExplainInstructionAsync()`, `GeneratePseudocodeAsync()`, `DetectPatternAsync()`
- Used by AnalysisController to populate LLMPane

### LLMPane (WinForms Control)
- Displays AI analysis results (rich text with code highlighting)
- Updates on instruction selection

### AIStatsControl (WinForms Control)
- **Purpose**: Display real-time AI, Trainer, and SQL statistics
- **Location**: Bottom tabs, second tab next to "AI Chat"
- **Three Stat Groups**:
  1. **AI Stats**: Model name, context usage %, tokens used/max, progress bar (green <60%, orange 60-80%, red >80%)
  2. **Trainer Stats**: Training status, patterns indexed, embeddings generated
  3. **SQL/Cache Stats**: Cache hit count, total queries, database size in KB
- **Methods**:
  - `UpdateAIStats(model, contextUsagePercent, currentTokens, maxTokens)` - Update LLM metrics
  - `UpdateTrainerStats(status, patternsIndexed, embeddingsGenerated)` - Update trainer progress
  - `UpdateSQLStats(cacheHits, totalQueries, dbSizeKB)` - Update cache/database metrics with hit rate
  - `ResetStats()` - Clear all statistics
- **Integration**: FormMain initializes LLMClient token context on app startup, displays status in status bar

### LocalLLMClient.cs (TOKEN LIMITS - IMPLEMENTED ✅)
- **Token Tracking**:
  - `GetModelContextLengthAsync()`: Queries LM Studio `/api/v1/models` for active model's `max_context_length` field
    - Endpoint: `http://localhost:1234/api/v1/models`
    - Parses response to find model object matching active model `key`
    - Extracts `max_context_length` (e.g., 131,072 for openai/gpt-oss-120b)
    - Falls back to `loaded_instances[0].config.context_length` if needed
    - Returns INT - real token count, not hardcoded default
  - `GetContextUsagePercent()`: Returns current usage as 0-100%
  - `GetCurrentTokens()` / `GetMaxTokens()`: Get token metrics
- **Automatic Token Management**:
  - `CompleteAsync()` reserves 20% for output automatically
  - Truncates prompts if exceeding limit
  - Warns when approaching 80% usage
  - Estimates tokens at 1 token ≈ 4 characters
- **Error Handling**:
  - Falls back to 4096 if model doesn't report context
  - Connection errors logged but app continues
- **Status**: ✅ Live in FormMain (initializes on app load)
- **Key Advantage**: **No more hardcoded defaults** - every token calculation uses real server context

### AILogsManager
- Logs all LLM queries/responses to `AppData/ZizzysReverseEngineering/AILogs/`
- Categories: INSTRUCTION_EXPLANATION, PSEUDOCODE, PATTERN_DETECTION

---

## UI Controller Patterns

### Event Suppression During Sync
```csharp
// Prevent cascading updates when synchronizing hex ↔ asm views
_suppressEvents = true;
// ... make changes ...
_suppressEvents = false;
```

### Async Debouncing for Expensive Ops
```csharp
// Used in DisassemblyController.OnLineEdited()
_asmToHexCts?.Cancel();
_asmToHexCts = new CancellationTokenSource();
await Task.Delay(80, _asmToHexCts.Token);  // Debounce rapid typing
byte[] bytes = await Task.Run(() => KeystoneAssembler.Assemble(...), _asmToHexCts.Token);
```

### Controllers
1. **AnalysisController**: Run analysis async, update SymbolTree + GraphControl, LLM integration
2. **DisassemblyController**: Sync disassembly selection → hex scroll, handle ASM editing → assemble → patch hex
3. **HexEditorController**: Sync hex selection → disassembly scroll
4. **MainMenuController**: File (Open/Save), Edit (Undo/Redo), View, Help menus
5. **ThemeMenuController**: Dark/Light theme toggle

---

## Project System (Phase 5 - Complete)

### ProjectModel / ProjectSerializer
- **Serializes**: Binary path, patches applied, view state (hex/asm scroll), theme, annotations
- **Format**: JSON (single file, human-readable)
- **Important**: Projects store **absolute paths** (relative path support not yet implemented)

### AnnotationStore
- Per-project user annotations: function names, symbols, comments
- Integrated with SymbolResolver for symbol display
- Persisted in project JSON

### SettingsManager
- Persistent app-level settings: theme, font, layout, auto-analyze flag
- Location: `AppData/ZizzysReverseEngineering/settings.json`
- Auto-loads on startup

### Logger
- File + in-memory logging with timestamps
- Categories: PATCH, ANALYSIS, DISASSEMBLY, SEARCH, LLM, etc.
- Location: `AppData/ZizzysReverseEngineering/logs/YYYY-MM-DD.log`

---

## External Dependencies

- **Iced 1.21.0**: x86/x64 disassembler (PE section decode)
- **Keystone.Net**: x86/x64 assembler (via Keystone.Net.dll wrapper)
- **.NET 10 WinForms**: UI framework (native Windows)
- **xUnit**: Test framework

**Key assemblies**: `Keystone.Net.dll`, `keystone.dll` (copied to output on build)

---

## Coding Conventions

### Nullability & Optional Fields
```csharp
#nullable enable  // Enforced in both projects
public string? Name { get; set; }  // Nullable reference type
public int? Count { get; set; }    // Nullable value type
```

### Event Naming
- `ByteChanged` (per-byte notification with args: offset, old, new)
- `BytesChanged` (bulk notification)
- `HistoryChanged`, `CommandExecuted` (undo/redo)

### Comments & Structure
- Section dividers: `// ---------------------------------------------------------`
- Method groups: Public API, Private helpers, Constants, Fields

### Constants
- `HexBuffer.BytesPerRow = 16` (hex grid layout)
- Address bitness: `Is64Bit` flag (set at load time)

---

## Common Tasks

### Add a Disassembly Feature
1. Edit `Disassembler.DecodePE()` or add utility methods to `CoreEngine`
2. If new instruction metadata needed: extend `Instruction` class
3. Update consuming controllers (DisassemblyController, etc.)

### Fix Hex/ASM Sync Lag
1. Check `DisassemblyController.OnLineEdited()` and `HexEditorController` event handlers
2. Increase `Task.Delay(ms)` value to debounce faster (default 80ms)
3. Or reduce workload (e.g., use `RebuildInstructionAtOffset()` instead of full rebuild)

### Export New Patch Format
1. Modify `PatchExporter` static methods in Core
2. Serialize via `HexBuffer.GetModifiedBytes()` (yields offset, original, current)
3. Update ProjectSerializer if format changes

### Add Project Metadata
1. Extend `ProjectModel` class (ProjectSystem/ProjectModel.cs)
2. Update `ProjectSerializer.ToJson()` / `FromJson()` for new fields
3. Bump `ProjectVersion` in ProjectModel
4. Update `ProjectManager` if restore logic needed

### Resize UI Controls
1. **Designer**: Edit `FormMain.Designer.cs` (auto-generated via VS designer)
2. **Programmatic**: Modify controller constructor or form load

### Run Analysis on a Binary
```csharp
var engine = new CoreEngine();
engine.LoadFile("program.exe");
engine.RunAnalysis();
Console.WriteLine($"Functions: {engine.Functions.Count}");
foreach (var func in engine.Functions)
    Console.WriteLine($"  {func.Name ?? $"0x{func.Address:X}"} @ 0x{func.Address:X}");
```

### Use LLM Analysis
```csharp
var client = new LocalLLMClient("localhost", 1234);
var analyzer = new LLMAnalyzer(client);
var explanation = await analyzer.ExplainInstructionAsync(instruction);
Console.WriteLine(explanation);
```

### Search for Patterns
```csharp
var patterns = PatternMatcher.FindBytePattern(buffer, "55 8B ?? C3");  // x64 prologue + stack
var prologues = PatternMatcher.FindAllStrings(buffer);  // ASCII + wide
```

### Track Edits with Undo/Redo
```csharp
// Changes auto-tracked by CoreEngine
_core.ApplyPatch(offset, newBytes, "NOP out call");

// Manual undo/redo
_core.UndoRedo.Undo();
_core.UndoRedo.Redo();
```

---

## Build & Test

### Build
```bash
dotnet build                    # All projects
dotnet build ReverseEngineering.Core/ReverseEngineering.Core.csproj  # Core only
dotnet build ReverseEngineering.WinForms/ReverseEngineering.WinForms.csproj  # UI only
```

### Run
```bash
dotnet run --project ReverseEngineering.WinForms
```

### Test
```bash
dotnet test                    # All tests
dotnet test --no-build        # Skip rebuild
```

### Clean
```bash
dotnet clean
```

---

## File Organization

### ReverseEngineering.Core/
- **Root**: `CoreEngine.cs`, `Disassembler.cs`, `HexBuffer.cs`, `Instruction.cs`, `PatchEngine.cs`, `SearchManager.cs`, `Logger.cs`
- **Analysis/**: CFG, functions, xrefs, symbols, patterns
- **ProjectSystem/**: Save/load, undo/redo, annotations, settings
- **LLM/**: LocalLLMClient, LLMAnalyzer
- **AILogs/**: AILogsManager

### ReverseEngineering.WinForms/
- **MainWindow/**: 5x controllers (Analysis, Disassembly, HexEditor, MainMenu, ThemeMenu)
- **GraphView/**: GraphControl (CFG visualization)
- **SymbolView/**: SymbolTreeControl (function/symbol tree)
- **HexEditor/**: Hex editing controls
- **Search/**: SearchDialog, SearchController
- **Annotation/**: AnnotationDialog
- **LLM/**: LLMPane (AI results display), AIStatsControl (context/token/trainer/cache metrics)
- **Settings/**: SettingsController
- **AILogs/**: AILogsViewer

---

## Performance Tips

1. **Use `RebuildInstructionAtOffset(offset)` for byte edits** — Much faster than full rebuild
2. **Debounce ASM editing** — Task.Delay(80ms) prevents excessive re-assembly
3. **Lazy-load analysis** — Build CFG/xrefs on-demand, not on binary load
4. **Cache symbol lookups** — SymbolResolver maintains `Dictionary<ulong, Symbol>`

---

## Next Steps for Developers

- **Bug fixes**: Check existing issues in project backlog
- **Enhance search**: Add filtering/sorting to SearchDialog
- **Improve logging**: Capture patch audit trail with timestamps
- **Decompiler**: Optional Ghidra HTTP server integration
- **Plugin system**: Allow loading user C# assemblies for custom analysis
