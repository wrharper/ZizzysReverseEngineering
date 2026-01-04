using Iced.Intel;
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

        // NEW: Fast lookup for incremental disassembly
        private readonly Dictionary<ulong, int> _addressToIndex = [];

        // Base address for PE files (you can adjust this if needed)
        public ulong ImageBase { get; private set; } = 0;

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

            // Decode full disassembly
            Disassembly = Disassembler.DecodePE(bytes);

            // Build address map
            RebuildAddressIndex();
        }

        // ---------------------------------------------------------
        //  FULL REBUILD (used for load/project restore)
        // ---------------------------------------------------------
        public void RebuildDisassemblyFromBuffer()
        {
            if (HexBuffer.Bytes.Length == 0)
                return;

            Disassembly = Disassembler.DecodePE(HexBuffer.Bytes);
            RebuildAddressIndex();
        }

        // ---------------------------------------------------------
        //  ADDRESS / OFFSET MAPPING
        // ---------------------------------------------------------
        public int AddressToOffset(ulong address)
        {
            if (Disassembly.Count == 0)
                return -1;

            foreach (var ins in Disassembly)
            {
                if (ins.Address == address)
                    return ins.FileOffset;

                if (address > ins.Address && address < ins.EndAddress)
                    return ins.FileOffset + (int)(address - ins.Address);
            }

            return -1;
        }

        public ulong OffsetToAddress(int offset)
        {
            return ImageBase + (ulong)offset;
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

            // Full rebuild is correct for bulk patching
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
        //  INCREMENTAL DISASSEMBLY SUPPORT
        // ---------------------------------------------------------

        private void RebuildAddressIndex()
        {
            _addressToIndex.Clear();

            for (int i = 0; i < Disassembly.Count; i++)
                _addressToIndex[Disassembly[i].Address] = i;
        }

        public int FindInstructionIndexByOffset(int offset)
        {
            ulong address = OffsetToAddress(offset);

            // Fast path
            if (_addressToIndex.TryGetValue(address, out int idx))
                return idx;

            // Slow path: find instruction whose span contains this address
            for (int i = 0; i < Disassembly.Count; i++)
            {
                var ins = Disassembly[i];
                if (address >= ins.Address && address < ins.EndAddress)
                    return i;
            }

            return -1;
        }

        public void RebuildInstructionAtOffset(int offset)
        {
            if (HexBuffer == null || Disassembly.Count == 0)
                return;

            int index = FindInstructionIndexByOffset(offset);
            if (index < 0)
                return;

            var oldIns = Disassembly[index];

            // Re-decode instruction at this address
            var newIns = DisassembleSingle(oldIns.Address, oldIns.FileOffset);

            // Replace instruction
            Disassembly[index] = newIns;
            _addressToIndex[newIns.Address] = index;

            // If length changed, next instruction may be affected
            if (newIns.Length != oldIns.Length)
            {
                ulong nextAddr = newIns.EndAddress;
                int nextIndex = index + 1;

                if (nextIndex < Disassembly.Count)
                {
                    int nextOffset = AddressToOffset(nextAddr);
                    if (nextOffset >= 0)
                    {
                        var nextIns = DisassembleSingle(nextAddr, nextOffset);
                        Disassembly[nextIndex] = nextIns;
                        _addressToIndex[nextIns.Address] = nextIndex;
                    }
                }
            }
        }
        public static byte[]? Assemble(Assembler asm, ulong address)
        {
            try
            {
                var writer = new CodeWriterImpl();
                asm.Assemble(writer, address);
                return writer.ToArray();
            }
            catch
            {
                return null; // invalid ASM or assemble failure
            }
        }

        // ---------------------------------------------------------
        //  SINGLE INSTRUCTION DISASSEMBLY
        // ---------------------------------------------------------
        public Instruction DisassembleSingle(ulong address, int offset)
        {
            // Use your existing disassembler
            return Disassembler.DecodeSingleInstruction(
                HexBuffer.Bytes,
                offset,
                address,
                Is64Bit
            );
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