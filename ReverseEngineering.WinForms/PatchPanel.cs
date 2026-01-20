using ReverseEngineering.Core;
using ReverseEngineering.WinForms.HexEditor;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    public class PatchPanel : UserControl
    {
        private CoreEngine _core = null!;
        private HexEditorControl _hexEditor = null!;
        private DisassemblyControl _disasmView = null!;
        private LogControl _log = null!;

        private TextBox txtOffset = null!;
        private TextBox txtBytes = null!;
        private Button btnApply = null!;

        public PatchPanel()
        {
            InitializeUi();
        }

        public void Initialize(CoreEngine core, HexEditorControl hex, DisassemblyControl disasm, LogControl log)
        {
            _core = core;
            _hexEditor = hex;
            _disasmView = disasm;
            _log = log;
        }

        private void InitializeUi()
        {
            Dock = DockStyle.Top;
            Height = 120;
            BackColor = Color.FromArgb(30, 30, 30);

            var lblOffset = new Label { Text = "Offset (hex):", ForeColor = Color.White, Left = 8, Top = 12, AutoSize = true };
            txtOffset = new TextBox { Left = 100, Top = 8, Width = 100 };

            var lblBytes = new Label { Text = "Bytes (hex):", ForeColor = Color.White, Left = 8, Top = 44, AutoSize = true };
            txtBytes = new TextBox { Left = 100, Top = 40, Width = 300 };

            btnApply = new Button { Text = "Apply Patch", Left = 100, Top = 72, Width = 120 };
            btnApply.Click += BtnApply_Click;

            Controls.Add(lblOffset);
            Controls.Add(txtOffset);
            Controls.Add(lblBytes);
            Controls.Add(txtBytes);
            Controls.Add(btnApply);
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            if (_core == null) return;

            try
            {
                if (!int.TryParse(txtOffset.Text, System.Globalization.NumberStyles.HexNumber, null, out int offset))
                {
                    MessageBox.Show("Invalid offset. Use hex (e.g., 1A2B).");
                    return;
                }

                var parts = txtBytes.Text.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var bytes = new byte[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    bytes[i] = Convert.ToByte(parts[i], 16);

                _core.ApplyPatch(offset, bytes);

                _hexEditor.Invalidate();
                _disasmView.SetInstructions(_core.Disassembly);
                _log.Append($"Patch applied at 0x{offset:X} ({bytes.Length} bytes).");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Patch failed: {ex.Message}");
            }
        }
    }
}