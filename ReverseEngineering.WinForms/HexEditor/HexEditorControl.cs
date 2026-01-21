using System;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.WinForms.HexEditor;

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
        private bool _suppressPaint = false;  // Suppress paints during tab switches
        private System.Windows.Forms.Timer? _repaintDebounceTimer;  // Debounce multiple repaints during tab switch
        private CoreEngine? _core;

        public event EventHandler<HexSelectionChangedEventArgs>? SelectionChanged;

        // Raised when any bytes change (bulk notification)
        public event EventHandler? BytesChanged;

        // Raised when a single byte changes: offset, oldValue, newValue
        public event Action<int, byte, byte>? ByteChanged;

        public HexEditorControl()
        {
            DoubleBuffered = true;
            Font = new Font("Consolas", 10);

            // Core modules
            _state = new HexEditorState();
            _selection = new HexEditorSelection(_state);
            _editing = new HexEditorEditing(_state, _selection, this);
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

            int rowsPerTick = 8;
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

            _state.OffsetColumnWidth = _state.CharWidth * 16 + 10;  // Increased for 16-char addresses
            _state.HexColumnWidth = _state.CharWidth * (HexEditorState.BytesPerRow * 3) + 20;
            _state.AsciiColumnWidth = _state.CharWidth * HexEditorState.BytesPerRow + 20;

            UpdateScroll();
            Invalidate();
        }

        public void SetCoreEngine(CoreEngine? core)
        {
            // Only update if actually a different CoreEngine instance
            if (_core != core)
            {
                _core = core;
                _renderer.SetCoreEngine(core);
            }
        }

        /// <summary>
        /// Navigate to a physical file offset or virtual address in the hex editor.
        /// If CoreEngine is available (disassembly loaded), treats address as virtual.
        /// Otherwise treats it as a physical file offset.
        /// </summary>
        public void GoToAddress(ulong address)
        {
            int offset;

            if (_core != null && _core.Disassembly.Count > 0)
            {
                // Disassembly is loaded - treat input as virtual address
                offset = _core.AddressToOffset(address);
                if (offset < 0)
                {
                    MessageBox.Show("Virtual address not found in disassembly mapping.");
                    return;
                }
            }
            else
            {
                // No disassembly - treat input as physical file offset
                offset = (int)address;
                if (offset < 0 || offset >= _state.Buffer?.Bytes.Length)
                {
                    MessageBox.Show("File offset out of range.");
                    return;
                }
            }

            // Calculate scroll position
            int row = offset / HexEditorState.BytesPerRow;
            int scrollY = row * _state.LineHeight;

            // Clamp to valid range
            int maxScroll = _scroll.Maximum - _scroll.LargeChange;
            if (scrollY > maxScroll)
                scrollY = maxScroll;

            _scroll.Value = scrollY;
            _state.ScrollOffsetY = scrollY;

            // Set caret and selection
            _state.CaretIndex = offset;
            _selection.SetSelection(offset, offset);
            RaiseSelectionChanged();
            Invalidate();
        }

        /// <summary>
        /// Show Go To Address dialog and navigate.
        /// Dialog changes hint based on disassembly availability.
        /// </summary>
        public void ShowGoToDialog()
        {
            // Determine if disassembly is loaded
            bool hasDisassembly = _core != null && _core.Disassembly.Count > 0;
            
            // Get current address or offset for dialog
            ulong current = 0;
            if (_state.CaretIndex >= 0)
            {
                if (hasDisassembly)
                    current = _core!.OffsetToAddress(_state.CaretIndex);
                else
                    current = (ulong)_state.CaretIndex;
            }

            using var dialog = new GoToAddressDialog(current, isVirtual: hasDisassembly);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                GoToAddress(dialog.Address);
            }
        }

        /// <summary>
        /// Set the image base for virtual address display in hex editor.
        /// </summary>
        public void SetImageBase(ulong imageBase)
        {
            _state.ImageBase = imageBase;
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
            // Skip painting if suppressed (during tab switches)
            if (_suppressPaint)
                return;
                
            _renderer.Paint(e.Graphics, ClientRectangle);
        }

        /// <summary>
        /// <summary>
        /// Called when visibility changes - suppress paint during transition, resume after
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            
            if (Visible)
            {
                // Recalculate scroll metrics immediately when becoming visible
                if (_state.Buffer != null)
                {
                    UpdateScroll();
                }
                
                // Resume painting after a brief delay to let tab animation complete
                _suppressPaint = false;
                
                // Debounce repaint requests to prevent multiple renders during tab transition
                _repaintDebounceTimer?.Dispose();
                _repaintDebounceTimer = new System.Windows.Forms.Timer();
                _repaintDebounceTimer.Interval = 50;  // Wait 50ms for all tab animations to settle
                _repaintDebounceTimer.Tick += (s, args) =>
                {
                    _repaintDebounceTimer?.Dispose();
                    _repaintDebounceTimer = null;
                    ForceRepaint();
                };
                _repaintDebounceTimer.Start();
            }
            else
            {
                // Suppress painting while hidden to avoid wasted work
                _suppressPaint = true;
                _repaintDebounceTimer?.Dispose();
                _repaintDebounceTimer = null;
            }
        }

        /// <summary>
        /// Force a full repaint - call this after tab becomes visible
        /// </summary>
        public void ForceRepaint()
        {
            _suppressPaint = false;
            Invalidate(true);  // Force complete redraw
            Update();  // Immediately paint instead of queuing
        }

        public HexBuffer? Buffer => _state.Buffer;

        public string CurrentFilePath =>
            _state.Buffer?.FilePath ?? string.Empty;

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

        public void SetSelection(int start, int end)
        {
            _selection.SetSelection(start, end);
            Invalidate();
        }

        /// <summary>
        /// Get current selection as HexSelectionChangedEventArgs for syncing views
        /// </summary>
        public HexSelectionChangedEventArgs? GetSelection()
        {
            if (_state.CaretIndex < 0)
                return null;
            
            int length = _selection.GetSelectionLength();
            return new HexSelectionChangedEventArgs(_state.CaretIndex, length);
        }

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

            _scroll.Value = Math.Max(
                _scroll.Minimum,
                Math.Min(_scroll.Maximum - _scroll.LargeChange, state.ScrollOffset)
            );

            Invalidate();
        }

        internal void CommitByteEdit(int offset, byte oldValue, byte newValue)
        {
            if (oldValue == newValue)
                return;

            _editing.WriteByte(offset, newValue);

            // This is the async trigger point: controllers listen to this
            ByteChanged?.Invoke(offset, oldValue, newValue);

            RaiseBytesChanged();
            Invalidate();
        }

        public void ScrollTo(int offset)
        {
            int row = offset / HexEditorState.BytesPerRow;
            int y = row * _state.LineHeight;

            _state.ScrollOffsetY = y;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _repaintDebounceTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}