using System;
using System.Collections.Generic;

namespace ReverseEngineering.Core
{
    public class Patch
    {
        public int Offset { get; set; }
        public byte[] OriginalBytes { get; set; } = [];
        public byte[] NewBytes { get; set; } = [];
        public string Description { get; set; } = "";
    }

    public class PatchEngine(HexBuffer buffer)
    {
        private readonly HexBuffer _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        private readonly List<Patch> _patches = [];

        public IReadOnlyList<Patch> Patches => _patches;

        /// <summary>
        /// Applies a patch and records metadata for undo/redo or project saving.
        /// </summary>
        public void ApplyPatch(int offset, byte[] newBytes, string description = "")
        {
            if (newBytes == null || newBytes.Length == 0)
                throw new ArgumentException("Patch data cannot be null or empty.", nameof(newBytes));

            if (offset < 0 || offset + newBytes.Length > _buffer.Bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Patch exceeds buffer size.");

            // Capture original bytes
            byte[] original = new byte[newBytes.Length];
            Array.Copy(_buffer.Bytes, offset, original, 0, newBytes.Length);

            // Apply patch to buffer
            _buffer.WriteBytes(offset, newBytes);

            // Record patch metadata
            _patches.Add(new Patch
            {
                Offset = offset,
                OriginalBytes = original,
                NewBytes = newBytes,
                Description = description
            });
        }

        /// <summary>
        /// Returns a simplified list of (offset, value) pairs for project saving.
        /// </summary>
        public IEnumerable<(int offset, byte value)> GetFlatPatchList()
        {
            foreach (var p in _patches)
            {
                for (int i = 0; i < p.NewBytes.Length; i++)
                    yield return (p.Offset + i, p.NewBytes[i]);
            }
        }
    }
}