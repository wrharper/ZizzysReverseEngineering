# AI Coding Agent Instructions

## Architecture Overview

**ZizzysReverseEngineering** is a .NET 10 Windows Forms application for interactive binary reverse engineering with live disassembly/patching. It has a strict layered architecture:

- **ReverseEngineering.Core** (library): PE parsing, disassembly, patching, project serialization
- **ReverseEngineering.WinForms** (UI): WinForms controls and MVC-style controllers for user interactions

### Data Flow

1. **Load binary** → `CoreEngine.LoadFile()` → `Disassembler.DecodePE()` (Iced.Intel) → `List<Instruction>` + address index
2. **Hex editor edit** → `HexBuffer.WriteByte()` → `ByteChanged` event → `DisassemblyController` re-disassembles affected instruction
3. **Asm editor edit** → `KeystoneAssembler.Assemble()` → writes bytes to `HexBuffer` → triggers re-disassembly
4. **Save project** → `ProjectManager.CaptureState()` → `ProjectSerializer.SaveToJson()` (metadata + patches applied)

## Core Engine Patterns

### `CoreEngine` (central orchestrator)
- **`LoadFile(path)`**: Detects bitness, parses PE headers, builds full disassembly + address index
- **`RebuildDisassemblyFromBuffer()`**: Full re-parse when importing project patches
- **`RebuildInstructionAtOffset(offset)`**: Incremental re-disassembly for small edits (much faster)
- **Address mapping**: Maintains `_addressToIndex` for O(1) offset↔address conversion

**When modifying disassembly logic**: Use `RebuildInstructionAtOffset()` for performance; only call `RebuildDisassemblyFromBuffer()` when buffer integrity is uncertain.

### `HexBuffer` (mutable binary + change tracking)
- Stores original bytes, current bytes, modified flags
- `WriteByte()/WriteBytes()` apply changes and set `Modified[offset]` flags
- `GetModifiedBytes()` yields (offset, original, current) for patch export/serialization

### `PatchEngine` (undo/redo ready)
- Records each edit as `Patch` metadata (offset, original bytes, new bytes, description)
- **Not yet used for undo/redo** but architecture is in place for future implementation

### `Instruction` (unified representation)
- Combines Iced.Intel decode with file offsets/RVA
- **Key fields**: `Address` (virtual), `FileOffset` (binary), `RVA`, `Bytes`, `Raw` (Iced instruction for operand analysis)
- Used by disassembly view, hex editor sync, and analysis code

## UI Controller Patterns

### Multi-view synchronization (HEX ↔ ASM)
Controllers suppress cascading events during sync with `_suppressEvents` flag to prevent re-triggering.

- **`HexEditorController`**: Selection → address → scroll disassembly
- **`DisassemblyController`**: Selection → offset → scroll hex; editing → assemble → patch hex

### Async debouncing for expensive ops
```csharp
// Example: DisassemblyController.OnLineEdited
_asmToHexCts?.Cancel();
_asmToHexCts = new CancellationTokenSource();
await Task.Delay(80, token);  // Debounce typing
byte[] bytes = await Task.Run(() => KeystoneAssembler.Assemble(...), token);
```

**Pattern**: Use `CancellationTokenSource` for long-running operations (assembly, re-disassembly) to allow cancellation on rapid user input.

## External Dependencies

- **Iced 1.21.0**: x86/x64 disassembler; decode PE sections to `Iced.Intel.Instruction`
- **Keystone.Net**: x86/x64 assembler; used by `KeystoneAssembler` wrapper
- **.NET 10 WinForms**: UI framework

**Assembly paths**: `KeystoneAssembler` (Keystone wrapper) and `AsmAssembler` (Iced-based, experimental)

## Project System (Save/Restore)

