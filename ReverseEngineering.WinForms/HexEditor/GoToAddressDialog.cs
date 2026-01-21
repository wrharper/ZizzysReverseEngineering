using System;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class GoToAddressDialog : Form
    {
        private readonly TextBox _textAddress;
        private readonly Button _btnOK;
        private readonly Button _btnCancel;

        public ulong Address { get; private set; }

        public GoToAddressDialog(ulong currentAddress = 0)
        {
            _textAddress = new TextBox();
            _btnOK = new Button();
            _btnCancel = new Button();

            InitializeComponent();
            _textAddress.Text = currentAddress.ToString("X");
            _textAddress.SelectAll();
        }

        private void InitializeComponent()
        {
            var lblAddress = new Label();

            this.SuspendLayout();

            // Label
            lblAddress.AutoSize = true;
            lblAddress.Location = new System.Drawing.Point(12, 15);
            lblAddress.Text = "Enter address (hex):";

            // TextBox
            _textAddress.Location = new System.Drawing.Point(12, 35);
            _textAddress.Size = new System.Drawing.Size(260, 20);
            _textAddress.Font = new System.Drawing.Font("Consolas", 10);

            // OK Button
            _btnOK.Location = new System.Drawing.Point(117, 65);
            _btnOK.Size = new System.Drawing.Size(75, 23);
            _btnOK.Text = "Go To";
            _btnOK.Click += BtnOK_Click;

            // Cancel Button
            _btnCancel.Location = new System.Drawing.Point(198, 65);
            _btnCancel.Size = new System.Drawing.Size(75, 23);
            _btnCancel.Text = "Cancel";
            _btnCancel.DialogResult = DialogResult.Cancel;

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 104);
            this.Controls.Add(lblAddress);
            this.Controls.Add(_textAddress);
            this.Controls.Add(_btnOK);
            this.Controls.Add(_btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Go To Address";
            this.AcceptButton = _btnOK;
            this.CancelButton = _btnCancel;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            string input = _textAddress.Text.Trim();
            if (ulong.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out ulong result))
            {
                Address = result;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Invalid hex address format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
