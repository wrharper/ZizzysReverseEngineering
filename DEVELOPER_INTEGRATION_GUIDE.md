# Developer Integration Guide

## Quick Start

### Setting Up Your Development Environment

1. **Clone and build**:
   ```bash
   git clone <repo>
   cd ZizzysReverseEngineering
   dotnet build
   dotnet run --project ReverseEngineering.WinForms
   ```

2. **Verify LM Studio connection**:
   - Ensure LM Studio is running on `localhost:1234`
   - Open app → Tools → Settings → LM Studio → Test Connection

3. **Load a test binary**:
   - File → Open Binary
   - Try a small binary first (notepad.exe, calc.exe)

---

## Architecture Overview

```
Binary File
    ↓
┌─────────────────────────────────────────────────┐
│  ReverseEngineering.Core (Logic)               │
│  ├── Disassembler.cs (Iced.Intel decoding)    │
│  ├── CoreEngine.cs (orchestrator)             │
│  ├── HexBuffer.cs (mutable binary + patches)  │
│  └── Analysis/ (CFG, xrefs, functions)        │
└─────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────┐
│  ReverseEngineering.WinForms (UI)              │
│  ├── FormMain.cs (main window)                 │
│  ├── HexEditor/ (binary view)                  │
│  ├── MainWindow/Controllers (sync logic)       │
│  └── Settings/ (SettingsDialog)                │
└─────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────┐
│  External Services                             │
│  ├── LM Studio (http://localhost:1234)        │
│  └── File System (projects, settings, logs)   │
└─────────────────────────────────────────────────┘
```

---

## Core Components & APIs

### 1. CoreEngine (Central Orchestrator)

**Location**: [ReverseEngineering.Core/CoreEngine.cs](ReverseEngineering.Core/CoreEngine.cs)

**Key Methods**:

```csharp
// Loading
void LoadFile(string path)                      // Load binary, disassemble
void RebuildDisassemblyFromBuffer()             // Full re-disassemble
void RebuildInstructionAtOffset(int offset)     // Incremental re-disassemble

// Analysis
void RunAnalysis()                              // CFG + xref + symbols
Function? FindFunctionAtAddress(ulong addr)    // Look up function
string? GetSymbolName(ulong addr)               // Resolve symbol

// Properties
List<Instruction> Disassembly { get; }         // All instructions
List<Function> Functions { get; }              // Discovered functions
ControlFlowGraph CFG { get; }                  // Control flow
UndoRedoManager UndoRedo { get; }              // History
```

**Example**: Load and analyze a binary

```csharp
var core = new CoreEngine();
core.FileLoaded += (disasm) => 
{
    Console.WriteLine($"Loaded {disasm.Count} instructions");
};
core.LoadFile("C:\\notepad.exe");
core.RunAnalysis();

var func = core.FindFunctionAtAddress(0x401000);
Console.WriteLine($"Function: {func?.Name}");
```

### 2. HexBuffer (Mutable Binary)

**Location**: [ReverseEngineering.Core/HexBuffer.cs](ReverseEngineering.Core/HexBuffer.cs)

**Key Methods**:

```csharp
// Reading
byte this[int index] { get; }                  // Direct indexing
byte[] Bytes { get; }                          // Full buffer
byte[] OriginalBytes { get; }                  // Unmodified copy

// Writing
void WriteByte(int offset, byte value)         // Single byte edit
void WriteBytes(int offset, byte[] values)     // Multi-byte patch

// State tracking
bool[] Modified { get; }                       // Which bytes changed
int GetModifiedCount()                         // How many bytes changed
List<(int, int)> GetModifiedRanges()          // Changed regions

// Analysis
bool HasModificationsInRange(int start, int end)
void RevertToOriginal()                        // Undo all changes
```

**Example**: Patch binary and get changes

```csharp
var buf = new HexBuffer(bytes, "file.exe");
buf.WriteByte(0x1000, 0x90);  // Write NOP
buf.WriteByte(0x1001, 0x90);
buf.WriteByte(0x1002, 0x90);

var modified = buf.GetModifiedBytes();        // All changes
foreach (var (offset, orig, new_val) in modified)
{
    Console.WriteLine($"0x{offset:X}: {orig:X2} → {new_val:X2}");
}
```

