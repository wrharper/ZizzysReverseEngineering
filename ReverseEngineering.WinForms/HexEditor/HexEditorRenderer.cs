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
        private CoreEngine? _core;
        
        // Cache for address lookups - keyed by offset
        private Dictionary<int, ulong>? _addressCache;
        private int _cachedCoreVersion = -1;  // Invalidate cache when core changes
        private ulong _lastComputedAddress = 0;  // Cache last computed address
        private int _lastComputedOffset = -1;    // Offset of last computed address

        public HexEditorRenderer(HexEditorState state, HexEditorSelection selection, HexEditorControl owner)
        {
            _s = state;
            _sel = selection;
            _owner = owner;
        }

        public void SetCoreEngine(CoreEngine? core)
        {
            // Only update if actually switching to a different CoreEngine instance
            if (_core != core)
            {
                _core = core;
                
                // Pre-build offset→address cache for fast lookups
                // This is much faster than the linear search in CoreEngine.OffsetToAddress()
                _addressCache = new Dictionary<int, ulong>();
                if (core != null && core.Disassembly.Count > 0)
                {
                    // Build cache from disassembly (O(n) one-time cost)
                    foreach (var ins in core.Disassembly)
                    {
                        _addressCache[ins.FileOffset] = ins.Address;
                    }
                }
                
                _cachedCoreVersion = core?.Disassembly.Count ?? 0;
                _lastComputedOffset = -1;
                _lastComputedAddress = 0;
            }
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
            // Convert file offset to virtual address with two-tier caching
            ulong virtualAddress;
            
            if (_core != null && _addressCache != null && _addressCache.Count > 0)
            {
                // Check single-value cache first (most common: sequential row draws)
                if (offset == _lastComputedOffset)
                {
                    virtualAddress = _lastComputedAddress;
                }
                // Try dictionary cache (pre-built in SetCoreEngine)
                else if (_addressCache.TryGetValue(offset, out virtualAddress))
                {
                    // Cache hit - update single-value cache
                    _lastComputedOffset = offset;
                    _lastComputedAddress = virtualAddress;
                }
                // Cache miss - fall back to expensive linear search (rare for valid offsets)
                else
                {
                    virtualAddress = _core.OffsetToAddress(offset);
                    _addressCache[offset] = virtualAddress;
                    _lastComputedOffset = offset;
                    _lastComputedAddress = virtualAddress;
                }
            }
            else
            {
                // Fallback: linear mapping if no disassembly yet
                virtualAddress = _s.ImageBase + (ulong)offset;
            }
            
            string text = virtualAddress.ToString("X16");
            var brush = HexEditorTheme.OffsetBrush ?? Brushes.Black;
            g.DrawString(text, _owner.Font, brush, new PointF(0, y));
        }

        private void DrawHexBytes(Graphics g, int start, int end, int y)
        {
            int x = _s.OffsetColumnWidth;

            var (selStart, selEnd) = _sel.GetSelectionRange();

            for (int i = start; i <= end; i++)
            {
                bool selected = (selStart >= 0 && i >= selStart && i <= selEnd);
                bool modified = _s.Buffer!.Modified[i];

                Brush fg = selected ? (HexEditorTheme.SelectionForeBrush ?? Brushes.White) : (HexEditorTheme.FgBrush ?? Brushes.Black);
                Brush bg = selected ? (HexEditorTheme.SelectionBackBrush ?? Brushes.Blue) :
                           modified ? (HexEditorTheme.ModifiedBackBrush ?? Brushes.Yellow) :
                           (HexEditorTheme.BgBrush ?? Brushes.White);

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

                Brush fg = selected ? (HexEditorTheme.SelectionForeBrush ?? Brushes.White) : (HexEditorTheme.AsciiBrush ?? Brushes.Black);
                Brush bg = selected ? (HexEditorTheme.SelectionBackBrush ?? Brushes.Blue) :
                           modified ? (HexEditorTheme.ModifiedBackBrush ?? Brushes.Yellow) :
                           (HexEditorTheme.BgBrush ?? Brushes.White);

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

            var pen = HexEditorTheme.SeparatorPen ?? Pens.Gray;
            g.DrawLine(pen, x, y, x, y + _s.LineHeight);
        }
    }
}