using ReverseEngineering.Core;
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.WinForms.HexEditor;
using System.Collections.Generic;
using System.ComponentModel;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class DisassemblyController
    {
        private readonly DisassemblyControl _view;
        private readonly HexEditorControl _hex;
        private CoreEngine _core;

        private List<Instruction> _instructions = [];
        public AsmViewState GetViewState() => _view.GetViewState();
        public void SetViewState(AsmViewState state) => _view.SetViewState(state);
        public DisassemblyController(DisassemblyControl view, HexEditorControl hex, CoreEngine core)
        {
            _view = view;
            _hex = hex;
            _core = core;

            // ASM → HEX sync
            _view.InstructionSelected += OnInstructionSelected;
        }

        // ---------------------------------------------------------
        //  ASM → HEX SYNC
        // ---------------------------------------------------------
        private void OnInstructionSelected(ulong address)
        {
            if (_core == null)
                return;

            int offset = _core.AddressToOffset(address);
            if (offset < 0)
                return;

            _hex.SetSelection(offset, offset);
            _hex.ScrollTo(offset);
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
        }

        // ---------------------------------------------------------
        //  SELECT INSTRUCTION (called by FormMain)
        // ---------------------------------------------------------
        public void SelectInstruction(int index)
        {
            _view.SelectInstruction(index);
        }

        public void ScrollTo(int index)
        {
            _view.ScrollTo(index);
        }

    }
}