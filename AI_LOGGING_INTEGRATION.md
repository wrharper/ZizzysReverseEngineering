# AI Logging System Integration Guide

## Overview

The AI Logging System provides comprehensive audit trails for all AI operations (LLM analysis, instruction explanations, pseudocode generation, pattern detection). This guide covers how to integrate logging into existing code.

---

## Core Components

### 1. AILogsManager (Core/AILogs/AILogsManager.cs)
**Responsibility**: Persist and retrieve AI operation logs to organized folder structure

```
AILogs/
├── AssemblyEdit/
│   ├── 2025-01-14/
│   │   ├── 140000_123abc.json
│   │   └── 140015_456def.json
├── InstructionExplanation/
│   ├── 2025-01-14/
│   │   └── 140030_789xyz.json
└── PseudocodeGeneration/
    └── 2025-01-14/
        └── 140045_aabbcc.json
```

**Key Methods**:
- `SaveLogEntry(AILogEntry)` - Persist a log
- `GetLogsByOperation(string operation)` - Retrieve all logs for operation type
- `ClearAllLogs()` / `ClearOperationLogs(string operation)` - Cleanup
- `GetStatistics()` - Return summary stats

### 2. AILogEntry (Core/AILogs/AILogsManager.cs)
**Responsibility**: Represent a single AI operation

```csharp
public class AILogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
    public string Operation { get; set; } = "";           // "AssemblyEdit", "Explanation", etc.
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Prompt { get; set; } = "";              // What we sent to AI
    public string AIOutput { get; set; } = "";            // What AI returned
    public string Status { get; set; } = "Success";       // Success, Error, Pending
    public long DurationMs { get; set; } = 0;             // Milliseconds
    public List<ByteChange> Changes { get; set; } = [];   // Binary modifications
}
```

### 3. ByteChange (Core/AILogs/AILogsManager.cs)
**Responsibility**: Track individual byte modifications from AI operations

```csharp
public class ByteChange
{
    public ulong Offset { get; set; }                      // Where in binary
    public byte OriginalByte { get; set; }                 // What was there
    public byte NewByte { get; set; }                      // What changed to
    public string AssemblyBefore { get; set; } = "";       // Iced disassembly before
    public string AssemblyAfter { get; set; } = "";        // Keystone reassembly after
}
```

---

## Integration Patterns

### Pattern 1: Assembly Editing (Keystone Operation)

**Location**: DisassemblyController.OnLineEdited()

```csharp
private async void OnLineEdited(int lineIndex)
{
    var asmText = _asmControl.GetLineText(lineIndex);
    var address = _asmControl.GetLineAddress(lineIndex);
    
    var timer = Stopwatch.StartNew();
    
    try
    {
        // Assemble new bytes
        var newBytes = await Task.Run(() =>
            KeystoneAssembler.Assemble(asmText, address, is64Bit: true));
        
        timer.Stop();
        
        if (newBytes.Length == 0)
            throw new Exception("Keystone assembly failed");
        
        // Get original instruction
        var origInstr = _core.GetInstructionAt(address);
        
        // Create log entry
        var logEntry = new AILogEntry
        {
            Operation = "AssemblyEdit",
            Prompt = $"Assemble: {asmText} at {address:X8}",
            AIOutput = $"Generated {newBytes.Length} bytes",
            Status = "Success",
            DurationMs = timer.ElapsedMilliseconds
        };
        
        // Track each byte change
        for (int i = 0; i < newBytes.Length; i++)
        {
            if (i < origInstr.Bytes.Length)
            {
                var origByte = origInstr.Bytes[i];
                if (origByte != newBytes[i])
                {
                    logEntry.Changes.Add(new ByteChange
                    {
                        Offset = address + (ulong)i,
                        OriginalByte = origByte,
                        NewByte = newBytes[i],
                        AssemblyBefore = origInstr.Mnemonic + " " + origInstr.Operands,
                        AssemblyAfter = asmText
                    });
                }
            }
        }
        
        // Save log
        _aiLogsManager.SaveLogEntry(logEntry);
        
        // Apply change
        _hex.WriteBytes(address, newBytes);
        _core.RebuildInstructionAtOffset(address);
    }
    catch (Exception ex)
    {
        // Log failure
        var errorEntry = new AILogEntry
        {
            Operation = "AssemblyEdit",
            Prompt = $"Assemble: {asmText}",
            AIOutput = $"Error: {ex.Message}",
            Status = "Error",
            DurationMs = timer.ElapsedMilliseconds
        };
        _aiLogsManager.SaveLogEntry(errorEntry);
        
        MessageBox.Show($"Assembly failed: {ex.Message}");
    }
}
```

### Pattern 2: LLM Analysis Operation

**Location**: AnalysisController.ExplainInstructionAsync()