- **`ProjectModel`**: Serializable snapshot (file path, theme, view state, patches)
- **`ProjectSerializer`**: JSON de/serialization (`ProjectModel` → JSON string)
- **`ProjectManager`**: Stateless helpers to capture/restore state
- **Patch format**: List of `PatchEntry` (offset + newValue); simple and portable

**Important**: Projects store absolute file paths. Relative path support is not yet implemented.

## Build & Run

- Target: `.NET 10.0-windows`
- Build: `dotnet build` or Visual Studio
- Run WinForms app: `dotnet run --project ReverseEngineering.WinForms`
- No tests framework configured yet

## Coding Conventions

- **Sealed partial PE parsing**: Only `PatchEntry` is `sealed`; most classes are standard
- **Nullability**: `#nullable enable` across both projects; use `?` for optional fields
- **Event naming**: `BytesChanged` (bulk notification), `ByteChanged` (per-byte with args)
- **Constants**: `HexBuffer.BytesPerRow = 16` (hex grid layout)
- **Comments**: Heavy use of `// ---------------------------------------------------------` section markers for readability

## Common Tasks

**Add a disassembly feature**: Edit `Disassembler.DecodePE()` or add utility methods to `CoreEngine`; update `Instruction` if new metadata needed.

**Fix hex/asm sync lag**: Check `DisassemblyController` and `HexEditorController` event handlers; increase debounce delay in `Task.Delay()` if needed.

**Export new patch format**: Modify `PatchExporter` static methods; serialize `HexBuffer.GetModifiedBytes()`.

**Add new project metadata**: Extend `ProjectModel` (in `ProjectSystem/ProjectModel.cs`); update `ProjectSerializer` JSON de/serialization; bump `ProjectVersion`.

**Resize or reposition UI controls**: Edit `FormMain.Designer.cs` (auto-generated) or programmatically in controller constructors.

---

## Architecture Roadmap & Future Expansion

### Current Status (MVP)
✅ **Implemented:**
- PE loader (basic DOS/COFF/PE32/PE32+ parsing)
- Iced-based disassembler (x86/x64)
- Keystone-based assembler
- Hex buffer with change tracking
- Project save/restore (JSON)
- Basic UI (disassembly view, hex editor, patch panel)
- Multi-view sync (HEX ↔ ASM)

✅ **Now Completed (Phase 2-5):**
- **Phase 2: Analysis Layer**
  - BasicBlockBuilder: Control flow analysis, CFG construction
  - FunctionFinder: Entry points, prologue detection, call graph
  - CrossReferenceEngine: Code/data/import xref tracking
  - SymbolResolver: Symbol discovery and resolution + **import/export parsing**
  - PatternMatcher: Byte patterns, instruction patterns, function signatures + **string scanning**
- **Phase 5: Utilities & Polish**
  - Undo/Redo: Full history management with UI wiring (Ctrl+Z/Y, Edit menu)
  - Search: Byte search, instruction search, function search, xref search (Ctrl+F)
  - Settings: Persistent app settings (theme, fonts, layout, auto-analyze)
  - Logging: File/memory logs with categories (PATCH, ANALYSIS, etc.)
- **Phase 3: Enhanced UI (Complete)**
  - SymbolTreeControl: Browse functions, symbols, xrefs
  - GraphControl: CFG visualization with node layout and edge rendering
  - AnalysisController: Async analysis execution, view updates
  - AnnotationDialog: Add function names, comments, symbol types
  - AnnotationStore: Persistent user annotations per project
- **Phase 4: LM Studio Integration**
  - LocalLLMClient: HTTP wrapper for LM Studio (localhost:1234)
  - LLMAnalyzer: Instruction explanation, pseudocode generation, pattern detection
  - LLMPane: WinForms control for AI analysis results
  - UI integration: Analysis menu with LLM commands (Ctrl+Shift+A)
  - Import table parsing: Enhanced IAT extraction from PE headers
  - String scanning: ASCII + wide string detection from binary

⏳ **Partial/Not Yet:**
- Decompiler integration (Ghidra HTTP server)
- Plugin system
- Debugger integration
- HTTP API

