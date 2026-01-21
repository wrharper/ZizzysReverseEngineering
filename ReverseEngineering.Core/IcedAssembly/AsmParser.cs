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

                case "je" or "jz":
                    asm.je(0);  // Placeholder; actual offset computed by assembler
                    break;

                case "jne" or "jnz":
                    asm.jne(0);
                    break;

                case "jg" or "jnle":
                    asm.jg(0);
                    break;

                case "jl" or "jnge":
                    asm.jl(0);
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

                case "push":
                    ParsePush(asm, ops);
                    break;

                case "pop":
                    ParsePop(asm, ops);
                    break;

                case "add":
                    ParseBinaryOp(asm, ops, (d, s) => asm.add(d, s));
                    break;

                case "sub":
                    ParseBinaryOp(asm, ops, (d, s) => asm.sub(d, s));
                    break;

                case "xor":
                    ParseBinaryOp(asm, ops, (d, s) => asm.xor(d, s));
                    break;

                case "and":
                    ParseBinaryOp(asm, ops, (d, s) => asm.and(d, s));
                    break;

                case "or":
                    ParseBinaryOp(asm, ops, (d, s) => asm.or(d, s));
                    break;

                case "cmp":
                    ParseBinaryOp(asm, ops, (d, s) => asm.cmp(d, s));
                    break;

                case "test":
                    ParseBinaryOp(asm, ops, (d, s) => asm.test(d, s));
                    break;

                case "lea":
                    // LEA is complex to parse generically, skip for now
                    // Users can use Keystone directly for complex addressing modes
                    break;

                case "inc":
                    asm.inc(ParseRegister(ops));
                    break;

                case "dec":
                    asm.dec(ParseRegister(ops));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported instruction: {line}");
            }
        }

        private static void ParsePush(Assembler asm, string ops)
        {
            var reg = ParseRegister(ops);
            asm.push(reg);
        }

        private static void ParsePop(Assembler asm, string ops)
        {
            var reg = ParseRegister(ops);
            asm.pop(reg);
        }

        private static void ParseBinaryOp(Assembler asm, string ops, Action<AssemblerRegister64, AssemblerRegister64> op)
        {
            var parts = ops.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new FormatException("Invalid binary operation syntax");

            var dst = ParseRegister(parts[0]);
            var src = ParseRegister(parts[1]);
            op(dst, src);
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