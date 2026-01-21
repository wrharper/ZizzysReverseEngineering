using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ReverseEngineering.Core
{
    /// <summary>
    /// Log entry with timestamp and severity.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public Exception? Exception { get; set; }

        public override string ToString() => $"[{Timestamp:HH:mm:ss.fff}] {Level,-7} {Category,-15} {Message}";
    }

    /// <summary>
    /// Central logging system with file and in-memory logs.
    /// </summary>
    public static class Logger
    {
        private static readonly List<LogEntry> _logHistory = [];
        private static readonly string _logPath = GetLogPath();
        private static readonly object _lockObj = new();

        private static string GetLogPath()
        {
            // Get exe directory first
            string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
                exeDir = AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(exeDir, "logs");
        }

        public static event Action<LogEntry>? LogAdded;

        static Logger()
        {
            try
            {
                Directory.CreateDirectory(_logPath);
            }
            catch { }
        }

        // ---------------------------------------------------------
        //  LOGGING METHODS
        // ---------------------------------------------------------
        public static void Info(string category, string message)
        {
            Log("INFO", category, message);
        }

        public static void Warning(string category, string message)
        {
            Log("WARN", category, message);
        }

        public static void Error(string category, string message, Exception? ex = null)
        {
            Log("ERROR", category, message, ex);
        }

        public static void Debug(string category, string message)
        {
            Log("DEBUG", category, message);
        }

        public static void PatchApplied(int offset, byte[] original, byte[] newBytes)
        {
            var msg = $"Patch @ 0x{offset:X}: {original.Length} bytes {string.Join(" ", original)} -> {string.Join(" ", newBytes)}";
            Info("PATCH", msg);
        }

        public static void FunctionDiscovered(ulong address, string name, int instructionCount)
        {
            Info("ANALYSIS", $"Function discovered @ 0x{address:X}: {name} ({instructionCount} instrs)");
        }

        // ---------------------------------------------------------
        //  INTERNAL
        // ---------------------------------------------------------
        private static void Log(string level, string category, string message, Exception? ex = null)
        {
            lock (_lockObj)
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Category = category,
                    Message = message,
                    Exception = ex
                };

                _logHistory.Add(entry);
                LogAdded?.Invoke(entry);

                // Keep only last 10000 entries in memory
                if (_logHistory.Count > 10000)
                    _logHistory.RemoveAt(0);

                // Write to file
                WriteToFile(entry);
            }
        }

        private static void WriteToFile(LogEntry entry)
        {
            try
            {
                var logFile = Path.Combine(_logPath, $"{DateTime.Now:yyyy-MM-dd}.log");
                var line = entry.ToString();

                if (entry.Exception != null)
                    line += $"\n{entry.Exception}";

                File.AppendAllText(logFile, line + "\n");
            }
            catch
            {
                // Silently fail if file write doesn't work
            }
        }

        // ---------------------------------------------------------
        //  LOG QUERIES
        // ---------------------------------------------------------
        public static IReadOnlyList<LogEntry> GetHistory() => _logHistory.AsReadOnly();

        public static IEnumerable<LogEntry> GetEntriesByLevel(string level)
        {
            foreach (var entry in _logHistory)
            {
                if (entry.Level == level)
                    yield return entry;
            }
        }

        public static IEnumerable<LogEntry> GetEntriesByCategory(string category)
        {
            foreach (var entry in _logHistory)
            {
                if (entry.Category == category)
                    yield return entry;
            }
        }

        public static void Clear()
        {
            lock (_lockObj)
            {
                _logHistory.Clear();
            }
        }
    }
}
