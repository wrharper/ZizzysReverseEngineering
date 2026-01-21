using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core.ProjectSystem;

namespace ReverseEngineering.WinForms.Settings
{
    public partial class SettingsDialog : Form
    {
        private readonly AppSettings _settings;
        private TabControl _tabControl = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;
        private Button _resetButton = null!;

        // ---------------------------------------------------------
        //  LM STUDIO TAB CONTROLS
        // ---------------------------------------------------------
        private TextBox _lmHostTextBox = null!;
        private NumericUpDown _lmPortNumeric = null!;
        private TextBox _lmModelTextBox = null!;
        private TrackBar _lmTempTrackBar = null!;
        private Label _lmTempLabel = null!;
        private NumericUpDown _lmMaxTokensNumeric = null!;
        private NumericUpDown _lmTimeoutNumeric = null!;
        private CheckBox _lmStreamingCheckBox = null!;
        private CheckBox _lmEnabledCheckBox = null!;
        private Button _testConnectionButton = null!;

        // ---------------------------------------------------------
        //  ANALYSIS TAB CONTROLS
        // ---------------------------------------------------------
        private CheckBox _autoAnalyzeLoadCheckBox = null!;
        private CheckBox _autoAnalyzePatchCheckBox = null!;
        private NumericUpDown _maxFunctionSizeNumeric = null!;
        private CheckBox _includeImportsCheckBox = null!;
        private CheckBox _includeExportsCheckBox = null!;
        private CheckBox _scanStringsCheckBox = null!;

        // ---------------------------------------------------------
        //  UI TAB CONTROLS
        // ---------------------------------------------------------
        private ComboBox _themeComboBox = null!;
        private ComboBox _fontFamilyComboBox = null!;
        private NumericUpDown _fontSizeNumeric = null!;
        private CheckBox _hexUppercaseCheckBox = null!;
        private NumericUpDown _hexBytesPerRowNumeric = null!;
        private CheckBox _rememberLayoutCheckBox = null!;

        // ---------------------------------------------------------
        //  ADVANCED TAB CONTROLS
        // ---------------------------------------------------------
        private CheckBox _detailedLoggingCheckBox = null!;
        private NumericUpDown _logRetentionNumeric = null!;

        // ---------------------------------------------------------
        //  THEME PREVIEW
        // ---------------------------------------------------------
        private AppTheme _originalTheme = Themes.Dark;  // Saved on open for revert on cancel
        private string _originalThemeName = "Dark";     // Theme name for settings restoration

        /// <summary>
        /// Which tab to show when dialog opens.
        /// Default is 0 (LM Studio). Set to 2 for UI tab (themes).
        /// </summary>
        private int _initialTabIndex = 0;

        public SettingsDialog(int initialTabIndex = 0)
        {
            _settings = SettingsManager.Current;
            _initialTabIndex = initialTabIndex;
            _originalTheme = ThemeManager.CurrentTheme;  // Save current theme for revert
            _originalThemeName = _settings.UI.Theme ?? "Dark";  // Save theme name for settings restoration
            InitializeComponents();
            LoadSettings();
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
        }

        private void InitializeComponents()
        {
            Text = "Application Settings";
            Size = new Size(600, 500);
            BackColor = ThemeManager.CurrentTheme.BackColor;
            ForeColor = ThemeManager.CurrentTheme.ForeColor;
            Font = new Font("Segoe UI", 9);

            // Main layout
            var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Controls.Add(mainPanel);

            // Tab control
            _tabControl = new TabControl { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(_tabControl);

            _tabControl.TabPages.Add(CreateLMStudioTab());
            _tabControl.TabPages.Add(CreateAnalysisTab());
            _tabControl.TabPages.Add(CreateUITab());
            _tabControl.TabPages.Add(CreateAdvancedTab());

            // Set initial tab if requested
            if (_initialTabIndex >= 0 && _initialTabIndex < _tabControl.TabPages.Count)
                _tabControl.SelectedIndex = _initialTabIndex;

            // Buttons
            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            mainPanel.Controls.Add(buttonPanel);

            _resetButton = new Button
            {
                Text = "Reset to Defaults",
                Width = 120,
                Height = 32,
                Location = new Point(10, 8),
                BackColor = ThemeManager.CurrentTheme.ButtonBack,
                ForeColor = ThemeManager.CurrentTheme.ButtonFore,
                FlatStyle = FlatStyle.Flat
            };
            _resetButton.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset all settings to defaults?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    SettingsManager.ResetToDefaults();
                    LoadSettings();
                }
            };
            buttonPanel.Controls.Add(_resetButton);