```csharp
public async Task ExplainInstructionAsync(int instructionIndex)
{
    var instr = _core.Disassembly[instructionIndex];
    var timer = Stopwatch.StartNew();
    
    try
    {
        // Create prompt for LLM
        var prompt = $"""
            Explain this x64 instruction:
            Address: {instr.Address:X8}
            Bytes: {string.Join(" ", instr.Bytes.Select(b => b.ToString("X2")))}
            Assembly: {instr.Mnemonic} {instr.Operands}
            """;
        
        // Call LLM (LM Studio via LocalLLMClient)
        var client = new LocalLLMClient(
            host: SettingsManager.Current.LMStudio.Host,
            port: SettingsManager.Current.LMStudio.Port
        );
        
        var response = await client.ExplainInstructionAsync(prompt);
        timer.Stop();
        
        // Create log entry
        var logEntry = new AILogEntry
        {
            Operation = "InstructionExplanation",
            Prompt = prompt,
            AIOutput = response,
            Status = "Success",
            DurationMs = timer.ElapsedMilliseconds
        };
        
        // Save to logs
        _aiLogsManager.SaveLogEntry(logEntry);
        
        // Display in UI
        _analysisPane.ShowExplanation(instr.Address, response);
    }
    catch (Exception ex)
    {
        timer.Stop();
        
        var errorEntry = new AILogEntry
        {
            Operation = "InstructionExplanation",
            Prompt = $"Explain instruction at {instr.Address:X8}",
            AIOutput = $"Error: {ex.Message}",
            Status = "Error",
            DurationMs = timer.ElapsedMilliseconds
        };
        
        _aiLogsManager.SaveLogEntry(errorEntry);
        MessageBox.Show($"Explanation failed: {ex.Message}");
    }
}
```

### Pattern 3: Async Streaming with Logging

**For long-running AI operations that stream results**:

```csharp
public async Task GeneratePseudocodeAsync(ulong functionAddress)
{
    var func = _core.FindFunctionAtAddress(functionAddress);
    if (func == null) return;
    
    var logEntry = new AILogEntry
    {
        Operation = "PseudocodeGeneration",
        Prompt = $"Generate pseudocode for {func.Name} (0x{functionAddress:X8})",
        Status = "Pending",
        Timestamp = DateTime.Now
    };
    
    var timer = Stopwatch.StartNew();
    var outputBuilder = new StringBuilder();
    
    try
    {
        var client = new LocalLLMClient(
            SettingsManager.Current.LMStudio.Host,
            SettingsManager.Current.LMStudio.Port
        );
        
        // Stream response with cancellation token
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        await foreach (var chunk in client.GeneratePseudocodeStreamAsync(
            functionAddress,
            func.GetInstructions(),
            cts.Token))
        {
            outputBuilder.Append(chunk);
            
            // Update UI in real-time
            _analysisPane.AppendPseudocode(chunk);
            Application.Current.Dispatcher.Invoke(() => { });  // Yield to UI thread
        }
        
        timer.Stop();
        
        logEntry.Status = "Success";
        logEntry.AIOutput = outputBuilder.ToString();
        logEntry.DurationMs = timer.ElapsedMilliseconds;
    }
    catch (OperationCanceledException)
    {
        logEntry.Status = "Timeout";
        logEntry.AIOutput = outputBuilder.ToString();  // Partial result
        logEntry.DurationMs = timer.ElapsedMilliseconds;
    }
    catch (Exception ex)
    {
        logEntry.Status = "Error";
        logEntry.AIOutput = $"Error: {ex.Message}";
        logEntry.DurationMs = timer.ElapsedMilliseconds;
    }
    finally
    {
        // Always save log (even partial/error)
        _aiLogsManager.SaveLogEntry(logEntry);
    }
}
```

### Pattern 4: Batch Operations

**For pattern matching or multi-instruction analysis**:

```csharp
public async Task FindPatternsAsync(string pattern)
{
    var logEntry = new AILogEntry
    {
        Operation = "PatternSearch",
        Prompt = $"Find pattern: {pattern}",
        Status = "In Progress"
    };
    
    var timer = Stopwatch.StartNew();
    var matches = new List<(ulong address, string context)>();
    
    try
    {
        // Use Iced to find pattern matches
        var results = _core.FindBytePattern(pattern);
        
        foreach (var (address, _) in results)
        {
            var instr = _core.GetInstructionAt(address);
            matches.Add((address, $"{instr.Mnemonic} {instr.Operands}"));
            
            // Optionally ask LLM about pattern
            if (SettingsManager.Current.Analysis.EnablPatternLLMAnalysis)
            {
                var client = new LocalLLMClient(...);
                var analysis = await client.AnalyzePatternAsync(instr);
                // Track analysis in changes or as sub-entry
            }
        }
        
        timer.Stop();
        
        logEntry.Status = "Success";
        logEntry.AIOutput = $"Found {matches.Count} matches:\n" +
            string.Join("\n", matches.Select(m => $"  0x{m.address:X8}: {m.context}"));
        logEntry.DurationMs = timer.ElapsedMilliseconds;
    }
    catch (Exception ex)
    {
        logEntry.Status = "Error";
        logEntry.AIOutput = $"Error: {ex.Message}";
        logEntry.DurationMs = timer.ElapsedMilliseconds;
    }
    finally
    {
        _aiLogsManager.SaveLogEntry(logEntry);
    }
}
```

