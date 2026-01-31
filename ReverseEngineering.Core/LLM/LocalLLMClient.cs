using ReverseEngineering.Core.ProjectSystem;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Client for LM Studio HTTP API (default localhost:1234)
    /// Handles completions, streaming, and error handling
    /// </summary>
    public class LocalLLMClient
    {
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private readonly int _timeoutSeconds;

        public string Model { get; set; } = "local-model"; // Set by user/config
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        
        // Token management
        private int _maxContextTokens = 4096;  // Default fallback
        private int _currentTokenUsage = 0;

        public LocalLLMClient(string baseUrl = "http://127.0.0.1:1234", int timeoutSeconds = 30000)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _timeoutSeconds = timeoutSeconds;
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        /// <summary>
        /// Check if LM Studio is running and accessible
        /// </summary>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get available models and debug info
        /// </summary>
        public async Task<string> GetDebugInfoAsync()
        {
            var sb = new StringBuilder();
            try
            {
                // Check /v1/models endpoint
                sb.AppendLine("=== /v1/models ===");
                var modelsResponse = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
                var modelsJson = await modelsResponse.Content.ReadAsStringAsync();
                sb.AppendLine(modelsJson);
                
                // Try to check if there's a config endpoint
                sb.AppendLine("\n=== Testing other endpoints ===");
                try
                {
                    var configResponse = await _httpClient.GetAsync($"{_baseUrl}/api/config");
                    if (configResponse.IsSuccessStatusCode)
                    {
                        var configJson = await configResponse.Content.ReadAsStringAsync();
                        sb.AppendLine("/api/config:\n" + configJson);
                    }
                }
                catch { sb.AppendLine("/api/config: Not available"); }
                
                // Try to check system prompt or other endpoints
                try
                {
                    var systemResponse = await _httpClient.GetAsync($"{_baseUrl}/v1/config");
                    if (systemResponse.IsSuccessStatusCode)
                    {
                        var systemJson = await systemResponse.Content.ReadAsStringAsync();
                        sb.AppendLine("/v1/config:\n" + systemJson);
                    }
                }
                catch { sb.AppendLine("/v1/config: Not available"); }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error: {ex.Message}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Get list of available models
        /// </summary>
        public async Task<string[]> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
                var json = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(json);
                var models = new System.Collections.Generic.List<string>();
                
                foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    models.Add(model.GetProperty("id").GetString() ?? "unknown");
                }
                
                return models.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
        /// <summary>
        /// Get context length for the currently loaded model
        /// Queries the LM Studio /api/v1/models endpoint for context info
        /// </summary>
        public async Task<int> GetModelContextLengthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
                var json = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(json);
                var dataArray = doc.RootElement.GetProperty("data");
                
                // Get first available model (LM Studio usually has just one loaded at a time)
                foreach (var model in dataArray.EnumerateArray())
                {
                    var id = model.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        // Update Model property with actual loaded model
                        Model = id;
                        Logger.Info("LLM", $"Found loaded model: {id}");
                        
                        // Try to get context from /api/v1/models endpoint
                        if (await TryGetContextFromDetailedModelsEndpoint(id))
                            return _maxContextTokens;
                        
                        Logger.Warning("LLM", $"Could not find context window for model '{id}'. Using default: {_maxContextTokens} tokens");
                        return _maxContextTokens;
                    }
                }
                
                Logger.Warning("LLM", "No models found in /v1/models response");
                return _maxContextTokens;
            }
            catch (Exception ex)
            {
                Logger.Error("LLM", $"Error getting model context length: {ex.Message}", ex);
                return _maxContextTokens;
            }
        }

        /// <summary>
        /// Try to get context from /api/v1/models endpoint
        /// </summary>
        private async Task<bool> TryGetContextFromDetailedModelsEndpoint(string modelId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/models");
                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                Logger.Info("LLM", $"[/api/v1/models Response]\n{json}");
                
                using var doc = JsonDocument.Parse(json);
                
                // Try to find the model in the response
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var modelObj in modelsArray.EnumerateArray())
                    {
                        // Look for matching model by key
                        if (modelObj.TryGetProperty("key", out var keyEl) && keyEl.GetString() == modelId)
                        {
                            // Check for max_context_length (standard field)
                            if (modelObj.TryGetProperty("max_context_length", out var maxCtx))
                            {
                                _maxContextTokens = maxCtx.GetInt32();
                                Logger.Info("LLM", $"Found max_context_length from /api/v1/models: {_maxContextTokens} tokens");
                                return true;
                            }
                            
                            // Check in loaded_instances config (fallback)
                            if (modelObj.TryGetProperty("loaded_instances", out var instances))
                            {
                                foreach (var instance in instances.EnumerateArray())
                                {
                                    if (instance.TryGetProperty("config", out var config) &&
                                        config.TryGetProperty("context_length", out var ctxLen))
                                    {
                                        _maxContextTokens = ctxLen.GetInt32();
                                        Logger.Info("LLM", $"Found context_length in loaded_instances config: {_maxContextTokens} tokens");
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                
                Logger.Warning("LLM", $"Model '{modelId}' not found in /api/v1/models response or missing context info");
            }
            catch (Exception ex)
            {
                Logger.Debug("LLM", $"/api/v1/models endpoint error: {ex.Message}");
            }
            return false;
        }

        
        /// <summary>
        /// Get current context usage as percentage
        /// </summary>
        public float GetContextUsagePercent()
        {
            if (_maxContextTokens <= 0)
                return 0f;
            return (_currentTokenUsage / (float)_maxContextTokens) * 100f;
        }

        /// <summary>
        /// Get current token usage
        /// </summary>
        public int GetCurrentTokens() => _currentTokenUsage;

        /// <summary>
        /// Get max context tokens
        /// </summary>
        public int GetMaxTokens() => _maxContextTokens;

        /// <summary>
        /// Estimate tokens in text (rough: 1 token ≈ 4 chars)
        /// </summary>
        private int EstimateTokens(string text)
        {
            return (text?.Length ?? 0) / 4;
        }

        /// <summary>
        /// Send a completion request to LM Studio
        /// </summary>
        public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                // Track token usage
                int promptTokens = EstimateTokens(prompt);
                _currentTokenUsage = promptTokens;

                // Reserve 20% of context for output
                int maxOutput = _maxContextTokens / 5;
                int maxInput = _maxContextTokens - maxOutput;

                // Truncate prompt if exceeds limit
                if (promptTokens > maxInput)
                {
                    int safeChars = maxInput * 4;
                    prompt = prompt.Substring(0, Math.Min(safeChars, prompt.Length));
                    promptTokens = maxInput;
                    _currentTokenUsage = promptTokens;
                }

                // Warn if approaching context limit
                if (GetContextUsagePercent() > 80f)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WARNING] Context usage at {GetContextUsagePercent():F1}% " +
                        $"({_currentTokenUsage}/{_maxContextTokens} tokens)");
                }

                var requestBody = new
                {
                    model = Model,
                    prompt = prompt,
                    max_tokens = int.MaxValue,
                    temperature = Temperature,
                    top_p = TopP,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/v1/completions",
                    content,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error: HTTP {response.StatusCode}";
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseJson);
                
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var text = choices[0].GetProperty("text").GetString();
                    return text?.Trim() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                return $"Timeout after {_timeoutSeconds}s";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Send a chat message request (OpenAI-compatible)
        /// </summary>
        public async Task<string> ChatAsync(string message, string systemPrompt = "", CancellationToken cancellationToken = default)
        {
            try
            {
                var messages = new System.Collections.Generic.List<object>();
                
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new { role = "system", content = systemPrompt });
                }
                
                messages.Add(new { role = "user", content = message });

                var requestBody = new
                {
                    model = Model,
                    messages = messages,
                    max_tokens = int.MaxValue,
                    temperature = Temperature,
                    top_p = TopP,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/v1/chat/completions",
                    content,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error: HTTP {response.StatusCode}";
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseJson);
                
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var text = choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                    return text?.Trim() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                return $"Timeout after {_timeoutSeconds}s";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Stream chat response with callbacks for each chunk
        /// </summary>
        public async Task StreamChatAsync(string message, string systemPrompt, Action<string> onChunkReceived, CancellationToken cancellationToken = default)
        {
            try
            {
                // Log prompt sizes to verify system prompt is being sent
                Logger.Info("LLM", $"StreamChatAsync - User message: {message.Length} chars, System prompt: {systemPrompt?.Length ?? 0} chars");
                
                // Log first 300 chars of system prompt to see what's being sent
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    var preview = systemPrompt.Length > 300 ? systemPrompt.Substring(0, 300) + "..." : systemPrompt;
                    Logger.Info("LLM", $"System prompt preview: {preview.Replace("\n", " | ")}");
                }

                var messages = new System.Collections.Generic.List<object>();
                
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new { role = "system", content = systemPrompt });
                }
                
                messages.Add(new { role = "user", content = message });

                var requestBody = new
                {
                    model = Model,
                    messages = messages,
                    max_tokens = int.MaxValue,
                    temperature = Temperature,
                    top_p = TopP,
                    stream = true  // Always stream for this method
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Logger.Info("LLM", $"StreamChatAsync - JSON request body: {json.Length} bytes");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/v1/chat/completions",
                    content,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Info("LLM", $"HTTP Error: {response.StatusCode}");
                    onChunkReceived?.Invoke($"Error: HTTP {response.StatusCode}");
                    return;
                }

                Logger.Info("LLM", "HTTP response received, starting to read streaming response...");

                // Read streaming response line by line
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new System.IO.StreamReader(stream))
                {
                    Logger.Info("LLM", "Stream opened, reading lines...");
                    string? line;
                    int lineCount = 0;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lineCount++;
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Skip empty lines and [DONE] marker
                        if (string.IsNullOrWhiteSpace(line) || line == "data: [DONE]")
                            continue;

                        // Remove "data: " prefix
                        if (line.StartsWith("data: "))
                            line = line.Substring(6);

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var choices = doc.RootElement.GetProperty("choices");
                            
                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var contentProp))
                                {
                                    var content_text = contentProp.GetString();
                                    if (!string.IsNullOrEmpty(content_text))
                                    {
                                        Logger.Info("LLM", $"Chunk received ({content_text.Length} chars)");
                                        onChunkReceived?.Invoke(content_text);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Info("LLM", $"Error parsing line {lineCount}: {ex.Message}");
                        }
                    }
                    Logger.Info("LLM", $"Stream ended. Total lines read: {lineCount}");
                }
            }
            catch (TaskCanceledException)
            {
                onChunkReceived?.Invoke($"\n[Timeout after {_timeoutSeconds}s]");
            }
            catch (Exception ex)
            {
                onChunkReceived?.Invoke($"\n[Error: {ex.Message}]");
            }
        }

        /// <summary>
        /// Get token count for text (using rough estimation)
        /// </summary>
        public int EstimateTokenCount(string text)
        {
            // Rough estimate: ~4 characters per token
            return (text?.Length ?? 0) / 4 + 1;
        }

        /// <summary>
        /// Apply settings from SettingsManager (call after loading settings)
        /// </summary>
        public void ApplySettingsFromManager()
        {
            try
            {
                var settings = ProjectSystem.SettingsManager.Current;
                Model = settings.LMStudio.ModelName ?? "neural-chat";
                Temperature = (float)settings.LMStudio.Temperature;
                TopP = 0.9f; // Not exposed in settings yet
            }
            catch
            {
                // Silently ignore if SettingsManager not available
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
