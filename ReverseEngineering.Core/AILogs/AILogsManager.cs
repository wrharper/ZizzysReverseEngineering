using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReverseEngineering.Core.AILogs
{
    /// <summary>
    /// Represents a single AI operation log entry with prompt, output, and changes
    /// </summary>
    public class AILogEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "";  // "ExplainInstruction", "GeneratePseudocode", etc.

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("aiOutput")]
        public string AIOutput { get; set; } = "";

        [JsonPropertyName("changes")]
        public List<ByteChange> Changes { get; set; } = [];

        [JsonPropertyName("duration_ms")]
        public long DurationMs { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Success";  // Success, Error, Cancelled

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = [];
    }

    /// <summary>
    /// Tracks a byte change made as a result of AI operation
    /// </summary>
    public class ByteChange
    {
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("original_byte")]
        public byte OriginalByte { get; set; }

        [JsonPropertyName("new_byte")]
        public byte NewByte { get; set; }

        [JsonPropertyName("assembly_before")]
        public string? AssemblyBefore { get; set; }

        [JsonPropertyName("assembly_after")]
        public string? AssemblyAfter { get; set; }
    }

    /// <summary>
    /// Manages organized storage and retrieval of AI operation logs
    /// Organized as: AILogs/[OperationType]/[YYYY-MM-DD]/[entry].json
    /// </summary>
    public class AILogsManager
    {
        private readonly string _logsRootPath;
        private readonly object _lockObj = new object();

        public AILogsManager(string? customLogsPath = null)
        {
            _logsRootPath = customLogsPath ?? GetDefaultLogsPath();
            Directory.CreateDirectory(_logsRootPath);
        }

        private static string GetDefaultLogsPath()
        {
            // Get exe directory first
            string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
                exeDir = AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(exeDir, "AILogs");
        }

        // ---------------------------------------------------------
        //  LOG OPERATIONS
        // ---------------------------------------------------------

        /// <summary>
        /// Save an AI log entry to disk
        /// </summary>
        public void SaveLogEntry(AILogEntry entry)
        {
            lock (_lockObj)
            {
                try
                {
                    // Create folder structure: AILogs/[Operation]/[Date]
                    var date = entry.Timestamp.ToString("yyyy-MM-dd");
                    var folderPath = Path.Combine(_logsRootPath, entry.Operation, date);
                    Directory.CreateDirectory(folderPath);

                    // Save entry as JSON: [timestamp_id].json
                    var timestamp = entry.Timestamp.ToString("HHmmss");
                    var fileName = $"{timestamp}_{entry.Id.Substring(0, 8)}.json";
                    var filePath = Path.Combine(folderPath, fileName);

                    var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving AI log: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get all logs for a specific operation type
        /// </summary>
        public List<AILogEntry> GetLogsByOperation(string operation)
        {
            lock (_lockObj)
            {
                var entries = new List<AILogEntry>();
                var operationPath = Path.Combine(_logsRootPath, operation);

                if (!Directory.Exists(operationPath))
                    return entries;

                foreach (var dateFolder in Directory.GetDirectories(operationPath))
                {
                    foreach (var logFile in Directory.GetFiles(dateFolder, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(logFile);
                            var entry = JsonSerializer.Deserialize<AILogEntry>(json);
                            if (entry != null)
                                entries.Add(entry);
                        }
                        catch { }
                    }
                }

                return entries;
            }
        }

        /// <summary>
        /// Get logs from a specific date for an operation
        /// </summary>
        public List<AILogEntry> GetLogsByOperationAndDate(string operation, DateTime date)
        {
            lock (_lockObj)
            {
                var entries = new List<AILogEntry>();
                var dateStr = date.ToString("yyyy-MM-dd");
                var folderPath = Path.Combine(_logsRootPath, operation, dateStr);

                if (!Directory.Exists(folderPath))
                    return entries;

                foreach (var logFile in Directory.GetFiles(folderPath, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(logFile);
                        var entry = JsonSerializer.Deserialize<AILogEntry>(json);
                        if (entry != null)
                            entries.Add(entry);
                    }
                    catch { }
                }

                return entries;
            }
        }

        /// <summary>
        /// Get all available operations
        /// </summary>
        public List<string> GetAvailableOperations()
        {
            lock (_lockObj)
            {
                var operations = new List<string>();

                if (!Directory.Exists(_logsRootPath))
                    return operations;

                foreach (var opFolder in Directory.GetDirectories(_logsRootPath))
                {
                    operations.Add(Path.GetFileName(opFolder));
                }

                return operations;
            }
        }

        /// <summary>
        /// Get all dates for an operation
        /// </summary>
        public List<DateTime> GetDatesForOperation(string operation)
        {
            lock (_lockObj)
            {
                var dates = new List<DateTime>();
                var operationPath = Path.Combine(_logsRootPath, operation);

                if (!Directory.Exists(operationPath))
                    return dates;

                foreach (var dateFolder in Directory.GetDirectories(operationPath))
                {
                    var folderName = Path.GetFileName(dateFolder);
                    if (DateTime.TryParseExact(folderName, "yyyy-MM-dd", null, 
                        System.Globalization.DateTimeStyles.None, out var date))
                    {
                        dates.Add(date);
                    }
                }

                return dates;
            }
        }

        /// <summary>
        /// Get total log count
        /// </summary>
        public int GetTotalLogCount()
        {
            lock (_lockObj)
            {
                int count = 0;

                if (!Directory.Exists(_logsRootPath))
                    return 0;

                foreach (var opFolder in Directory.GetDirectories(_logsRootPath))
                {
                    foreach (var dateFolder in Directory.GetDirectories(opFolder))
                    {
                        count += Directory.GetFiles(dateFolder, "*.json").Length;
                    }
                }

                return count;
            }
        }

        // ---------------------------------------------------------
        //  FORMATTING & DISPLAY
        // ---------------------------------------------------------

        /// <summary>
        /// Format log entry as readable text
        /// </summary>
        public string FormatLogEntry(AILogEntry entry)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"║ Operation: {entry.Operation,-55} ║");
            sb.AppendLine($"║ Time: {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff,-53} ║");
            sb.AppendLine($"║ Duration: {entry.DurationMs}ms | Status: {entry.Status,-38} ║");
            sb.AppendLine($"╠══════════════════════════════════════════════════════════════╣");

            if (!string.IsNullOrEmpty(entry.Prompt))
            {
                sb.AppendLine($"║ PROMPT");
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                foreach (var line in entry.Prompt.Split('\n'))
                {
                    sb.AppendLine($"║ {line,-62} ║");
                }
            }

            if (!string.IsNullOrEmpty(entry.AIOutput))
            {
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                sb.AppendLine($"║ AI OUTPUT");
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                foreach (var line in entry.AIOutput.Split('\n'))
                {
                    var displayLine = line.Length > 60 ? line.Substring(0, 60) : line;
                    sb.AppendLine($"║ {displayLine,-62} ║");
                }
            }

            if (entry.Changes.Count > 0)
            {
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                sb.AppendLine($"║ CHANGES ({entry.Changes.Count} bytes modified)");
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                foreach (var change in entry.Changes)
                {
                    sb.AppendLine($"║ 0x{change.Offset:X8}: {change.OriginalByte:X2} → {change.NewByte:X2}");
                    if (!string.IsNullOrEmpty(change.AssemblyBefore))
                    {
                        sb.AppendLine($"║   Before: {change.AssemblyBefore}");
                        sb.AppendLine($"║   After:  {change.AssemblyAfter}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(entry.ErrorMessage))
            {
                sb.AppendLine($"╟──────────────────────────────────────────────────────────────╢");
                sb.AppendLine($"║ ERROR: {entry.ErrorMessage}");
            }

            sb.AppendLine($"╚══════════════════════════════════════════════════════════════╝");

            return sb.ToString();
        }

        // ---------------------------------------------------------
        //  CLEANUP & MANAGEMENT
        // ---------------------------------------------------------

        /// <summary>
        /// Clear all logs
        /// </summary>
        public void ClearAllLogs()
        {
            lock (_lockObj)
            {
                try
                {
                    if (Directory.Exists(_logsRootPath))
                    {
                        Directory.Delete(_logsRootPath, recursive: true);
                    }
                    Directory.CreateDirectory(_logsRootPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing AI logs: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear logs for specific operation
        /// </summary>
        public void ClearOperationLogs(string operation)
        {
            lock (_lockObj)
            {
                try
                {
                    var operationPath = Path.Combine(_logsRootPath, operation);
                    if (Directory.Exists(operationPath))
                    {
                        Directory.Delete(operationPath, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing operation logs: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Export logs as JSON archive
        /// </summary>
        public string ExportLogsAsJson()
        {
            lock (_lockObj)
            {
                var allLogs = new Dictionary<string, List<AILogEntry>>();

                foreach (var op in GetAvailableOperations())
                {
                    allLogs[op] = GetLogsByOperation(op);
                }

                return JsonSerializer.Serialize(allLogs, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Get root logs path
        /// </summary>
        public string GetLogsPath() => _logsRootPath;

        /// <summary>
        /// Get statistics
        /// </summary>
        public (int totalLogs, int operationTypes, DateTime? oldestLog, DateTime? newestLog) GetStatistics()
        {
            lock (_lockObj)
            {
                var allLogs = new List<AILogEntry>();
                var operations = GetAvailableOperations();

                foreach (var op in operations)
                {
                    allLogs.AddRange(GetLogsByOperation(op));
                }

                var oldestLog = allLogs.Count > 0 ? allLogs.Min(l => l.Timestamp) : (DateTime?)null;
                var newestLog = allLogs.Count > 0 ? allLogs.Max(l => l.Timestamp) : (DateTime?)null;

                return (allLogs.Count, operations.Count, oldestLog, newestLog);
            }
        }
    }
}
