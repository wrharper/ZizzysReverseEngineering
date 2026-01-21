# LLM Session Integration Guide

## Quick Start

### 1. Basic Session Usage

```csharp
// In AnalysisController or any place with access to _engine
var llmClient = new LocalLLMClient("localhost", 1234);
var analyzer = new LLMAnalyzer(llmClient);

// Create or retrieve session
var session = analyzer.GetOrCreateSession(_engine);

// Send query with full binary context
string response = await session.QueryAsync("Analyze the entry point function");
```

### 2. Session Persistence Across Operations

```csharp
// Session is created once and reused
var session = analyzer.GetOrCreateSession(_engine);

// Query 1: Learn about binary structure
var analysis1 = await session.QueryAsync("What are the main functions?");
lLMPane.DisplayText(analysis1);

// User edits binary
engine.HexBuffer.WriteByte(0x1000, 0x90);
session.UpdateContext();  // Notify AI of changes

// Query 2: Ask about impact
var analysis2 = await session.QueryAsync("What's the effect of that NOP?");
// AI has full context of both the function AND the patch
lLMPane.DisplayText(analysis2);
```

### 3. Integration with UI Controllers

#### AnalysisController
```csharp
public class AnalysisController
{
    private LLMAnalyzer? _llmAnalyzer;
    private LLMSession? _llmSession;
    
    public async Task QueryAIAsync(string question)
    {
        try
        {
            var llmClient = new LocalLLMClient("localhost", 1234);
            _llmAnalyzer ??= new LLMAnalyzer(llmClient);
            _llmSession ??= _llmAnalyzer.GetOrCreateSession(_engine);
            
            // Show "thinking..." status
            _llmPane.UpdateStatus("Querying AI...");
            
            // Send query with full context
            var response = await _llmSession.QueryAsync(question);
            
            // Display response
            _llmPane.DisplayText(response);
            _llmPane.UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            _llmPane.DisplayError($"Error: {ex.Message}");
        }
    }
}
```

#### HexEditorController (On Binary Change)
```csharp
public class HexEditorController
{
    private void OnByteChanged(object? sender, HexByteChangedEventArgs e)
    {
        // ... existing logic ...
        
        // Notify LLM session of change
        if (_llmSession != null)
        {
            _llmSession.UpdateContext();
            _llmPane.UpdateStatus("Context updated - AI ready for follow-up");
        }
    }
}
```

#### DisassemblyController (On Selection)
```csharp
public class DisassemblyController
{
    private async void OnInstructionSelectedAsync()
    {
        var selectedInstruction = GetSelectedInstruction();
        if (selectedInstruction == null) return;
        
        var query = $"Explain the instruction at 0x{selectedInstruction.Address:X}";
        
        // Use existing session with full binary context
        var llmClient = new LocalLLMClient("localhost", 1234);
        var analyzer = new LLMAnalyzer(llmClient);
        var session = analyzer.GetOrCreateSession(_engine);
        
        var explanation = await session.QueryAsync(query);
        _llmPane.DisplayText(explanation);
    }
}
```

---

## System Prompt Structure

When a query is sent, the full context is included as the system prompt:

```
SYSTEM: You are an expert reverse engineer analyzing a binary executable.
        [Complete binary metadata: functions, xrefs, symbols, strings, patterns]
        [Recent patches if any]

USER: What is the entry point doing?

ASSISTANT: Based on the analysis:
           - Entry point is _start (0x140001000)
           - Calls main() at 0x140002500
           - Sets up stack frame before calling into main
           - ...
```

The AI has full context to answer questions about:
- ✅ Specific functions and their callers/callees
- ✅ Cross-references and data dependencies
- ✅ API usage (imported functions)
- ✅ String references and their locations
- ✅ Pattern analysis results
- ✅ Recent patches applied
- ✅ Call chains (execution paths)

---

## Advanced Queries

### Ask About Specific Code Regions
```csharp
await session.QueryAsync("What's happening between 0x140001000 and 0x140001500?");
```
AI response includes functions in that range, their purposes, xrefs to/from that region.

### Ask About Data Flow
```csharp
await session.QueryAsync("How does data flow from main() to the encryption function?");
```
AI uses call chains and xrefs from context to explain the path.

### Ask About Vulnerabilities
```csharp
await session.QueryAsync("Where could I bypass the authentication check?");
```
AI suggests specific addresses where patches would be effective.

