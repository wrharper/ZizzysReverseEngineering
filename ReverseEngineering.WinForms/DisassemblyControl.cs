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
        public string GetAllText()
        {
            return string.Join("\n", Lines);
        }

        public string GetVisibleText()
        {
            int f = GetCharIndexFromPosition(new Point(0, 0));
            int fl = GetLineFromCharIndex(f);
            int l = GetCharIndexFromPosition(new Point(0, Height - 1));
            int ll = GetLineFromCharIndex(l);
            if (ll < fl) ll = fl;

            var sb = new StringBuilder();
            for (int i = fl; i <= ll && i < Lines.Length; i++)
                sb.AppendLine(Lines[i]);
            return sb.ToString();
        }

        // NEW: full disassembly text (entire _instructions list, not just viewport)
        public string GetFullDisassemblyText()
        {
            if (_instructions == null || _instructions.Count == 0)
                return string.Empty;

            var sb = new StringBuilder(_instructions.Count * 40);

            string? currentSection = null;
            int w = Is64Bit ? 16 : 8;
            string fmt = "{0:X" + w + "}: {1} {2}\n";

            foreach (var ins in _instructions)
            {
                if (ins.SectionName != currentSection)
                {
                    if (currentSection != null)
                        sb.Append("\n");

                    currentSection = ins.SectionName;
                    sb.AppendFormat("═══ {0} SECTION ═══\n", currentSection?.ToUpper() ?? "UNKNOWN");
                }

                sb.AppendFormat(fmt, ins.Address, ins.Mnemonic, ins.Operands);
            }

            return sb.ToString();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Is64Bit { get; set; } = false;

        public event Action<ulong>? InstructionSelected;
        public event Action<int, string>? LineEdited;

        private List<Instruction> _instructions = [];
        private int _selectedIndex = -1;
        private int _displayStartIndex = 0;
        private const int VIEWPORT_SIZE = 1000;
        public int SelectedIndex => _selectedIndex;

        private readonly Color _highlightBack = Color.FromArgb(60, 90, 160);
        private readonly Color _highlightFore = Color.White;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedInstructionIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                HighlightSelectedLine(_selectedIndex);
                EnsureVisible(_selectedIndex);
            }
        }

        public AsmViewState GetViewState()
        {
            int f = GetCharIndexFromPosition(new Point(0, 0));
            int fl = GetLineFromCharIndex(f);
            return new AsmViewState
            {
                SelectedInstructionIndex = _selectedIndex,
                ScrollOffset = fl
            };
        }

        public void SetViewState(AsmViewState state)
        {
            if (state == null) return;
            _selectedIndex = state.SelectedInstructionIndex;
            HighlightSelectedLine(_selectedIndex);
            EnsureVisible(state.ScrollOffset);
        }

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

        private void Disasm_TextChanged(object? sender, EventArgs e)
        {
            int line = GetLineFromCharIndex(SelectionStart);
            if (line < 0 || line >= _instructions.Count) return;
            string text = Lines[line];
            LineEdited?.Invoke(line, text);
        }

        public void SelectInstruction(int index)
        {
            if (index < 0 || index >= _instructions.Count) return;
            _selectedIndex = index;
            HighlightSelectedLine(_selectedIndex);
            EnsureVisible(index);
        }

        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= _instructions.Count) return;
            int c = GetFirstCharIndexFromLine(index);
            if (c < 0) return;
            SelectionStart = c;
            SelectionLength = 0;
            ScrollToCaret();
        }

        private void Disasm_MouseClick(object? sender, MouseEventArgs e)
        {
            int index = GetLineIndexFromY(e.Y);
            if (index < 0 || index >= _instructions.Count) return;
            InstructionSelected?.Invoke(_instructions[index].Address);
        }

        private int GetLineIndexFromY(int y)
        {
            int c = GetCharIndexFromPosition(new Point(0, y));
            return GetLineFromCharIndex(c);
        }

        private void HighlightSelectedLine(int i)
        {
            if (i < 0 || i >= _instructions.Count) return;

            int s = SelectionStart, l = SelectionLength;

            SelectAll();
            SelectionBackColor = BackColor;
            SelectionColor = ForeColor;

            int ls = GetFirstCharIndexFromLine(i);
            var lt = _instructions[i].ToString() ?? "";
            int ll = lt.Length;

            if (ls >= 0)
            {
                SelectionStart = ls;
                SelectionLength = ll;
                SelectionBackColor = _highlightBack;
                SelectionColor = _highlightFore;
            }

            SelectionStart = s;
            SelectionLength = l;
        }

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

        private void RefreshViewport()
        {
            if (_instructions.Count == 0) return;

            int dc = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            if (dc <= 0)
            {
                _displayStartIndex = Math.Max(0, _instructions.Count - VIEWPORT_SIZE);
                dc = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            }

            int w = Is64Bit ? 16 : 8;
            string fmt = "{0:X" + w + "}: {1} {2}\n";
            var sb = new StringBuilder(dc * 40);
            string? cs = null;

            if (_displayStartIndex > 0)
                sb.AppendLine($"[... {_displayStartIndex} instructions before ...]");

            for (int i = _displayStartIndex; i < _displayStartIndex + dc; i++)
            {
                var ins = _instructions[i];
                if (ins.SectionName != cs)
                {
                    if (cs != null) sb.Append("\n");
                    cs = ins.SectionName;
                    sb.AppendFormat("═══ {0} SECTION ═══\n", cs?.ToUpper() ?? "UNKNOWN");
                }
                sb.AppendFormat(fmt, ins.Address, ins.Mnemonic, ins.Operands);
            }

            if (_displayStartIndex + dc < _instructions.Count)
                sb.AppendLine($"[... {_instructions.Count - (_displayStartIndex + dc)} instructions after ...]");

            Text = sb.ToString();
            SelectionStart = 0;
            SelectionLength = 0;
            HighlightSelectedLine(_selectedIndex);
        }

        public void JumpToInstruction(int instructionIndex)
        {
            if (instructionIndex < 0 || instructionIndex >= _instructions.Count) return;
            _displayStartIndex = Math.Max(0, instructionIndex - VIEWPORT_SIZE / 2);
            RefreshViewport();
            HighlightSelectedLine(instructionIndex - _displayStartIndex);
        }

        public void JumpToAddress(ulong address)
        {
            for (int i = 0; i < _instructions.Count; i++)
                if (_instructions[i].Address == address)
                {
                    JumpToInstruction(i);
                    return;
                }
        }

        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _instructions.Count) return;
            int c = GetFirstCharIndexFromLine(index);
            if (c < 0) return;
            SelectionStart = c;
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

        private void RefreshViewportColored()
        {
            if (_instructions.Count == 0) return;

            int dc = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            if (dc <= 0)
            {
                _displayStartIndex = Math.Max(0, _instructions.Count - VIEWPORT_SIZE);
                dc = Math.Min(VIEWPORT_SIZE, _instructions.Count - _displayStartIndex);
            }

            int w = Is64Bit ? 16 : 8;
            string addrFmt = "{0:X" + w + "}: ";

            SuspendLayout();
            Clear();

            string? cs = null;

            if (_displayStartIndex > 0)
            {
                SelectionColor = Color.Gray;
                AppendText($"[... {_displayStartIndex} instructions before ...]\n");
            }

            for (int i = _displayStartIndex; i < _displayStartIndex + dc; i++)
            {
                var ins = _instructions[i];

                if (ins.SectionName != cs)
                {
                    if (cs != null) AppendText("\n");
                    cs = ins.SectionName;
                    SelectionColor = Color.Yellow;
                    AppendText($"═══ {cs?.ToUpper() ?? "UNKNOWN"} SECTION ═══\n");
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

            if (_displayStartIndex + dc < _instructions.Count)
            {
                SelectionColor = Color.Gray;
                AppendText($"[... {_instructions.Count - (_displayStartIndex + dc)} instructions after ...]\n");
            }

            SelectionStart = 0;
            SelectionLength = 0;
            SelectionColor = ForeColor;
            ResumeLayout();

            HighlightSelectedLine(_selectedIndex);
        }

        public ulong GetSelectedInstructionAddress()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _instructions.Count)
                return _instructions[_selectedIndex].Address;
            return 0;
        }
    }
}