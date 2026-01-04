using System;
using System.Collections.Generic;
using System.IO;

namespace ReverseEngineering.Core
{
    public class CoreEngine
    {
        public bool Is64Bit { get; private set; }
        public HexBuffer HexBuffer { get; private set; } = new HexBuffer([]);
        public List<Instruction> Disassembly { get; private set; } = [];

        private PatchEngine? _patchEngine;

        // ---------------------------------------------------------
        //  LOAD FILE
        // ---------------------------------------------------------
        public void LoadFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Binary not found", path);

            var bytes = File.ReadAllBytes(path);

            // Detect PE32 vs PE32+
            Is64Bit = DetectBitness(bytes);

            // Create buffer
            HexBuffer = new HexBuffer(bytes, path);

            // Patch engine for future undo/redo
            _patchEngine = new PatchEngine(HexBuffer);

            // Decode disassembly
            Disassembly = Disassembler.DecodePE(bytes);
        }

        // ---------------------------------------------------------
        //  REBUILD DISASSEMBLY AFTER PATCHES
        // ---------------------------------------------------------
        public void RebuildDisassemblyFromBuffer()
        {
            if (HexBuffer.Bytes.Length == 0)
                return;

            Disassembly = Disassembler.DecodePE(HexBuffer.Bytes);
        }

        // ---------------------------------------------------------
        //  ADDRESS / OFFSET MAPPING
        // ---------------------------------------------------------
        public int AddressToOffset(ulong address)
        {
            if (Disassembly.Count == 0)
                return -1;

            // Fast path: find instruction with matching VA
            foreach (var ins in Disassembly)
            {
                if (ins.Address == address)
                    return ins.FileOffset;

                if (address > ins.Address && address < ins.Address + (ulong)ins.Length)
                    return ins.FileOffset + (int)(address - ins.Address);
            }

            return -1;
        }

        public int OffsetToInstructionIndex(int offset)
        {
            if (Disassembly.Count == 0)
                return -1;

            for (int i = 0; i < Disassembly.Count; i++)
            {
                var ins = Disassembly[i];

                int start = ins.FileOffset;
                int end = start + ins.Length;

                if (offset >= start && offset < end)
                    return i;
            }

            return -1;
        }

        // ---------------------------------------------------------
        //  PATCHING
        // ---------------------------------------------------------
        public void ApplyPatch(int offset, byte[] newBytes, string description = "")
        {
            _patchEngine ??= new PatchEngine(HexBuffer);

            _patchEngine.ApplyPatch(offset, newBytes, description);

            // Re-decode disassembly
            RebuildDisassemblyFromBuffer();
        }

        public IEnumerable<(int offset, byte value)> GetFlatPatchList()
        {
            if (_patchEngine == null)
                yield break;

            foreach (var p in _patchEngine.GetFlatPatchList())
                yield return p;
        }

        // ---------------------------------------------------------
        //  INTERNAL HELPERS
        // ---------------------------------------------------------
        private static bool DetectBitness(byte[] bytes)
        {
            int peHeaderOffset = BitConverter.ToInt32(bytes, 0x3C);
            int optionalHeaderMagicOffset = peHeaderOffset + 4 + 20;
            ushort magic = BitConverter.ToUInt16(bytes, optionalHeaderMagicOffset);

            return magic == 0x20B; // PE32+ (64-bit)
        }
    }
}