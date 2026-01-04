using System;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorControl : UserControl
    {
        private readonly HexEditorState _state;
        private readonly VScrollBar _scroll;

        private readonly HexEditorRenderer _renderer;
        private readonly HexEditorInteraction _interaction;
        private readonly HexEditorEditing _editing;
        private readonly HexEditorSelection _selection;

        private bool _dragging;

        public event EventHandler<HexSelectionChangedEventArgs>? SelectionChanged;
        public event EventHandler? BytesChanged;
        public event Action<int, byte, byte>? ByteChanged;

        public HexEditorControl()
        {
            DoubleBuffered = true;
            Font = new Font("Consolas", 10);

            // Core modules
            _state = new HexEditorState();
            _selection = new HexEditorSelection(_state);
            _editing = new HexEditorEditing(_state, _selection);
            _interaction = new HexEditorInteraction(_state, _selection, _editing, this);
            _renderer = new HexEditorRenderer(_state, _selection, this);

            // Scrollbar
            _scroll = new VScrollBar { Dock = DockStyle.Right, Width = 20 };
            _scroll.Scroll += (s, e) =>
            {
                _state.ScrollOffsetY = _scroll.Value;
                Invalidate();
            };
            Controls.Add(_scroll);

            // Mouse
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _interaction.MouseDown(e.Location);
                    RaiseSelectionChanged();
                }
            };

            MouseMove += (s, e) =>
            {
                if (_dragging)
                {
                    _interaction.MouseMove(e.Location, true);
                    RaiseSelectionChanged();
                }
            };

            MouseUp += (s, e) => _dragging = false;

            // Keyboard
            KeyPress += (s, e) =>
            {
                _interaction.KeyPress(e.KeyChar);
                RaiseSelectionChanged();
            };

            KeyDown += HandleKeyDown;
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: _interaction.MoveCaret(-1); break;
                case Keys.Right: _interaction.MoveCaret(+1); break;
                case Keys.Up: _interaction.MoveCaret(-HexEditorState.BytesPerRow); break;
                case Keys.Down: _interaction.MoveCaret(+HexEditorState.BytesPerRow); break;
                case Keys.PageUp: _interaction.MoveCaret(-(HexEditorState.BytesPerRow * 16)); break;
                case Keys.PageDown: _interaction.MoveCaret((HexEditorState.BytesPerRow * 16)); break;
            }

            RaiseSelectionChanged();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // How many rows to scroll per wheel tick
            int rowsPerTick = 8; // tweak this number to taste

            int deltaRows = e.Delta > 0 ? -rowsPerTick : rowsPerTick;
            int deltaPixels = deltaRows * _state.LineHeight;

            int newValue = Math.Max(
                _scroll.Minimum,
                Math.Min(_scroll.Maximum - _scroll.LargeChange, _scroll.Value + deltaPixels)
            );

            _scroll.Value = newValue;
            _state.ScrollOffsetY = newValue;

            Invalidate();
        }
        // ---------------------------------------------------------
        //  BUFFER SETUP
        // ---------------------------------------------------------
        public void SetBuffer(HexBuffer buffer)
        {
            _state.Buffer = buffer;

            _state.TotalRows =
                (buffer.Bytes.Length + HexEditorState.BytesPerRow - 1)
                / HexEditorState.BytesPerRow;

            using var g = CreateGraphics();
            var size = g.MeasureString("W", Font);
            _state.CharWidth = (int)Math.Ceiling(size.Width);
            _state.LineHeight = (int)Math.Ceiling(size.Height);

            _state.OffsetColumnWidth = _state.CharWidth * 8 + 10;
            _state.HexColumnWidth = _state.CharWidth * (HexEditorState.BytesPerRow * 3) + 20;
            _state.AsciiColumnWidth = _state.CharWidth * HexEditorState.BytesPerRow + 20;

            UpdateScroll();
            Invalidate();
        }

        private void UpdateScroll()
        {
            int contentHeight = _state.TotalRows * _state.LineHeight;
            int viewport = ClientSize.Height;

            int maxScroll = Math.Max(0, contentHeight - viewport);

            _scroll.Minimum = 0;
            _scroll.LargeChange = Math.Max(_state.LineHeight, viewport / 2);
            _scroll.SmallChange = _state.LineHeight;
            _scroll.Maximum = maxScroll + _scroll.LargeChange;

            if (_state.ScrollOffsetY > maxScroll)
                _state.ScrollOffsetY = maxScroll;

            _scroll.Value = Math.Max(
                0,
                Math.Min(_scroll.Maximum - _scroll.LargeChange, _state.ScrollOffsetY)
            );
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            _renderer.Paint(e.Graphics, ClientRectangle);
        }

        // ---------------------------------------------------------
        //  PUBLIC ACCESSORS
        // ---------------------------------------------------------
        public HexBuffer? Buffer => _state.Buffer;

        public string CurrentFilePath =>
            _state.Buffer?.FilePath ?? string.Empty;

        // ---------------------------------------------------------
        //  EVENTS
        // ---------------------------------------------------------
        internal void RaiseSelectionChanged()
        {
            int length = _selection.GetSelectionLength();

            SelectionChanged?.Invoke(
                this,
                new HexSelectionChangedEventArgs(_state.CaretIndex, length)
            );
        }

        public void RaiseBytesChanged()
        {
            BytesChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------
        //  SELECTION API
        // ---------------------------------------------------------
        public void SetSelection(int start, int end)
        {
            _selection.SetSelection(start, end);
            Invalidate();
        }

        // ---------------------------------------------------------
        //  COPY OPERATIONS
        // ---------------------------------------------------------
        public void CopyOffset()
        {
            if (_state.CaretIndex >= 0)
                Clipboard.SetText(_state.CaretIndex.ToString("X8"));
        }

        public void CopyBytes()
        {
            var text = _editing.GetSelectedBytesAsHex();
            if (text != null)
                Clipboard.SetText(text);
        }

        public void CopyAscii()
        {
            var text = _editing.GetSelectedBytesAsAscii();
            if (text != null)
                Clipboard.SetText(text);
        }

        public void CopyFullLine()
        {
            var text = _editing.GetFullLineText();
            if (text != null)
                Clipboard.SetText(text);
        }
        // ---------------------------------------------------------
        //  VIEW STATE API (used by ProjectSystem)
        // ---------------------------------------------------------
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int VerticalScrollOffset
        {
            get => _state.ScrollOffsetY;
            set
            {
                _state.ScrollOffsetY = value;
                _scroll.Value = Math.Max(
                    _scroll.Minimum,
                    Math.Min(_scroll.Maximum - _scroll.LargeChange, value)
                );
                Invalidate();
            }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int CaretIndex
        {
            get => _state.CaretIndex;
            set
            {
                _state.CaretIndex = value;
                Invalidate();
            }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int SelectionStart
        {
            get => _state.SelectionStart;
            set
            {
                _state.SelectionStart = value;
                Invalidate();
            }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int SelectionEnd
        {
            get => _state.SelectionEnd;
            set
            {
                _state.SelectionEnd = value;
                Invalidate();
            }
        }

        // ---------------------------------------------------------
        //  GET / SET VIEW STATE (ProjectSystem integration)
        // ---------------------------------------------------------
        public ReverseEngineering.Core.ProjectSystem.HexViewState GetViewState()
        {
            return new ReverseEngineering.Core.ProjectSystem.HexViewState
            {
                ScrollOffset = _state.ScrollOffsetY,
                CaretIndex = _state.CaretIndex,
                SelectionStart = _state.SelectionStart,
                SelectionEnd = _state.SelectionEnd
            };
        }

        public void SetViewState(ReverseEngineering.Core.ProjectSystem.HexViewState state)
        {
            if (state == null)
                return;

            _state.ScrollOffsetY = state.ScrollOffset;
            _state.CaretIndex = state.CaretIndex;
            _state.SelectionStart = state.SelectionStart;
            _state.SelectionEnd = state.SelectionEnd;

            // Sync scrollbar
            _scroll.Value = Math.Max(
                _scroll.Minimum,
                Math.Min(_scroll.Maximum - _scroll.LargeChange, state.ScrollOffset)
            );

            Invalidate();
        }
        // ---------------------------------------------------------
        //  BYTE EDIT COMMIT (external callers)
        // ---------------------------------------------------------
        internal void CommitByteEdit(int offset, byte oldValue, byte newValue)
        {
            if (oldValue == newValue)
                return;

            _editing.WriteByte(offset, newValue);
            ByteChanged?.Invoke(offset, oldValue, newValue);
            Invalidate();
        }
        public void ScrollTo(int offset)
        {
            int row = offset / HexEditorState.BytesPerRow;
            int y = row * _state.LineHeight;

            _state.ScrollOffsetY = y;
            Invalidate();
        }
    }
}