#nullable disable

namespace ReverseEngineering.WinForms.LLM
{
    public partial class LLMPane : UserControl
    {
        private RichTextBox _conversationBox;
        private TextBox _inputBox;
        private Button _sendButton;
        private Label _statusLabel;
        private bool _isProcessing;
        private bool _isStreaming;

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

            // Middle: Conversation display
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

            // Bottom: Input panel
            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(5)
            };

            _inputBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = true,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9),
                PlaceholderText = "Ask a question or request a patch..."
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
        private bool IsUserAtBottom()
        {
            int visibleLines = _conversationBox.Height / _conversationBox.Font.Height;
            int firstVisible = _conversationBox.GetLineFromCharIndex(_conversationBox.GetCharIndexFromPosition(new Point(0, 0)));
            int lastVisible = firstVisible + visibleLines;

            int totalLines = _conversationBox.Lines.Length;

            // If the last visible line is within 2 lines of the end, consider it "at bottom"
            return lastVisible >= totalLines - 2;
        }
        private void SetupUI()
        {
            // Already initialized
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

            _inputBox.Clear();

            UserQuery?.Invoke(this, new QueryEventArgs { Query = query });
        }

        // -----------------------------
        // THREAD-SAFE UI METHODS BELOW
        // -----------------------------

        public void DisplayResponse(string response)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayResponse(response)));
                return;
            }

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

        public void StartStreamingResponse()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(StartStreamingResponse));
                return;
            }

            bool shouldScroll = IsUserAtBottom();

            _isStreaming = true;
            _conversationBox.SelectionColor = Color.LimeGreen;
            _conversationBox.SelectionFont = new Font(_conversationBox.Font, FontStyle.Bold);
            _conversationBox.AppendText("\n[AI]: ");
            _conversationBox.SelectionColor = ThemeManager.CurrentTheme.ForeColor;
            _conversationBox.SelectionFont = new Font(_conversationBox.Font, FontStyle.Regular);

            if (shouldScroll)
                _conversationBox.ScrollToCaret();
        }

        public void AppendStreamedChunk(string chunk)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendStreamedChunk(chunk)));
                return;
            }

            bool shouldScroll = IsUserAtBottom();

            if (!_isStreaming)
                StartStreamingResponse();

            _conversationBox.AppendText(chunk);

            if (shouldScroll)
                _conversationBox.ScrollToCaret();
        }

        public void FinishStreamingResponse()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(FinishStreamingResponse));
                return;
            }

            bool shouldScroll = IsUserAtBottom();

            _isStreaming = false;
            _conversationBox.AppendText("\n");

            if (shouldScroll)
                _conversationBox.ScrollToCaret();

            _statusLabel.ForeColor = SystemColors.GrayText;
            _statusLabel.Text = "Ready";
            _isProcessing = false;
            _sendButton.Enabled = true;
        }

        public void DisplayError(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayError(error)));
                return;
            }

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
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetAnalyzing(task)));
                return;
            }

            _statusLabel.Text = task;
            _statusLabel.ForeColor = System.Drawing.Color.Blue;
            _isProcessing = true;
            _sendButton.Enabled = false;
        }

        public bool IsProcessing => _isProcessing;

        public void Clear()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(Clear));
                return;
            }

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

    public class QueryEventArgs : EventArgs
    {
        public string Query { get; set; } = string.Empty;
    }
}