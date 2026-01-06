using Keystone;
using Keystone.Net;
using System;
using Microsoft.VisualBasic;

namespace ReverseEngineering.Core.Keystone
{
    public static class KeystoneAssembler
    {
        private static readonly object _lock = new();

        public static byte[] Assemble(string asmText, ulong address, bool is64Bit)
        {
            lock (_lock)
            {
                // Select architecture
                var arch = Architecture.X86;

                var mode = is64Bit ? Mode.MODE_64 : Mode.MODE_32;

                // Create Keystone engine
                using var ks = new Engine(arch, mode);

                // Set base address so relative jumps are correct
                ks.SetOption((uint)OptionType.SYNTAX, (uint)Syntax.SYNTAX_INTEL);

                ks.SetOption(OptionType.SYM_RESOLVER, null);

                // Assemble
                var result = ks.Assemble(asmText, address);

                if (result == null || result.Buffer == null)
                    throw new Exception("Keystone failed to assemble the given code.");

                return result.Buffer;
            }
        }
    }
}
