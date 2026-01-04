using System.Collections.Generic;
using Iced.Intel;

namespace ReverseEngineering.Core.IcedAssembly
{
    public static class AsmAssembler
    {
        public static byte[] Encode(Assembler asm, ulong rip)
        {
            var writer = new CodeWriterImpl();
            asm.Assemble(writer, rip);
            return writer.ToArray();
        }

        private sealed class CodeWriterImpl : CodeWriter
        {
            private readonly List<byte> _bytes = [];

            public override void WriteByte(byte value)
            {
                _bytes.Add(value);
            }

            public byte[] ToArray() => [.. _bytes];
        }
    }
}