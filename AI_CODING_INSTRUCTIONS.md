# AI Coding Agent Instructions

## Quick Reference

- **Project**: ZizzysReverseEngineering (.NET 10.0-windows)
- **Build**: `dotnet build` | **Run**: `dotnet run --project ReverseEngineering.WinForms`
- **Test**: `dotnet test` (11/11 passing, xUnit 2.6.6 + Moq)
- **Status**: ✅ Production phases 1-5 complete, tests integrated, full architecture in place

---

## Architecture Overview

**ZizzysReverseEngineering** is a .NET 10 Windows Forms application for interactive binary reverse engineering with live disassembly/patching.

### Three-Layer Design

```
ReverseEngineering.WinForms (UI Layer)
  ├─ Controllers: AnalysisController, DisassemblyController, HexEditorController, etc.
  ├─ Controls: DisassemblyControl, HexEditorControl, GraphControl, SymbolTreeControl, LLMPane
  └─ Utilities: Theme, Settings, Logging

ReverseEngineering.Core (Business Logic)
  ├─ Core: CoreEngine, Disassembler, HexBuffer, Instruction, PatchEngine
  ├─ Analysis: BasicBlockBuilder, CFG, FunctionFinder, CrossReferences, SymbolResolver, PatternMatcher
  ├─ ProjectSystem: ProjectModel, ProjectSerializer, Manager, UndoRedo, Annotations
  ├─ LLM: LocalLLMClient, LLMAnalyzer
  └─ Utilities: Logger, SearchManager, SettingsManager

ReverseEngineering.Tests (xUnit)
  └─ Core/: CoreEngineTests.cs (11 passing)
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
RebuildInstructionAtOffset(offset) → Disassembly updated
```

**Run Analysis**:
```
AnalysisController.RunAnalysisAsync() → CoreEngine.RunAnalysis() →
BasicBlockBuilder → FunctionFinder → CrossRefs → Symbols →
AnalysisCompleted event → UI updates
```

---

## Core Components Reference

### `CoreEngine` (Central Orchestrator)
- **`LoadFile(path)`**: PE parse, detect x86/x64, build disassembly + address map
- **`RebuildDisassemblyFromBuffer()`**: Full re-disassembly (heavy)
- **`RebuildInstructionAtOffset(offset)`**: Incremental re-disassembly (fast, for byte edits)
- **`RunAnalysis()`**: Execute analysis pipeline
- **Public State**: `Disassembly`, `HexBuffer`, `Functions`, `CFG`, `CrossReferences`, `Symbols`

**Key Pattern**: Address ↔ Offset conversion via `_addressToIndex` dict (O(1) lookup)

### `HexBuffer` (Mutable Binary + Change Tracking)
- **`WriteByte(offset, value)`**: Single byte edit + set `Modified[offset]` flag
- **`WriteBytes(offset, bytes)`**: Bulk edit
- **`GetModifiedBytes()`**: Yields `(offset, originalValue, newValue)` tuples
- **Events**: `ByteChanged()`, `BytesChanged()`

### `Instruction` (Unified Representation)
- **`Address`** (virtual), **`FileOffset`** (binary), **`RVA`**, **`Bytes`**, **`Raw`** (Iced.Intel)
- **Analysis Fields**: `FunctionAddress`, `BasicBlockAddress`, `XRefsFrom`, `SymbolName`, `Annotation`

### Analysis Components (Phase 2+)
- **BasicBlockBuilder**: CFG construction from instructions
- **FunctionFinder**: Function discovery (entry points, prologues, call graph)
- **CrossReferenceEngine**: Code/data xref tracking
- **SymbolResolver**: Symbol discovery and resolution
- **PatternMatcher**: Byte patterns + instruction patterns

### Utilities (Phase 5)
- **UndoRedoManager**: Full history with undo/redo
- **SearchManager**: Byte/instruction/function/symbol search
- **SettingsManager**: Persistent JSON settings
- **Logger**: File + memory logging with categories
- **AnnotationStore**: Per-project user annotations

---

## Build & Run

