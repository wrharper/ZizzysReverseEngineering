using System;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms
{
    public class LogControl : UserControl
    {
        private readonly TextBox _textBox;

        public LogControl()
        {
            _textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGray
            };

            Controls.Add(_textBox);

            // Subscribe to Logger events to capture all logging
            Logger.LogAdded += OnLogAdded;
        }

        /// <summary>
        /// Handle logger events - display them in the log control.
        /// </summary>
        private void OnLogAdded(LogEntry entry)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<LogEntry>(OnLogAdded), entry);
                return;
            }

            // Display the log entry
            _textBox.AppendText(entry.ToString() + Environment.NewLine);
        }

        public void Append(string message)
        {
            _textBox.AppendText(message + Environment.NewLine);
        }
    }
}