using System;
using System.Windows.Forms;
using ReverseEngineering.Core.LLM;

namespace ReverseEngineering.WinForms.LLM
{
    /// <summary>
    /// Master-level interactive RE tool: Chat interface with LM Studio
    /// Users query the AI about the binary, AI can make patches/byte changes on request
    /// Full binary context for intelligent analysis
    /// </summary>
    public partial class LLMPane : UserControl
    {
        private RichTextBox _conversationBox;
        private TextBox _inputBox;
        private Button _sendButton;
        private Label _statusLabel;
        private bool _isProcessing;

        public event EventHandler<QueryEventArgs>? UserQuery;

        public LLMPane()
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            // Top: Status bar
            var statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(5, 3, 5, 3)
            };

            _statusLabel = new Label
            {
                Text = "Ready - Ask questions about the binary or request patches",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                ForeColor = System.Drawing.SystemColors.GrayText,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            statusPanel.Controls.Add(_statusLabel);
            Controls.Add(statusPanel);

            // Middle: Conversation display (read-only)
            _conversationBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = true,
                Font = new System.Drawing.Font("Consolas", 10),
                BackColor = ThemeManager.CurrentTheme.BackColor,
                ForeColor = ThemeManager.CurrentTheme.ForeColor,
                Text = "AI RE Assistant ready.\n\nAsk questions like:\n" +
                       "- What does this function do?\n" +
                       "- NOP out the call at 0x401000\n" +
                       "- Explain the loop structure here\n" +
                       "- What are these registers doing?"
            };
            Controls.Add(_conversationBox);

            // Bottom: Input panel (question/request)
            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(5)
            };

            _inputBox = new TextBox
            {
                Multiline = true,
                AcceptsTab = false,
                AcceptsReturn = true,
                WordWrap = true,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9),
                PlaceholderText = "Ask a question or request a patch...",
                Margin = new Padding(0, 0, 5, 0)
            };
            inputPanel.Controls.Add(_inputBox);

            _sendButton = new Button
            {
                Text = "Send",
                Dock = DockStyle.Right,
                Width = 70,
                Height = 70
            };
            _sendButton.Click += OnSendClick;
            inputPanel.Controls.Add(_sendButton);

            Controls.Add(inputPanel);
        }

        private void SetupUI()
        {
            // Already initialized in InitializeComponent
        }

        private void OnSendClick(object? sender, EventArgs e)
        {
            if (_isProcessing) return;
            
            var query = _inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            _isProcessing = true;
            _sendButton.Enabled = false;
            _statusLabel.Text = "Processing...";
            _statusLabel.ForeColor = System.Drawing.Color.Blue;

            // Display user message
            _conversationBox.SelectionColor = ThemeManager.CurrentTheme.Accent;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Bold);
            _conversationBox.AppendText("\n[You]: ");
            _conversationBox.SelectionColor = ThemeManager.CurrentTheme.ForeColor;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Regular);
            _conversationBox.AppendText(query + "\n");

            // Clear input
            _inputBox.Clear();

            // Raise event for external handler
            UserQuery?.Invoke(this, new QueryEventArgs { Query = query });
        }

        public void DisplayResponse(string response)
        {
            _conversationBox.SelectionColor = System.Drawing.Color.LimeGreen;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Bold);
            _conversationBox.AppendText("\n[AI]: ");
            _conversationBox.SelectionColor = ThemeManager.CurrentTheme.ForeColor;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Regular);
            _conversationBox.AppendText(response + "\n");

            _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            _statusLabel.Text = "Ready";
            _isProcessing = false;
            _sendButton.Enabled = true;
        }

        public void DisplayError(string error)
        {
            _conversationBox.SelectionColor = System.Drawing.Color.Red;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Bold);
            _conversationBox.AppendText("\n[ERROR]: ");
            _conversationBox.SelectionColor = ThemeManager.CurrentTheme.ForeColor;
            _conversationBox.SelectionFont = new System.Drawing.Font(_conversationBox.Font, System.Drawing.FontStyle.Regular);
            _conversationBox.AppendText(error + "\n");

            _statusLabel.ForeColor = System.Drawing.Color.Red;
            _statusLabel.Text = "Error occurred";
            _isProcessing = false;
            _sendButton.Enabled = true;
        }

        public void SetAnalyzing(string task)
        {
            _statusLabel.Text = $"Processing: {task}...";
            _statusLabel.ForeColor = System.Drawing.Color.Blue;
            _isProcessing = true;
            _sendButton.Enabled = false;
        }

        public bool IsProcessing => _isProcessing;

        public void Clear()
        {
            _conversationBox.Clear();
            _conversationBox.AppendText("AI RE Assistant ready.\n\nAsk questions like:\n" +
                                        "- What does this function do?\n" +
                                        "- NOP out the call at 0x401000\n" +
                                        "- Explain the loop structure here\n" +
                                        "- What are these registers doing?");
            _statusLabel.Text = "Ready";
            _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            _isProcessing = false;
            _sendButton.Enabled = true;
        }
    }

    /// <summary>
    /// Event args for user queries
    /// </summary>
    public class QueryEventArgs : EventArgs
    {
        public string Query { get; set; } = string.Empty;
    }
}