- Target: `.NET 10.0-windows`
- Build: `dotnet build`
- Run WinForms: `dotnet run --project ReverseEngineering.WinForms`
- **Tests**: xUnit 2.6.6, Moq 4.20.70, Microsoft.NET.Test.Sdk 17.9.0
  - `dotnet test` (runs all 11 tests)
  - `dotnet test --no-build` (fast, no rebuild)
  - `dotnet test --filter "FullyQualifiedName~HexBuffer"`

---

## Coding Conventions

- **Nullability**: `#nullable enable`; use `?` for optional
- **Events**: `BytesChanged` (bulk), `ByteChanged(offset, old, new)` (per-byte)
- **Constants**: `HexBuffer.BytesPerRow = 16`
- **Comments**: Heavy section markers `// ----` for readability
- **Casting**: Explicit `(int)` for ulong→int conversions (seen in DisassemblyController)

---

## Testing Architecture

### Framework
- **xUnit 2.6.6** + **Moq 4.20.70**
- **Current Coverage**: 11 tests, 100% pass rate
- **Test File**: `ReverseEngineering.Tests/Core/CoreEngineTests.cs`

### Test Classes & Coverage
- `HexBufferTests` (4 tests): Constructor, WriteByte, WriteBytes, indexer
- `KeystoneAssemblerTests` (4 tests): x64/x86 assembly, invalid instructions
- `InstructionTests` (1 test): Property initialization
- `PatchEngineTests` (2 tests): Constructor, initialization

### Writing Tests

**Pattern 1: Public API (No Mocks)**
```csharp
[Fact]
public void HexBuffer_WriteByte_UpdatesValue()
{
    var buffer = new HexBuffer(new byte[] { 0x00, 0x01, 0x02 });
    buffer.WriteByte(1, 0xFF);
    Assert.Equal(0xFF, buffer[1]);
}
```

**Pattern 2: Mocking (When Needed)**
```csharp
[Fact]
public void CoreEngine_LoadFile_DecodesDisassembly()
{
    var mockDisasm = new Mock<IDisassembler>();
    mockDisasm.Setup(d => d.Decode(It.IsAny<byte[]>()))
        .Returns(new List<Instruction>());
    var engine = new CoreEngine(mockDisasm.Object);
    engine.LoadFile("test.bin");
    mockDisasm.Verify(d => d.Decode(It.IsAny<byte[]>()), Times.Once);
}
```

**Pattern 3: Integration Testing**
```csharp
[Fact]
public void CoreEngine_LoadFile_BuildsAddressIndex()
{
    var engine = new CoreEngine();
    engine.LoadFile("path/to/binary.exe");
    Assert.NotEmpty(engine.Disassembly);
}
```

### Coverage Goals (Priority Order)

1. **Core Layer** (High)
   - HexBuffer: Read/write/modify, change tracking
   - Disassembler: PE parsing, bitness detection, decode
   - Instruction: Properties, address mapping
   - PatchEngine: Recording, serialization

2. **Analysis Layer** (Medium)
   - BasicBlockBuilder: CFG construction
   - FunctionFinder: Function discovery
   - CrossReferenceEngine: Xref tracking
   - SymbolResolver: Symbol resolution

3. **UI Layer** (Low; focus on controllers)
   - DisassemblyController: Event handling, sync
   - HexEditorController: Selection, navigation

### Running Tests
```bash
dotnet test                                    # All tests
dotnet test --filter "FullyQualifiedName~HexBuffer"  # Specific
dotnet test --verbosity detailed               # Verbose
dotnet test --no-build                         # Fast (no rebuild)
```

---

## Error Handling & Exception Patterns

### Standard Exception Types
```csharp
throw new FileNotFoundException($"Binary not found: {path}");
throw new InvalidOperationException($"Invalid PE header at 0x{offset:X}");
throw new ArgumentException($"Invalid x86 mnemonic: {mnemonic}");
throw new IndexOutOfRangeException($"Address 0x{address:X} not in binary");
```

