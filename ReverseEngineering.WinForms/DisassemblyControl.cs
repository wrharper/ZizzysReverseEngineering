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
        private int _displayStartIndex = 0;  // Start of current viewport
        private const int VIEWPORT_SIZE = 1000;  // Instructions to show at once

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
            var lineText = _instructions[_selectedIndex].ToString() ?? "";
            int lineLength = lineText.Length;

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
            _displayStartIndex = 0;

            if (_instructions.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            RefreshViewport();
        }

        /// <summary>
        /// Rebuild display for current viewport around _displayStartIndex
        /// </summary>
        private void RefreshViewport()
        {
            if (_instructions.Count == 0)
                return;

            int displayCount = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            if (displayCount <= 0)
            {
                _displayStartIndex = Math.Max(0, _instructions.Count - VIEWPORT_SIZE);
                displayCount = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            }

            int width = Is64Bit ? 16 : 8;
            string fmt = "{0:X" + width + "}: {1} {2}\n";

            var sb = new StringBuilder(displayCount * 40);
            string? currentSection = null;

            // Show context header
            if (_displayStartIndex > 0)
                sb.AppendLine($"[... {_displayStartIndex} instructions before ...]");

            for (int i = _displayStartIndex; i < _displayStartIndex + displayCount; i++)
            {
                var ins = _instructions[i];

                if (ins.SectionName != currentSection)
                {
                    if (currentSection != null)
                        sb.Append("\n");
                    
                    currentSection = ins.SectionName;
                    sb.AppendFormat("═══ {0} SECTION ═══\n", currentSection?.ToUpper() ?? "UNKNOWN");
                }

                sb.AppendFormat(fmt,
                    ins.Address,
                    ins.Mnemonic,
                    ins.Operands
                );
            }

            // Show remaining count
            if (_displayStartIndex + displayCount < _instructions.Count)
                sb.AppendLine($"[... {_instructions.Count - (_displayStartIndex + displayCount)} instructions after ...]");

            Text = sb.ToString();
            SelectionStart = 0;
            SelectionLength = 0;

            HighlightSelectedLine(Get_selectedIndex());
        }

        /// <summary>
        /// Jump to a specific instruction index
        /// </summary>
        public void JumpToInstruction(int instructionIndex)
        {
            if (instructionIndex < 0 || instructionIndex >= _instructions.Count)
                return;

            _displayStartIndex = Math.Max(0, instructionIndex - VIEWPORT_SIZE / 2);
            RefreshViewport();

            // Highlight the target instruction
            HighlightSelectedLine(instructionIndex - _displayStartIndex);
        }

        /// <summary>
        /// Jump to a specific address
        /// </summary>
        public void JumpToAddress(ulong address)
        {
            for (int i = 0; i < _instructions.Count; i++)
            {
                if (_instructions[i].Address == address)
                {
                    JumpToInstruction(i);
                    return;
                }
            }
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
            _displayStartIndex = 0;

            if (_instructions.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            RefreshViewportColored();
        }

        /// <summary>
        /// Rebuild colored display for current viewport
        /// </summary>
        private void RefreshViewportColored()
        {
            if (_instructions.Count == 0)
                return;

            int displayCount = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            if (displayCount <= 0)
            {
                _displayStartIndex = Math.Max(0, _instructions.Count - VIEWPORT_SIZE);
                displayCount = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            }

            int width = Is64Bit ? 16 : 8;
            string addrFmt = "{0:X" + width + "}: ";

            SuspendLayout();
            Clear();

            string? currentSection = null;

            // Show context header
            if (_displayStartIndex > 0)
            {
                SelectionColor = Color.Gray;
                AppendText($"[... {_displayStartIndex} instructions before ...]\n");
            }

            for (int i = _displayStartIndex; i < _displayStartIndex + displayCount; i++)
            {
                var ins = _instructions[i];

                if (ins.SectionName != currentSection)
                {
                    if (currentSection != null)
                        AppendText("\n");
                    
                    currentSection = ins.SectionName;
                    SelectionColor = Color.Yellow;
                    AppendText($"═══ {currentSection?.ToUpper() ?? "UNKNOWN"} SECTION ═══\n");
                }

                SelectionColor = Color.DarkGray;
                AppendText(string.Format(addrFmt, ins.Address));

                SelectionColor = Color.LimeGreen;
                AppendText(ins.Mnemonic);

                SelectionColor = Color.White;
                if (!string.IsNullOrWhiteSpace(ins.Operands))
                    AppendText(" " + ins.Operands);

                AppendText("\n");
            }

            // Show remaining count
            if (_displayStartIndex + displayCount < _instructions.Count)
            {
                SelectionColor = Color.Gray;
                AppendText($"[... {_instructions.Count - (_displayStartIndex + displayCount)} instructions after ...]\n");
            }

            SelectionStart = 0;
            SelectionLength = 0;
            SelectionColor = ForeColor;

            ResumeLayout();

            HighlightSelectedLine(Get_selectedIndex());
        }

        public ulong GetSelectedInstructionAddress()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _instructions.Count)
            {
                return _instructions[_selectedIndex].Address;
            }
            return 0;
        }
    }
}