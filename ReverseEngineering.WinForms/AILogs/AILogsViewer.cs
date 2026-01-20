using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core.AILogs;

namespace ReverseEngineering.WinForms.AILogs
{
    /// <summary>
    /// AI Logs viewer with organized tabs for Prompts, Outputs, and Changes
    /// </summary>
    public partial class AILogsViewer : Form
    {
        private readonly AILogsManager _logsManager;
        private string _selectedOperation = "";
        private DateTime _selectedDate = DateTime.Today;

        private TabControl _tabControl = null!;
        private TextBox _promptTextBox = null!;
        private TextBox _outputTextBox = null!;
        private ListBox _changesListBox = null!;
        private ListBox _logsListBox = null!;
        private ComboBox _operationComboBox = null!;
        private Label _statsLabel = null!;

        public AILogsViewer(AILogsManager logsManager)
        {
            _logsManager = logsManager ?? throw new ArgumentNullException(nameof(logsManager));
            InitializeComponents();
            LoadOperations();
        }

        private void InitializeComponents()
        {
            Text = "AI Logs Viewer";
            Size = new Size(900, 700);
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.FromArgb(220, 220, 220);
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterParent;

            // Main split
            var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Controls.Add(mainPanel);

            // Top: Operation filter + stats
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(5) };
            mainPanel.Controls.Add(topPanel);

            var label = new Label { Text = "Operation:", Location = new Point(10, 10), AutoSize = true, ForeColor = Color.FromArgb(200, 200, 200) };
            topPanel.Controls.Add(label);

            _operationComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 250,
                Location = new Point(100, 8),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            _operationComboBox.SelectedIndexChanged += (s, e) => LoadLogs();
            topPanel.Controls.Add(_operationComboBox);

            var refreshBtn = new Button
            {
                Text = "Refresh",
                Width = 80,
                Height = 28,
                Location = new Point(360, 8),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            refreshBtn.Click += (s, e) => LoadOperations();
            topPanel.Controls.Add(refreshBtn);

            _statsLabel = new Label
            {
                Text = "Loading...",
                Location = new Point(450, 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150)
            };
            topPanel.Controls.Add(_statsLabel);

            // Middle: Split container (left = logs list, right = details)
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 200
            };
            mainPanel.Controls.Add(splitContainer);

            // Left: Logs list
            _logsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            _logsListBox.SelectedIndexChanged += (s, e) => DisplayLog();
            splitContainer.Panel1.Controls.Add(_logsListBox);

            // Right: Tabs for details
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            splitContainer.Panel2.Controls.Add(_tabControl);

            // Tab 1: Prompt
            var promptTab = new TabPage { Text = "Prompt", BackColor = Color.FromArgb(45, 45, 48) };
            _promptTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9)
            };
            promptTab.Controls.Add(_promptTextBox);
            _tabControl.TabPages.Add(promptTab);

            // Tab 2: Output
            var outputTab = new TabPage { Text = "Output", BackColor = Color.FromArgb(45, 45, 48) };
            _outputTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9)
            };
            outputTab.Controls.Add(_outputTextBox);
            _tabControl.TabPages.Add(outputTab);

            // Tab 3: Changes
            var changesTab = new TabPage { Text = "Changes", BackColor = Color.FromArgb(45, 45, 48) };
            _changesListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            changesTab.Controls.Add(_changesListBox);
            _tabControl.TabPages.Add(changesTab);

            // Bottom: Buttons
            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            mainPanel.Controls.Add(buttonPanel);

            var clearBtn = new Button
            {
                Text = "Clear All Logs",
                Width = 120,
                Height = 32,
                Location = new Point(10, 8),
                BackColor = Color.FromArgb(200, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            clearBtn.Click += (s, e) =>
            {
                if (MessageBox.Show("Clear all AI logs? This cannot be undone.", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _logsManager.ClearAllLogs();
                    LoadOperations();
                }
            };
            buttonPanel.Controls.Add(clearBtn);

            var exportBtn = new Button
            {
                Text = "Export as JSON",
                Width = 120,
                Height = 32,
                Location = new Point(140, 8),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            exportBtn.Click += (s, e) =>
            {
                var json = _logsManager.ExportLogsAsJson();
                var dlg = new SaveFileDialog { Filter = "JSON Files|*.json", FileName = "ai_logs.json" };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(dlg.FileName, json);
                    MessageBox.Show($"Exported to {dlg.FileName}");
                }
            };
            buttonPanel.Controls.Add(exportBtn);

            var closeBtn = new Button
            {
                Text = "Close",
                Width = 100,
                Height = 32,
                Location = new Point(780, 8),
                BackColor = Color.FromArgb(63, 63, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            buttonPanel.Controls.Add(closeBtn);

            AcceptButton = closeBtn;
        }

        private void LoadOperations()
        {
            _operationComboBox.Items.Clear();
            var operations = _logsManager.GetAvailableOperations();

            if (operations.Count == 0)
            {
                _operationComboBox.Items.Add("(No logs)");
                _operationComboBox.SelectedIndex = 0;
                _logsListBox.Items.Clear();
                UpdateStats();
                return;
            }

            foreach (var op in operations)
                _operationComboBox.Items.Add(op);

            if (_operationComboBox.Items.Count > 0)
                _operationComboBox.SelectedIndex = 0;

            UpdateStats();
        }

        private void LoadLogs()
        {
            _logsListBox.Items.Clear();
            _promptTextBox.Clear();
            _outputTextBox.Clear();
            _changesListBox.Items.Clear();

            if (_operationComboBox.SelectedIndex < 0)
                return;

            _selectedOperation = _operationComboBox.SelectedItem?.ToString() ?? "";
            var logs = _logsManager.GetLogsByOperation(_selectedOperation);

            foreach (var log in logs)
            {
                _logsListBox.Items.Add($"{log.Timestamp:HH:mm:ss} - {log.Status} ({log.DurationMs}ms)");
            }
        }

        private void DisplayLog()
        {
            if (_logsListBox.SelectedIndex < 0)
                return;

            var logs = _logsManager.GetLogsByOperation(_selectedOperation);
            if (_logsListBox.SelectedIndex >= logs.Count)
                return;

            var log = logs[_logsListBox.SelectedIndex];

            _promptTextBox.Text = log.Prompt;
            _outputTextBox.Text = log.AIOutput;

            _changesListBox.Items.Clear();
            foreach (var change in log.Changes)
            {
                var item = $"[0x{change.Offset:X8}] {change.OriginalByte:X2} → {change.NewByte:X2}";
                if (!string.IsNullOrEmpty(change.AssemblyBefore))
                    item += $" | {change.AssemblyBefore} → {change.AssemblyAfter}";

                _changesListBox.Items.Add(item);
            }
        }

        private void UpdateStats()
        {
            var (total, opCount, oldest, newest) = _logsManager.GetStatistics();
            var statsText = $"Total Logs: {total} | Operations: {opCount}";
            if (oldest.HasValue && newest.HasValue)
                statsText += $" | Range: {oldest:MMdd HH:mm} → {newest:MMdd HH:mm}";

            _statsLabel.Text = statsText;
        }
    }
}
