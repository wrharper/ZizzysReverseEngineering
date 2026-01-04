using ReverseEngineering.Core;
using ReverseEngineering.Core.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    public class DisassemblyControl : RichTextBox
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Is64Bit { get; set; } = false;

        public event Action<ulong>? InstructionSelected;
        public event Action<int, string>? LineEdited;

        private List<Instruction> _instructions = [];
        private int _selectedIndex = -1;

        public int SelectedIndex => _selectedIndex;

        private readonly Color _highlightBack = Color.FromArgb(60, 90, 160);
        private readonly Color _highlightFore = Color.White;

        // ---------------------------------------------------------
        //  VIEW STATE PROPERTIES (Designer must NOT serialize)
        // ---------------------------------------------------------
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedInstructionIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                HighlightSelectedLine(Get_selectedIndex());
                EnsureVisible(_selectedIndex);
            }
        }

        // ---------------------------------------------------------
        //  GET / SET VIEW STATE (ProjectSystem integration)
        // ---------------------------------------------------------
        public AsmViewState GetViewState()
        {
            // First visible line based on top-left position
            int firstVisibleChar = GetCharIndexFromPosition(new Point(0, 0));
            int firstVisibleLine = GetLineFromCharIndex(firstVisibleChar);

            return new AsmViewState
            {
                SelectedInstructionIndex = _selectedIndex,
                ScrollOffset = firstVisibleLine
            };
        }

        public void SetViewState(AsmViewState state)
        {
            if (state == null)
                return;

            _selectedIndex = state.SelectedInstructionIndex;

            HighlightSelectedLine(Get_selectedIndex());
            EnsureVisible(state.ScrollOffset);
        }

        // ---------------------------------------------------------
        //  SINGLE VALID CONSTRUCTOR
        // ---------------------------------------------------------
        public DisassemblyControl()
        {
            ReadOnly = false;

            BorderStyle = BorderStyle.None;
            BackColor = Color.Black;
            ForeColor = Color.White;
            Font = new Font("Consolas", 10);
            WordWrap = false;
            DetectUrls = false;
            ScrollBars = RichTextBoxScrollBars.Vertical;

            MouseClick += Disasm_MouseClick;
            TextChanged += Disasm_TextChanged;
        }

        // ---------------------------------------------------------
        //  ASM EDITING EVENT
        // ---------------------------------------------------------
        private void Disasm_TextChanged(object? sender, EventArgs e)
        {
            int line = GetLineFromCharIndex(SelectionStart);
            if (line < 0 || line >= _instructions.Count)
                return;

            string text = Lines[line];
            LineEdited?.Invoke(line, text);
        }

        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------
        public void SelectInstruction(int index)
        {
            if (index < 0 || index >= _instructions.Count)
                return;

            _selectedIndex = index;

            HighlightSelectedLine(Get_selectedIndex());
            EnsureVisible(index);
        }

        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= _instructions.Count)
                return;

            int charIndex = GetFirstCharIndexFromLine(index);
            if (charIndex < 0)
                return;

            SelectionStart = charIndex;
            SelectionLength = 0;

            ScrollToCaret();
        }

        // ---------------------------------------------------------
        //  CLICK HANDLING
        // ---------------------------------------------------------
        private void Disasm_MouseClick(object? sender, MouseEventArgs e)
        {
            int index = GetLineIndexFromY(e.Y);
            if (index < 0 || index >= _instructions.Count)
                return;

            InstructionSelected?.Invoke(_instructions[index].Address);
        }

        private int GetLineIndexFromY(int y)
        {
            int charIndex = GetCharIndexFromPosition(new Point(0, y));
            return GetLineFromCharIndex(charIndex);
        }

        private int Get_selectedIndex()
        {
            return _selectedIndex;
        }

        // ---------------------------------------------------------
        //  HIGHLIGHTING
        // ---------------------------------------------------------
        private void HighlightSelectedLine(int _selectedIndex)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _instructions.Count)
                return;

            int savedStart = SelectionStart;
            int savedLength = SelectionLength;

            // Clear formatting
            SelectAll();
            SelectionBackColor = BackColor;
            SelectionColor = ForeColor;

            // Highlight selected line
            int lineStart = GetFirstCharIndexFromLine(_selectedIndex);
            int lineLength = _instructions[_selectedIndex].ToString().Length;

            if (lineStart >= 0)
            {
                SelectionStart = lineStart;
                SelectionLength = lineLength;

                SelectionBackColor = _highlightBack;
                SelectionColor = _highlightFore;
            }

            // Restore caret
            SelectionStart = savedStart;
            SelectionLength = savedLength;
        }

        // ---------------------------------------------------------
        //  LOADING INSTRUCTIONS
        // ---------------------------------------------------------
        public void SetInstructions(List<Instruction> instructions)
        {
            _instructions = instructions ?? [];

            if (_instructions.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            int width = Is64Bit ? 16 : 8;
            string fmt = "{0:X" + width + "}: {1} {2}\n";

            var sb = new StringBuilder(_instructions.Count * 40);

            foreach (var ins in _instructions)
            {
                sb.AppendFormat(fmt,
                    ins.Address,
                    ins.Mnemonic,
                    ins.Operands
                );
            }

            Text = sb.ToString();
            SelectionStart = 0;
            SelectionLength = 0;

            HighlightSelectedLine(Get_selectedIndex());
        }

        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _instructions.Count)
                return;

            int charIndex = GetFirstCharIndexFromLine(index);
            if (charIndex < 0)
                return;

            SelectionStart = charIndex;
            SelectionLength = 0;

            ScrollToCaret();
        }

        public void SetInstructionsColored(List<Instruction> instructions)
        {
            _instructions = instructions ?? [];

            if (_instructions.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            int width = Is64Bit ? 16 : 8;
            string addrFmt = "{0:X" + width + "}: ";

            SuspendLayout();
            Clear();

            foreach (var ins in _instructions)
            {
                SelectionColor = Color.DarkGray;
                AppendText(string.Format(addrFmt, ins.Address));

                SelectionColor = Color.LightGreen;
                AppendText(ins.Mnemonic);

                SelectionColor = Color.White;
                if (!string.IsNullOrWhiteSpace(ins.Operands))
                    AppendText(" " + ins.Operands);

                AppendText("\n");
            }

            SelectionStart = 0;
            SelectionLength = 0;
            SelectionColor = ForeColor;

            ResumeLayout();

            HighlightSelectedLine(Get_selectedIndex());
        }
    }
}