using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Iced.Intel;

namespace ReverseEngineering.Core
{
    public static class Disassembler
    {
        /// <summary>
        /// Progress callback: (instructionsProcessed, totalEstimate)
        /// </summary>
        public delegate void ProgressCallback(int processed, int total);

        public static List<ReverseEngineering.Core.Instruction> DecodePE(byte[] fileBytes, ProgressCallback? onProgress = null)
        {
            var result = new List<ReverseEngineering.Core.Instruction>();

            using var stream = new MemoryStream(fileBytes);
            using var reader = new BinaryReader(stream);

            // ---------------------------------------------------------
            //  DOS HEADER
            // ---------------------------------------------------------
            stream.Position = 0x3C;
            int peHeaderOffset = reader.ReadInt32();

            // ---------------------------------------------------------
            //  PE SIGNATURE
            // ---------------------------------------------------------
            stream.Position = peHeaderOffset;
            uint signature = reader.ReadUInt32(); // "PE\0\0"

            // ---------------------------------------------------------
            //  COFF HEADER
            // ---------------------------------------------------------
            ushort machine = reader.ReadUInt16();
            ushort numberOfSections = reader.ReadUInt16();
            reader.BaseStream.Position += 12; // skip timestamp + symbol table
            ushort sizeOfOptionalHeader = reader.ReadUInt16();
            ushort characteristics = reader.ReadUInt16();

            // ---------------------------------------------------------
            //  OPTIONAL HEADER
            // ---------------------------------------------------------
            ushort magic = reader.ReadUInt16();
            bool is64 = magic == 0x20B;
            bool is32 = magic == 0x10B;

            if (!is32 && !is64)
                throw new NotSupportedException($"Unknown PE magic: 0x{magic:X}");

            ulong imageBase;
            uint addressOfEntryPoint;
            uint sectionAlignment;
            uint fileAlignment;

            if (is64)
            {
                // PE32+
                reader.ReadByte(); // MajorLinkerVersion
                reader.ReadByte(); // MinorLinkerVersion
                reader.ReadUInt32(); // SizeOfCode
                reader.ReadUInt32(); // SizeOfInitializedData
                reader.ReadUInt32(); // SizeOfUninitializedData
                addressOfEntryPoint = reader.ReadUInt32();
                reader.ReadUInt32(); // BaseOfCode
                imageBase = reader.ReadUInt64();
                sectionAlignment = reader.ReadUInt32();
                fileAlignment = reader.ReadUInt32();

                long optionalHeaderEnd = peHeaderOffset + 4 + 20 + sizeOfOptionalHeader;
                reader.BaseStream.Position = optionalHeaderEnd;
            }
            else
            {
                // PE32
                reader.ReadByte(); // MajorLinkerVersion
                reader.ReadByte(); // MinorLinkerVersion
                reader.ReadUInt32(); // SizeOfCode
                reader.ReadUInt32(); // SizeOfInitializedData
                reader.ReadUInt32(); // SizeOfUninitializedData
                addressOfEntryPoint = reader.ReadUInt32();
                uint baseOfCode = reader.ReadUInt32();
                uint baseOfData = reader.ReadUInt32();
                imageBase = reader.ReadUInt32();
                sectionAlignment = reader.ReadUInt32();
                fileAlignment = reader.ReadUInt32();

                long optionalHeaderEnd = peHeaderOffset + 4 + 20 + sizeOfOptionalHeader;
                reader.BaseStream.Position = optionalHeaderEnd;
            }

            // ---------------------------------------------------------
            //  SECTION HEADERS
            // ---------------------------------------------------------
            var sections = new List<SectionInfo>();

            for (int i = 0; i < numberOfSections; i++)
            {
                var nameBytes = reader.ReadBytes(8);
                string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                uint virtualSize = reader.ReadUInt32();
                uint virtualAddress = reader.ReadUInt32();
                uint sizeOfRawData = reader.ReadUInt32();
                uint pointerToRawData = reader.ReadUInt32();

                reader.ReadUInt32(); // reloc ptr
                reader.ReadUInt32(); // line ptr
                reader.ReadUInt16(); // num reloc
                reader.ReadUInt16(); // num line

                uint sectCharacteristics = reader.ReadUInt32();

                sections.Add(new SectionInfo
                {
                    Name = name,
                    RVA = virtualAddress,
                    RawOffset = pointerToRawData,
                    RawSize = sizeOfRawData,
                    VirtualSize = virtualSize,
                    Characteristics = sectCharacteristics
                });
            }

            // ---------------------------------------------------------
            //  FIND ALL EXECUTABLE SECTIONS
            // ---------------------------------------------------------
            const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000;

            var executableSections = new List<(SectionInfo section, int index)>();
            for (int i = 0; i < sections.Count; i++)
            {
                if ((sections[i].Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0)
                {
                    executableSections.Add((sections[i], i));
                }
            }

            if (executableSections.Count == 0)
                throw new Exception("No executable sections found.");

            // ---------------------------------------------------------
            //  DECODE ALL EXECUTABLE SECTIONS
            // ---------------------------------------------------------
            // Calculate total bytes to process across all executable sections
            long totalBytesToProcess = 0;
            foreach (var (section, _) in executableSections)
            {
                totalBytesToProcess += section.RawSize;
            }

            long bytesProcessed = 0;

            foreach (var (sectionInfo, sectionIndex) in executableSections)
            {
                // Extract code bytes for this section
                byte[] code = new byte[sectionInfo.RawSize];
                Array.Copy(fileBytes, sectionInfo.RawOffset, code, 0, sectionInfo.RawSize);

                // Decode using Iced
                var codeReader = new ByteArrayCodeReader(code);
                var decoder = Iced.Intel.Decoder.Create(is64 ? 64 : 32, codeReader);

                ulong sectionVA = imageBase + sectionInfo.RVA;
                decoder.IP = sectionVA;

                var formatter = new NasmFormatter();
                var output = new StringOutput();

                int instructionsInSection = 0;
                int lastProgressReport = 0;

                while (codeReader.CanReadByte)
                {
                    ulong currentIP = decoder.IP;

                    var icedIns = decoder.Decode();
                    if (icedIns.Code == Code.INVALID)
                    {
                        // Skip invalid/padding bytes (common in code sections)
                        decoder.IP += 1;
                        continue;
                    }

                    output.Reset();
                    formatter.Format(icedIns, output);

                    string formatted = output.ToString();
                    int space = formatted.IndexOf(' ');
                    string mnemonic = space > 0 ? formatted[..space] : formatted;
                    string operands = space > 0 ? formatted[(space + 1)..] : "";

                    int offsetInSection = (int)(currentIP - sectionVA);

                    // Validate we have enough bytes left in the section
                    int bytesToCopy = Math.Min(icedIns.Length, (int)(code.Length - offsetInSection));
                    if (bytesToCopy <= 0)
                        continue;

                    byte[] insBytes = new byte[bytesToCopy];
                    Array.Copy(code, offsetInSection, insBytes, 0, bytesToCopy);

                    // Build instruction object
                    var ins = new ReverseEngineering.Core.Instruction
                    {
                        Raw = icedIns,
                        Address = currentIP,
                        RVA = (uint)(currentIP - imageBase),
                        FileOffset = (int)(sectionInfo.RawOffset + offsetInSection),
                        SectionIndex = sectionIndex,
                        SectionName = sectionInfo.Name,

                        Mnemonic = mnemonic,
                        Operands = operands,

                        Length = bytesToCopy,
                        Bytes = insBytes,

                        IsCall = icedIns.FlowControl == FlowControl.Call,
                        IsJump = icedIns.FlowControl == FlowControl.UnconditionalBranch,
                        IsConditionalJump = icedIns.FlowControl == FlowControl.ConditionalBranch,
                        IsReturn = icedIns.FlowControl == FlowControl.Return,
                        IsNop = icedIns.Mnemonic == Mnemonic.Nop
                    };

                    result.Add(ins);
                    instructionsInSection++;

                    // Report progress every 50 instructions based on bytes processed
                    if (instructionsInSection - lastProgressReport >= 50)
                    {
                        lastProgressReport = instructionsInSection;
                        // Calculate progress based on bytes processed vs total bytes
                        long currentBytesInSection = bytesProcessed + offsetInSection;
                        int progressPercent = totalBytesToProcess > 0 
                            ? (int)((currentBytesInSection * 100) / totalBytesToProcess)
                            : 0;
                        // Report as (percentage, 100) for easier scaling
                        onProgress?.Invoke(progressPercent, 100);
                    }
                }

                // Update bytes processed after completing this section
                bytesProcessed += sectionInfo.RawSize;

                // Report progress after section complete
                int sectionProgressPercent = totalBytesToProcess > 0 
                    ? (int)((bytesProcessed * 100) / totalBytesToProcess)
                    : 0;
                onProgress?.Invoke(sectionProgressPercent, 100);
            }

            return result;
        }

        public static Instruction DecodeSingleInstruction(byte[] bytes, int offset, ulong address, bool is64Bit)
        {
            var reader = new ByteArrayCodeReader(bytes)
            {
                Position = offset
            };

            var decoder = Iced.Intel.Decoder.Create(is64Bit ? 64 : 32, reader);
            decoder.IP = address;

            var icedIns = decoder.Decode();

            return new Instruction
            {
                Address = address,
                FileOffset = offset,
                Length = icedIns.Length,
                Bytes = bytes.AsSpan(offset, icedIns.Length).ToArray(),
                Raw = icedIns,
                Mnemonic = icedIns.Mnemonic.ToString(),
                Operands = icedIns.Op0Kind.ToString(), // you can format this better
                IsCall = icedIns.FlowControl == FlowControl.Call,
                IsJump = icedIns.FlowControl == FlowControl.UnconditionalBranch,
                IsConditionalJump = icedIns.FlowControl == FlowControl.ConditionalBranch,
                IsReturn = icedIns.FlowControl == FlowControl.Return,
                IsNop = icedIns.Mnemonic == Mnemonic.Nop
            };
        }
        // ---------------------------------------------------------
        //  INTERNAL SECTION STRUCT
        // ---------------------------------------------------------
        private class SectionInfo
        {
            public string Name = "";
            public uint RVA;
            public uint RawOffset;
            public uint RawSize;
            public uint VirtualSize;
            public uint Characteristics;
        }
    }
}