### Try-Catch Pattern (UI)
```csharp
try
{
    _core.LoadFile(path);
    UpdateViews();
}
catch (FileNotFoundException ex)
{
    MessageBox.Show($"File not found: {path}", "Error");
    Logger.Error($"LoadFile failed: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    MessageBox.Show($"Invalid binary format", "Error");
    Logger.Error($"PE parsing failed: {ex.Message}");
}
```

### Graceful Degradation
```csharp
try
{
    _core.RunAnalysis();
}
catch (OutOfMemoryException)
{
    MessageBox.Show("Binary too large for analysis", "Warning");
    // Fall back to basic disassembly
}
finally
{
    _analysisInProgress = false;
}
```

---

## Performance Guidelines

### Critical Hot Paths

1. **Address ↔ Offset Conversion** (O(1) via dict)
   - ✅ Use `_addressToIndex` dictionary
   - ❌ Don't iterate Disassembly list

2. **Instruction Re-disassembly** (On byte edit)
   - ✅ Use `RebuildInstructionAtOffset()` (fast)
   - ❌ Don't use `RebuildDisassemblyFromBuffer()` for small changes

3. **View Synchronization** (HEX ↔ ASM)
   - ✅ Use `_suppressEvents` to prevent cascades
   - ❌ Don't update both views independently

4. **Analysis Pipeline**
   - ✅ Run all analyzers in sequence once
   - ❌ Don't run each independently

### Benchmarking
```bash
time dotnet build
dotnet test --verbosity minimal
dotnet-trace collect --process-id <PID> dotnet run --project ReverseEngineering.WinForms
```

### Memory Tips
- Lazy-load CFG/xrefs on-demand
- Cache symbol lookups (dict for O(1))
- Stream files > 100MB (not yet implemented)
- Unsubscribe events in `Dispose()` to prevent leaks

### Profiling Targets
- `LoadFile()` time: < 1s for 10MB
- `RunAnalysis()` time: < 5s for 10MB
- Memory peak: < 500MB typical
- UI lag: < 100ms for hex/asm edits

---

## Third-Party Libraries

### Iced 1.21.0 (Disassembler)
**Key Classes**: `Decoder`, `Instruction`, `OpKind`, `Register`

**Usage**:
```csharp
var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), DecoderOptions.None);
while (decoder.IP < (ulong)bytes.Length)
{
    decoder.Decode(out var ins);
    // Process: ins.Mnemonic, ins.OpCount, ins.Operands, etc.
}
```

**Mapping to Our `Instruction`**:
```csharp
var our_ins = new Instruction
{
    Address = currentRVA,
    FileOffset = (int)decoder.IP,
    Raw = iced_ins,           // Store for operand access
    Bytes = ExtractBytes(iced_ins)
};
```

### Keystone.Net (Assembler)
**Key Classes**: `Keystone`, `KeystoneMode`, `KeystoneArch`

**Usage**:
```csharp
using var ks = new Keystone(KeystoneArch.KS_ARCH_X86, KeystoneMode.KS_MODE_64);
var result = ks.Assemble("mov rax, rbx; ret");
if (!result.Ok) throw new ArgumentException($"Assembly failed: {result.ErrorMessage}");
byte[] machineCode = result.Buffer;
```

**In KeystoneAssembler.cs**:
```csharp
public static byte[] Assemble(string asmCode, bool is64Bit)
{
    var mode = is64Bit ? KeystoneMode.KS_MODE_64 : KeystoneMode.KS_MODE_32;
    using var ks = new Keystone(KeystoneArch.KS_ARCH_X86, mode);
    var result = ks.Assemble(asmCode);
    if (!result.Ok) throw new ArgumentException($"Invalid: {result.ErrorMessage}");
    return result.Buffer;
}
```

### LM Studio HTTP API
**Endpoint**: `http://localhost:1234/v1/chat/completions`

**Integration (LocalLLMClient.cs)**:
```csharp
var client = new HttpClient() { BaseAddress = new Uri("http://localhost:1234") };
var payload = new { model = "model", messages = new[] { ... } };
var response = await client.PostAsJsonAsync("/v1/chat/completions", payload);
var result = await response.Content.ReadAsAsync<LLMResponse>();
```

