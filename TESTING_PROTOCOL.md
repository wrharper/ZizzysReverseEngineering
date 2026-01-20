# LM Studio Integration Testing Protocol

## Core Testing Rule

**NEVER use timeouts, pagination heads/tails, or any length shorteners when testing the LM Studio client.**

### Why This Rule Exists

Local LLMs are fundamentally different from cloud-based services:

1. **Speed is Unpredictable**: A response that takes 2 seconds locally might take 60 seconds on a slower machine
2. **Output Length is Unknown**: Some prompts generate 500 tokens, others 5000+
3. **Truncation Hides Failures**: If a response is cut off, you won't see:
   - Why the LLM failed to answer
   - Partial/incomplete reasoning
   - Diagnostic information in response tails
4. **Timeout Masks Performance Issues**: A timeout doesn't indicate failure—it indicates your timeout was too short

### Implications for Testing

- **No timeouts during testing**: Use `RequestTimeoutSeconds = 300` (5 min) minimum
- **Capture full output**: Log entire responses (use `StringBuilder` or `List<string>`)
- **No pagination**: Don't show "first 500 chars" or "1-100 of 500 lines"
- **No length limiters**: Don't truncate results in UI or logs
- **Stream everything**: If streaming, collect all chunks before testing completion

---

## Testing Categories

### 1. Connection Testing

**Goal**: Verify LocalLLMClient can reach LM Studio

```csharp
// Test: IsHealthyAsync() with no timeout
var client = new LocalLLMClient("localhost", 1234, "neural-chat");
bool healthy = await client.IsHealthyAsync();

// Expected: Returns true if LM Studio is running, false otherwise
// Do NOT use timeout—wait full duration
```

**Manual Step**: 
1. Ensure LM Studio is running (`localhost:1234`)
2. Call `IsHealthyAsync()` 
3. Verify response (true/false)

---

### 2. Model Query Testing

**Goal**: Verify `GetAvailableModelsAsync()` returns full list

```csharp
var client = new LocalLLMClient("localhost", 1234);
var models = await client.GetAvailableModelsAsync(); // No timeout!

// Expected: Returns List<string> with all loaded models
// Example: ["neural-chat", "mistral", "llama2"]
```

**Manual Step**:
1. Run in debug console
2. Log **entire list** (all model names)
3. Verify count matches LM Studio UI

---

### 3. Simple Completion Testing

**Goal**: Test basic text generation without truncation

```csharp
var client = new LocalLLMClient("localhost", 1234, "neural-chat");
client.Temperature = 0.7;
client.MaxTokens = 2048;
client.RequestTimeoutSeconds = 300; // 5 minutes

var prompt = "What is a reverse engineering technique?";
var response = await client.CompleteAsync(prompt);

// CAPTURE FULL RESPONSE
// Log: response.ToString().Length + "\n" + response
```

**Expected Output**:
- Full paragraph (500-2000 chars)
- Complete sentences
- No truncation marks

**Manual Step**:
1. Paste prompt above into test code
2. Run without timeout
3. Log complete response to file
4. Verify no truncation in response

---

### 4. Instruction Explanation Testing

**Goal**: Test `LLMAnalyzer.ExplainInstructionAsync()` with full output

```csharp
var analyzer = new LLMAnalyzer(client);

// Real disassembly example
var instruction = new Instruction
{
    Address = 0x401000,
    Bytes = [0x55, 0x48, 0x89, 0xE5],
    Mnemonic = "PUSH RBP; MOV RBP, RSP"
};

string explanation = await analyzer.ExplainInstructionAsync(instruction);

// NEVER truncate this output!
// File.WriteAllText("explanation.txt", explanation);
```

**Expected Output**:
- Multi-sentence explanation
- 500-1000+ characters
- Includes what the instruction does + why
- Explanation of registers involved

**Manual Step**:
1. Replace instruction with real x64 code from loaded binary
2. Call `ExplainInstructionAsync()`
3. Save **entire response** to file
4. Verify completeness

---

### 5. Pseudocode Generation Testing

**Goal**: Test `GeneratePseudocodeAsync()` with full output

```csharp
var analyzer = new LLMAnalyzer(client);

var instructions = new List<Instruction>
{
    new() { Mnemonic = "PUSH RBP", Bytes = [0x55] },
    new() { Mnemonic = "MOV RBP, RSP", Bytes = [0x48, 0x89, 0xE5] },
    new() { Mnemonic = "SUB RSP, 0x20", Bytes = [0x48, 0x83, 0xEC, 0x20] },
    new() { Mnemonic = "MOV RAX, [RBP+0x10]", Bytes = [0x48, 0x8B, 0x45, 0x10] },
    new() { Mnemonic = "RET", Bytes = [0xC3] }
};

string pseudocode = await analyzer.GeneratePseudocodeAsync(instructions);

// FULL OUTPUT - NO TRUNCATION
```

**Expected Output**:
- Multi-line pseudocode
- Variable declarations
- Function body
- 1000-3000+ chars typical

---

### 6. Pattern Detection Testing

**Goal**: Test `DetectPatternAsync()` with full analysis

```csharp
var analyzer = new LLMAnalyzer(client);

string pattern = "55 48 89 E5 48 83 EC ?? A1 ?? ?? ?? ?? 85 C0 74 ?? C3";
string analysis = await analyzer.DetectPatternAsync(pattern);

// Log entire analysis
```

**Expected Output**:
- Pattern identification (e.g., "x64 function prologue")
- What each bytes does
- Confidence level
- Examples of where this pattern is common

---

### 7. Streaming Response Testing

