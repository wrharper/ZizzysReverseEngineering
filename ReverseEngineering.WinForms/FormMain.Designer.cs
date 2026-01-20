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
            this.symbolTree = new SymbolTreeControl();
            this.graphControl = new GraphControl();
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

            // SymbolTree
            this.symbolTree.Dock = DockStyle.Fill;
            this.symbolTree.Text = "Symbols & Functions";

            // GraphControl
            this.graphControl.Dock = DockStyle.Fill;
            this.graphControl.Text = "Control Flow Graph";

            // LLMPane
            this.llmPane.Dock = DockStyle.Fill;
            this.llmPane.Text = "LM Studio Analysis";

            // LogControl
            this.logControl.Dock = DockStyle.Fill;

            // Compose left side: Hex over Disasm
            this.splitLeft.Panel1.Controls.Add(this.hexEditor);
            this.splitLeft.Panel2.Controls.Add(this.disasmView);

            // Compose right side top: SymbolTree and GraphControl in tabs
            var tabsTop = new TabControl { Dock = DockStyle.Fill };
            tabsTop.TabPages.Add(new TabPage("Symbols") { Controls = { this.symbolTree } });
            tabsTop.TabPages.Add(new TabPage("CFG") { Controls = { this.graphControl } });
            this.splitRight.Panel1.Controls.Add(tabsTop);

            // Compose right side bottom: LLMPane and LogControl in tabs
            var tabsBottom = new TabControl { Dock = DockStyle.Fill };
            tabsBottom.TabPages.Add(new TabPage("LLM Analysis") { Controls = { this.llmPane } });
            tabsBottom.TabPages.Add(new TabPage("Log") { Controls = { this.logControl } });
            this.splitRight.Panel2.Controls.Add(tabsBottom);

            // Add patch panel on top of right side
            var rightWithPatch = new Panel { Dock = DockStyle.Fill };
            this.patchPanel.Dock = DockStyle.Top;
            this.patchPanel.Height = 100;
            rightWithPatch.Controls.Add(this.splitRight);
            rightWithPatch.Controls.Add(this.patchPanel);

            // Compose main layout
            this.splitMain.Panel1.Controls.Add(this.splitLeft);
            this.splitMain.Panel2.Controls.Add(rightWithPatch);

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