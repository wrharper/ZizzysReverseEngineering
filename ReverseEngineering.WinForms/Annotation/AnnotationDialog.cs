using System;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.Core.ProjectSystem;

namespace ReverseEngineering.WinForms.Annotation
{
    /// <summary>
    /// Dialog to edit annotations for an address.
    /// </summary>
    public partial class AnnotationDialog : Form
    {
        private readonly ulong _address;
        private readonly AnnotationStore _store;

        public AnnotationDialog(ulong address, AnnotationStore store)
        {
            _address = address;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            InitializeComponent();
            LoadAnnotation();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = $"Annotate 0x{_address:X}";
            Size = new Size(500, 300);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;

            // Layout
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(8)
            };

            // Function Name
            panel.Controls.Add(new Label { Text = "Function Name:", AutoSize = true }, 0, 0);
            var funcNameBox = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(funcNameBox, 1, 0);
            funcNameBox.Name = "funcNameBox";

            // Symbol Type
            panel.Controls.Add(new Label { Text = "Symbol Type:", AutoSize = true }, 0, 1);
            var typeBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Items = { "function", "data", "import", "export", "string" }
            };
            panel.Controls.Add(typeBox, 1, 1);
            typeBox.Name = "typeBox";

            // Comment
            panel.Controls.Add(new Label { Text = "Comment:", AutoSize = true }, 0, 2);
            var commentBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 100 };
            panel.Controls.Add(commentBox, 1, 2);
            commentBox.Name = "commentBox";

            // Buttons
            var btnPanel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            var okBtn = new Button { Text = "OK", Width = 80, Height = 24 };
            okBtn.Click += (s, e) => SaveAndClose(funcNameBox.Text, typeBox.Text, commentBox.Text);
            btnPanel.Controls.Add(okBtn);

            var cancelBtn = new Button { Text = "Cancel", Width = 80, Height = 24 };
            cancelBtn.Click += (s, e) => Close();
            btnPanel.Controls.Add(cancelBtn);

            var deleteBtn = new Button { Text = "Delete", Width = 80, Height = 24 };
            deleteBtn.Click += (s, e) => DeleteAndClose();
            btnPanel.Controls.Add(deleteBtn);

            panel.Controls.Add(btnPanel, 0, 4);
            panel.SetColumnSpan(btnPanel, 2);

            Controls.Add(panel);
            ResumeLayout(false);
        }

        private void LoadAnnotation()
        {
            var ann = _store.GetAnnotation(_address);
            if (ann == null)
                return;

            if (Controls["funcNameBox"] is TextBox funcBox)
                funcBox.Text = ann.FunctionName ?? "";

            if (Controls["commentBox"] is TextBox commentBox)
                commentBox.Text = ann.Comment ?? "";

            if (Controls["typeBox"] is ComboBox typeBox && ann.SymbolType != null)
                typeBox.SelectedItem = ann.SymbolType;
        }

        private void SaveAndClose(string funcName, string symbolType, string comment)
        {
            if (!string.IsNullOrWhiteSpace(funcName))
                _store.SetFunctionName(_address, funcName);

            if (!string.IsNullOrWhiteSpace(symbolType))
                _store.SetSymbolType(_address, symbolType);

            if (!string.IsNullOrWhiteSpace(comment))
                _store.SetComment(_address, comment);

            Close();
        }

        private void DeleteAndClose()
        {
            if (MessageBox.Show("Delete this annotation?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _store.RemoveAnnotation(_address);
                Close();
            }
        }
    }
}