### Ask For Patch Suggestions
```csharp
await session.QueryAsync("How do I NOP out the license verification?");
```
AI provides:
- Address: `0x140002100`
- Current bytes: `E8 45 12 00 00` (CALL license_check)
- Patch bytes: `90 90 90 90 90` (5x NOP)

---

## Error Handling

```csharp
try
{
    var session = analyzer.GetOrCreateSession(_engine);
    var response = await session.QueryAsync(userQuery);
}
catch (HttpRequestException)
{
    MessageBox.Show("LM Studio not running. Start it on localhost:1234");
}
catch (TimeoutException)
{
    MessageBox.Show("AI response timed out. Try shorter queries.");
}
catch (Exception ex)
{
    MessageBox.Show($"Error: {ex.Message}");
}
```

---

## Session Lifecycle

```
┌─────────────────────────────────────┐
│  User opens binary file             │
│  CoreEngine.LoadFile()              │
│  CoreEngine.RunAnalysis()           │
└─────────────────┬───────────────────┘
                  ↓
        ┌─────────────────────┐
        │ First AI Query      │
        │ CreateSession()     │
        │ GenerateContext()   │
        │ GeneratePrompt()    │
        └─────────────────────┘
                  ↓
        ┌─────────────────────┐
        │ Session Created     │
        │ Context Stored      │
        │ Ready for queries   │
        └──────────┬──────────┘
                   │
         ┌─────────┴─────────┐
         ↓                   ↓
    User Query          Binary Modified
    (send query)        (UpdateContext)
         │                   │
         └─────────┬─────────┘
                   ↓
        ┌─────────────────────┐
        │ Send Query with     │
        │ Updated Context     │
        └─────────────────────┘
                   ↓
        ┌─────────────────────┐
        │ AI Response         │
        │ Display in LLMPane  │
        └─────────────────────┘
```

---

## Performance Tips

1. **First query will be slower** (~2-3 seconds) due to context generation
2. **Subsequent queries are faster** - context is cached
3. **On binary modification**, `UpdateContext()` takes ~30-50ms
4. **Keep queries focused** - AI works better with specific questions
5. **Reference addresses** - Always use `0xADDRESS` format

---

## Testing the Integration

```csharp
// Test harness
[Test]
public async Task TestLLMSessionWithBinaryContext()
{
    // Load test binary
    var engine = new CoreEngine();
    engine.LoadFile("test_binary.exe");
    engine.RunAnalysis();
    
    // Create session
    var analyzer = new LLMAnalyzer(new MockLLMClient());
    var session = analyzer.GetOrCreateSession(engine);
    
    // Verify context was generated
    var context = session.GetCurrentContext();
    Assert.True(context.TotalFunctions > 0);
    Assert.True(context.Functions.Count > 0);
    
    // Send test query
    var response = await session.QueryAsync("Test query");
    Assert.NotNull(response);
    Assert.True(response.Length > 0);
}
```

---

## UI Component Integration

### LLMPane Updates
```csharp
public class LLMPane : UserControl
{
    public void DisplayText(string response)
    {
        _resultBox.Text = response;
        _statusLabel.Text = "Ready";
    }
    
    public void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
    
    public void DisplayError(string error)
    {
        _resultBox.Text = $"[ERROR]\n{error}";
        _resultBox.ForeColor = Color.Red;
    }
}
```

### Query Input
Add a query textbox + Send button to LLMPane:
```csharp
private TextBox _queryBox = new();
private Button _sendButton = new();

private async void SendButton_Click(object? sender, EventArgs e)
{
    string query = _queryBox.Text;
    await QueryAIAsync(query);
}
```

---

## What the AI Sees

When you ask "What does the main function do?", the AI receives:

```
SYSTEM CONTEXT:
- Binary: program.exe (PE x64, 1.23 MB)
- Entry point: 0x140001000
- Main function: 0x140002500 (2048 bytes, 12 basic blocks)
- Called by: entry point
- Calls: [0x140003000, 0x140004500, 0x140005200, ...]
- Xrefs: 12 references to this function
- Imported APIs used: CreateFileA, WriteFile, ReadFile, malloc, free
- Strings in this region: "Error: ...", "Processing...", "Done"
- Patterns: None specific to this function

USER QUERY:
"What does the main function do?"

AI RESPONSE:
Based on the analysis context:
- main() is at 0x140002500
- Sets up stack frame and local variables
- Makes calls to CreateFileA (likely opens a file)
- Uses WriteFile/ReadFile for I/O
- Calls utility functions at 0x140003000, 0x140004500
- Error strings suggest error handling
- Appears to be: Read file → Process → Write output
```

