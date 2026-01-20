using System;
using System.Windows.Forms;
using ReverseEngineering.Core.LLM;

namespace ReverseEngineering.WinForms.LLM
{
    /// <summary>
    /// WinForms control to display LM Studio analysis results
    /// Shows pseudocode, explanations, patterns, and function signatures
    /// </summary>
    public partial class LLMPane : UserControl
    {
        private readonly RichTextBox _resultBox;
        private readonly Label _statusLabel;
        private readonly Button _copyButton;
        private bool _isAnalyzing;

        public LLMPane()
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            // Panel for buttons
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };

            _statusLabel = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Left,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Width = 200,
                ForeColor = System.Drawing.SystemColors.GrayText
            };
            buttonPanel.Controls.Add(_statusLabel);

            _copyButton = new Button
            {
                Text = "Copy",
                Dock = DockStyle.Right,
                Width = 70,
                Margin = new Padding(5)
            };
            _copyButton.Click += (s, e) => CopyToClipboard();
            buttonPanel.Controls.Add(_copyButton);

            // Rich text box for results
            _resultBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = true,
                Font = new System.Drawing.Font("Consolas", 10),
                BackColor = System.Drawing.Color.White,
                Text = "No analysis yet. Select an instruction or function and run analysis."
            };
            _resultBox.LinkClicked += (s, e) =>
            {
                if (e.LinkText.StartsWith("http"))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true });
                }
            };

            Controls.Add(_resultBox);
            Controls.Add(buttonPanel);
        }

        private void SetupUI()
        {
            // Already done in InitializeComponent
        }

        public void Clear()
        {
            _resultBox.Text = "Ready for analysis.";
            _statusLabel.Text = "Ready";
            _isAnalyzing = false;
            _copyButton.Enabled = true;
        }

        public void DisplayResult(string title, string content)
        {
            _resultBox.Clear();
            
            // Title in bold
            _resultBox.SelectionFont = new System.Drawing.Font(_resultBox.Font, System.Drawing.FontStyle.Bold);
            _resultBox.AppendText($"{title}\n");
            _resultBox.AppendText("".PadRight(title.Length, '=') + "\n\n");
            
            // Content
            _resultBox.SelectionFont = new System.Drawing.Font(_resultBox.Font, System.Drawing.FontStyle.Regular);
            _resultBox.AppendText(content);
            
            _statusLabel.Text = $"Displayed: {title}";
            _isAnalyzing = false;
            _copyButton.Enabled = true;
        }

        public void DisplayError(string error)
        {
            _resultBox.Clear();
            _resultBox.SelectionColor = System.Drawing.Color.Red;
            _resultBox.AppendText("ERROR\n");
            _resultBox.SelectionColor = System.Drawing.SystemColors.WindowText;
            _resultBox.AppendText(error);
            _statusLabel.Text = "Error";
            _isAnalyzing = false;
            _copyButton.Enabled = true;
        }

        public void SetAnalyzing(string task)
        {
            _statusLabel.Text = $"Analyzing: {task}...";
            _statusLabel.ForeColor = System.Drawing.Color.Blue;
            _resultBox.Text = $"Analyzing: {task}...\n\nPlease wait...";
            _isAnalyzing = true;
            _copyButton.Enabled = false;
        }

        public void CopyToClipboard()
        {
            if (!string.IsNullOrWhiteSpace(_resultBox.Text))
            {
                Clipboard.SetText(_resultBox.Text);
                _statusLabel.Text = "Copied to clipboard";
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 2000;
                timer.Tick += (s, e) =>
                {
                    _statusLabel.Text = "Ready";
                    timer.Stop();
                };
                timer.Start();
            }
        }

        public bool IsAnalyzing => _isAnalyzing;
    }
}
