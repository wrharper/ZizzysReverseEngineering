using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Manages an LLM conversation session with current binary context
    /// Tracks context changes and maintains conversation history
    /// </summary>
    public class LLMSession
    {
        private readonly LocalLLMClient _client;
        private readonly BinaryContextGenerator _contextGenerator;
        
        private BinaryContextData? _currentContext;
        private List<ChatMessage> _history = [];

        public string SessionId { get; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime LastUpdatedContext { get; private set; } = DateTime.UtcNow;
        
        public bool HasContext => _currentContext != null;
        public int MessageCount => _history.Count;

        public LLMSession(LocalLLMClient client, BinaryContextGenerator contextGenerator)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _contextGenerator = contextGenerator ?? throw new ArgumentNullException(nameof(contextGenerator));
        }

        /// <summary>
        /// Initialize or update session context with current binary state
        /// </summary>
        public void UpdateContext()
        {
            var newContext = _contextGenerator.GenerateContext();
            
            if (_currentContext == null)
            {
                // First initialization
                _currentContext = newContext;
                LastUpdatedContext = DateTime.UtcNow;
            }
            else if (HasContextChanged(_currentContext, newContext))
            {
                // Context changed, add update message to history
                var updatePrompt = _contextGenerator.GenerateContextUpdatePrompt(_currentContext, newContext);
                _history.Add(new ChatMessage 
                { 
                    Role = "system", 
                    Content = updatePrompt,
                    Timestamp = DateTime.UtcNow
                });
                
                _currentContext = newContext;
                LastUpdatedContext = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Send user query and get AI response
        /// Uses current context as system prompt
        /// </summary>
        public async Task<string> QueryAsync(
            string userQuery,
            CancellationToken cancellationToken = default)
        {
            if (!HasContext)
                UpdateContext();

            if (_currentContext == null)
                return "Error: No binary context available";

            // Add user message to history
            _history.Add(new ChatMessage
            {
                Role = "user",
                Content = userQuery,
                Timestamp = DateTime.UtcNow
            });

            // Generate system prompt from current context
            var systemPrompt = _contextGenerator.GenerateSystemPrompt(_currentContext);

            try
            {
                // Send to LLM
                var response = await _client.ChatAsync(userQuery, systemPrompt, cancellationToken);
                
                // Add response to history
                _history.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.UtcNow
                });

                return response;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error querying LLM: {ex.Message}";
                _history.Add(new ChatMessage
                {
                    Role = "system",
                    Content = errorMsg,
                    Timestamp = DateTime.UtcNow
                });
                return errorMsg;
            }
        }

        /// <summary>
        /// Get conversation history
        /// </summary>
        public IReadOnlyList<ChatMessage> GetHistory()
        {
            return _history.AsReadOnly();
        }

        /// <summary>
        /// Clear conversation history (keep context)
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>
        /// Get current binary context
        /// </summary>
        public BinaryContextData? GetContext()
        {
            return _currentContext;
        }

        private static bool HasContextChanged(BinaryContextData? previous, BinaryContextData? current)
        {
            if (previous == null || current == null)
                return previous != current;

            // Check if any significant analysis data changed
            return previous.ModifiedBytes != current.ModifiedBytes
                || previous.TotalFunctions != current.TotalFunctions
                || previous.TotalCrossReferences != current.TotalCrossReferences
                || previous.TotalSymbols != current.TotalSymbols;
        }
    }

    /// <summary>
    /// Chat message in session history
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            var roleTag = Role switch
            {
                "user" => "User",
                "assistant" => "AI",
                "system" => "System",
                _ => Role
            };
            return $"[{roleTag}] {Content[..Math.Min(50, Content.Length)]}...";
        }
    }
}
