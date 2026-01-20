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
        private readonly DisassemblyControl _view;
        private readonly HexEditorControl _hex;
        private CoreEngine _core;
        private AILogsManager? _aiLogs;

        private List<Instruction> _instructions = [];
        private CancellationTokenSource? _asmToHexCts;
        private bool _suppressEvents;

        public DisassemblyController(DisassemblyControl view, HexEditorControl hex, CoreEngine core, AILogsManager? aiLogs = null)
        {
            _view = view;
            _hex = hex;
            _core = core;
            _aiLogs = aiLogs;

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

            int offset = (int)_core.AddressToOffset(address);
            if (offset < 0)
                return;

            _suppressEvents = true;
            _hex.SetSelection(offset, offset);
            _hex.ScrollTo(offset);
            _suppressEvents = false;
        }
private async void OnLineEdited(int index, string text)
{
    if (_suppressEvents)
        return;

    _asmToHexCts?.Cancel();
    _asmToHexCts = new CancellationTokenSource();
    var token = _asmToHexCts.Token;

    var timer = Stopwatch.StartNew();
    
    try
    {
        await Task.Delay(80, token);

    if (index < 0 || index >= _instructions.Count())
            return;

        var ins = _instructions[index];
        ulong address = ins.Address;

        byte[] bytes = await Task.Run(() => KeystoneAssembler.Assemble(text, address, _core.Is64Bit), token);
        if (bytes == null || bytes.Length == 0)
        {
            LogAssemblyEdit(ins, text, bytes ?? [], timer, false, "Keystone returned empty bytes");
            return;
        }

        int offset = (int)_core.AddressToOffset(address);
        _core.HexBuffer.WriteBytes(offset, bytes);

        await Task.Run(() => _core.RebuildInstructionAtOffset(offset), token);

        // Log successful assembly edit
        LogAssemblyEdit(ins, text, bytes, timer, true, null);

        // Synchronize hex view with new bytes
        _hex.SetSelection(offset, offset + bytes.Length - 1);
        _hex.ScrollTo(offset);

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
    catch (Exception ex)
    {
        timer.Stop();
        
        // Log failure
        if (_aiLogs != null && index >= 0 && index < _instructions.Count)
        {
            var ins = _instructions[index];
            var errorEntry = new AILogEntry
            {
                Operation = "AssemblyEdit",
                Prompt = $"Assemble: {text} at {ins.Address:X8}",
                AIOutput = $"Error: {ex.Message}",
                Status = "Error",
                DurationMs = timer.ElapsedMilliseconds
            };
            _aiLogs.SaveLogEntry(errorEntry);
        }
    }
}

private void LogAssemblyEdit(Instruction originalInstruction, string newAsmText, byte[] newBytes, Stopwatch timer, bool success, string? errorMsg)
{
    if (_aiLogs == null)
        return;

    timer.Stop();

    var logEntry = new AILogEntry
    {
        Operation = "AssemblyEdit",
        Prompt = $"Assemble: {newAsmText} at {originalInstruction.Address:X8}",
        AIOutput = success ? $"Generated {newBytes.Length} bytes" : (errorMsg ?? "Assembly failed"),
        Status = success ? "Success" : "Error",
        DurationMs = timer.ElapsedMilliseconds
    };

    // Track byte changes
    if (success && newBytes.Length > 0)
    {
        for (int i = 0; i < newBytes.Length && i < originalInstruction.Bytes.Length; i++)
        {
            var origByte = originalInstruction.Bytes[i];
            if (origByte != newBytes[i])
            {
                logEntry.Changes.Add(new ByteChange
                {
                    Offset = (int)(originalInstruction.Address + (ulong)i),
                    OriginalByte = origByte,
                    NewByte = newBytes[i],
                    AssemblyBefore = originalInstruction.Mnemonic + " " + originalInstruction.Operands,
                    AssemblyAfter = newAsmText
                });
            }
        }
    }

    _aiLogs.SaveLogEntry(logEntry);
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

        public int GetSelectedInstructionIndex()
        {
            var selectedAddress = _view.GetSelectedInstructionAddress();
            if (selectedAddress == 0) return -1;

            var index = _core.OffsetToInstructionIndex((int)_core.AddressToOffset(selectedAddress));
            return index;
        }

        public ulong GetSelectedInstructionAddress() => _view.GetSelectedInstructionAddress();

        public void RefreshDisassembly()
        {
            _instructions = _core.Disassembly;
            _view.SetInstructions(_instructions);
        }
    }
}