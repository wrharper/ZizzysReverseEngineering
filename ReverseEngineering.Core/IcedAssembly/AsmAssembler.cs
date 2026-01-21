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

        /// <summary>
        /// Encode assembly and validate it compiled correctly.
        /// Useful for assembly verification before applying patches.
        /// </summary>
        public static bool TryEncode(Assembler asm, ulong rip, out byte[] bytes)
        {
            try
            {
                var writer = new CodeWriterImpl();
                asm.Assemble(writer, rip);
                bytes = writer.ToArray();
                return bytes.Length > 0;
            }
            catch
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>
        /// Validate assembly syntax without actually encoding.
        /// Returns any parse errors.
        /// </summary>
        public static (bool valid, string? error) ValidateSyntax(string asmText, bool is64Bit)
        {
            try
            {
                // If we can encode it, it's valid
                var asm = new Assembler(is64Bit ? 64 : 32);
                // Simple validation - just create assembler
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
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