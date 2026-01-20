# Settings System Documentation

## Overview

The application now has a comprehensive, persistent settings system organized into four logical sections:

1. **LM Studio** - Local LLM configuration
2. **Analysis** - Analysis layer behavior
3. **UI** - Visual preferences
4. **Advanced** - Logging and performance tuning

All settings are persisted to:
```
%APPDATA%\ZizzysReverseEngineering\settings.json
```

---

## Settings Structure

### LMStudioSettings

Controls LocalLLMClient behavior and configuration:

```csharp
public class LMStudioSettings
{
    public string Host { get; set; } = "localhost";         // LM Studio host
    public int Port { get; set; } = 1234;                   // LM Studio port
    public string? ModelName { get; set; } = "neural-chat";  // Model to use
    public double Temperature { get; set; } = 0.7;           // 0.0-1.0
    public int MaxTokens { get; set; } = 4096;               // Output length
    public bool EnableStreaming { get; set; } = true;        // Stream responses
    public int RequestTimeoutSeconds { get; set; } = 300;    // 5 min default
    public bool EnableLLMAnalysis { get; set; } = true;      // Use AI features
}
```

**Important**: `RequestTimeoutSeconds` is set to 300 seconds (5 minutes) by default to prevent premature timeouts during LM testing. For production use, adjust based on your network speed and model performance.

### AnalysisSettings

Controls automatic analysis behavior:

```csharp
public class AnalysisSettings
{
    public bool AutoAnalyzeOnLoad { get; set; } = true;           // Analyze when opening binary
    public bool AutoAnalyzeOnPatch { get; set; } = true;          // Reanalyze after patches
    public int MaxFunctionSize { get; set; } = 10000;             // Bytes
    public bool IncludeImportsInAnalysis { get; set; } = true;    // Include IAT
    public bool IncludeExportsInAnalysis { get; set; } = true;    // Include EAT
    public bool ScanStrings { get; set; } = true;                 // String detection
}
```

### UISettings

Controls user interface appearance and layout:

```csharp
public class UISettings
{
    public string Theme { get; set; } = "Dark";                 // "Dark", "Light", "HighContrast"
    public string FontFamily { get; set; } = "Consolas";         // Font for code
    public int FontSize { get; set; } = 10;                      // Points (8-24)
    public bool RememberLayout { get; set; } = true;             // Remember window state
    public bool ShowLineNumbers { get; set; } = true;            // Disassembly line numbers
    public bool HexViewUppercase { get; set; } = true;           // A-F vs a-f
    public int HexBytesPerRow { get; set; } = 16;                // 4-64 bytes
    public Dictionary<string, object> WindowLayout { get; set; } // Serialized layout
}
```

### Advanced Settings

```csharp
public bool EnableDetailedLogging { get; set; } = false;        // Verbose logging
public int LogRetentionDays { get; set; } = 30;                 // Clean old logs
public Dictionary<string, string> CustomPatterns { get; set; }  // User patterns (future)
```

---

## Accessing Settings in Code

### Static API (Everywhere)

```csharp
using ReverseEngineering.Core.ProjectSystem;

// Load/save
SettingsManager.LoadSettings();
SettingsManager.SaveSettings();

// LM Studio
string url = SettingsManager.GetLMStudioUrl();  // "http://localhost:1234"
SettingsManager.SetLMStudioHost("192.168.1.100");
SettingsManager.SetLMStudioPort(1234);
SettingsManager.SetLMStudioTemperature(0.8);
SettingsManager.SetLMStudioStreaming(true);

// Analysis
SettingsManager.SetAutoAnalyzeOnLoad(true);
SettingsManager.SetAutoAnalyzeOnPatch(false);

// UI
SettingsManager.SetTheme("Dark");
SettingsManager.SetFont("Consolas", 10);
SettingsManager.SetHexViewUppercase(true);

// Current
var settings = SettingsManager.Current;
var temp = settings.LMStudio.Temperature;
```

### Validation

```csharp
// Check if settings are valid
bool isValid = SettingsManager.ValidateLMStudioConnection();

// Get detailed error messages
string errors = SettingsManager.GetValidationErrors();
if (!errors.IsEmpty)
    MessageBox.Show(errors, "Settings Error");
```

### Import/Export

```csharp
// Export as JSON
string json = SettingsManager.ExportSettingsAsJson();
File.WriteAllText("my_settings.json", json);

// Import from JSON
string imported = File.ReadAllText("my_settings.json");
if (SettingsManager.TryImportSettings(imported))
    MessageBox.Show("Settings imported");
else
    MessageBox.Show("Import failed");

// Reset to defaults
SettingsManager.ResetToDefaults();
```

