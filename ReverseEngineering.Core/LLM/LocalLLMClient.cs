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
        public int MaxTokens { get; set; } = 512;
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;

        public LocalLLMClient(string baseUrl = "http://localhost:1234", int timeoutSeconds = 30)
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
                    max_tokens = MaxTokens,
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
                    max_tokens = MaxTokens,
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
                MaxTokens = settings.LMStudio.MaxTokens;
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
