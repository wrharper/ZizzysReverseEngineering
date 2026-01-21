using System.Diagnostics;
using ReverseEngineering.Core;
using ReverseEngineering.Core.AILogs;
using ReverseEngineering.Core.IcedAssembly;
using ReverseEngineering.Core.Keystone;
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class DisassemblyController
    {
        private readonly DisassemblyViewerVirtual _view;
        private readonly HexEditorControl _hex;
        private CoreEngine _core;
        private AILogsManager? _aiLogs;

        private List<Instruction> _instructions = [];

        public DisassemblyController(DisassemblyViewerVirtual view, HexEditorControl hex, CoreEngine core, AILogsManager? aiLogs = null)
        {
            _view = view;
            _hex = hex;
            _core = core;
            _aiLogs = aiLogs;

            // ASM → HEX sync (selection)
            _view.InstructionSelected += OnInstructionSelected;

            // Note: DisassemblyViewerVirtual doesn't support inline editing.
            // For assembly edits, use the hex editor or a dedicated assembly dialog.
        }

        // ---------------------------------------------------------
        //  ASM → HEX SYNC (selection)
        // ---------------------------------------------------------
        private void OnInstructionSelected(ulong address)
        {
            if (_core == null)
                return;

            int offset = (int)_core.AddressToOffset(address);
            if (offset < 0)
                return;

            _hex.SetSelection(offset, offset);
            _hex.ScrollTo(offset);
        }

        // ---------------------------------------------------------
        //  INITIALIZE (EMPTY STATE)
        // ---------------------------------------------------------
        public void Initialize()
        {
            _instructions = [];
            _view.Is64Bit = false;
            _view.SetInstructions(_instructions);
        }

        // ---------------------------------------------------------
        //  LOAD DISASSEMBLY FROM CORE ENGINE
        // ---------------------------------------------------------
        public void Load(CoreEngine core)
        {
            _core = core;

            _instructions = core.Disassembly;
            _view.Is64Bit = core.Is64Bit;
            _view.SetInstructions(_instructions);
            _view.ApplyTheme(ThemeManager.CurrentTheme);  // Apply theme to viewer
        }

        // ---------------------------------------------------------
        //  VIEW STATE
        // ---------------------------------------------------------
        public AsmViewState GetViewState()
        {
            var (selectedIndex, scrollOffset) = _view.GetViewState();
            return new AsmViewState
            {
                SelectedInstructionIndex = selectedIndex,
                ScrollOffset = scrollOffset
            };
        }

        public void SetViewState(AsmViewState state)
        {
            if (state == null)
                return;

            _view.SetViewState(state.SelectedInstructionIndex, state.ScrollOffset);
        }

        // ---------------------------------------------------------
        //  EXTERNAL CONTROL HELPERS
        // ---------------------------------------------------------
        public void SelectInstruction(int index) => _view.SelectInstruction(index);
        public void ScrollTo(int index) => _view.EnsureVisible(index);

        public int GetSelectedInstructionIndex() => _view.SelectedIndex;

        public ulong GetSelectedInstructionAddress()
        {
            int idx = _view.SelectedIndex;
            if (idx < 0 || idx >= _instructions.Count)
                return 0;
            return _instructions[idx].Address;
        }

        public void RefreshDisassembly()
        {
            _instructions = _core.Disassembly;
            _view.SetInstructions(_instructions);
        }
    }
}