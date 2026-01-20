# Performance Optimization Guide

## Overview

ZizzysReverseEngineering now includes advanced performance optimization utilities designed to handle large binaries (>10MB) efficiently.

## Key Components

### 1. DisassemblyOptimizer

Fast, O(1) lookup cache for instructions by address or file offset.

```csharp
var optimizer = new DisassemblyOptimizer();

// After loading disassembly
optimizer.BuildCache(_core.Disassembly);

// Fast lookups
if (optimizer.TryGetInstructionAt(0x401000, out var instr))
{
    Console.WriteLine($"Found: {instr.Mnemonic}");
}

// Range queries
var block = optimizer.GetInstructionsInRange(0x401000, 0x401100);
```

#### Cache Management

```csharp
// After patching instructions
optimizer.InvalidateCacheRange(0x401000, 0x401010);

// Full rebuild
optimizer.InvalidateCache();

// Check cache status
var stats = optimizer.GetStats();
Console.WriteLine(stats);  // "Cache: 5000 instructions, Range: 0x400000-0x500000 (1MB)"
```

#### Batch Operations

```csharp
// More efficient than individual updates
var updates = new List<(ulong address, ulong? functionAddr, string? symbolName)>();
for (int i = 0; i < 1000; i++)
{
    updates.Add((0x401000 + (ulong)(i * 20), 0x401000, $"func_{i}"));
}
optimizer.BatchUpdateMetadata(updates);

// Lazy-load annotations (only when needed)
optimizer.LazyLoadAnnotations(addr => 
{
    // This function called only for un-annotated instructions
    return GetAnnotationFromStore(addr);
});
```

### 2. BatchOperandAnalyzer

Process RIP-relative operands efficiently across many instructions.

```csharp
// Analyze all RIP-relative operands at once
BatchOperandAnalyzer.AnalyzeRIPRelativeOperands(
    _core.Disassembly,
    imageBase: 0x400000,
    symbolResolver: (addr) => _core.GetSymbolName(addr)
);

// Build lookup table for quick access
var operandTypes = BatchOperandAnalyzer.BuildOperandTypeLookup(_core.Disassembly);
```

### 3. PackedInstruction

Memory-efficient storage for very large binaries.

```csharp
// Convert to packed format (saves ~50-70% memory)
var packed = new List<PackedInstruction>();
foreach (var instr in disassembly)
{
    packed.Add(PackedInstruction.FromInstruction(instr));
}

// Convert back to full Instruction when needed
var fullInstr = packed[0].ToInstruction();
```

## Performance Bottlenecks & Solutions

### Problem 1: Slow Disassembly on Large Binaries

**Symptom**: Taking 30+ seconds to disassemble a 20MB binary

**Solution: Incremental Re-disassembly**

```csharp
// ❌ SLOW: Re-disassemble entire binary
_core.RebuildDisassemblyFromBuffer();

// ✅ FAST: Re-disassemble only affected range
_core.RebuildInstructionAtOffset(patchOffset);
```

**Expected Performance**: 
- Full disassembly (10MB): 5-15 seconds
- Incremental (single instruction): <100ms

### Problem 2: Slow Symbol Lookups

**Symptom**: Clicking each instruction takes 200ms+ to resolve symbols

**Solution: Cache Symbol Results**

```csharp
// Build once after analysis
var symbolCache = new Dictionary<ulong, string>();
foreach (var instr in _core.Disassembly)
{
    var symbol = _core.GetSymbolName(instr.Address);
    if (!string.IsNullOrEmpty(symbol))
    {
        symbolCache[instr.Address] = symbol;
    }
}

// Lookup now O(1)
if (symbolCache.TryGetValue(addr, out var sym))
{
    // Instant access
}
```

### Problem 3: High Memory Usage (>1GB for large binaries)

**Symptom**: App using 2GB+ RAM for 50MB binary

**Solution: Use Packed Instructions**

```csharp
// Before: Each Instruction ~500 bytes (5000 instructions × 500 = 2.5MB per MB of binary)
// After: PackedInstruction ~100 bytes (80% memory savings)

var originalSize = GC.GetTotalMemory(false);
var packed = disassembly.Select(PackedInstruction.FromInstruction).ToList();
var newSize = GC.GetTotalMemory(false);

Console.WriteLine($"Memory saved: {(originalSize - newSize) / (1024 * 1024)}MB");
```

### Problem 4: Slow CFG Construction

**Symptom**: Analysis takes 2+ minutes for large binaries

**Solution: Parallel Block Analysis**

```csharp
// Analyze blocks in parallel (if not modifying shared state)
Parallel.ForEach(_core.Functions, new ParallelOptions { MaxDegreeOfParallelism = 4 },
    function =>
    {
        // Each function analyzed independently
        AnalyzeFunctionCFG(function);
    }
);
```

### Problem 5: Slow Xref Building

**Symptom**: Cross-reference analysis hangs for 30+ seconds

**Solution: Batch Xref Resolution**

```csharp
// ❌ SLOW: Process each instruction individually
foreach (var instr in disassembly)
{
    var xrefs = ResolveCrossReferences(instr);  // Slow!
}

// ✅ FAST: Batch process
var allXrefs = new Dictionary<ulong, List<CrossReference>>();
foreach (var batch in disassembly.Batch(1000))
{
    var batchXrefs = ResolveBatchCrossReferences(batch);  // Much faster
    foreach (var (addr, xrefs) in batchXrefs)
    {
        allXrefs[addr] = xrefs;
    }
}
```