### Phase 2: Analysis Layer (Planned)
**Location**: New `ReverseEngineering.Core/Analysis/` namespace

**Components**:
- **`BasicBlockBuilder`**: Identify block boundaries via control flow analysis (JMP, CALL, RET, conditional branches)
- **`ControlFlowGraph`**: Build CFG from basic blocks; support forward/backward traversal
- **`FunctionFinder`**: Discover functions via:
  - Entry point analysis (PE entry point, exported symbols)
  - Prologue pattern matching (PUSH RBP, MOV RSP, SUB RSP)
  - Call graph traversal (CALL targets)
- **`CrossReferenceEngine`**: Track:
  - Code → Code (JMP, CALL targets)
  - Code → Data (MOV, LEA operands)
  - Data → Code (function pointers, vtables)
- **`SymbolResolver`**: Collect and normalize:
  - Imported functions (IAT entries)
  - Exported functions
  - Relocations
  - Named addresses (from user annotations)
- **`PatternMatcher`**: Byte signatures and instruction pattern detection

**Integration**: Extend `Instruction` with `CrossReferences`, `FunctionId`, `BasicBlockId` fields. Add analysis results to `CoreEngine` as read-only properties.

### Phase 3: Enhanced UI Layer (Planned)
- **Graph View**: Visualize CFG (basic blocks as nodes, edges as control flow)
- **Navigation Pane**: Function tree, symbol browser, imports/exports
- **Inline Annotations**: User can tag functions, add comments, rename symbols
- **Search & Filter**: Byte patterns, instruction mnemonics, cross-references

**Location**: New views in `ReverseEngineering.WinForms/` (GraphControl.cs, SymbolTreeControl.cs, etc.)

### Phase 4: LM Studio Integration (Planned)
- **LocalLLMClient**: C# wrapper for LM Studio HTTP API (default localhost:1234)
- **LLMAnalyzer**: Instruction explanation, pseudocode generation, pattern identification
- **LLMPane**: WinForms control to display AI analysis results
- **AnalysisPrompts**: Curated prompts for common RE tasks

**Integration**: Wire LLMAnalyzer to fire on instruction selection; display results in AnalysisController UI.

**Key advantage over Ghidra**: Local, offline, no licensing, extensible with custom prompts.

### Phase 5: Utilities & Polish (Ongoing)
- **Undo/Redo**: Wire `PatchEngine` history to UI, add menu items
- **Search**: Implement byte search, instruction search, string search
- **Settings**: Persist UI layout, theme, font preferences
- **Logging**: Expand to file logs, patch audit trail, error reports

### Phase 6: Integration & Scripting (Optional)
- **C# Plugin System**: Allow loading user assemblies to extend analysis
- **Debugger Integration**: Optional x64dbg/WinDbg bridge for live patching
- **Performance Profiling**: Benchmark & optimize analysis pipeline

### Design Principles for All Phases
1. **Separation of Concerns**: Analysis layer remains independent of UI; UI remains independent of analysis
2. **Event-Driven Updates**: When analysis completes, raise events; UI subscribes and updates
3. **Incremental Analysis**: Support partial re-analysis (e.g., when user patches code, only re-analyze affected block)
4. **Lazy Loading**: Build CFG/xrefs on-demand, not during initial load
5. **Caching**: Cache analysis results; invalidate only affected sections on patches

