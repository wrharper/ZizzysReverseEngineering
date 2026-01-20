using Keystone.Net;
using System;

namespace ReverseEngineering.Core.Keystone
{
    public static class KeystoneAssembler
    {
        private static readonly object _lock = new();

        public static byte[] Assemble(string asmText, ulong address, bool is64Bit)
        {
            lock (_lock)
            {
                var arch = Architecture.X86;
                var mode = is64Bit ? Mode.X64 : Mode.X32;

                using var ks = new Engine(arch, mode);
                ks.SetOption(OptionType.SYNTAX, (uint)OptionValue.SYNTAX_INTEL);

                try
                {
                    var encoded = ks.Assemble(asmText, address);
                    if (encoded?.Buffer != null)
                        return encoded.Buffer;
                }
                catch (KeystoneException)
                {
                    // Assembly failed; fall through to return empty array.
                }

                // Return an empty byte array when assembly fails instead of throwing.
                return Array.Empty<byte>();
            }
        }
    }
}