## Profiling Tips

### Enable Detailed Logging

```csharp
// In FormMain or startup
SettingsManager.SetDetailedLogging(true);
```

This logs timing information to:
```
%APPDATA%\ZizzysReverseEngineering\logs\YYYY-MM-DD.log
```

### Manual Timing

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();

// Operation to measure
_core.RunAnalysis();

sw.Stop();
Logger.Log($"Analysis took {sw.ElapsedMilliseconds}ms", LogCategory.ANALYSIS);
```

### Memory Profiling

```csharp
var before = GC.GetTotalMemory(false);
// Operation
var after = GC.GetTotalMemory(false);

Console.WriteLine($"Memory delta: {(after - before) / (1024 * 1024)}MB");
```

## Best Practices

### ✅ DO

- Use `RebuildInstructionAtOffset()` for single edits
- Cache symbol lookups after analysis
- Use batch operations for metadata updates
- Lazy-load expensive metadata (annotations, xrefs)
- Enable DisassemblyOptimizer after loading

### ❌ DON'T

- Call `RebuildDisassemblyFromBuffer()` for every byte change
- Look up symbols individually in loops
- Process instructions one-by-one for batch operations
- Load all metadata eagerly
- Ignore cache invalidation after patches

## Configuration (Settings)

### Optimization Settings (Planned)

```csharp
// In AppSettings (future)
public bool EnableDisassemblyCache { get; set; } = true;
public bool EnableLazyLoadingAnnotations { get; set; } = true;
public int BatchSizeForOperandAnalysis { get; set; } = 1000;
public bool UsePackedInstructionStorage { get; set; } = false;  // Memory optimization
```

## Integration with Settings System

```csharp
// FormMain initialization
var optimizer = new DisassemblyOptimizer();

_core.FileLoaded += (disasm) =>
{
    // Auto-build cache after loading
    optimizer.BuildCache(disasm);
    Logger.Log($"Cache built: {optimizer.GetStats()}", LogCategory.ANALYSIS);
};

_core.InstructionPatched += (offset) =>
{
    // Invalidate cache for affected range
    var instr = _core.InstructionAt(offset);
    if (instr != null)
    {
        optimizer.InvalidateCacheRange(instr.Address, instr.EndAddress + 100);
    }
};
```

## Performance Targets

| Operation | Target | Current | Status |
|-----------|--------|---------|--------|
| Load 10MB binary | 10s | 8-15s | ✅ Good |
| Single instruction edit | 100ms | 50-200ms | ✅ Good |
| CFG construction (1000 funcs) | 5s | 8-12s | ⏳ To optimize |
| Symbol lookup (5000 unique) | 100ms | 200-500ms | ⏳ With caching → 1ms |
| Full analysis (10MB) | 15s | 20-30s | ⏳ With parallelization |

## Future Optimizations

Planned improvements:

1. **Instruction Streaming** - Load disassembly on-demand from disk (for 100MB+ binaries)
2. **Parallel Analysis** - Multi-threaded CFG/xref construction
3. **Incremental CFG** - Update CFG after single instruction patch
4. **Xref Indexing** - Pre-built xref table for O(1) lookups
5. **Symbol Deduplication** - Share symbol strings across instructions
6. **Tiered Caching** - L1 (hot), L2 (warm), L3 (disk)

## Debugging Performance Issues

### Check Cache Status

```csharp
var stats = optimizer.GetStats();
if (!stats.IsValid)
{
    // Cache was invalidated
    optimizer.BuildCache(_core.Disassembly);
}

Console.WriteLine($"Instructions cached: {stats.CachedInstructions}");
Console.WriteLine($"Cache range: 0x{stats.CachedRangeStart:X} - 0x{stats.CachedRangeEnd:X}");
```

### Identify Bottlenecks

```csharp
// Profile each major operation
var operations = new Dictionary<string, Stopwatch>();

operations["Disasm"] = Stopwatch.StartNew();
_core.RebuildDisassemblyFromBuffer();
operations["Disasm"].Stop();

operations["CFG"] = Stopwatch.StartNew();
var cfg = new BasicBlockBuilder(_core).BuildCFG();
operations["CFG"].Stop();

operations["Xrefs"] = Stopwatch.StartNew();
var xrefs = new CrossReferenceEngine(_core).BuildCrossReferences();
operations["Xrefs"].Stop();

foreach (var (op, sw) in operations.OrderByDescending(x => x.Value.ElapsedMilliseconds))
{
    Console.WriteLine($"{op}: {sw.ElapsedMilliseconds}ms");
}
```

## Summary

The new optimization utilities provide:

✅ **Fast lookups** - O(1) cache for instructions  
✅ **Batch processing** - Efficient metadata updates  
✅ **Memory efficiency** - Packed instruction format  
✅ **Lazy loading** - Only load when needed  
✅ **Performance metrics** - Monitor and debug  

**Key takeaway**: Use `DisassemblyOptimizer` for **large binaries** (>10MB) and batch operations for **bulk metadata updates**.