**Configuration**:
- Host: localhost (configurable)
- Port: 1234 (configurable)
- Model: User-selected in LM Studio UI
- Timeout: 60 seconds default

---

## Known Limitations

| Limitation | Issue | Workaround | Fix |
|-----------|-------|-----------|-----|
| No Relative Paths | Projects store absolute paths | Manual updates on move | Implement path resolution |
| Single Architecture | Can't load x86 + x64 together | Restart app | Multiple CoreEngine instances |
| No Live Debugger | Can't step patched code | Export patches, test separately | x64dbg/WinDbg plugin (Phase 6) |
| Limited String Detection | Only ASCII/wide, not obfuscated | Use pattern search | Custom string plugins |
| No Decompiler | Disassembly only | Use LM Studio explanation | Ghidra HTTP integration (optional) |

### Workarounds

**Slow PE Parsing**: Large binaries (> 100MB) take time
- Solution: Lazy-load sections, stream imports/exports

**Memory Spike During Analysis**: CFG = ~2x binary size
- Solution: Progressive analysis (one function at a time)

**Hex/ASM Sync Lag**: Event cascading on rapid edits
- Solution: Already implemented (`_suppressEvents` + debouncing)

**LLM Timeouts**: Long model response times
- Solution: Already have CancellationToken in place

---

## Configuration & Settings

### Application Settings
**Location**: `AppData/ZizzysReverseEngineering/settings.json`

```json
{
  "Theme": "Dark",
  "FontFamily": "Consolas",
  "FontSize": 10,
  "HexBytesPerRow": 16,
  "AutoAnalyzeOnLoad": true,
  "AutoSaveProjects": true,
  "AutoSaveInterval": 60000,
  "LLMHost": "localhost",
  "LLMPort": 1234,
  "LogLevel": "Info"
}
```

### Project Settings (Saved Project)
```json
{
  "BinaryPath": "C:\\path\\to\\binary.exe",
  "ProjectVersion": 1,
  "Theme": "Dark",
  "LastViewState": {
    "HexScrollOffset": 0,
    "AsmScrollOffset": 0,
    "SelectedAddress": 4294967296
  },
  "Patches": [ ... ],
  "Annotations": { ... }
}
```

---

## Troubleshooting

### Build Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Keystone.dll not found | Post-copy failed | Check .csproj post-build events; `dotnet clean && dotnet build` |
| Iced version mismatch | Multiple versions | Check all .csproj (should be 1.21.0+) |
| NuGet timeout | Network/server slow | `dotnet nuget locals all --clear` then rebuild |

### Runtime Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Invalid PE header | Binary not x86/x64 or corrupted | Run `file binary.exe`; verify MD5 |
| Keystone assembly failed | Invalid mnemonic | Check Intel 64 Reference Manual; verify bitness |
| LLM connection refused | LM Studio not running | Start LM Studio; verify port 1234 (or update settings) |
| Out of memory | Binary too large or leak | Check event handler cleanup; profile with dotnet-trace |

### UI Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Hex/ASM out of sync | Event suppression failed | Manual scroll to same address; check `_suppressEvents` logic |
| Slow typing in asm editor | Validation too aggressive | Increase `Task.Delay()` ms in `DisassemblyController.OnLineEdited()` |
| Graph doesn't render | Analysis not run or CFG empty | Run `CoreEngine.RunAnalysis()` first; check `CFG != null` |

---

## API Reference

### CoreEngine
```csharp
public class CoreEngine
{
    public void LoadFile(string path);
    public void RebuildDisassemblyFromBuffer();
    public void RebuildInstructionAtOffset(int offset);
    
    public List<Instruction> Disassembly { get; }
    public HexBuffer HexBuffer { get; }
    public List<Function> Functions { get; }
    public ControlFlowGraph CFG { get; }
    public Dictionary<ulong, List<CrossReference>> CrossReferences { get; }
    public Dictionary<ulong, Symbol> Symbols { get; }
    
    public void RunAnalysis();
    public Function? FindFunctionAtAddress(ulong address);
    public string? GetSymbolName(ulong address);
}
```

