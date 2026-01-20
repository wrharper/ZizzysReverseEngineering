# API Reference: Analysis Layer & Utilities

## Core Analysis API

### `CoreEngine.RunAnalysis()`
Executes full analysis pipeline on loaded disassembly.

```csharp
public void RunAnalysis()
```

**After calling**, access:
- `engine.Functions` - `List<Function>` discovered functions
- `engine.CFG` - `ControlFlowGraph` for entry point
- `engine.CrossReferences` - `Dictionary<ulong, List<CrossReference>>` xrefs
- `engine.Symbols` - `Dictionary<ulong, Symbol>` symbols

**Example**:
```csharp
engine.LoadFile("app.exe");
engine.RunAnalysis();

foreach (var func in engine.Functions)
    Console.WriteLine($"Function @ 0x{func.Address:X}: {func.Name}");
```

---

## BasicBlock & CFG API

### `BasicBlock`
Represents a basic block in the program.

**Properties**:
- `ulong StartAddress` - Virtual address of first instruction
- `ulong EndAddress` - Virtual address of last byte
- `int StartInstructionIndex` - Index in disassembly list
- `int EndInstructionIndex` - Index in disassembly list
- `List<ulong> Successors` - Successor block addresses
- `List<ulong> Predecessors` - Predecessor block addresses
- `bool IsEntryPoint` - True if function entry
- `ulong? ParentFunctionAddress` - Parent function (if any)

### `ControlFlowGraph`
Control flow graph with traversal methods.

**Properties**:
- `IReadOnlyDictionary<ulong, BasicBlock> Blocks` - All blocks keyed by address
- `IReadOnlyList<ulong> EntryPoints` - Entry block addresses

**Methods**:
```csharp
public void AddBlock(BasicBlock block)
public BasicBlock? GetBlock(ulong address)
public BasicBlock? GetBlockContainingAddress(ulong address)
public IEnumerable<BasicBlock> GetSuccessors(BasicBlock block)
public IEnumerable<BasicBlock> GetPredecessors(BasicBlock block)
public IEnumerable<BasicBlock> TraverseDFS(ulong startAddress)
public IEnumerable<BasicBlock> TraverseBFS(ulong startAddress)
```

**Example**:
```csharp
var cfg = engine.CFG;
foreach (var block in cfg.TraverseBFS(cfg.EntryPoints[0]))
    Console.WriteLine($"Block @ 0x{block.StartAddress:X}: {block.InstructionCount} instructions");
```

---

## Function Discovery API

### `Function`
Represents a discovered function.

**Properties**:
- `ulong Address`
- `string? Name`
- `string? Source` - "export", "import", "entry", "prologue", "call"
- `int InstructionCount`
- `bool IsImported`, `IsExported`, `IsEntryPoint`
- `ControlFlowGraph? CFG` - Function's control flow

### `FunctionFinder.FindFunctions()`
Discovers functions using multiple heuristics.

```csharp
public static List<Function> FindFunctions(
    List<Instruction> disassembly,
    CoreEngine engine,
    bool includeExports = true,
    bool includeImports = true,
    bool includePrologues = true,
    bool includeCallGraph = true)
```

**Example**:
```csharp
var functions = FunctionFinder.FindFunctions(engine.Disassembly, engine);
foreach (var func in functions)
{
    if (func.CFG != null)
        Console.WriteLine($"{func.Name} has {func.CFG.TotalBlocks} blocks");
}
```

---

## Cross-Reference API

### `CrossReference`
Represents a single cross-reference.

**Properties**:
- `ulong SourceAddress`
- `ulong TargetAddress`
- `string RefType` - "code", "data", "import", "string", etc.
- `string? Description`

### `CrossReferenceEngine.BuildXRefs()`
Builds cross-reference database.

```csharp
public static Dictionary<ulong, List<CrossReference>> BuildXRefs(
    List<Instruction> disassembly,
    ulong imageBase = 0x140000000)
```

**Query Methods**:
```csharp
public static List<CrossReference> GetOutgoingRefs(
    ulong address,
    Dictionary<ulong, List<CrossReference>> xrefs)

public static List<CrossReference> GetIncomingRefs(
    ulong address,
    Dictionary<ulong, List<CrossReference>> xrefs)
```

**Example**:
```csharp
var xrefs = engine.CrossReferences;

// Who calls function at 0x400000?
var incoming = CrossReferenceEngine.GetIncomingRefs(0x400000, xrefs);
foreach (var xref in incoming)
    Console.WriteLine($"Called from 0x{xref.SourceAddress:X}");

// Where does 0x400000 jump to?
var outgoing = CrossReferenceEngine.GetOutgoingRefs(0x400000, xrefs);
foreach (var xref in outgoing)
    Console.WriteLine($"Jumps to 0x{xref.TargetAddress:X} ({xref.RefType})");
```

---

## Symbol Resolution API

### `Symbol`
Represents a named symbol.

**Properties**:
- `ulong Address`
- `string Name`
- `string SymbolType` - "function", "data", "import", "export", etc.
- `string? Section` - ".text", ".data", etc.
- `uint Size`
- `bool IsImported`, `IsExported`
- `string? SourceDLL` - For imports

