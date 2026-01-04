using System;
using System.Drawing;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorInteraction
    {
        private readonly HexEditorState _s;
        private readonly HexEditorSelection _sel;
        private readonly HexEditorEditing _edit;
        private readonly HexEditorControl _owner;

        public HexEditorInteraction(
            HexEditorState state,
            HexEditorSelection selection,
            HexEditorEditing editing,
            HexEditorControl owner)
        {
            _s = state;
            _sel = selection;
            _edit = editing;
            _owner = owner;
        }

        // ---------------------------------------------------------
        //  MOUSE
        // ---------------------------------------------------------
        public void MouseDown(Point p)
        {
            if (_s.Buffer?.Bytes == null)
                return;

            int index = PointToByteIndex(p);

            _sel.SetSelection(index, index);
            _s.CaretIndex = index;
            _s.EditingHexNibble = false;

            _owner.Invalidate();
        }

        public void MouseMove(Point p, bool dragging)
        {
            if (!dragging || _s.Buffer?.Bytes == null)
                return;

            int index = PointToByteIndex(p);

            var (start, _) = _sel.GetSelectionRange();
            _sel.SetSelection(start, index);

            _s.CaretIndex = index;

            _owner.Invalidate();
        }

        // ---------------------------------------------------------
        //  KEYBOARD
        // ---------------------------------------------------------
        public void KeyPress(char c)
        {
            if (_s.Buffer?.Bytes == null)
                return;

            c = char.ToUpper(c);

            // HEX input
            if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
            {
                int value = (c <= '9') ? c - '0' : c - 'A' + 10;

                byte original = _s.Buffer.Bytes[_s.CaretIndex];
                byte updated;

                if (!_s.EditingHexNibble)
                {
                    updated = (byte)((value << 4) | (original & 0x0F));
                    _s.EditingHexNibble = true;
                }
                else
                {
                    updated = (byte)((original & 0xF0) | value);
                    _s.EditingHexNibble = false;
                    MoveCaret(+1);
                }

                _edit.WriteByte(_s.CaretIndex, updated);
                _owner.RaiseBytesChanged();
                _owner.Invalidate();
                return;
            }

            // ASCII input
            if (c >= 32 && c < 127)
            {
                _edit.WriteByte(_s.CaretIndex, (byte)c);
                MoveCaret(+1);

                _owner.RaiseBytesChanged();
                _owner.Invalidate();
            }
        }

        // ---------------------------------------------------------
        //  CARET MOVEMENT
        // ---------------------------------------------------------
        public void MoveCaret(int delta)
        {
            if (_s.Buffer?.Bytes == null)
                return;

            int max = _s.Buffer.Bytes.Length - 1;

            _s.CaretIndex = Math.Max(0, Math.Min(_s.CaretIndex + delta, max));

            _sel.SetSelection(_s.CaretIndex, _s.CaretIndex);

            EnsureCaretVisible();
            _owner.Invalidate();
        }

        // ---------------------------------------------------------
        //  SCROLLING
        // ---------------------------------------------------------
        private void EnsureCaretVisible()
        {
            int newOffset = ComputeScrollOffsetForCaret();
            if (newOffset >= 0)
                _s.ScrollOffsetY = newOffset;
        }

        public int ComputeScrollOffsetForCaret()
        {
            if (_s.CaretIndex < 0)
                return -1;

            int row = _s.CaretIndex / HexEditorState.BytesPerRow;
            int y = row * _s.LineHeight;
            int viewport = _owner.ClientSize.Height;

            int offset = _s.ScrollOffsetY;

            if (y < offset)
                offset = y;
            else if (y > offset + viewport - _s.LineHeight)
                offset = y - (viewport - _s.LineHeight);

            return offset;
        }

        // ---------------------------------------------------------
        //  HIT TESTING
        // ---------------------------------------------------------
        private int PointToByteIndex(Point p)
        {
            int row = (p.Y + _s.ScrollOffsetY) / _s.LineHeight;
            row = Math.Max(0, Math.Min(row, _s.TotalRows - 1));

            int x = p.X;

            int hexStartX = _s.OffsetColumnWidth;
            int asciiStartX = _s.OffsetColumnWidth + _s.HexColumnWidth;

            int col;

            // HEX column
            if (x >= hexStartX && x < asciiStartX)
            {
                int rel = x - hexStartX;
                col = rel / (_s.CharWidth * 3);
            }
            // ASCII column
            else if (x >= asciiStartX)
            {
                int rel = x - asciiStartX;
                col = rel / _s.CharWidth;
            }
            else
            {
                col = 0;
            }

            col = Math.Max(0, Math.Min(col, HexEditorState.BytesPerRow - 1));

            int index = row * HexEditorState.BytesPerRow + col;

            if (_s.Buffer == null)
                return 0;

            return Math.Max(0, Math.Min(index, _s.Buffer.Bytes.Length - 1));
        }
    }
}