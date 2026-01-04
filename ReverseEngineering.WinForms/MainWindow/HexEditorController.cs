// Project: ReverseEngineering.WinForms
// File: MainWindow/HexEditorController.cs

using System;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class HexEditorController
    {
        private readonly HexEditorControl _hex;
        private readonly ToolStripStatusLabel _offset;
        private readonly ToolStripStatusLabel _selection;

        private readonly DisassemblyController _disasm;
        private readonly CoreEngine _core;

        public HexEditorController(
            HexEditorControl hex,
            ToolStripStatusLabel offset,
            ToolStripStatusLabel selection,
            DisassemblyController disasm,
            CoreEngine core)
        {
            _hex = hex;
            _offset = offset;
            _selection = selection;
            _disasm = disasm;
            _core = core;

            _hex.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object? sender, HexSelectionChangedEventArgs e)
        {
            // Update status bar
            _offset.Text = $"Offset: 0x{e.CaretOffset:X}";
            _selection.Text = e.SelectionLength > 0
                ? $"Selection: {e.SelectionLength} bytes"
                : "Selection: -";

            // HEX → ASM sync
            int index = _core.OffsetToInstructionIndex(e.CaretOffset);
            if (index >= 0)
            {
                _disasm.SelectInstruction(index);
                _disasm.ScrollTo(index);   // ⭐ FIXED
            }
        }
    }
}