### `SymbolResolver.ResolveSymbols()`
Collects symbols from various sources.

```csharp
public static Dictionary<ulong, Symbol> ResolveSymbols(
    List<Instruction> disassembly,
    CoreEngine engine,
    bool includeImports = true,
    bool includeExports = true,
    bool includeStrings = false)
```

**Query Methods**:
```csharp
public static string? GetSymbolName(ulong address, Dictionary<ulong, Symbol> symbols)
public static Symbol? FindSymbolByName(string name, Dictionary<ulong, Symbol> symbols)
public static void AddUserAnnotation(ulong address, string name, string symbolType, Dictionary<ulong, Symbol> symbols)
public static IEnumerable<Symbol> GetSymbolsByType(string symbolType, Dictionary<ulong, Symbol> symbols)
```

**Example**:
```csharp
var symbols = engine.Symbols;

// Get symbol at address
var sym = symbols[0x400000];
Console.WriteLine($"{sym.Name}: {sym.SymbolType}");

// Find by name
var mainSym = SymbolResolver.FindSymbolByName("main", symbols);

// List all functions
foreach (var func in SymbolResolver.GetSymbolsByType("function", symbols))
    Console.WriteLine(func.Name);
```

---

## Pattern Matching API

### `PatternMatch`
Result from pattern matching.

**Properties**:
- `ulong Address`
- `int Offset`
- `byte[]? MatchedBytes`
- `string? Description`

### `PatternMatcher` (Static Methods)

**Byte pattern matching**:
```csharp
public static List<PatternMatch> FindBytePattern(
    byte[] buffer,
    string pattern,      // "55 8B EC" or "55 8B ?? E5"
    string? description = null)

public static Dictionary<string, List<PatternMatch>> FindMultiplePatterns(
    byte[] buffer,
    Dictionary<string, string> patterns)
```

**Instruction pattern matching**:
```csharp
public static List<PatternMatch> FindInstructionPattern(
    List<Instruction> disassembly,
    Func<Instruction, bool> predicate,
    string? description = null)
```

**Built-in patterns**:
```csharp
public static List<PatternMatch> FindX64Prologues(byte[] buffer)
public static List<PatternMatch> FindStackSetup(byte[] buffer)
public static List<PatternMatch> FindReturnInstructions(byte[] buffer)
public static List<PatternMatch> FindNOPSled(byte[] buffer, int minLength = 4)
```

**Example**:
```csharp
// Find x64 prologues
var prologues = PatternMatcher.FindX64Prologues(engine.HexBuffer.Bytes);
Console.WriteLine($"Found {prologues.Count} prologues");

// Find all RET instructions
var returns = PatternMatcher.FindReturnInstructions(engine.HexBuffer.Bytes);

// Custom pattern: find stack setup with any immediate
var stackSetups = PatternMatcher.FindBytePattern(
    engine.HexBuffer.Bytes,
    "48 83 EC ??",
    "stack_setup");
```

---

## Search API

### `SearchResult`
Result from a search operation.

**Properties**:
- `ulong Address`
- `int Offset`
- `string ResultType` - "byte", "instruction", "function", "symbol", etc.
- `string? Description`
- `byte[]? Data`

### `SearchManager` (Static Methods)

**Byte search**:
```csharp
public static List<SearchResult> SearchBytes(HexBuffer buffer, byte[] pattern)
public static List<SearchResult> SearchBytePattern(HexBuffer buffer, string pattern)  // with wildcards
```

**Instruction search**:
```csharp
public static List<SearchResult> SearchInstructionsByMnemonic(List<Instruction> disassembly, string mnemonic)
public static List<SearchResult> SearchInstructions(List<Instruction> disassembly, Func<Instruction, bool> predicate, string resultType = "instruction")
```

**Function/symbol search**:
```csharp
public static List<SearchResult> SearchFunctionsByName(List<Function> functions, string name)
public static List<SearchResult> SearchSymbolsByName(Dictionary<ulong, Symbol> symbols, string name)
```

**Cross-reference search**:
```csharp
public static List<SearchResult> FindReferencesToAddress(ulong address, Dictionary<ulong, List<CrossReference>> xrefs)
public static List<SearchResult> FindReferencesFromAddress(ulong address, Dictionary<ulong, List<CrossReference>> xrefs)
```

**Example**:
```csharp
// Search for all CALL instructions
var calls = SearchManager.SearchInstructionsByMnemonic(engine.Disassembly, "call");

// Find all references to an address
var refs = SearchManager.FindReferencesToAddress(0x400000, engine.CrossReferences);

// Search for functions with "init" in name
var inits = SearchManager.SearchFunctionsByName(engine.Functions, "init");
```

---

## Undo/Redo API

### `UndoRedoManager`
Full undo/redo history stack.

**Properties**:
- `bool CanUndo` - Can undo?
- `bool CanRedo` - Can redo?

**Methods**:
```csharp
public void Execute(Command command)
public void Undo()
public void Redo()
public void Clear()
public string? GetNextUndoDescription()
public string? GetNextRedoDescription()
public IEnumerable<string> GetUndoHistory()
```