---

## Settings Dialog (UI)

Launch via **Tools → Settings...** (Ctrl+,)

### Tabs

#### 1. LM Studio Tab

- **Enable LLM Analysis**: Toggle AI features on/off
- **Host**: Hostname or IP (default: localhost)
- **Port**: Port number (default: 1234)
- **Model**: Model name for display/selection
- **Temperature**: Slider 0.0-1.0 (lower = more deterministic)
- **Max Tokens**: Maximum response length
- **Request Timeout**: Seconds to wait (default: 300 = 5 min)
- **Enable Streaming**: Stream responses line-by-line
- **Test Connection**: Verify localhost:port is reachable

#### 2. Analysis Tab

- **Auto-analyze on load**: Run full analysis when opening binary
- **Auto-analyze on patch**: Rerun analysis after edits
- **Include imports/exports**: Include IAT/EAT in analysis
- **Scan strings**: Detect ASCII and wide strings
- **Max Function Size**: Threshold for function detection

#### 3. UI Tab

- **Theme**: Dark/Light/HighContrast (requires restart)
- **Font**: Consolas/Courier New/Segoe UI Mono/Liberation Mono
- **Font Size**: 8-24 points
- **Hex uppercase**: Display A-F or a-f
- **Hex bytes per row**: 4-64 bytes per row
- **Remember layout**: Save window state on exit

#### 4. Advanced Tab

- **Detailed logging**: Enable verbose file logs
- **Log retention**: Days to keep old logs
- (Future: Custom patterns, plugins, etc.)

---

## Settings Persistence

### JSON Format

Settings are stored in:
```json
{
  "lastOpenedFile": null,
  "lastOpenedProject": null,
  "maxUndoHistorySize": 100,
  "appVersion": "1.0.0",
  "lmStudio": {
    "host": "localhost",
    "port": 1234,
    "modelName": "neural-chat",
    "temperature": 0.7,
    "maxTokens": 4096,
    "enableStreaming": true,
    "requestTimeoutSeconds": 300,
    "enableLLMAnalysis": true
  },
  "analysis": {
    "autoAnalyzeOnLoad": true,
    "autoAnalyzeOnPatch": true,
    "maxFunctionSize": 10000,
    "includeImportsInAnalysis": true,
    "includeExportsInAnalysis": true,
    "scanStrings": true
  },
  "ui": {
    "theme": "Dark",
    "fontFamily": "Consolas",
    "fontSize": 10,
    "rememberLayout": true,
    "showLineNumbers": true,
    "hexViewUppercase": true,
    "hexBytesPerRow": 16,
    "windowLayout": {}
  },
  "enableDetailedLogging": false,
  "logRetentionDays": 30,
  "customPatterns": {}
}
```

### File Locations

```
%APPDATA%\ZizzysReverseEngineering\
├── settings.json           # Main settings
├── logs/
│   ├── 2025-01-15.log
│   ├── 2025-01-16.log
│   └── ...
└── projects/               # Saved projects
    ├── myapp.zre
    └── ...
```

### Auto-Save

Settings are automatically saved when:
- User clicks "OK" in settings dialog
- Any `SettingsManager.Set*()` call is made
- Application exits normally

---

## LM Studio Configuration Best Practices

### For Development/Testing

```json
"lmStudio": {
    "host": "localhost",
    "port": 1234,
    "modelName": "neural-chat",
    "temperature": 0.7,
    "maxTokens": 4096,
    "enableStreaming": true,
    "requestTimeoutSeconds": 300,  // 5 min - LOCAL LLMs ARE SLOW!
    "enableLLMAnalysis": true
}
```

### For Production/Remote Use

```json
"lmStudio": {
    "host": "192.168.1.50",        // Remote machine
    "port": 1234,
    "modelName": "mistral-7b",
    "temperature": 0.5,            // More deterministic
    "maxTokens": 2048,             // Shorter responses
    "enableStreaming": false,      // Full response only
    "requestTimeoutSeconds": 120,  // 2 minutes
    "enableLLMAnalysis": true
}
```

### For Low-Latency Inference

```json
"lmStudio": {
    "temperature": 0.3,            // Deterministic
    "maxTokens": 1024,             // Limit output
    "enableStreaming": true,       // Faster visual feedback
    "requestTimeoutSeconds": 60    // Stricter timeout
}
```

