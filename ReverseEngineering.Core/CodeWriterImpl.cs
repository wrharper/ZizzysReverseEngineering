using Iced.Intel;
using System.Collections.Generic;

namespace ReverseEngineering.Core
{
    internal sealed class CodeWriterImpl : CodeWriter
    {
        private readonly List<byte> _bytes = [];

        public override void WriteByte(byte value)
        {
            _bytes.Add(value);
        }

        public byte[] ToArray() => [.. _bytes];
    }
}