### File Structure (After All Phases)
```
ReverseEngineering.Core/
├── Disassembler.cs, CoreEngine.cs, HexBuffer.cs (existing)
├── Analysis/
│   ├── BasicBlockBuilder.cs
│   ├── ControlFlowGraph.cs
│   ├── FunctionFinder.cs
│   ├── CrossReferenceEngine.cs
│   ├── SymbolResolver.cs
│   └── PatternMatcher.cs
├── Decompiler/ (optional)
│   ├── DecompilerClient.cs
│   └── GhidraClient.cs
└── Plugins/ (optional)
    └── PluginLoader.cs

ReverseEngineering.WinForms/
├── FormMain.cs, MainMenuController.cs (existing)
├── GraphView/ (Phase 3)
│   ├── GraphControl.cs
│   └── CFGRenderer.cs
├── SymbolView/ (Phase 3)
│   ├── SymbolTreeControl.cs
│   └── SymbolTreeController.cs
└── DecompilerView/ (Phase 4)
    ├── DecompilerControl.cs
    └── DecompilerController.cs
```

### Immediate Next Steps
1. **Stabilize MVP**: Ensure all current features are bug-free, add edge case handling
2. **Add Undo/Redo**: Wire `PatchEngine.Patches` history to UI
3. **Implement BasicBlockBuilder**: Start Phase 2 with CFG foundation
4. **Add Search**: File menu item → byte/instruction search
5. **Improve Logging**: Capture all patches with timestamps for audit trail

---

## Phase 2-5 Components (Now Implemented)

### Analysis Layer Components

**BasicBlockBuilder** (`ReverseEngineering.Core/Analysis/BasicBlockBuilder.cs`)
- Identifies instruction boundaries and control flow
- Builds CFG with predecessors/successors tracking
- Handles JMP, RET, conditional branches, CALL targets
- Returns `ControlFlowGraph` with all blocks interconnected

**FunctionFinder** (`ReverseEngineering.Core/Analysis/FunctionFinder.cs`)
- Discovers functions via: entry point, exports, prologues, call graph
- Prologue patterns: PUSH RBP, MOV RBP RSP, SUB RSP imm
- Builds CFG for each discovered function
- Returns `List<Function>` with CFG and metadata

**CrossReferenceEngine** (`ReverseEngineering.Core/Analysis/CrossReferenceEngine.cs`)
- Builds xref database from disassembly
- Tracks code→code (JMP/CALL), code→data (MOV/LEA RIP-relative)
- Provides `GetOutgoingRefs()` and `GetIncomingRefs()` queries
- Returns `Dictionary<ulong, List<CrossReference>>`

**SymbolResolver** (`ReverseEngineering.Core/Analysis/SymbolResolver.cs`)
- Resolves symbols from imports, exports, discovered functions
- Supports user annotations (function names, types)
- Returns `Dictionary<ulong, Symbol>` with fast name lookup

**PatternMatcher** (`ReverseEngineering.Core/Analysis/PatternMatcher.cs`)
- Byte pattern matching with wildcards ("55 8B ?? C3")
- Instruction pattern matching via predicates
- Built-in patterns: x64 prologues, stack setup, NOPs
- Uses Iced.Intel for instruction analysis

### Instruction Extensions
- `FunctionAddress`: Parent function (if any)
- `BasicBlockAddress`: Parent block (if any)
- `XRefsFrom`: Cross-references emitted by this instruction
- `SymbolName`: Symbol at this address
- `Annotation`: User annotation
- `IsPatched`: Modification flag

### CoreEngine Analysis API
```csharp
// Entry point: Run full analysis
_core.RunAnalysis();

// Query results
_core.Functions;           // List<Function>
_core.CFG;                 // ControlFlowGraph
_core.CrossReferences;     // Dictionary<ulong, List<CrossReference>>
_core.Symbols;             // Dictionary<ulong, Symbol>

// Helpers
_core.FindFunctionAtAddress(addr);
_core.GetSymbolName(addr);
_core.AnnotateAddress(addr, name, type);
_core.FindBytePattern(pattern);
_core.FindPrologues();
```

### Utilities Layer

**UndoRedoManager** (`ProjectSystem/UndoRedoManager.cs`)
- Full undo/redo stack with history
- `PatchCommand` for automatic serialization
- UI integration: Edit menu with Ctrl+Z/Y hotkeys
- `HistoryChanged` event for menu updates