### 3. Instruction (Unified Representation)

**Location**: [ReverseEngineering.Core/Instruction.cs](ReverseEngineering.Core/Instruction.cs)

**Key Properties**:

```csharp
// Address mapping
ulong Address { get; set; }                    // Virtual address (IP)
int FileOffset { get; set; }                   // Offset in binary
uint RVA { get; set; }                         // Relative virtual address

// Disassembly
string Mnemonic { get; set; }                  // "MOV", "JMP", etc.
string Operands { get; set; }                  // "RAX, RBX"
byte[] Bytes { get; set; }                     // Raw bytes

// Iced.Intel
Iced.Intel.Instruction? Raw { get; set; }     // For operand analysis

// Analysis metadata
ulong? FunctionAddress { get; set; }           // Parent function
List<CrossReference> XRefsFrom { get; set; }   // References from this instruction
string? SymbolName { get; set; }               // Symbol at this address
ulong? RIPRelativeTarget { get; set; }         // Resolved data/import reference
```

**Example**: Navigate from instruction

```csharp
var instr = core.Disassembly[100];
Console.WriteLine($"{instr.Address:X8}: {instr.Mnemonic} {instr.Operands}");

if (instr.FunctionAddress.HasValue)
{
    var func = core.FindFunctionAtAddress(instr.FunctionAddress.Value);
    Console.WriteLine($"  Part of: {func?.Name}");
}

if (instr.RIPRelativeTarget.HasValue)
{
    Console.WriteLine($"  References: 0x{instr.RIPRelativeTarget:X}");
}
```

### 4. LocalLLMClient (AI Integration)

**Location**: [ReverseEngineering.Core/LLM/LocalLLMClient.cs](ReverseEngineering.Core/LLM/LocalLLMClient.cs)

**Key Methods**:

```csharp
// Connection
Task<bool> IsHealthyAsync()                    // LM Studio running?
Task<string[]> GetAvailableModelsAsync()       // List loaded models

// Completion
Task<string> CompleteAsync(string prompt)      // Get AI response
Task<string> ChatAsync(string message)         // Multi-turn chat

// Configuration
string Model { get; set; }                     // Model name
int MaxTokens { get; set; }                    // Output length
float Temperature { get; set; }                // 0=deterministic, 1=random
void ApplySettingsFromManager()                // Load settings
```

**Example**: Use LM Studio for instruction explanation

```csharp
var client = new LocalLLMClient("localhost", 1234, "neural-chat");
client.ApplySettingsFromManager();  // Load from app settings

if (await client.IsHealthyAsync())
{
    var prompt = "Explain this x86-64 instruction: MOV RAX, [RIP + 0x1000]";
    var response = await client.CompleteAsync(prompt);
    Console.WriteLine(response);
}
```

### 5. SettingsManager (Configuration)

**Location**: [ReverseEngineering.Core/ProjectSystem/SettingsManager.cs](ReverseEngineering.Core/ProjectSystem/SettingsManager.cs)

**Key Methods**:

```csharp
// Persistence
void LoadSettings()                            // Load from disk
void SaveSettings()                            // Save to disk
AppSettings Current { get; }                   // Current settings object

// LM Studio
string GetLMStudioUrl()                        // http://host:port
void SetLMStudioHost(string host)
void SetLMStudioPort(int port)
void SetLMStudioTemperature(double temp)

// Analysis
void SetAutoAnalyzeOnLoad(bool enabled)
void SetMaxFunctionSize(int size)

// UI
void SetTheme(string theme)
void SetFont(string family, int size)

// Validation
bool ValidateLMStudioConnection()
string GetValidationErrors()

// Import/Export
string ExportSettingsAsJson()
bool TryImportSettings(string json)
```

**Example**: Configure app on startup

