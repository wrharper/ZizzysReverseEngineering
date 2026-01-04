// File: FormMain.Designer.cs
using System.Windows.Forms;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms
{
    partial class FormMain
    {
        private MenuStrip menuStrip1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusFile;
        private ToolStripStatusLabel statusOffset;
        private ToolStripStatusLabel statusSelection;

        private SplitContainer splitMain;
        private SplitContainer splitLeft;

        private HexEditorControl hexEditor;
        private DisassemblyControl disasmView;
        private PatchPanel patchPanel;
        private LogControl logControl;

        private void InitializeComponent()
        {
            this.menuStrip1 = new MenuStrip();
            this.statusStrip1 = new StatusStrip();
            this.statusFile = new ToolStripStatusLabel();
            this.statusOffset = new ToolStripStatusLabel();
            this.statusSelection = new ToolStripStatusLabel();

            this.splitMain = new SplitContainer();
            this.splitLeft = new SplitContainer();

            this.hexEditor = new HexEditorControl();
            this.disasmView = new DisassemblyControl();
            this.patchPanel = new PatchPanel();
            this.logControl = new LogControl();

            // MenuStrip
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1200, 24);

            // StatusStrip
            this.statusStrip1.Items.AddRange(new ToolStripItem[]
            {
                this.statusFile,
                this.statusOffset,
                this.statusSelection
            });
            this.statusStrip1.Location = new System.Drawing.Point(0, 700);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1200, 22);

            this.statusFile.Text = "No file loaded";
            this.statusOffset.Text = "Offset: -";
            this.statusSelection.Text = "Selection: -";

            // SplitMain
            this.splitMain.Dock = DockStyle.Fill;
            this.splitMain.Orientation = Orientation.Vertical;
            this.splitMain.SplitterDistance = 900;

            // SplitLeft
            this.splitLeft.Dock = DockStyle.Fill;
            this.splitLeft.Orientation = Orientation.Horizontal;
            this.splitLeft.SplitterDistance = 450;

            // HexEditor
            this.hexEditor.Dock = DockStyle.Fill;

            // DisasmView
            this.disasmView.Dock = DockStyle.Fill;

            // PatchPanel
            this.patchPanel.Dock = DockStyle.Top;
            this.patchPanel.Height = 200;

            // LogControl
            this.logControl.Dock = DockStyle.Fill;

            // Compose left side
            this.splitLeft.Panel1.Controls.Add(this.hexEditor);
            this.splitLeft.Panel2.Controls.Add(this.disasmView);

            // Compose right side
            this.splitMain.Panel1.Controls.Add(this.splitLeft);
            this.splitMain.Panel2.Controls.Add(this.logControl);
            this.splitMain.Panel2.Controls.Add(this.patchPanel);

            // Form
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);

            this.MainMenuStrip = this.menuStrip1;
            this.Text = "Zizzy RE Tool (WinForms)";
            this.WindowState = FormWindowState.Maximized;

            this.hexEditor.ContextMenuStrip = CreateHexEditorMenu();
            this.disasmView.ContextMenuStrip = CreateCopyPasteMenu();
        }
    }
}