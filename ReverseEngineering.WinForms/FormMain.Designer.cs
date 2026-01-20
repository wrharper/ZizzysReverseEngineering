// File: FormMain.Designer.cs
using System.Windows.Forms;
using ReverseEngineering.WinForms.HexEditor;
using ReverseEngineering.WinForms.SymbolView;
using ReverseEngineering.WinForms.GraphView;
using ReverseEngineering.WinForms.LLM;

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
        private SplitContainer splitRight;

        private HexEditorControl hexEditor;
        private DisassemblyControl disasmView;
        private PatchPanel patchPanel;
        private LogControl logControl;
        private SymbolTreeControl symbolTree;
        private GraphControl graphControl;
        private LLMPane llmPane;

        private void InitializeComponent()
        {
            this.menuStrip1 = new MenuStrip();
            this.statusStrip1 = new StatusStrip();
            this.statusFile = new ToolStripStatusLabel();
            this.statusOffset = new ToolStripStatusLabel();
            this.statusSelection = new ToolStripStatusLabel();

            this.splitMain = new SplitContainer();
            this.splitLeft = new SplitContainer();
            this.splitRight = new SplitContainer();

            this.hexEditor = new HexEditorControl();
            this.disasmView = new DisassemblyControl();
            this.patchPanel = new PatchPanel();
            this.logControl = new LogControl();
            this.symbolTree = null!;  // Will be initialized in FormMain.cs after CoreEngine is ready
            this.graphControl = null!;  // Will be initialized in FormMain.cs after CoreEngine is ready
            this.llmPane = new LLMPane();

            // MenuStrip
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1400, 24);

            // StatusStrip
            this.statusStrip1.Items.AddRange(new ToolStripItem[]
            {
                this.statusFile,
                this.statusOffset,
                this.statusSelection
            });
            this.statusStrip1.Location = new System.Drawing.Point(0, 700);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1400, 22);

            this.statusFile.Text = "No file loaded";
            this.statusOffset.Text = "Offset: -";
            this.statusSelection.Text = "Selection: -";

            // SplitMain (Left: Hex/Disasm, Right: Analysis/LLM/Log)
            this.splitMain.Dock = DockStyle.Fill;
            this.splitMain.Orientation = Orientation.Vertical;
            this.splitMain.SplitterDistance = 750;

            // SplitLeft (Top: Hex, Bottom: Disasm)
            this.splitLeft.Dock = DockStyle.Fill;
            this.splitLeft.Orientation = Orientation.Horizontal;
            this.splitLeft.SplitterDistance = 350;

            // SplitRight (Top: SymbolTree/Graph tabs, Bottom: LLM/Log tabs)
            this.splitRight.Dock = DockStyle.Fill;
            this.splitRight.Orientation = Orientation.Horizontal;
            this.splitRight.SplitterDistance = 350;

            // HexEditor
            this.hexEditor.Dock = DockStyle.Fill;

            // DisasmView
            this.disasmView.Dock = DockStyle.Fill;

            // PatchPanel
            this.patchPanel.Dock = DockStyle.Top;
            this.patchPanel.Height = 150;

            // NOTE: SymbolTree, GraphControl, LLMPane will be configured in FormMain.cs
            // after CoreEngine initialization

            // Compose left side: Hex over Disasm
            this.splitLeft.Panel1.Controls.Add(this.hexEditor);
            this.splitLeft.Panel2.Controls.Add(this.disasmView);

            // NOTE: Right side tab controls will be configured in FormMain.cs
            // after symbolTree, graphControl, llmPane, logControl are initialized

            // Form
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);

            this.MainMenuStrip = this.menuStrip1;
            this.Text = "Zizzy RE Tool (WinForms + LM Studio)";
            this.WindowState = FormWindowState.Maximized;

            this.hexEditor.ContextMenuStrip = CreateHexEditorMenu();
            this.disasmView.ContextMenuStrip = CreateCopyPasteMenu();
        }
    }
}