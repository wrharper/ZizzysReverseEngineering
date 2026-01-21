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
        /// Send a completion request to LM Studio
        /// </summary>
        public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
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
