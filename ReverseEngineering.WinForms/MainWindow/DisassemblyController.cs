using ReverseEngineering.Core;
using ReverseEngineering.Core.IcedAssembly;
using ReverseEngineering.Core.Keystone;
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class DisassemblyController
    {
        private readonly DisassemblyControl _view;
        private readonly HexEditorControl _hex;
        private CoreEngine _core;

        private List<Instruction> _instructions = [];
        private CancellationTokenSource? _asmToHexCts;
        private bool _suppressEvents;

        public DisassemblyController(DisassemblyControl view, HexEditorControl hex, CoreEngine core)
        {
            _view = view;
            _hex = hex;
            _core = core;

            // ASM → HEX sync (selection)
            _view.InstructionSelected += OnInstructionSelected;

            // ASM → HEX sync (editing)
            _view.LineEdited += OnLineEdited;
        }

        // ---------------------------------------------------------
        //  ASM → HEX SYNC (selection)
        // ---------------------------------------------------------
        private void OnInstructionSelected(ulong address)
        {
            if (_core == null)
                return;

            int offset = _core.AddressToOffset(address);
            if (offset < 0)
                return;

            _suppressEvents = true;
            _hex.SetSelection(offset, offset);
            _hex.ScrollTo(offset);
            _suppressEvents = false;
        }

        // ---------------------------------------------------------
        //  ASM → HEX SYNC (editing)
        // ---------------------------------------------------------
        private async void OnLineEdited(int index, string text)
        {
            if (_suppressEvents)
                return;

            _asmToHexCts?.Cancel();
            _asmToHexCts = new CancellationTokenSource();
            var token = _asmToHexCts.Token;

            try
            {
                await Task.Delay(80, token);

                if (index < 0 || index >= _instructions.Count)
                    return;

                var ins = _instructions[index];
                ulong address = ins.Address;

                byte[] bytes = await Task.Run(() => KeystoneAssembler.Assemble(text, address, _core.Is64Bit), token);
                if (bytes == null || bytes.Length == 0)
                    return;

                int offset = _core.AddressToOffset(address);
                _core.HexBuffer.WriteBytes(offset, bytes);

                await Task.Run(() => _core.RebuildInstructionAtOffset(offset), token);

                _suppressEvents = true;
                _instructions = _core.Disassembly;
                _view.Is64Bit = _core.Is64Bit;
                _view.SetInstructions(_instructions);
                _hex.SetBuffer(_core.HexBuffer);
                _suppressEvents = false;
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
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
        //  VIEW STATE
        // ---------------------------------------------------------
        public AsmViewState GetViewState() => _view.GetViewState();
        public void SetViewState(AsmViewState state) => _view.SetViewState(state);

        // ---------------------------------------------------------
        //  EXTERNAL CONTROL HELPERS
        // ---------------------------------------------------------
        public void SelectInstruction(int index) => _view.SelectInstruction(index);
        public void ScrollTo(int index) => _view.ScrollTo(index);
    }
}