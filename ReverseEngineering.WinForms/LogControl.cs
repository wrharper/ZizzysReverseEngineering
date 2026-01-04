using System.Windows.Forms;

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
        }

        public void Append(string message)
        {
            _textBox.AppendText(message + Environment.NewLine);
        }
    }
}