using ReverseEngineering.Core;
using ReverseEngineering.Core.LLM;
using ReverseEngineering.WinForms.HexEditor;
using ReverseEngineering.WinForms.MainWindow;
using ReverseEngineering.WinForms.LLM;
using System;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    public partial class FormMain : Form
    {
        private readonly CoreEngine _core = new();

        private readonly MainMenuController _menuController;
        private readonly ThemeMenuController _themeController;
        private readonly HexEditorController _hexController;
        private readonly DisassemblyController _disasmController;
        private readonly AnalysisController? _analysisController;

        public FormMain()
        {
            InitializeComponent();

            // ---------------------------------------------------------
            //  LLM CLIENT (Optional, for LM Studio integration)
            // ---------------------------------------------------------
            var llmClient = new LocalLLMClient();

            // ---------------------------------------------------------
            //  CONTROLLERS (RichTextBox disassembly)
            // ---------------------------------------------------------
            _disasmController = new DisassemblyController(disasmView, hexEditor, _core);

            _analysisController = new AnalysisController(_core, symbolTree, graphControl, llmClient, llmPane);

            _menuController = new MainMenuController(
                this,
                menuStrip1,
                hexEditor,
                logControl,
                _disasmController,
                statusFile,
                _core,
                _analysisController
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
            //  DEFAULT THEME
            // ---------------------------------------------------------
            ThemeManager.ApplyTheme(this, Themes.Dark);
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

            menu.Items.Add("Copy", null, (s, e) => disasmView.Copy());
            menu.Items.Add("Paste", null, (s, e) => disasmView.Paste());

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