**Goal**: Verify streaming captures ALL chunks

```csharp
var client = new LocalLLMClient("localhost", 1234, "neural-chat")
{
    EnableStreaming = true,
    RequestTimeoutSeconds = 300
};

var sb = new StringBuilder();
await client.CompleteAsync("Explain x86-64 calling conventions", onChunk: chunk =>
{
    sb.Append(chunk);
});

string fullResponse = sb.ToString();
// Log complete response
```

**Verify**:
- All chunks captured
- No truncation at response boundaries
- Response is coherent
- Final response length logged

---

## Manual Testing Workflow

### Pre-Test Checklist

- [ ] LM Studio running on `localhost:1234`
- [ ] At least one model loaded (verify in LM Studio UI)
- [ ] Network connectivity (ping localhost:1234)
- [ ] No timeout-related code in test
- [ ] Log file ready to capture output

### Running a Test

1. **Open Visual Studio debugger**
2. **Set breakpoint after LLM call**
3. **Step through without timeout interruption**
4. **Inspect variable in debugger** (copy full value)
5. **Log to file** (don't rely on console truncation)
6. **Verify output completeness**

### Example Debug Session

```csharp
// In AnalysisController or test class
[DebuggerNonUserCode]
public async Task TestExplainInstructionManually()
{
    var client = new LocalLLMClient("localhost", 1234, "neural-chat");
    client.RequestTimeoutSeconds = 300; // 5 minutes
    
    var instruction = new Instruction 
    { 
        Mnemonic = "MOV RAX, RBX",
        Bytes = [0x48, 0x89, 0xD8]
    };
    
    var analyzer = new LLMAnalyzer(client);
    
    // BREAKPOINT HERE
    string result = await analyzer.ExplainInstructionAsync(instruction);
    
    // INSPECT 'result' in debugger
    // Copy full text to file
    File.WriteAllText("test_result.txt", result);
}
```

---

## Performance Expectations

| Operation | Typical Duration | Max Duration | Notes |
|-----------|------------------|--------------|-------|
| IsHealthyAsync() | 50-200ms | 2s | Very fast |
| CompleteAsync() (short) | 5-30s | 120s | Depends on model/prompt |
| CompleteAsync() (long) | 30-180s | 300s+ | Full output, no truncation |
| ExplainInstruction | 10-60s | 120s | 1-2 instructions |
| GeneratePseudocode (5 inst) | 20-120s | 180s | May generate large code block |
| DetectPattern (regex) | 15-60s | 120s | Pattern analysis |

**Important**: These are **guidelines**, not limits. A slow machine might take 2-3x longer.

---

## Debugging Failed Responses

### If Response is Empty

1. Check LM Studio console (is model processing?)
2. Verify model is loaded
3. Check `client.Temperature` (0.0 = deterministic but can fail)
4. Increase `MaxTokens` slightly
5. Retry without timeout

### If Response is Truncated

1. **NEVER blame truncation on timeout**—check actual response length
2. Review LM Studio output generation (is it stopping early?)
3. Check if response ends with incomplete sentence
4. Try different prompt
5. Check `client.MaxTokens` setting (may need to increase)

### If Test Times Out

1. **Timeout is NOT a failure indicator**—extend timeout duration
2. Example: Change 60s timeout to 300s
3. Log that operation took >60s but verify it succeeds eventually
4. Consider performance of host machine

---

## CI/CD Testing Guidelines

If integrating into automated tests:

1. **Use extremely long timeouts**: 600 seconds minimum
2. **Capture full output**: No string truncation
3. **Log response times**: Document performance baselines
4. **Don't fail on slow responses**: Only fail on actual errors
5. **Mock for fast tests**: Use canned responses for speed
6. **Real LM Studio for integration tests**: Use full client for comprehensive testing

Example:

```csharp
[Test]
[Timeout(600000)] // 10 minutes - NO timeout during actual LLM call
public async Task LLMAnalyzer_ExplainInstruction_ReturnsNonEmpty()
{
    var client = new LocalLLMClient("localhost", 1234, "neural-chat");
    var analyzer = new LLMAnalyzer(client);
    
    var instruction = new Instruction { Mnemonic = "RET", Bytes = [0xC3] };
    
    string result = await analyzer.ExplainInstructionAsync(instruction);
    
    // Assert full response, not truncated
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.GreaterThan(50)); // Not empty
    Assert.That(result, Contains.Substring("return")); // Semantic check
}
```

---

## Documentation & Logs

### Saving Test Results

```csharp
// Always log full response
var results = new StringBuilder();
results.AppendLine($"Test: ExplainInstruction");
results.AppendLine($"Time: {DateTime.Now:O}");
results.AppendLine($"Instruction: {instruction.Mnemonic}");
results.AppendLine($"Response Length: {explanation.Length} chars");
results.AppendLine("---");
results.AppendLine(explanation);

File.WriteAllText("test_results.txt", results.ToString());
```

### Shared Test Data

Store test binaries and expected outputs in:
- `/TestData/Binaries/` - Sample .exe/.dll files
- `/TestData/Results/` - Expected LLM analysis output

---

## Summary: The Hard Rule

**During LM Studio testing:**

✅ **DO**:
- Use long timeouts (5+ minutes)
- Capture and log full output
- Test without truncation
- Wait for complete response

❌ **DON'T**:
- Use short timeouts (<60s)
- Truncate output in logs/UI
- Use `.Substring()`, `.Take()`, or `.TrimEnd()`
- Show "first N chars" in debugging
- Assume timeout = failure

**Result**: Full diagnostic data for debugging LLM behavior, performance metrics, and quality assessment.