---

## Viewing and Managing Logs

### UI Access
1. **Tools** → **AI** → **View Logs...**
2. Select operation type from dropdown
3. View organized logs by date
4. Click a log to see details in 3 tabs:
   - **Prompt**: What was sent to AI/system
   - **Output**: AI response or result
   - **Changes**: Byte modifications with before/after assembly

### Export
- **Export Report** button saves all logs as formatted text
- Default location: Current working directory
- Filename: `compatibility_report_YYYYMMDD_HHmmss.txt`

### Clear
- **Clear All Logs** removes all logs (confirmation dialog)
- Logs stored in `AILogs/` folder (relocatable)

---

## Best Practices

### 1. Always Use Try/Catch
```csharp
try
{
    // AI operation
}
catch (Exception ex)
{
    var errorEntry = new AILogEntry
    {
        Status = "Error",
        AIOutput = $"Error: {ex.Message}"
    };
    _aiLogsManager.SaveLogEntry(errorEntry);
    // Handle error
}
finally
{
    // Always save, even on error
}
```

### 2. Track Duration
```csharp
var timer = Stopwatch.StartNew();
// ... operation ...
timer.Stop();
logEntry.DurationMs = timer.ElapsedMilliseconds;
```

### 3. Use Descriptive Operation Names
- `AssemblyEdit` - User edited assembly line
- `InstructionExplanation` - LLM explained instruction
- `PseudocodeGeneration` - LLM generated pseudocode
- `PatternSearch` - Searched for byte pattern
- `XrefAnalysis` - Built cross-reference data

### 4. Populate Changes for Auditing
```csharp
for (int i = 0; i < newBytes.Length; i++)
{
    if (newBytes[i] != originalBytes[i])
    {
        logEntry.Changes.Add(new ByteChange
        {
            Offset = address + (ulong)i,
            OriginalByte = originalBytes[i],
            NewByte = newBytes[i],
            AssemblyBefore = oldAsm,
            AssemblyAfter = newAsm
        });
    }
}
```

### 5. Test with Compatibility Tests
Run **Tools** → **Compatibility Tests** to verify:
- ✅ AI Logging system works
- ✅ ByteChange tracking captures correctly
- ✅ Logs persist to disk
- ✅ Round-trip (log → retrieve) works

---

## Performance Considerations

### Logging Overhead
- **AILogsManager**: ~5-10ms per log save (async write to disk)
- **JSON serialization**: <2ms per entry
- **Folder creation**: Lazy (only on first save per operation/date)

### Mitigation Strategies
1. **Batch Operations**: Group related changes into single log entry
2. **Async Writes**: Use `Task.Run()` for disk I/O
3. **Lazy Folder Creation**: AILogsManager creates folders on-demand

```csharp
// Good: Single entry for batch
var entry = new AILogEntry { Operation = "BatchAssembly" };
foreach (var (addr, asm) in changes)
{
    // Accumulate changes
    entry.Changes.Add(...);
}
_aiLogsManager.SaveLogEntry(entry);  // One disk write

// Avoid: Separate entry per change
foreach (var (addr, asm) in changes)
{
    _aiLogsManager.SaveLogEntry(new AILogEntry { ... });  // N disk writes
}
```

---

## Integration Checklist

- [ ] Import `using ReverseEngineering.Core.AILogs;`
- [ ] Inject `AILogsManager` into controller
- [ ] Wrap AI/Keystone operations in try/catch
- [ ] Create `AILogEntry` with operation name, prompt, output
- [ ] Track `ByteChange` for modified bytes
- [ ] Record duration with `Stopwatch`
- [ ] Call `_aiLogsManager.SaveLogEntry(entry)`
- [ ] Test with Compatibility Tests dialog
- [ ] Verify logs appear in **Tools** → **View Logs...**
- [ ] Export and review report

---

## Example: Complete Integration

```csharp
public class MyAnalysisController
{
    private readonly AILogsManager _aiLogs;
    private readonly CoreEngine _core;
    
    public MyAnalysisController(CoreEngine core, AILogsManager aiLogs)
    {
        _core = core;
        _aiLogs = aiLogs;
    }
    
    public async Task AnalyzeInstructionAsync(ulong address)
    {
        var entry = new AILogEntry
        {
            Operation = "CustomAnalysis",
            Timestamp = DateTime.Now
        };
        
        var timer = Stopwatch.StartNew();
        
        try
        {
            var instr = _core.GetInstructionAt(address);
            entry.Prompt = $"Analyze: {instr.Mnemonic} {instr.Operands} @ 0x{address:X8}";
            
            var result = await MyLLM.AnalyzeAsync(instr);
            entry.AIOutput = result;
            entry.Status = "Success";
        }
        catch (Exception ex)
        {
            entry.AIOutput = $"Error: {ex.Message}";
            entry.Status = "Error";
        }
        finally
        {
            entry.DurationMs = timer.ElapsedMilliseconds;
            _aiLogs.SaveLogEntry(entry);
        }
    }
}
```

Done! Logs are now automatically tracked and viewable through the UI.

