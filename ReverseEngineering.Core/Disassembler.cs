using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Iced.Intel;

namespace ReverseEngineering.Core
{
    public static class Disassembler
    {
        public static List<ReverseEngineering.Core.Instruction> DecodePE(byte[] fileBytes)
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
            //  FIND EXECUTABLE SECTION
            // ---------------------------------------------------------
            const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000;

            int textIndex = sections.FindIndex(s => (s.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0);
            if (textIndex < 0)
                throw new Exception("No executable section found.");

            var text = sections[textIndex];

            // ---------------------------------------------------------
            //  EXTRACT CODE BYTES
            // ---------------------------------------------------------
            byte[] code = new byte[text.RawSize];
            Array.Copy(fileBytes, text.RawOffset, code, 0, text.RawSize);

            // ---------------------------------------------------------
            //  DECODE USING ICED
            // ---------------------------------------------------------
            var codeReader = new ByteArrayCodeReader(code);
            var decoder = Iced.Intel.Decoder.Create(is64 ? 64 : 32, codeReader);

            ulong sectionVA = imageBase + text.RVA;
            decoder.IP = sectionVA;

            var formatter = new NasmFormatter();
            var output = new StringOutput();

            while (codeReader.CanReadByte)
            {
                ulong currentIP = decoder.IP;

                var icedIns = decoder.Decode();
                if (icedIns.Code == Code.INVALID)
                    break;

                output.Reset();
                formatter.Format(icedIns, output);

                string formatted = output.ToString();
                int space = formatted.IndexOf(' ');
                string mnemonic = space > 0 ? formatted[..space] : formatted;
                string operands = space > 0 ? formatted[(space + 1)..] : "";

                int offsetInSection = (int)(currentIP - sectionVA);

                byte[] insBytes = new byte[icedIns.Length];
                Array.Copy(code, offsetInSection, insBytes, 0, icedIns.Length);

                // ---------------------------------------------------------
                //  BUILD INSTRUCTION OBJECT
                // ---------------------------------------------------------
                var ins = new ReverseEngineering.Core.Instruction
                {
                    Raw = icedIns,
                    Address = currentIP,
                    RVA = (uint)(currentIP - imageBase),
                    FileOffset = (int)(text.RawOffset + offsetInSection),
                    SectionIndex = textIndex,

                    Mnemonic = mnemonic,
                    Operands = operands,

                    Length = icedIns.Length,
                    Bytes = insBytes,

                    IsCall = icedIns.FlowControl == FlowControl.Call,
                    IsJump = icedIns.FlowControl == FlowControl.UnconditionalBranch,
                    IsConditionalJump = icedIns.FlowControl == FlowControl.ConditionalBranch,
                    IsReturn = icedIns.FlowControl == FlowControl.Return,
                    IsNop = icedIns.Mnemonic == Mnemonic.Nop
                };

                result.Add(ins);
            }

            return result;
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