---

## Programmatic Integration

### Loading LM Studio Settings into Client

```csharp
// Automatic during FormMain init
var client = new LocalLLMClient(
    SettingsManager.Current.LMStudio.Host,
    SettingsManager.Current.LMStudio.Port,
    SettingsManager.Current.LMStudio.ModelName ?? "neural-chat"
)
{
    Temperature = SettingsManager.Current.LMStudio.Temperature,
    MaxTokens = SettingsManager.Current.LMStudio.MaxTokens,
    RequestTimeoutSeconds = SettingsManager.Current.LMStudio.RequestTimeoutSeconds,
    EnableStreaming = SettingsManager.Current.LMStudio.EnableStreaming
};
```

### Responding to Settings Changes

```csharp
// In FormMain.cs
private void OnSettingsChanged()
{
    // Reload client config
    _llmClient.RequestTimeoutSeconds = SettingsManager.Current.LMStudio.RequestTimeoutSeconds;
    _llmClient.Temperature = SettingsManager.Current.LMStudio.Temperature;
    _llmClient.MaxTokens = SettingsManager.Current.LMStudio.MaxTokens;
    
    // Reload UI config (requires restart for theme)
    _hex.Font = new Font(
        SettingsManager.Current.UI.FontFamily,
        SettingsManager.Current.UI.FontSize
    );
}
```

### Adding New Settings

To add a new setting:

1. **Add property to AppSettings class**:
   ```csharp
   public bool MyNewSetting { get; set; } = false;
   ```

2. **Add accessor in SettingsManager**:
   ```csharp
   public static void SetMyNewSetting(bool value)
   {
       _currentSettings.MyNewSetting = value;
       SaveSettings();
   }
   ```

3. **Add UI control in SettingsDialog**:
   ```csharp
   var checkbox = AddCheckBox(panel, "My New Setting", 10, ref y, _settings.MyNewSetting);
   ```

4. **Add save logic in SaveSettings()**:
   ```csharp
   _settings.MyNewSetting = checkbox.Checked;
   ```

---

## Validation & Error Handling

### Built-in Validation

```csharp
public static string GetValidationErrors()
{
    // Returns multi-line string with all validation errors
    // Examples:
    // - "LM Studio host cannot be empty"
    // - "Port must be between 1 and 65535"
    // - "Temperature must be between 0.0 and 1.0"
    // - "Font size must be between 8 and 24"
}
```

### Safe Settings Access

```csharp
// All setters validate and clamp values automatically
SettingsManager.SetLMStudioPort(99999);        // Clamped to 65535
SettingsManager.SetLMStudioTemperature(1.5);   // Clamped to 1.0
SettingsManager.SetFontSize(100);              // Clamped to 24
```

---

## Troubleshooting

### Settings File Corruption

If `settings.json` is invalid or corrupted:

```
Solution: Delete %APPDATA%\ZizzysReverseEngineering\settings.json
App will auto-create with defaults on next run
```

### Lost Settings After Crash

Settings are saved when:
- Dialog OK is clicked
- Any `SettingsManager.Set*()` called
- Application exits normally

If app crashes before saving, settings revert to last successful save.

### LM Studio Connection Fails

1. Check host/port in Settings → LM Studio
2. Verify LM Studio is running: http://host:port/api/models
3. Verify network connectivity (ping host)
4. Check firewall rules
5. Test connection with "Test Connection" button in settings dialog

---

## Future Enhancements

Planned settings additions:

- [ ] Per-project overrides (override global settings per .zre file)
- [ ] Settings profiles (save/load multiple configurations)
- [ ] Custom regex patterns for string scanning
- [ ] Plugin configuration
- [ ] Debugger integration settings (x64dbg, WinDbg connection)
- [ ] Export/import presets for team sharing
- [ ] Theme editor (custom colors)

---

## Summary

The new settings system provides:

✅ **Centralized Configuration**: All app settings in one place  
✅ **LM Studio Integration**: Full local LLM support with tuning  
✅ **Persistent Storage**: JSON-based, survives app restarts  
✅ **UI Dialog**: Clean interface for user configuration  
✅ **Programmatic API**: Easy access from code  
✅ **Validation**: Input checking and error messages  
✅ **Export/Import**: Share settings with team members  

**Access**: Tools → Settings... (Ctrl+,)  
**Storage**: %APPDATA%\ZizzysReverseEngineering\settings.json
