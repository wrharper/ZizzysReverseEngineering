# AI Coding Agent Instructions

## Quick Facts

- **Status**: Production-ready (Phases 1-5 complete, Phase 4 LM Studio integrated)
- **Framework**: .NET 10 Windows Forms
- **Architecture**: Strict separation between Core (logic) and WinForms (UI)
- **Build**: `dotnet build` | **Run**: `dotnet run --project ReverseEngineering.WinForms`
- **Test**: `dotnet test` (xUnit, minimal test coverage)

---

## System Architecture

### Three-Layer Design

```
ReverseEngineering.WinForms (UI Layer)
  ├─ Controllers (5x): AnalysisController, DisassemblyController, HexEditorController, MainMenuController, ThemeMenuController
  ├─ Views: DisassemblyControl, HexEditor, GraphControl, SymbolTreeControl, LLMPane, SearchDialog
  └─ Models: Theme, ThemeManager

ReverseEngineering.Core (Business Logic)
  ├─ Core: CoreEngine, Disassembler, HexBuffer, PatchEngine, Instruction, SearchManager, Logger
  ├─ Analysis: BasicBlockBuilder, ControlFlowGraph, FunctionFinder, CrossReferenceEngine, SymbolResolver, PatternMatcher
  ├─ ProjectSystem: ProjectModel, ProjectSerializer, ProjectManager, UndoRedoManager, AnnotationStore, SettingsManager
  ├─ LLM: LocalLLMClient, LLMAnalyzer
  └─ AILogs: AILogsManager

ReverseEngineering.Tests (xUnit)
  ├─ Core: CoreEngineTests
  ├─ Analysis, LMStudio, UI, Utilities (minimal)
```

### Data Flow Examples

**Load Binary**:
```
LoadFile() → DetectBitness(bytes) → Disassembler.DecodePE() → 
  List<Instruction> + _addressToIndex + HexBuffer → UI updates
```

**Edit Hex**:
```
HexEditorControl.OnValueChanged() → HexBuffer.WriteByte() → 
  RebuildInstructionAtOffset(offset) → Disassembly updated → DisassemblyControl refreshes
```

**Run Analysis**:
```
AnalysisController.RunAnalysisAsync() → CoreEngine.RunAnalysis() →
  BasicBlockBuilder → FunctionFinder → CrossReferenceEngine → SymbolResolver →
  AnalysisCompleted event → SymbolTreeControl + GraphControl update
```

---

## Core Components

### `CoreEngine` (Central Orchestrator)
- **`LoadFile(path)`**: PE parse, detect x86/x64, build full disassembly + address map
- **`RebuildDisassemblyFromBuffer()`**: Recalculate all instructions (heavy, for project import)
- **`RebuildInstructionAtOffset(offset)`**: Incremental re-disassembly (fast, for byte edits)
- **`RunAnalysis()`**: Execute analysis pipeline (BasicBlockBuilder → FunctionFinder → CrossRefs → Symbols)
- **Public State**: `Disassembly`, `HexBuffer`, `Functions`, `CFG`, `CrossReferences`, `Symbols`, `UndoRedo`

**Key Pattern**: Address ↔ Offset conversion via `_addressToIndex` dict (O(1) lookup)

### `HexBuffer` (Mutable Binary + Change Tracking)
- **`WriteByte(offset, value)`**: Single byte edit + set `Modified[offset]` flag
- **`WriteBytes(offset, bytes)`**: Bulk edit
- **`GetModifiedBytes()`**: Yields `(offset, originalValue, newValue)` tuples for export
- **Events**: `ByteChanged(offset, oldValue, newValue)`, `BytesChanged()`

### `Instruction` (Unified Representation)
- **`Address`** (virtual), **`FileOffset`** (binary), **`RVA`**, **`Bytes`**, **`EndAddress`**
- **`Raw`** (Iced.Intel.Instruction for operand analysis)
- **Analysis Fields**: `FunctionAddress`, `BasicBlockAddress`, `XRefsFrom`, `SymbolName`, `Annotation`

### `UndoRedoManager` (History Management)
- **`Execute(Command)`**: Execute command + push undo stack + clear redo stack
- **`Undo()`** / **`Redo()`**: Pop stacks and execute
- **Events**: `HistoryChanged`, `CommandExecuted`
- **Max history**: 100 commands by default (trim oldest)

---

## Analysis Pipeline (Phase 2 - Implemented)

All components live in `ReverseEngineering.Core.Analysis/` namespace.

### BasicBlockBuilder
- Input: `List<Instruction>`, entry point address
- Identifies block boundaries (JMP, RET, conditional branches, CALL)
- Output: `ControlFlowGraph` with predecessors/successors

### FunctionFinder
- Discovers functions via: PE entry point, exported symbols, prologues (PUSH RBP, MOV RSP, SUB RSP), call graph
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
- **LLM/**: LLMPane (AI results display)
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
