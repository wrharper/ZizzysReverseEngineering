// Project: ReverseEngineering.WinForms
// File: HexEditor/HexSelectionChangedEventArgs.cs

using System;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexSelectionChangedEventArgs : EventArgs
    {
        public int CaretOffset { get; }
        public int SelectionLength { get; }

        public HexSelectionChangedEventArgs(int caretOffset, int selectionLength)
        {
            CaretOffset = caretOffset;
            SelectionLength = selectionLength;
        }
    }
}