```csharp
SettingsManager.LoadSettings();  // Load saved settings

// Or set defaults programmatically
SettingsManager.SetLMStudioHost("localhost");
SettingsManager.SetLMStudioPort(1234);
SettingsManager.SetLMStudioTemperature(0.7);
SettingsManager.SetAutoAnalyzeOnLoad(true);
SettingsManager.SaveSettings();

// Verify validity
if (!SettingsManager.ValidateLMStudioConnection())
{
    var errors = SettingsManager.GetValidationErrors();
    MessageBox.Show(errors);
}
```

### 6. DisassemblyOptimizer (Performance)

**Location**: [ReverseEngineering.Core/DisassemblyOptimizer.cs](ReverseEngineering.Core/DisassemblyOptimizer.cs)

**Key Methods**:

```csharp
// Caching
void BuildCache(List<Instruction> instructions)
void InvalidateCache()
void InvalidateCacheRange(ulong start, ulong end)

// Fast lookups
bool TryGetInstructionAt(ulong address, out Instruction? instr)
bool TryGetInstructionAtOffset(int offset, out Instruction? instr)
List<Instruction> GetInstructionsInRange(ulong start, ulong end)

// Batch operations
void BatchUpdateMetadata(List<(ulong, ulong?, string?)> updates)
void LazyLoadAnnotations(Func<ulong, string?> provider)

// Diagnostics
CacheStats GetStats()
```

**Example**: Use optimizer for performance

```csharp
var optimizer = new DisassemblyOptimizer();
optimizer.BuildCache(core.Disassembly);

// Fast lookup (O(1) vs O(n))
if (optimizer.TryGetInstructionAt(0x401000, out var instr))
{
    Console.WriteLine(instr.Mnemonic);
}

// Check cache
var stats = optimizer.GetStats();
Console.WriteLine($"Cached: {stats.CachedInstructions} instructions");
```

---

## UI Controller Patterns

### DisassemblyController (Hex ↔ Asm Sync)

**Location**: [ReverseEngineering.WinForms/MainWindow/DisassemblyController.cs](ReverseEngineering.WinForms/MainWindow/DisassemblyController.cs)

**Key Methods**:

```csharp
// Navigation
void SelectInstruction(int index)
int GetSelectedInstructionIndex()
ulong GetSelectedInstructionAddress()

// Editing
void OnLineEdited(int lineIndex, string newText)

// Display
void RefreshDisassembly()
void ScrollToAddress(ulong address)
```

**Example**: Navigate to function

```csharp
var func = core.FindFunctionAtAddress(0x401000);
if (func != null)
{
    var instr = core.Disassembly.FirstOrDefault(i => i.Address == func.Address);
    if (instr != null)
    {
        disasmController.SelectInstruction(core.Disassembly.IndexOf(instr));
    }
}
```

### HexEditorController (Selection Sync)

**Location**: [ReverseEngineering.WinForms/MainWindow/HexEditorController.cs](ReverseEngineering.WinForms/MainWindow/HexEditorController.cs)

**Key Methods**:

```csharp
// Selection
void OnHexSelectionChanged(int startOffset, int endOffset)
void SyncToDisassembly(int hexOffset)

// Editing
void OnHexByteEdited(int offset, byte value)
```

**Example**: Respond to hex edit

```csharp
hexEditorController.HexByteChanged += (offset, oldVal, newVal) =>
{
    core.RebuildInstructionAtOffset(offset);
    disasmController.RefreshDisassembly();
};
```

### MainMenuController (Menu & Dialogs)

**Location**: [ReverseEngineering.WinForms/MainWindow/MainMenuController.cs](ReverseEngineering.WinForms/MainWindow/MainMenuController.cs)

**Key Methods**:

```csharp
// File operations
void OpenBinary()
void SaveProject()

// Undo/Redo
void UndoClick()
void RedoClick()

// Analysis
void RunAnalysisClick()
void ExplainInstructionClick()

// Settings
void ShowSettingsDialog()
```

---

## Common Tasks

### Task 1: Add a New Analysis Feature

**Goal**: Implement a new analysis algorithm (e.g., detect function prologues)

