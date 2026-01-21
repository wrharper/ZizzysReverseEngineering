using ReverseEngineering.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Virtual scrolling disassembly viewer - renders only visible instructions.
    /// Handles multi-MB binaries with constant memory footprint.
    /// Uses theme colors for syntax highlighting.
    /// </summary>
    public class DisassemblyViewerVirtual : Control
    {
        private List<Instruction> _allInstructions = [];
        private int _selectedIndex = -1;
        private int _topVisibleIndex = 0;  // First instruction at top of viewport
        private int _visibleLineCount = 20;  // Lines that fit on screen
        private int _lineHeight = 18;
        private int _addressWidth = 140;  // Width of address column
        private VScrollBar _scrollBar;  // Vertical scrollbar
        private bool _suspendLayout = false;  // Prevent layout recalc when hidden
        
        private readonly Font _font = new Font("Consolas", 10);
        
        // Brushes and pens - updated on theme change
        private SolidBrush _foreground = new SolidBrush(Color.White);
        private SolidBrush _background = new SolidBrush(Color.Black);
        private SolidBrush _addressBrush = new SolidBrush(Color.FromArgb(100, 180, 255));
        private SolidBrush _mnemonicBrush = new SolidBrush(Color.FromArgb(150, 220, 100));
        private SolidBrush _operandBrush = new SolidBrush(Color.FromArgb(220, 150, 100));
        private SolidBrush _immediateBrush = new SolidBrush(Color.FromArgb(200, 200, 100));
        private SolidBrush _commentBrush = new SolidBrush(Color.FromArgb(100, 150, 100));
        private SolidBrush _selectedBack = new SolidBrush(Color.FromArgb(60, 90, 160));
        private Pen _gridPen = new Pen(Color.FromArgb(40, 40, 80));

        public event Action<ulong>? InstructionSelected;

        public int SelectedIndex => _selectedIndex;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Is64Bit { get; set; } = true;  // Display format for addresses

        public DisassemblyViewerVirtual()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            ForeColor = Color.White;
            Font = _font;

            // Create and configure scrollbar
            _scrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                SmallChange = 1,
                LargeChange = Math.Max(1, _visibleLineCount)
            };
            _scrollBar.ValueChanged += ScrollBar_ValueChanged;
            Controls.Add(_scrollBar);
            
            // Apply theme on creation
            ApplyTheme(ThemeManager.CurrentTheme);
        }

        /// <summary>
        /// Apply theme colors to disassembly viewer
        /// </summary>
        public void ApplyTheme(AppTheme theme)
        {
            BackColor = theme.BackColor;
            ForeColor = theme.ForeColor;
            
            // Dispose old brushes/pens
            _foreground.Dispose();
            _background.Dispose();
            _addressBrush.Dispose();
            _mnemonicBrush.Dispose();
            _operandBrush.Dispose();
            _immediateBrush.Dispose();
            _commentBrush.Dispose();
            _selectedBack.Dispose();
            _gridPen.Dispose();
            
            // Create new brushes with theme colors
            _foreground = new SolidBrush(theme.ForeColor);
            _background = new SolidBrush(theme.BackColor);
            _addressBrush = new SolidBrush(theme.SyntaxAddress);
            _mnemonicBrush = new SolidBrush(theme.SyntaxMnemonic);
            _operandBrush = new SolidBrush(theme.SyntaxOperand);
            _immediateBrush = new SolidBrush(theme.SyntaxImmediate);
            _commentBrush = new SolidBrush(theme.SyntaxComment);
            _selectedBack = new SolidBrush(theme.SyntaxSelectedBg);
            _gridPen = new Pen(theme.Separator);
            
            Invalidate();
        }

        /// <summary>
        /// Set the instruction list and recalculate viewport.
        /// </summary>
        public void SetInstructions(List<Instruction> instructions)
        {
            _allInstructions = instructions ?? [];
            _topVisibleIndex = 0;
            _selectedIndex = -1;
            UpdateScrollBar();
            InvalidateLayout();
        }

        private void UpdateScrollBar()
        {
            if (_allInstructions.Count <= _visibleLineCount)
            {
                // All instructions fit on screen, disable scrollbar
                _scrollBar.Maximum = 0;
                _scrollBar.Value = 0;
                _scrollBar.Enabled = false;
            }
            else
            {
                // Calculate scrollbar range
                int scrollableRange = _allInstructions.Count - _visibleLineCount;
                _scrollBar.Maximum = scrollableRange;
                _scrollBar.Value = Math.Min(_topVisibleIndex, scrollableRange);
                _scrollBar.LargeChange = Math.Max(1, _visibleLineCount);
                _scrollBar.Enabled = true;
            }
        }

        private void ScrollBar_ValueChanged(object? sender, EventArgs e)
        {
            _topVisibleIndex = _scrollBar.Value;
            Invalidate();
        }

        /// <summary>
        /// Select an instruction by index, scrolling it into view.
        /// </summary>
        public void SelectInstruction(int index)
        {
            if (index < 0 || index >= _allInstructions.Count)
                return;

            _selectedIndex = index;

            // Scroll to make it visible
            if (index < _topVisibleIndex)
                _topVisibleIndex = index;
            else if (index >= _topVisibleIndex + _visibleLineCount)
                _topVisibleIndex = Math.Max(0, index - _visibleLineCount + 1);

            UpdateScrollBar();
            Invalidate();
        }

        /// <summary>
        /// Scroll to show a specific instruction.
        /// </summary>
        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= _allInstructions.Count)
                return;

            if (index < _topVisibleIndex)
                _topVisibleIndex = index;
            else if (index >= _topVisibleIndex + _visibleLineCount)
                _topVisibleIndex = Math.Max(0, index - _visibleLineCount / 2);

            UpdateScrollBar();
            Invalidate();
        }

        /// <summary>
        /// Get current view state (for project save/restore).
        /// </summary>
        public (int selectedIndex, int scrollOffset) GetViewState()
        {
            return (_selectedIndex, _topVisibleIndex);
        }

        /// <summary>
        /// Restore view state.
        /// </summary>
        public void SetViewState(int selectedIndex, int scrollOffset)
        {
            _selectedIndex = Math.Max(-1, Math.Min(selectedIndex, _allInstructions.Count - 1));
            _topVisibleIndex = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, _allInstructions.Count - _visibleLineCount)));
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_suspendLayout)
                InvalidateLayout();
        }

        /// <summary>
        /// Called when visibility changes - suspend layout when hidden
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // Suspend layout calculations while hidden to improve performance
            _suspendLayout = !Visible;
        }

        private void InvalidateLayout()
        {
            // Account for scrollbar width when calculating visible lines
            int availableWidth = Width - (_scrollBar.Visible ? _scrollBar.Width : 0);
            _visibleLineCount = (Height / _lineHeight) + 2;  // +2 for buffer
            UpdateScrollBar();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.Black);

            if (_allInstructions.Count == 0)
            {
                e.Graphics.DrawString("No instructions loaded", _font, _foreground, 10, 10);
                return;
            }

            // Draw visible instructions
            int endIndex = Math.Min(_topVisibleIndex + _visibleLineCount, _allInstructions.Count);
            int y = 0;

            for (int i = _topVisibleIndex; i < endIndex; i++)
            {
                DrawInstructionLine(e.Graphics, _allInstructions[i], i, y);
                y += _lineHeight;
            }

            // Draw scroll hint at bottom
            if (endIndex < _allInstructions.Count)
            {
                string hint = $"... {_allInstructions.Count - endIndex} more instructions";
                e.Graphics.DrawString(hint, _font, new SolidBrush(Color.Gray), 10, Height - _lineHeight);
            }
        }

        private void DrawInstructionLine(Graphics g, Instruction instr, int index, int y)
        {
            bool isSelected = (index == _selectedIndex);

            // Draw selection background
            if (isSelected)
            {
                g.FillRectangle(_selectedBack, 0, y, Width, _lineHeight);
            }

            int x = 5;

            // Draw address
            string addrStr = $"0x{instr.Address:X}";
            g.DrawString(addrStr, _font, _addressBrush, x, y);
            x += _addressWidth;

            // Draw separator
            g.DrawLine(_gridPen, x - 5, y, x - 5, y + _lineHeight);

            // Draw mnemonic
            g.DrawString(instr.Mnemonic, _font, _mnemonicBrush, x, y);
            x += 80;

            // Draw operands
            g.DrawString(instr.Operands, _font, _operandBrush, x, y);

            // Draw annotation if present
            if (!string.IsNullOrEmpty(instr.Annotation))
            {
                x = Width - 200;
                g.DrawString($"; {instr.Annotation}", _font, _commentBrush, x, y);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            int lineIndex = e.Y / _lineHeight;
            int instructionIndex = _topVisibleIndex + lineIndex;

            if (instructionIndex >= 0 && instructionIndex < _allInstructions.Count)
            {
                SelectInstruction(instructionIndex);
                InstructionSelected?.Invoke(_allInstructions[instructionIndex].Address);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            int delta = (e.Delta > 0) ? -3 : 3;  // Negative = scroll up
            _topVisibleIndex = Math.Max(0, Math.Min(_topVisibleIndex + delta, Math.Max(0, _allInstructions.Count - _visibleLineCount)));

            UpdateScrollBar();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if ((keyData & Keys.Up) == Keys.Up || (keyData & Keys.Down) == Keys.Down ||
                (keyData & Keys.PageUp) == Keys.PageUp || (keyData & Keys.PageDown) == Keys.PageDown)
                return true;

            return base.IsInputKey(keyData);
        }

        /// <summary>
        /// Show Go To Address dialog and navigate to instruction.
        /// Only works if instructions are loaded.
        /// </summary>
        public void ShowGoToDialog()
        {
            if (_allInstructions.Count == 0)
            {
                MessageBox.Show("Disassembly not yet loaded. Please wait for binary loading to complete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new GoToAddressDialog(
                _selectedIndex >= 0 && _selectedIndex < _allInstructions.Count 
                    ? _allInstructions[_selectedIndex].Address 
                    : 0,
                isVirtual: true
            );

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // Find instruction by address
                int index = _allInstructions.FindIndex(i => i.Address == dialog.Address);
                if (index >= 0)
                {
                    SelectInstruction(index);
                    InstructionSelected?.Invoke(_allInstructions[index].Address);
                }
                else
                {
                    MessageBox.Show("Address not found in disassembly.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Up:
                    if (_selectedIndex > 0)
                        SelectInstruction(_selectedIndex - 1);
                    e.Handled = true;
                    break;

                case Keys.Down:
                    if (_selectedIndex < _allInstructions.Count - 1)
                        SelectInstruction(_selectedIndex + 1);
                    e.Handled = true;
                    break;

                case Keys.PageUp:
                    _topVisibleIndex = Math.Max(0, _topVisibleIndex - _visibleLineCount);
                    UpdateScrollBar();
                    Invalidate();
                    e.Handled = true;
                    break;

                case Keys.PageDown:
                    _topVisibleIndex = Math.Min(_topVisibleIndex + _visibleLineCount, Math.Max(0, _allInstructions.Count - _visibleLineCount));
                    UpdateScrollBar();
                    Invalidate();
                    e.Handled = true;
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font?.Dispose();
                _foreground?.Dispose();
                _background?.Dispose();
                _addressBrush?.Dispose();
                _mnemonicBrush?.Dispose();
                _operandBrush?.Dispose();
                _selectedBack?.Dispose();
                _gridPen?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