**Events**:
- `event Action<Command>? CommandExecuted`
- `event Action? HistoryChanged`

**Example**:
```csharp
var undoRedo = engine.UndoRedo;

// Apply patch (automatically tracked)
engine.ApplyPatch(0x1000, new byte[] { 0x90, 0x90 }, "NOP out");

// Undo
undoRedo.Undo();

// Redo
undoRedo.Redo();

// Check history
Console.WriteLine($"Next undo: {undoRedo.GetNextUndoDescription()}");
```

---

## Settings & Logging API

### `SettingsManager` (Static)
Application-level settings persistence.

```csharp
public static void LoadSettings()
public static void SaveSettings()
public static AppSettings Current { get; }
public static void SetLastOpenedFile(string path)
public static void SetTheme(string theme)
public static void SetFont(string fontFamily, int fontSize)
public static void SetAutoAnalyze(bool enabled)
```

### `Logger` (Static)
File + memory logging.

```csharp
public static void Info(string category, string message)
public static void Warning(string category, string message)
public static void Error(string category, string message, Exception? ex = null)
public static void PatchApplied(int offset, byte[] original, byte[] newBytes)
public static IReadOnlyList<LogEntry> GetHistory()
public static IEnumerable<LogEntry> GetEntriesByCategory(string category)
```

**Example**:
```csharp
Logger.Info("ANALYSIS", "Starting function discovery...");
Logger.PatchApplied(0x1000, oldBytes, newBytes);
Logger.Error("ANALYSIS", "Failed to build CFG", exception);

// Query logs
var recentLogs = Logger.GetHistory();
var patches = Logger.GetEntriesByCategory("PATCH");
```

---

## Annotations API

### `AnnotationStore`
Per-project user annotations.

```csharp
public void SetFunctionName(ulong address, string name)
public void SetComment(ulong address, string comment)
public void SetSymbolType(ulong address, string symbolType)
public void RemoveAnnotation(ulong address)
public Annotation? GetAnnotation(ulong address)
public string? GetFunctionName(ulong address)
public IReadOnlyDictionary<ulong, Annotation> GetAll()
public void SaveToFile(string path)
public void LoadFromFile(string path)
```

**Example**:
```csharp
var store = new AnnotationStore();

store.SetFunctionName(0x400000, "main");
store.SetComment(0x400000, "Program entry point");
store.SetSymbolType(0x400000, "function");

store.SaveToFile("project.annotations.json");
```

---

## UI Components

### `SearchDialog(CoreEngine core)`
Multi-tab search UI (Ctrl+F).

**Events**:
- `event Action<SearchResult>? ResultSelected`

**Usage**:
```csharp
var dialog = new SearchDialog(_core);
dialog.ResultSelected += (result) => NavigateToAddress(result.Address);
dialog.Show();
```

### `SymbolTreeControl(CoreEngine core)`
Function/symbol tree view.

**Methods**:
- `void PopulateFromAnalysis()`
- `void Refresh()`

**Events**:
- `event Action<ulong>? SymbolSelected`

### `GraphControl(CoreEngine core)`
CFG visualization.

**Methods**:
- `void DisplayCFG(ControlFlowGraph cfg)`

**Events**:
- `event Action<ulong>? BlockSelected`

### `AnalysisController`
Async analysis runner.

```csharp
public async Task RunAnalysisAsync()
public void CancelAnalysis()
public void ShowFunctionCFG(ulong functionAddress)
```

**Events**:
- `event Action? AnalysisStarted`
- `event Action? AnalysisCompleted`

**Example**:
```csharp
var controller = new AnalysisController(_core, _symbolTree, _graphControl);
controller.AnalysisCompleted += () => MessageBox.Show("Done!");
await controller.RunAnalysisAsync();
```

---

## Performance Tips

1. **Cache Analysis Results**: Call `RunAnalysis()` once, query multiple times
2. **Async Large Binaries**: Use `AnalysisController.RunAnalysisAsync()` for 1MB+
3. **Pattern Matching**: Compile frequently-used patterns
4. **Xref Queries**: Use dictionaries for O(1) lookup
5. **Settings Batch**: Call `SettingsManager.SaveSettings()` once per session

---

## Common Patterns

### Find All Callers of a Function
```csharp
var callers = SearchManager.FindReferencesToAddress(funcAddress, engine.CrossReferences)
    .Where(r => r.RefType == "call")
    .ToList();
```

### Navigate to Function by Name
```csharp
var func = engine.Functions.FirstOrDefault(f => f.Name == "main");
if (func != null)
    NavigateToAddress(func.Address);
```

### Annotate All Functions
```csharp
var store = new AnnotationStore();
foreach (var func in engine.Functions)
    store.SetFunctionName(func.Address, func.Name ?? "unnamed");
```

### Find Dead Code
```csharp
var reachable = new HashSet<ulong>();
MarkReachable(engine.CFG.EntryPoints[0], engine.CFG, reachable);
var deadCode = engine.Functions.Where(f => !reachable.Contains(f.Address));
```