1. **Create analyzer in Core**:
   ```csharp
   // ReverseEngineering.Core/Analysis/PrologueDetector.cs
   public class PrologueDetector
   {
       public static List<ulong> FindPrologues(List<Instruction> disasm)
       {
           var prologues = new List<ulong>();
           for (int i = 0; i < disasm.Count - 2; i++)
           {
               // PUSH RBP; MOV RBP, RSP
               if (disasm[i].Mnemonic == "PUSH" && /* ... */)
               {
                   prologues.Add(disasm[i].Address);
               }
           }
           return prologues;
       }
   }
   ```

2. **Integrate into CoreEngine**:
   ```csharp
   // In CoreEngine.RunAnalysis()
   var prologues = PrologueDetector.FindPrologues(Disassembly);
   foreach (var addr in prologues)
   {
       // Mark functions
   }
   ```

3. **Wire to UI** (optional):
   ```csharp
   // In DisassemblyControl: Highlight prologue instructions
   if (address is in prologues)
   {
       color = Color.Yellow;
   }
   ```

### Task 2: Add a Hex Editor Feature

**Goal**: Add copy-as-hex functionality

1. **Add to HexEditorControl**:
   ```csharp
   public string GetSelectionAsHex()
   {
       var sb = new StringBuilder();
       for (int i = SelectionStart; i <= SelectionEnd; i++)
       {
           sb.Append(_buffer[i].ToString("X2"));
       }
       return sb.ToString();
   }
   ```

2. **Wire to context menu**:
   ```csharp
   // In HexEditorControl.OnContextMenuStrip
   copyHexItem.Click += (s, e) =>
   {
       Clipboard.SetText(GetSelectionAsHex());
   };
   ```

### Task 3: Integrate Custom LLM Prompt

**Goal**: Add "Detect Vulnerability Patterns" LLM feature

1. **Add to LLMAnalyzer**:
   ```csharp
   public async Task<string> DetectVulnerabilitiesAsync(
       List<Instruction> function)
   {
       var code = FormatFunctionForLLM(function);
       var prompt = $@"
   Analyze this x86-64 function for security issues:
   
   {code}
   
   List potential vulnerabilities and security concerns.";
       
       return await _client.CompleteAsync(prompt);
   }
   ```

2. **Wire to UI**:
   ```csharp
   // In MainMenuController.BuildMenu()
   analysisMenu.DropDownItems.Add(
       new ToolStripMenuItem("Find Vulnerabilities (LLM)",
           null, async (s, e) => await _analysisController.DetectVulnerabilitiesAsync(func))
   );
   ```

### Task 4: Add Settings Option

**Goal**: Add "Show function argument hints" setting

1. **Add to SettingsManager**:
   ```csharp
   // In AppSettings
   public bool ShowArgumentHints { get; set; } = true;
   ```

2. **Add UI control in SettingsDialog**:
   ```csharp
   _showHintsCheckBox = AddCheckBox(panel, "Show argument hints", 10, ref y,
       _settings.ShowArgumentHints);
   ```

3. **Save in SaveSettings()**:
   ```csharp
   _settings.ShowArgumentHints = _showHintsCheckBox.Checked;
   ```

4. **Use in DisassemblyControl**:
   ```csharp
   if (SettingsManager.Current.UI.ShowArgumentHints)
   {
       // Display hints
   }
   ```

---

## Testing Guidelines

### Unit Testing

```csharp
// Test HexBuffer patch tracking
[Test]
public void WriteByte_SetsBit()
{
    var buf = new HexBuffer(new byte[100]);
    buf.WriteByte(5, 0xFF);
    Assert.True(buf.Modified[5]);
    Assert.AreEqual(0xFF, buf[5]);
}

// Test instruction cache
[Test]
public void DisassemblyOptimizer_FastLookup()
{
    var optimizer = new DisassemblyOptimizer();
    var instr = new List<Instruction> { 
        new() { Address = 0x1000, Mnemonic = "NOP" } 
    };
    optimizer.BuildCache(instr);
    
    Assert.True(optimizer.TryGetInstructionAt(0x1000, out var found));
    Assert.AreEqual("NOP", found?.Mnemonic);
}
```

### Integration Testing

