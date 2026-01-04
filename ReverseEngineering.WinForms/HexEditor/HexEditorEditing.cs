using System;
using System.Text;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorEditing
    {
        private readonly HexEditorState _s;
        private readonly HexEditorSelection _sel;

        public HexEditorEditing(HexEditorState state, HexEditorSelection selection)
        {
            _s = state;
            _sel = selection;
        }

        // ---------------------------------------------------------
        //  BYTE WRITING
        // ---------------------------------------------------------
        public void WriteByte(int offset, byte value)
        {
            if (_s.Buffer == null)
                return;

            if (offset < 0 || offset >= _s.Buffer.Bytes.Length)
                return;

            _s.Buffer.WriteByte(offset, value);
        }

        public void WriteBytes(int offset, byte[] values)
        {
            if (_s.Buffer == null)
                return;

            _s.Buffer.WriteBytes(offset, values);
        }

        // ---------------------------------------------------------
        //  SELECTION → HEX STRING
        // ---------------------------------------------------------
        public string? GetSelectedBytesAsHex()
        {
            if (_s.Buffer == null)
                return null;

            var (start, end) = _sel.GetSelectionRange();
            if (start < 0 || end < 0)
                return null;

            return _s.Buffer.GetHexString(start, end);
        }

        // ---------------------------------------------------------
        //  SELECTION → ASCII STRING
        // ---------------------------------------------------------
        public string? GetSelectedBytesAsAscii()
        {
            if (_s.Buffer == null)
                return null;

            var (start, end) = _sel.GetSelectionRange();
            if (start < 0 || end < 0)
                return null;

            return _s.Buffer.GetAsciiString(start, end);
        }

        // ---------------------------------------------------------
        //  FULL LINE TEXT (offset → formatted line)
        // ---------------------------------------------------------
        public string? GetFullLineText()
        {
            if (_s.Buffer == null)
                return null;

            int caret = _s.CaretIndex;
            if (caret < 0)
                return null;

            return _s.Buffer.GetFullLineString(caret);
        }
    }
}