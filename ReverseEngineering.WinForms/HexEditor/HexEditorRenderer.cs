using System;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorRenderer
    {
        private readonly HexEditorState _s;
        private readonly HexEditorSelection _sel;
        private readonly HexEditorControl _owner;

        public HexEditorRenderer(HexEditorState state, HexEditorSelection selection, HexEditorControl owner)
        {
            _s = state;
            _sel = selection;
            _owner = owner;
        }

        public void Paint(Graphics g, Rectangle clip)
        {
            g.Clear(HexEditorTheme.Background);

            if (_s.Buffer == null || _s.Buffer.Bytes.Length == 0)
                return;

            int firstRow = Math.Max(0, _s.ScrollOffsetY / _s.LineHeight);
            int lastRow = Math.Min(_s.TotalRows - 1,
                (_s.ScrollOffsetY + clip.Height) / _s.LineHeight);

            for (int row = firstRow; row <= lastRow; row++)
                DrawRow(g, row);
        }

        private void DrawRow(Graphics g, int row)
        {
            int y = row * _s.LineHeight - _s.ScrollOffsetY;

            int start = row * HexEditorState.BytesPerRow;
            int end = Math.Min(start + HexEditorState.BytesPerRow - 1,
                               _s.Buffer!.Bytes.Length - 1);

            DrawOffset(g, start, y);
            DrawHexBytes(g, start, end, y);
            DrawAscii(g, start, end, y);
            DrawCaret(g, row, y);
        }

        private void DrawOffset(Graphics g, int offset, int y)
        {
            string text = offset.ToString("X8");
            g.DrawString(text, _owner.Font, HexEditorTheme.OffsetBrush,
                new PointF(0, y));
        }

        private void DrawHexBytes(Graphics g, int start, int end, int y)
        {
            int x = _s.OffsetColumnWidth;

            var (selStart, selEnd) = _sel.GetSelectionRange();

            for (int i = start; i <= end; i++)
            {
                bool selected = (selStart >= 0 && i >= selStart && i <= selEnd);
                bool modified = _s.Buffer!.Modified[i];

                Brush fg = selected ? HexEditorTheme.SelectionForeBrush : HexEditorTheme.FgBrush;
                Brush bg = selected ? HexEditorTheme.SelectionBackBrush :
                           modified ? HexEditorTheme.ModifiedBackBrush :
                           HexEditorTheme.BgBrush;

                string hex = _s.Buffer.Bytes[i].ToString("X2");

                g.FillRectangle(bg, new Rectangle(x, y, _s.CharWidth * 3, _s.LineHeight));
                g.DrawString(hex, _owner.Font, fg, new PointF(x, y));

                x += _s.CharWidth * 3;
            }
        }

        private void DrawAscii(Graphics g, int start, int end, int y)
        {
            int x = _s.OffsetColumnWidth + _s.HexColumnWidth;

            var (selStart, selEnd) = _sel.GetSelectionRange();

            for (int i = start; i <= end; i++)
            {
                bool selected = (selStart >= 0 && i >= selStart && i <= selEnd);
                bool modified = _s.Buffer!.Modified[i];

                Brush fg = selected ? HexEditorTheme.SelectionForeBrush : HexEditorTheme.AsciiBrush;
                Brush bg = selected ? HexEditorTheme.SelectionBackBrush :
                           modified ? HexEditorTheme.ModifiedBackBrush :
                           HexEditorTheme.BgBrush;

                byte b = _s.Buffer.Bytes[i];
                char c = (b >= 32 && b <= 126) ? (char)b : '.';

                g.FillRectangle(bg, new Rectangle(x, y, _s.CharWidth, _s.LineHeight));
                g.DrawString(c.ToString(), _owner.Font, fg, new PointF(x, y));

                x += _s.CharWidth;
            }
        }

        private void DrawCaret(Graphics g, int row, int y)
        {
            if (_s.CaretIndex < 0)
                return;

            int caretRow = _s.CaretIndex / HexEditorState.BytesPerRow;
            if (caretRow != row)
                return;

            int caretCol = _s.CaretIndex % HexEditorState.BytesPerRow;
            int x = _s.OffsetColumnWidth + caretCol * (_s.CharWidth * 3);

            g.DrawLine(HexEditorTheme.SeparatorPen, x, y, x, y + _s.LineHeight);
        }
    }
}