```csharp
// Test LM Studio client
[Test]
[Timeout(600000)]  // 10 minutes - no premature timeout!
public async Task LocalLLMClient_Explain()
{
    var client = new LocalLLMClient("localhost", 1234, "neural-chat");
    if (!await client.IsHealthyAsync())
    {
        Assert.Inconclusive("LM Studio not running");
    }
    
    var response = await client.CompleteAsync("What is x86-64?");
    Assert.That(response.Length, Is.GreaterThan(100));
    Assert.That(response, Does.Not.Contain("Error"));
}
```

### Manual Testing (Settings/UI)

1. Open app → Tools → Settings
2. Change LM Studio host to invalid value
3. Click OK
4. Verify error message appears
5. Reset to localhost:1234
6. Test connection button

---

## Performance Considerations

### Large Binary Optimization

For binaries >50MB:

```csharp
// Enable caching
var optimizer = new DisassemblyOptimizer();
optimizer.BuildCache(core.Disassembly);

// Use batch operations
optimizer.BatchUpdateMetadata(functionMetadata);

// Use incremental re-disassembly
core.RebuildInstructionAtOffset(patchOffset);  // Not full rebuild

// Check modified ranges
var ranges = hexBuffer.GetModifiedRanges();
foreach (var (start, end) in ranges)
{
    core.RebuildInstructionAtOffset(start);  // Re-analyze each range
}
```

### Debugging Performance

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
core.RunAnalysis();
sw.Stop();
Logger.Log($"Analysis: {sw.ElapsedMilliseconds}ms");

var stats = optimizer.GetStats();
Logger.Log($"Cache: {stats}");
```

---

## Documentation Structure

Key docs for developers:

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Overall design (in copilot-instructions.md)
- **[SETTINGS_SYSTEM.md](SETTINGS_SYSTEM.md)** - Configuration system
- **[TESTING_PROTOCOL.md](TESTING_PROTOCOL.md)** - LLM testing rules
- **[PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md)** - Optimization utilities
- **[PHASE4_LM_STUDIO_INTEGRATION.md](PHASE4_LM_STUDIO_INTEGRATION.md)** - LLM details

---

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| "LM Studio connection failed" | Wrong host/port | Tools → Settings → LM Studio, verify localhost:1234 |
| "Disassembly very slow" | No cache | Use DisassemblyOptimizer after loading |
| "Settings not persisting" | File locked | Check %APPDATA%\ZizzysReverseEngineering\settings.json |
| "LLM response truncated" | Timeout too short | TESTING_PROTOCOL: use 300s timeout |
| "Hex editor unresponsive" | Large binary + no optimization | Enable DisassemblyOptimizer + use incremental ops |

---

## Quick Reference

### Loading & Analyzing

```csharp
var core = new CoreEngine();
core.LoadFile("binary.exe");
core.RunAnalysis();

var optimizer = new DisassemblyOptimizer();
optimizer.BuildCache(core.Disassembly);
```

### UI Integration

```csharp
// In FormMain.cs
var disasmController = new DisassemblyController(disasmControl, core);
var analysisController = new AnalysisController(core, symbolTree, graphControl);
var menuController = new MainMenuController(this, menuStrip, hexEditor, log, 
    disasmController, statusLabel, core, analysisController);
```

### Settings

```csharp
SettingsManager.LoadSettings();
var url = SettingsManager.GetLMStudioUrl();
SettingsManager.SetLMStudioTemperature(0.8);
SettingsManager.SaveSettings();
```

### LLM Integration

```csharp
var client = new LocalLLMClient("localhost", 1234, "neural-chat");
client.ApplySettingsFromManager();
var response = await client.CompleteAsync("Explain: MOV RAX, RBX");
```

---

## What's Next?

**Planned features**:
- [ ] Debugger integration (x64dbg bridge)
- [ ] Plugin system (load custom analyzers)
- [ ] Decompiler backend (Ghidra HTTP)
- [ ] Performance profiler (for binaries)
- [ ] Memory dump analysis
- [ ] Malware sandbox integration

**For contributors**: Create issue/PR with feature proposal, follow architecture patterns, write tests.
