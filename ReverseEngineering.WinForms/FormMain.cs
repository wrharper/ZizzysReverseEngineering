using ReverseEngineering.Core;
using ReverseEngineering.Core.LLM;
using ReverseEngineering.WinForms.HexEditor;
using ReverseEngineering.WinForms.MainWindow;
using ReverseEngineering.WinForms.LLM;
using ReverseEngineering.WinForms.SymbolView;
using ReverseEngineering.WinForms.GraphView;
using ReverseEngineering.WinForms.StringView;
using System;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    public partial class FormMain : Form
    {
        private readonly CoreEngine _core = new();
        private SplitContainer? _verticalSplit;

        private readonly MainMenuController _menuController;
        private readonly ThemeMenuController _themeController;
        private readonly HexEditorController _hexController;
        private readonly DisassemblyController _disasmController;
        private readonly AnalysisController? _analysisController;
        
        private PEInfoControl? peInfoControl;
        private StringsControl? stringsControl;

        public FormMain()
        {
            InitializeComponent();

            // ---------------------------------------------------------
            //  INITIALIZE CONTROLS THAT REQUIRE CoreEngine
            // ---------------------------------------------------------
            symbolTree = new SymbolTreeControl(_core);
            graphControl = new GraphControl(_core);
            peInfoControl = new PEInfoControl();
            stringsControl = new StringsControl(_core);

            // ---------------------------------------------------------
            //  COMPOSE LAYOUT (after controls are initialized)
            // ---------------------------------------------------------
            ComposeLayout();

            // ---------------------------------------------------------
            //  LLM CLIENT (Optional, for LM Studio integration)
            // ---------------------------------------------------------
            var llmClient = new LocalLLMClient();

            // ---------------------------------------------------------
            //  CONTROLLERS (RichTextBox disassembly)
            // ---------------------------------------------------------
            _disasmController = new DisassemblyController(disasmView, hexEditor, _core);

            _analysisController = new AnalysisController(_core, symbolTree, graphControl, llmClient, llmPane, null, stringsControl);

            _menuController = new MainMenuController(
                this,
                menuStrip1,
                hexEditor,
                logControl,
                _disasmController,
                statusFile,
                _core,
                _analysisController,
                null,
                peInfoControl,
                llmPane
            );

            _themeController = new ThemeMenuController(
                this,
                menuStrip1
            );

            _hexController = new HexEditorController(
                hexEditor,
                statusOffset,
                statusSelection,
                _disasmController,
                _core
            );

            // ---------------------------------------------------------
            //  EVENT WIRING
            // ---------------------------------------------------------
            hexEditor.BytesChanged += HexEditor_BytesChanged;
            hexEditor.SelectionChanged += HexEditor_SelectionChanged;

            disasmView.InstructionSelected += DisasmView_InstructionSelected;

            // ---------------------------------------------------------
            //  INITIALIZE THEME
            // ---------------------------------------------------------
            ThemeManager.Initialize();  // Load theme from settings
            ThemeManager.ApplyTheme(this);  // Apply current theme
        }

        // ---------------------------------------------------------
        //  LAYOUT COMPOSITION
        // ---------------------------------------------------------
        private void ComposeLayout()
        {
            // Configure controls now that they're initialized
            symbolTree.Dock = DockStyle.Fill;
            symbolTree.Text = "Symbols & Functions";

            graphControl.Dock = DockStyle.Fill;
            graphControl.Text = "Control Flow Graph";

            logControl.Dock = DockStyle.Fill;

            // LEFT SIDE: Hex Editor + Disassembly in tabs
            var leftTabs = new TabControl { Dock = DockStyle.Fill };
            hexEditor.Dock = DockStyle.Fill;
            disasmView.Dock = DockStyle.Fill;
            
            var hexPage = new TabPage("Hex Editor");
            hexPage.Controls.Add(hexEditor);
            leftTabs.TabPages.Add(hexPage);
            
            var disasmPage = new TabPage("Disassembly");
            disasmPage.Controls.Add(disasmView);
            leftTabs.TabPages.Add(disasmPage);

            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(leftTabs);

            // RIGHT SIDE: Symbols, CFG, Strings, PE Info, and Log tabs
            var rightTabs = new TabControl { Dock = DockStyle.Fill };
            
            var symbolsPage = new TabPage("Symbols");
            symbolsPage.Controls.Add(symbolTree);
            rightTabs.TabPages.Add(symbolsPage);
            
            var cfgPage = new TabPage("CFG");
            cfgPage.Controls.Add(graphControl);
            rightTabs.TabPages.Add(cfgPage);
            
            var stringsPage = new TabPage("Strings");
            stringsControl!.Dock = DockStyle.Fill;
            stringsPage.Controls.Add(stringsControl);
            rightTabs.TabPages.Add(stringsPage);
            
            var peInfoPage = new TabPage("PE Info");
            peInfoControl!.Dock = DockStyle.Fill;
            peInfoPage.Controls.Add(peInfoControl);
            rightTabs.TabPages.Add(peInfoPage);
            
            var logPage = new TabPage("Log");
            logPage.Controls.Add(logControl);
            rightTabs.TabPages.Add(logPage);

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(rightTabs);

            // BOTTOM PANEL: AI Chat
            var bottomTabs = new TabControl { Dock = DockStyle.Fill };
            llmPane.Dock = DockStyle.Fill;
            
            var chatPage = new TabPage("AI Chat");
            chatPage.Controls.Add(llmPane);
            bottomTabs.TabPages.Add(chatPage);

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(bottomTabs);

            // Configure main horizontal split (left/right content)
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.SplitterDistance = 700;  // Will be adjusted to 50/50 on form load
            splitMain.Panel1.Controls.Add(leftPanel);
            splitMain.Panel2.Controls.Add(rightPanel);

            // Create vertical split: top (main content) and bottom (AI Chat)
            _verticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 500  // Will be set properly on form load
            };
            _verticalSplit.Panel1.Controls.Add(splitMain);
            _verticalSplit.Panel2.Controls.Add(bottomPanel);

            // Clear form and add vertical split
            this.Controls.Remove(splitMain);
            this.Controls.Add(_verticalSplit);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);

            // Smart tab switching with view synchronization
            // When switching tabs, scroll the destination view to show the same location
            // as the view you're leaving - keeps context consistent
            leftTabs.SelectedIndexChanged += (s, e) =>
            {
                SuspendLayout();
                try
                {
                    if (leftTabs.SelectedIndex == 0)  // Switching to Hex Editor
                    {
                        // Sync hex editor to show the disassembly's current selection
                        int selectedIdx = disasmView.SelectedIndex;
                        if (selectedIdx >= 0 && selectedIdx < _core.Disassembly.Count)
                        {
                            ulong address = _core.Disassembly[selectedIdx].Address;
                            int offset = _core.AddressToOffset(address);
                            if (offset >= 0)
                            {
                                hexEditor.SetSelection(offset, offset);
                                hexEditor.ScrollTo(offset);
                            }
                        }
                        // Force immediate repaint
                        hexEditor.ForceRepaint();
                    }
                    else if (leftTabs.SelectedIndex == 1)  // Switching to Disassembly
                    {
                        // Sync disassembly to show the hex editor's current selection
                        var selArgs = hexEditor.GetSelection();
                        if (selArgs != null && selArgs.SelectionLength > 0)
                        {
                            int offset = selArgs.CaretOffset;
                            int index = _core.OffsetToInstructionIndex(offset);
                            if (index >= 0)
                            {
                                disasmView.SelectInstruction(index);
                                disasmView.EnsureVisible(index);
                            }
                        }
                    }
                }
                finally
                {
                    ResumeLayout(false);
                }
            };

            // Set proper splitter distances on form load (50/50 left-right, 60% top content)
            this.Load += (s, e) =>
            {
                if (_verticalSplit != null && _verticalSplit.Height > 0)
                {
                    _verticalSplit.SplitterDistance = (int)(_verticalSplit.Height * 0.6);
                }
                // Set left/right to 50/50
                if (splitMain != null && splitMain.Width > 0)
                {
                    splitMain.SplitterDistance = splitMain.Width / 2;
                }
            };
        }

        // ---------------------------------------------------------
        //  HEX → DISASM SYNC
        // ---------------------------------------------------------
        private void HexEditor_SelectionChanged(object? sender, HexSelectionChangedEventArgs e)
        {
            int offset = e.CaretOffset;

            int index = _core.OffsetToInstructionIndex(offset);
            if (index < 0)
                return;

            // RichTextBox disassembly selection
            disasmView.SelectInstruction(index);
        }

        // ---------------------------------------------------------
        //  DISASM → HEX SYNC
        // ---------------------------------------------------------
        private void DisasmView_InstructionSelected(ulong address)
        {
            int offset = _core.AddressToOffset(address);
            if (offset < 0)
                return;

            hexEditor.SetSelection(offset, offset);
        }

        // ---------------------------------------------------------
        //  HEX BYTES CHANGED → REBUILD DISASSEMBLY
        // ---------------------------------------------------------
        private void HexEditor_BytesChanged(object? sender, EventArgs e)
        {
            if (_core.HexBuffer == null)
                return;

            _core.RebuildDisassemblyFromBuffer();
            _disasmController.Load(_core);
        }

        // ---------------------------------------------------------
        //  CONTEXT MENUS
        // ---------------------------------------------------------
        private ContextMenuStrip CreateCopyPasteMenu()
        {
            var menu = new ContextMenuStrip();

            // Virtual disassembly viewer doesn't support copy/paste operations
            // Users can use hex editor or analyze via selection

            return menu;
        }

        private ContextMenuStrip CreateHexEditorMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Copy Offset", null, (s, e) => hexEditor.CopyOffset());
            menu.Items.Add("Copy Bytes", null, (s, e) => hexEditor.CopyBytes());
            menu.Items.Add("Copy ASCII", null, (s, e) => hexEditor.CopyAscii());
            menu.Items.Add("Copy Full Line", null, (s, e) => hexEditor.CopyFullLine());

            return menu;
        }

        public void Log(string msg)
        {
            logControl.Append(msg);
        }
    }
}