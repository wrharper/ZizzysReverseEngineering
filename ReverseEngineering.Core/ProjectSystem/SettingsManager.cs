using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// LM Studio connection settings.
    /// </summary>
    public class LMStudioSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1234;
        public string? ModelName { get; set; } = "neural-chat";
        public double Temperature { get; set; } = 0.7;
        public bool EnableStreaming { get; set; } = true;
        public bool EnableLLMAnalysis { get; set; } = true;
    }

    /// <summary>
    /// Analysis layer settings.
    /// </summary>
    public class AnalysisSettings
    {
        public bool AutoAnalyzeOnLoad { get; set; } = true;
        public bool AutoAnalyzeOnPatch { get; set; } = true;
        public int MaxFunctionSize { get; set; } = 10000;
        public bool IncludeImportsInAnalysis { get; set; } = true;
        public bool IncludeExportsInAnalysis { get; set; } = true;
        public bool ScanStrings { get; set; } = true;
    }

    /// <summary>
    /// UI-specific settings.
    /// </summary>
    public class UISettings
    {
        public string Theme { get; set; } = "Dark";
        public string FontFamily { get; set; } = "Consolas";
        public int FontSize { get; set; } = 10;
        public bool RememberLayout { get; set; } = true;
        public bool ShowLineNumbers { get; set; } = true;
        public bool HexViewUppercase { get; set; } = true;
        public int HexBytesPerRow { get; set; } = 16;
        public Dictionary<string, object> WindowLayout { get; set; } = [];
    }

    /// <summary>
    /// Application-level settings (not per-project).
    /// Persisted to JSON with sections for LM Studio, Analysis, UI, and General.
    /// </summary>
    public class AppSettings
    {
        // General settings
        public string? LastOpenedFile { get; set; }
        public string? LastOpenedProject { get; set; }
        public int MaxUndoHistorySize { get; set; } = 100;
        public string AppVersion { get; set; } = "1.0.0";

        // Subsystems (organized settings)
        public LMStudioSettings LMStudio { get; set; } = new();
        public AnalysisSettings Analysis { get; set; } = new();
        public UISettings UI { get; set; } = new();

        // Advanced
        public bool EnableDetailedLogging { get; set; } = false;
        public int LogRetentionDays { get; set; } = 30;
        public Dictionary<string, string> CustomPatterns { get; set; } = [];
    }

    /// <summary>
    /// Manages application-level settings persistence.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsPath = GetSettingsPath();

        private static AppSettings _currentSettings = new();

        private static string GetSettingsPath()
        {
            // Get exe directory first
            string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
                exeDir = AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(exeDir, "settings.json");
        }

        static SettingsManager()
        {
            LoadSettings();
        }

        // ---------------------------------------------------------
        //  SETTINGS LOAD/SAVE
        // ---------------------------------------------------------
        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // If load fails, use defaults
                _currentSettings = new AppSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? "");

                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail if save doesn't work
            }
        }

        // ---------------------------------------------------------
        //  SETTINGS ACCESS
        // ---------------------------------------------------------
        public static AppSettings Current => _currentSettings;

        public static void SetLastOpenedFile(string path)
        {
            _currentSettings.LastOpenedFile = path;
            SaveSettings();
        }

        public static void SetLastOpenedProject(string path)
        {
            _currentSettings.LastOpenedProject = path;
            SaveSettings();
        }

        // ---------------------------------------------------------
        //  LM STUDIO SETTINGS
        // ---------------------------------------------------------
        public static string GetLMStudioUrl()
        {
            return $"http://{_currentSettings.LMStudio.Host}:{_currentSettings.LMStudio.Port}";
        }

        public static void SetLMStudioHost(string host)
        {
            _currentSettings.LMStudio.Host = host;
            SaveSettings();
        }

        public static void SetLMStudioPort(int port)
        {
            _currentSettings.LMStudio.Port = Math.Max(1, Math.Min(port, 65535));
            SaveSettings();
        }

        public static void SetLMStudioModel(string? modelName)
        {
            _currentSettings.LMStudio.ModelName = modelName;
            SaveSettings();
        }

        public static void SetLMStudioTemperature(double temperature)
        {
            _currentSettings.LMStudio.Temperature = Math.Max(0.0, Math.Min(temperature, 1.0));
            SaveSettings();
        }

        public static void SetLMStudioStreaming(bool enabled)
        {
            _currentSettings.LMStudio.EnableStreaming = enabled;
            SaveSettings();
        }

        public static void SetLMAnalysisEnabled(bool enabled)
        {
            _currentSettings.LMStudio.EnableLLMAnalysis = enabled;
            SaveSettings();
        }

        // ---------------------------------------------------------
        //  ANALYSIS SETTINGS
        // ---------------------------------------------------------
        public static void SetAutoAnalyzeOnLoad(bool enabled)
        {
            _currentSettings.Analysis.AutoAnalyzeOnLoad = enabled;
            SaveSettings();
        }

        public static void SetAutoAnalyzeOnPatch(bool enabled)
        {
            _currentSettings.Analysis.AutoAnalyzeOnPatch = enabled;
            SaveSettings();
        }

        public static void SetMaxFunctionSize(int size)
        {
            _currentSettings.Analysis.MaxFunctionSize = Math.Max(100, size);
            SaveSettings();
        }

        // ---------------------------------------------------------
        //  UI SETTINGS
        // ---------------------------------------------------------
        public static void SetTheme(string theme)
        {
            _currentSettings.UI.Theme = theme;
            SaveSettings();
        }

        public static string GetTheme()
        {
            return _currentSettings.UI.Theme;
        }

        public static void SetFont(string family, int size)
        {
            _currentSettings.UI.FontFamily = family;
            _currentSettings.UI.FontSize = Math.Max(8, Math.Min(size, 24));
            SaveSettings();
        }

        public static void SetHexViewUppercase(bool uppercase)
        {
            _currentSettings.UI.HexViewUppercase = uppercase;
            SaveSettings();
        }

        public static void SetHexBytesPerRow(int bytesPerRow)
        {
            _currentSettings.UI.HexBytesPerRow = Math.Max(4, Math.Min(bytesPerRow, 64));
            SaveSettings();
        }

        // ---------------------------------------------------------
        //  THEME SUPPORT (Legacy)
        // ---------------------------------------------------------
        public static void SetUILayout(Dictionary<string, object> layout)
        {
            _currentSettings.UI.WindowLayout = layout;
            SaveSettings();
        }

        public static Dictionary<string, object> GetUILayout()
        {
            return _currentSettings.UI.WindowLayout;
        }

        // ---------------------------------------------------------
        //  RESET TO DEFAULTS
        // ---------------------------------------------------------
        public static void ResetToDefaults()
        {
            _currentSettings = new AppSettings();
            SaveSettings();
        }

        // ---------------------------------------------------------
        //  EXPORT/IMPORT SETTINGS
        // ---------------------------------------------------------
        public static string ExportSettingsAsJson()
        {
            return JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public static bool TryImportSettings(string json)
        {
            try
            {
                var imported = JsonSerializer.Deserialize<AppSettings>(json);
                if (imported != null)
                {
                    _currentSettings = imported;
                    SaveSettings();
                    return true;
                }
            }
            catch { }
            return false;
        }

        // ---------------------------------------------------------
        //  VALIDATION
        // ---------------------------------------------------------
        public static bool ValidateLMStudioConnection()
        {
            // Check if host/port are valid
            if (string.IsNullOrWhiteSpace(_currentSettings.LMStudio.Host))
                return false;
            if (_currentSettings.LMStudio.Port < 1 || _currentSettings.LMStudio.Port > 65535)
                return false;
            return true;
        }

        public static string GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_currentSettings.LMStudio.Host))
                errors.Add("LM Studio host cannot be empty");

            if (_currentSettings.LMStudio.Port < 1 || _currentSettings.LMStudio.Port > 65535)
                errors.Add("LM Studio port must be between 1 and 65535");

            if (_currentSettings.LMStudio.Temperature < 0.0 || _currentSettings.LMStudio.Temperature > 1.0)
                errors.Add("Temperature must be between 0.0 and 1.0");

            if (_currentSettings.UI.FontSize < 8 || _currentSettings.UI.FontSize > 24)
                errors.Add("Font size must be between 8 and 24");

            return errors.Count > 0 ? string.Join("\n", errors) : "";
        }
    }
}
