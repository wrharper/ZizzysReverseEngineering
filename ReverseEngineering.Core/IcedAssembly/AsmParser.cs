using System;
using Iced.Intel;

namespace ReverseEngineering.Core.IcedAssembly
{
    public static class AsmParser
    {
        public static Assembler FromText(string asmText, bool is64Bit, ulong rip)
        {
            var asm = new Assembler(is64Bit ? 64 : 32);

            foreach (var rawLine in asmText.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(';'))
                    continue;

                ParseLine(asm, line, rip);
            }

            return asm;
        }

        private static void ParseLine(Assembler asm, string line, ulong rip)
        {
            int space = line.IndexOf(' ');
            string mnemonic = (space < 0 ? line : line[..space]).ToLower();
            string ops = (space < 0 ? "" : line[(space + 1)..]).Trim();

            switch (mnemonic)
            {
                case "mov":
                    ParseMov(asm, ops);
                    break;

                case "jmp":
                    ParseJmpRel32(asm, ops, rip);
                    break;

                case "call":
                    ParseCallRel32(asm, ops, rip);
                    break;

                case "ret":
                    asm.ret();
                    break;

                case "nop":
                    asm.nop();
                    break;

                default:
                    throw new NotSupportedException($"Unsupported instruction: {line}");
            }
        }

        private static void ParseMov(Assembler asm, string ops)
        {
            var parts = ops.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new FormatException("Invalid mov syntax");

            var dst = ParseRegister(parts[0]);
            var src = parts[1];

            // hex immediate
            if (src.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(src[2..], System.Globalization.NumberStyles.HexNumber, null, out ulong immHex))
            {
                asm.mov(dst, (long)immHex);
                return;
            }

            // decimal immediate
            if (long.TryParse(src, out long immDec))
            {
                asm.mov(dst, immDec);
                return;
            }

            // register
            var srcReg = ParseRegister(src);
            asm.mov(dst, srcReg);
        }

        // jmp rel32: E9 <rel32>
        private static void ParseJmpRel32(Assembler asm, string ops, ulong rip)
        {
            string t = ops.Trim();

            long rel;

            // hex absolute address
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out ulong target))
            {
                rel = (long)target - (long)(rip + 5);
            }
            // relative immediate
            else if (long.TryParse(t, out long relImm))
            {
                rel = relImm;
            }
            else
            {
                throw new FormatException("Invalid jmp target");
            }

            asm.db(0xE9);
            asm.dd((int)rel);
        }

        // call rel32: E8 <rel32>
        private static void ParseCallRel32(Assembler asm, string ops, ulong rip)
        {
            string t = ops.Trim();

            long rel;

            // hex absolute address
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out ulong target))
            {
                rel = (long)target - (long)(rip + 5);
            }
            // relative immediate
            else if (long.TryParse(t, out long relImm))
            {
                rel = relImm;
            }
            else
            {
                throw new FormatException("Invalid call target");
            }

            asm.db(0xE8);
            asm.dd((int)rel);
        }

        private static AssemblerRegister64 ParseRegister(string text)
        {
            return text.ToLower() switch
            {
                "rax" => AssemblerRegisters.rax,
                "rbx" => AssemblerRegisters.rbx,
                "rcx" => AssemblerRegisters.rcx,
                "rdx" => AssemblerRegisters.rdx,
                "rsi" => AssemblerRegisters.rsi,
                "rdi" => AssemblerRegisters.rdi,
                "rbp" => AssemblerRegisters.rbp,
                "rsp" => AssemblerRegisters.rsp,
                "r8" => AssemblerRegisters.r8,
                "r9" => AssemblerRegisters.r9,
                "r10" => AssemblerRegisters.r10,
                "r11" => AssemblerRegisters.r11,
                "r12" => AssemblerRegisters.r12,
                "r13" => AssemblerRegisters.r13,
                "r14" => AssemblerRegisters.r14,
                "r15" => AssemblerRegisters.r15,

                _ => throw new NotSupportedException($"Unknown register: {text}")
            };
        }
    }
}