### HexBuffer
```csharp
public class HexBuffer
{
    public HexBuffer(byte[] data);
    public byte this[int offset] { get; }
    public void WriteByte(int offset, byte value);
    public void WriteBytes(int offset, byte[] bytes);
    public IEnumerable<(int offset, byte original, byte current)> GetModifiedBytes();
    
    public event EventHandler<ByteChangeEventArgs>? ByteChanged;
    public event EventHandler? BytesChanged;
    
    public const int BytesPerRow = 16;
}
```

### Instruction
```csharp
public class Instruction
{
    public ulong Address { get; set; }                  // Virtual
    public int FileOffset { get; set; }                 // Binary
    public ulong RVA { get; set; }
    public byte[] Bytes { get; set; }
    public Iced.Intel.Instruction Raw { get; set; }
    
    public ulong? FunctionAddress { get; set; }
    public ulong? BasicBlockAddress { get; set; }
    public List<CrossReference> XRefsFrom { get; set; }
    public string? SymbolName { get; set; }
    public string? Annotation { get; set; }
}
```

### PatchEngine
```csharp
public class PatchEngine
{
    public List<PatchEntry> Patches { get; }
    public void RecordPatch(int offset, byte[] original, byte[] newBytes, string desc);
    public void UndoLastPatch();
    public List<PatchEntry> ExportPatches();
}
```

---

## Development Patterns

### Adding Analysis to Instruction
```csharp
var ins = _core.Disassembly[0];
ins.FunctionAddress = funcAddr;
ins.BasicBlockAddress = blockAddr;
ins.XRefsFrom = xrefs;
ins.SymbolName = "my_function";
```

### Running Analysis Async
```csharp
var controller = new AnalysisController(_core, _symbolTree, _graphControl);
controller.AnalysisCompleted += () => MessageBox.Show("Done!");
await controller.RunAnalysisAsync();
```

### Searching for Patterns
```csharp
var results = _core.FindBytePattern("55 48 89 ?? 48 83 EC");
var prologues = _core.FindPrologues();
```

### Using Undo/Redo
```csharp
_core.ApplyPatch(offset, newBytes, "NOP out call");  // Auto-tracked
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

---

## Next Phase Priorities

### Immediate (Next 1-2 weeks)
1. Expand test coverage to 66+ tests (all Core APIs)
2. Add missing Analysis classes if needed
3. Wire UI controls (SymbolTreeControl, GraphControl, LLMPane)
4. Fix edge cases (large files, corrupted binaries)

### Short-term (Next 1 month)
1. Performance optimization (profile hot paths)
2. Search implementation (byte/instruction/function dialogs)
3. Undo/Redo UI integration (Edit menu, Ctrl+Z/Y)
4. Settings UI (theme/font/layout preferences)

### Medium-term (Next 2-3 months)
1. Plugin system (custom C# extensions)
2. Decompiler integration (Ghidra/RetDec HTTP)
3. Debugger hooks (x64dbg live breakpoints)
4. Import/export enhancements (IDA, Ghidra formats)

### Long-term (3+ months)
1. Mobile viewer (iOS/Android read-only)
2. Cloud collaboration (shared annotations)
3. ML integration (function classification, anomaly detection)
4. VR visualization (3D CFG rendering)

---

## Design Principles

✅ **Separation of Concerns**: Analysis independent of UI; UI independent of analysis  
✅ **Event-Driven**: Analysis complete → events → UI subscribes and updates  
✅ **Incremental**: Partial re-analysis when user patches code  
✅ **Lazy Loading**: Build CFG/xrefs on-demand, not at load time  
✅ **Caching**: Cache results; invalidate only affected sections  

---

## For Questions

- **Decision History**: See [CONVERSATION_LOG.md](CONVERSATION_LOG.md)
- **Authoritative Reference**: This file [AI_CODING_INSTRUCTIONS.md](AI_CODING_INSTRUCTIONS.md)
- **Recent Fixes**: Check CONVERSATION_LOG.md for phases 1-8 details
