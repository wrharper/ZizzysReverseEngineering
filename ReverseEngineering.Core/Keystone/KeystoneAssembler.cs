using Keystone.Net;
using System;
using System.Threading;
namespace ReverseEngineering.Core.Keystone
{
        public static class KeystoneAssembler            
    {
                private static readonly Lock _lock = new();
                public static byte[] Assemble(string asmText, ulong address, bool is64Bit)                        
        {
                        using (_lock.Acquire())                                    
            {
                                // Select architecture                var arch = KS_ARCH.X86;
                                var mode = is64Bit ? (int)Mode.MODE_64 : (int)Mode.MODE_32;
                                // Create Keystone engine                using var ks = new Engine(arch, mode);
                                // Set base address so relative jumps are correct                ks.SetOption((int)OptionType.SYNTAX_INTEL, 0);
                                ks.SetOption((int)OptionType.OPT_SYM_RESOLVER, null);
                                // Assemble                var result = ks.Assemble(asmText, address);
                                if (result == null || result.Buffer == null)                    throw new Exception("Keystone failed to assemble the given code.");
                                return result.Buffer;
                            
            }
                    
        }
            
    }
}