            _okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80,
                Height = 32,
                Location = new Point(350, 8),
                BackColor = ThemeManager.CurrentTheme.Accent,
                ForeColor = ThemeManager.CurrentTheme.ForeColor,
                FlatStyle = FlatStyle.Flat
            };
            _okButton.Click += (s, e) => SaveSettings();
            buttonPanel.Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 32,
                Location = new Point(440, 8),
                BackColor = ThemeManager.CurrentTheme.ButtonBack,
                ForeColor = ThemeManager.CurrentTheme.ButtonFore,
                FlatStyle = FlatStyle.Flat
            };
            _cancelButton.Click += (s, e) => CancelThemePreview();
            buttonPanel.Controls.Add(_cancelButton);

            CancelButton = _cancelButton;
            AcceptButton = _okButton;
        }

        // ---------------------------------------------------------
        //  LM STUDIO TAB
        // ---------------------------------------------------------
        private TabPage CreateLMStudioTab()
        {
            var tab = new TabPage { Text = "LM Studio", BackColor = ThemeManager.CurrentTheme.BackColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15) };
            tab.Controls.Add(panel);

            int y = 10;

            // Enabled checkbox
            _lmEnabledCheckBox = AddCheckBox(panel, "Enable LLM Analysis", 10, ref y);

            // Host
            AddLabel(panel, "Host:", 10, y);
            _lmHostTextBox = new TextBox { Text = _settings.LMStudio.Host, Width = 300, Location = new Point(150, y), BackColor = ThemeManager.CurrentTheme.PanelColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            panel.Controls.Add(_lmHostTextBox);
            y += 30;

            // Port
            AddLabel(panel, "Port:", 10, y);
            _lmPortNumeric = new NumericUpDown 
            { 
                Minimum = 1,
                Maximum = 65535,
                Value = Math.Min(_settings.LMStudio.Port, 65535),
                Width = 100, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_lmPortNumeric);
            y += 30;

            // Model
            AddLabel(panel, "Model:", 10, y);
            _lmModelTextBox = new TextBox { Text = _settings.LMStudio.ModelName ?? "", Width = 300, Location = new Point(150, y), BackColor = ThemeManager.CurrentTheme.PanelColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            panel.Controls.Add(_lmModelTextBox);
            y += 30;

            // Temperature
            AddLabel(panel, "Temperature:", 10, y);
            _lmTempTrackBar = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)(_settings.LMStudio.Temperature * 100), Width = 300, Location = new Point(150, y) };
            _lmTempTrackBar.ValueChanged += (s, e) => _lmTempLabel.Text = $"Temperature: {_lmTempTrackBar.Value / 100.0:F2}";
            panel.Controls.Add(_lmTempTrackBar);
            _lmTempLabel = new Label { Text = $"Temperature: {_settings.LMStudio.Temperature:F2}", Location = new Point(460, y + 2), AutoSize = true, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            panel.Controls.Add(_lmTempLabel);
            y += 30;

            // Max Tokens
            AddLabel(panel, "Max Tokens:", 10, y);
            _lmMaxTokensNumeric = new NumericUpDown 
            { 
                Minimum = 1,
                Maximum = 32768,
                Value = Math.Min(_settings.LMStudio.MaxTokens, 32768),
                Width = 100, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_lmMaxTokensNumeric);
            y += 30;

            // Timeout
            AddLabel(panel, "Request Timeout (seconds):", 10, y);
            _lmTimeoutNumeric = new NumericUpDown 
            { 
                Minimum = 10,
                Maximum = 1800,
                Value = Math.Clamp(_settings.LMStudio.RequestTimeoutSeconds, 10, 1800),
                Width = 100, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_lmTimeoutNumeric);
            y += 30;

            // Streaming
            _lmStreamingCheckBox = AddCheckBox(panel, "Enable Streaming", 150, ref y, _settings.LMStudio.EnableStreaming);

            // Test button
            y += 10;
            _testConnectionButton = new Button
            {
                Text = "Test Connection",
                Width = 150,
                Height = 32,
                Location = new Point(150, y),
                BackColor = ThemeManager.CurrentTheme.Accent,
                ForeColor = ThemeManager.CurrentTheme.ForeColor,
                FlatStyle = FlatStyle.Flat
            };
            _testConnectionButton.Click += TestConnection;
            panel.Controls.Add(_testConnectionButton);

            return tab;
        }

        // ---------------------------------------------------------
        //  ANALYSIS TAB
        // ---------------------------------------------------------
        private TabPage CreateAnalysisTab()
        {
            var tab = new TabPage { Text = "Analysis", BackColor = ThemeManager.CurrentTheme.BackColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15) };
            tab.Controls.Add(panel);

            int y = 10;

            _autoAnalyzeLoadCheckBox = AddCheckBox(panel, "Auto-analyze on load", 10, ref y, _settings.Analysis.AutoAnalyzeOnLoad);
            _autoAnalyzePatchCheckBox = AddCheckBox(panel, "Auto-analyze on patch", 10, ref y, _settings.Analysis.AutoAnalyzeOnPatch);
            _includeImportsCheckBox = AddCheckBox(panel, "Include imports in analysis", 10, ref y, _settings.Analysis.IncludeImportsInAnalysis);
            _includeExportsCheckBox = AddCheckBox(panel, "Include exports in analysis", 10, ref y, _settings.Analysis.IncludeExportsInAnalysis);
            _scanStringsCheckBox = AddCheckBox(panel, "Scan for strings", 10, ref y, _settings.Analysis.ScanStrings);

            y += 10;
            AddLabel(panel, "Max Function Size:", 10, y);
            _maxFunctionSizeNumeric = new NumericUpDown 
            { 
                Minimum = 100,
                Maximum = 100000,
                Value = Math.Clamp(_settings.Analysis.MaxFunctionSize, 100, 100000),
                Width = 150, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_maxFunctionSizeNumeric);

            return tab;
        }

        // ---------------------------------------------------------
        //  UI TAB
        // ---------------------------------------------------------
        private TabPage CreateUITab()
        {
            var tab = new TabPage { Text = "UI", BackColor = ThemeManager.CurrentTheme.BackColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15) };
            tab.Controls.Add(panel);

            int y = 10;

            // Theme
            AddLabel(panel, "Theme:", 10, y);
            _themeComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Location = new Point(150, y), BackColor = ThemeManager.CurrentTheme.PanelColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            _themeComboBox.Items.AddRange(new[] { "Dark", "Light", "Midnight", "HackerGreen" });
            _themeComboBox.SelectedItem = _settings.UI.Theme ?? "Dark";
            _themeComboBox.SelectedIndexChanged += ThemeComboBox_SelectedIndexChanged;
            panel.Controls.Add(_themeComboBox);
            y += 30;

            // Font
            AddLabel(panel, "Font:", 10, y);
            _fontFamilyComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Location = new Point(150, y), BackColor = ThemeManager.CurrentTheme.PanelColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            foreach (var family in new[] { "Consolas", "Courier New", "Segoe UI Mono", "Liberation Mono" })
                _fontFamilyComboBox.Items.Add(family);
            _fontFamilyComboBox.SelectedItem = _settings.UI.FontFamily;
            panel.Controls.Add(_fontFamilyComboBox);
            y += 30;

            AddLabel(panel, "Font Size:", 10, y);
            _fontSizeNumeric = new NumericUpDown 
            { 
                Minimum = 8,
                Maximum = 24,
                Value = Math.Clamp(_settings.UI.FontSize, 8, 24),
                Width = 80, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_fontSizeNumeric);
            y += 30;

            // Hex view
            _hexUppercaseCheckBox = AddCheckBox(panel, "Hex view: uppercase", 10, ref y, _settings.UI.HexViewUppercase);

            AddLabel(panel, "Hex bytes per row:", 10, y);
            _hexBytesPerRowNumeric = new NumericUpDown 
            { 
                Minimum = 4,
                Maximum = 64,
                Value = Math.Clamp(_settings.UI.HexBytesPerRow, 4, 64),
                Width = 80, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_hexBytesPerRowNumeric);
            y += 30;

            _rememberLayoutCheckBox = AddCheckBox(panel, "Remember window layout", 10, ref y, _settings.UI.RememberLayout);

            return tab;
        }

        // ---------------------------------------------------------
        //  ADVANCED TAB
        // ---------------------------------------------------------
        private TabPage CreateAdvancedTab()
        {
            var tab = new TabPage { Text = "Advanced", BackColor = ThemeManager.CurrentTheme.BackColor, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15) };
            tab.Controls.Add(panel);

            int y = 10;

            _detailedLoggingCheckBox = AddCheckBox(panel, "Enable detailed logging", 10, ref y, _settings.EnableDetailedLogging);

            AddLabel(panel, "Log retention (days):", 10, y);
            _logRetentionNumeric = new NumericUpDown 
            { 
                Minimum = 1,
                Maximum = 365,
                Value = Math.Clamp(_settings.LogRetentionDays, 1, 365),
                Width = 80, 
                Location = new Point(150, y), 
                BackColor = ThemeManager.CurrentTheme.PanelColor, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor 
            };
            panel.Controls.Add(_logRetentionNumeric);

            return tab;
        }

        // ---------------------------------------------------------
        //  RESPONSIVE LAYOUT HELPERS
        // ---------------------------------------------------------
        /// <summary>
        /// Adds a label-control pair with responsive layout.
        /// Labels are anchored left, controls are anchored left+right for responsiveness.
        /// </summary>
        private int AddLabeledControl(Panel panel, string labelText, Control control, int y, int controlWidth = 150)
        {
            const int labelX = 10;
            const int controlX = 150;
            const int rowHeight = 30;

            var label = new Label 
            { 
                Text = labelText, 
                Location = new Point(labelX, y + 2), 
                AutoSize = true, 
                ForeColor = ThemeManager.CurrentTheme.ForeColor,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(label);

            control.Location = new Point(controlX, y);
            control.Width = controlWidth;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            panel.Controls.Add(control);

            return y + rowHeight;
        }

        // ---------------------------------------------------------
        //  HELPERS
        // ---------------------------------------------------------
        private Label AddLabel(Panel panel, string text, int x, int y)
        {
            var label = new Label { Text = text, Location = new Point(x, y + 2), AutoSize = true, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            panel.Controls.Add(label);
            return label;
        }

        private CheckBox AddCheckBox(Panel panel, string text, int x, ref int y, bool? initialValue = null)
        {
            var cb = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true, Checked = initialValue ?? false, ForeColor = ThemeManager.CurrentTheme.ForeColor };
            panel.Controls.Add(cb);
            y += 25;
            return cb;
        }

        private void LoadSettings()
        {
            _lmEnabledCheckBox.Checked = _settings.LMStudio.EnableLLMAnalysis;
            _lmHostTextBox.Text = _settings.LMStudio.Host;
            _lmPortNumeric.Value = _settings.LMStudio.Port;
            _lmModelTextBox.Text = _settings.LMStudio.ModelName ?? "";
            _lmTempTrackBar.Value = (int)(_settings.LMStudio.Temperature * 100);
            _lmMaxTokensNumeric.Value = _settings.LMStudio.MaxTokens;
            _lmTimeoutNumeric.Value = _settings.LMStudio.RequestTimeoutSeconds;
            _lmStreamingCheckBox.Checked = _settings.LMStudio.EnableStreaming;
        }

        private void SaveSettings()
        {
            SettingsManager.SetLMAnalysisEnabled(_lmEnabledCheckBox.Checked);
            SettingsManager.SetLMStudioHost(_lmHostTextBox.Text);
            SettingsManager.SetLMStudioPort((int)_lmPortNumeric.Value);
            SettingsManager.SetLMStudioModel(_lmModelTextBox.Text);
            SettingsManager.SetLMStudioTemperature(_lmTempTrackBar.Value / 100.0);
            SettingsManager.SetLMStudioMaxTokens((int)_lmMaxTokensNumeric.Value);
            SettingsManager.SetLMStudioTimeout((int)_lmTimeoutNumeric.Value);
            SettingsManager.SetLMStudioStreaming(_lmStreamingCheckBox.Checked);

            SettingsManager.SetAutoAnalyzeOnLoad(_autoAnalyzeLoadCheckBox.Checked);
            SettingsManager.SetAutoAnalyzeOnPatch(_autoAnalyzePatchCheckBox.Checked);
            SettingsManager.SetMaxFunctionSize((int)_maxFunctionSizeNumeric.Value);

            // Theme already saved on dropdown change - just ensure current value is persisted
            var themeName = _themeComboBox.SelectedItem?.ToString() ?? "Dark";
            SettingsManager.Current.UI.Theme = themeName;

            SettingsManager.SetFont(_fontFamilyComboBox.SelectedItem?.ToString() ?? "Consolas", (int)_fontSizeNumeric.Value);
            SettingsManager.SetHexViewUppercase(_hexUppercaseCheckBox.Checked);
            SettingsManager.SetHexBytesPerRow((int)_hexBytesPerRowNumeric.Value);

            _settings.EnableDetailedLogging = _detailedLoggingCheckBox.Checked;
            _settings.LogRetentionDays = (int)_logRetentionNumeric.Value;

            SettingsManager.SaveSettings();
        }

        private void TestConnection(object? sender, EventArgs e)
        {
            string url = $"http://{_lmHostTextBox.Text}:{_lmPortNumeric.Value}";
            MessageBox.Show($"Would test connection to: {url}\n\n(Feature: Call LocalLLMClient.IsHealthyAsync())", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ThemeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Apply theme visually for live preview
            var themeName = _themeComboBox.SelectedItem?.ToString() ?? "Dark";
            var selectedTheme = themeName switch
            {
                "Dark" => Themes.Dark,
                "Light" => Themes.Light,
                "Midnight" => Themes.Midnight,
                "HackerGreen" => Themes.HackerGreen,
                _ => Themes.Dark
            };

            // Update ThemeManager current theme
            ThemeManager._CurrentTheme = selectedTheme;
            
            // Apply to dialog manually 
            BackColor = ThemeManager.CurrentTheme.BackColor;
            ForeColor = ThemeManager.CurrentTheme.ForeColor;
            foreach (Control c in Controls)
                ThemeManager.Apply(c);
            Invalidate(true);

            // Apply to parent form (owner)
            if (this.Owner is Form parentForm)
            {
                ThemeManager.ApplyTheme(parentForm);
                parentForm.Invalidate(true);
                parentForm.Update();
            }

            // Do NOT save settings yet - only on OK button click
        }

        private void CancelThemePreview()
        {
            // Revert to original theme visually without saving
            ThemeManager._CurrentTheme = _originalTheme;
            
            // Revert SettingsManager to original theme name
            SettingsManager.Current.UI.Theme = _originalThemeName;
            
            // Reapply to dialog
            BackColor = ThemeManager.CurrentTheme.BackColor;
            ForeColor = ThemeManager.CurrentTheme.ForeColor;
            foreach (Control c in Controls)
                ThemeManager.Apply(c);
            Invalidate(true);

            // Reapply to parent form (owner)
            if (this.Owner is Form parentForm)
            {
                ThemeManager.ApplyTheme(parentForm);
                parentForm.Invalidate(true);
                parentForm.Update();  // Force immediate repaint
            }

            Close();
        }
    }
}