**SearchManager** (`Core/SearchManager.cs`)
- Unified search API: bytes, instructions, functions, symbols, xrefs
- Pattern matching via PatternMatcher
- Hex string parsing ("48 89 E5" or "4889E5")
- Returns `List<SearchResult>` with address + description

**SettingsManager** (`ProjectSystem/SettingsManager.cs`)
- Persistent JSON settings (AppData/ZizzysReverseEngineering/settings.json)
- Theme, font, layout, auto-analyze flag
- Auto-load on startup

**Logger** (`Core/Logger.cs`)
- File + in-memory logging with timestamps
- Categories: PATCH, ANALYSIS, etc.
- Query by level/category
- Logs to: `AppData/ZizzysReverseEngineering/logs/YYYY-MM-DD.log`

**AnnotationStore** (`ProjectSystem/AnnotationStore.cs`)
- Per-project user annotations (function names, comments, types)
- JSON serialization for project saving
- Integrated with SymbolResolver

### UI Layer Components

**SearchDialog** (`WinForms/Search/SearchDialog.cs`)
- Tab-based search: bytes, mnemonics, functions, symbols, xrefs
- Real-time result grid with double-click navigation
- Wired to Ctrl+F in MainMenuController

**SymbolTreeControl** (`WinForms/SymbolView/SymbolTreeControl.cs`)
- TreeView displaying functions, symbols, xref summary
- Double-click selects address, triggers navigation
- Updates from `CoreEngine.RunAnalysis()` results

**GraphControl** (`WinForms/GraphView/GraphControl.cs`)
- CFG visualization: blocks as rectangles, edges as arrows
- Hierarchical layout via BFS level assignment
- Mouse zoom, pan, click-to-select blocks
- Arrow rendering with proper endpoints

**AnalysisController** (`WinForms/MainWindow/AnalysisController.cs`)
- Async analysis execution with cancellation
- Updates views after analysis completes
- `AnalysisStarted` / `AnalysisCompleted` events
- Per-function CFG navigation

**AnnotationDialog** (`WinForms/Annotation/AnnotationDialog.cs`)
- Edit function name, symbol type, comment for an address
- Save/delete buttons
- Pre-loads existing annotation

### Integration Points

**MainMenuController** additions:
- Edit menu: Undo/Redo with Ctrl+Z/Y
- Find dialog: Ctrl+F
- Analysis menu (future): "Run Analysis" button
- Settings menu (future): Theme/font options

**Disassembly View** (future):
- Right-click context menu: "Annotate", "Show Xrefs", "Go to Function"
- Highlight patched instructions, function entries

**Hex View** (future):
- Right-click: "Search from here", "Add breakpoint" (debugging)

---

## Development Patterns for New Features

### Adding Analysis to an Instruction
```csharp
var ins = _core.Disassembly[0];
ins.FunctionAddress = funcAddr;
ins.BasicBlockAddress = blockAddr;
ins.XRefsFrom = xrefs;
ins.SymbolName = "my_function";
```

### Running Analysis Asynchronously
```csharp
var controller = new AnalysisController(_core, _symbolTree, _graphControl);
controller.AnalysisCompleted += () => MessageBox.Show("Done!");
await controller.RunAnalysisAsync();
```

### Searching for Patterns
```csharp
var results = _core.FindBytePattern("55 48 89 ?? 48 83 EC");  // x64 prologue + stack
var prologues = _core.FindPrologues();
```

### Using Undo/Redo
```csharp
// Automatically tracked
_core.ApplyPatch(offset, newBytes, "NOP out call");

// Manual undo/redo
_core.UndoRedo.Undo();
_core.UndoRedo.Redo();
```

### Persisting Annotations
```csharp
var store = new AnnotationStore();
store.SetFunctionName(0x400000, "main");
store.SaveToFile("project_annotations.json");
store.LoadFromFile("project_annotations.json");
```
