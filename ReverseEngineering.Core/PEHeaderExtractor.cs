using System;
using System.Collections.Generic;
using System.Text;

namespace ReverseEngineering.Core
{
    /// <summary>
    /// Extracts and parses PE header information from binary data.
    /// Supports both PE32 and PE32+ (x64) formats.
    /// </summary>
    public class PEHeaderExtractor
    {
        public class PEInfo
        {
            public bool IsValid { get; set; }
            public bool Is64Bit { get; set; }
            public string Signature { get; set; } = string.Empty;
            
            // DOS Header
            public ushort DosSignature { get; set; }
            public uint PEOffset { get; set; }
            
            // NT Header (COFF)
            public ushort Machine { get; set; }
            public ushort NumberOfSections { get; set; }
            public uint TimeDateStamp { get; set; }
            public uint PointerToSymbolTable { get; set; }
            public uint NumberOfSymbols { get; set; }
            public ushort SizeOfOptionalHeader { get; set; }
            public ushort Characteristics { get; set; }
            
            // Optional Header
            public ushort Magic { get; set; }
            public byte MajorLinkerVersion { get; set; }
            public byte MinorLinkerVersion { get; set; }
            public uint SizeOfCode { get; set; }
            public uint SizeOfInitializedData { get; set; }
            public uint SizeOfUninitializedData { get; set; }
            public uint AddressOfEntryPoint { get; set; }
            public uint BaseOfCode { get; set; }
            public ulong ImageBase { get; set; }
            public uint SectionAlignment { get; set; }
            public uint FileAlignment { get; set; }
            public ushort MajorOperatingSystemVersion { get; set; }
            public ushort MinorOperatingSystemVersion { get; set; }
            public uint SizeOfImage { get; set; }
            public uint SizeOfHeaders { get; set; }
            public uint Subsystem { get; set; }
            
            // Sections
            public List<SectionHeader> Sections { get; set; } = [];
        }

        public class SectionHeader
        {
            public string Name { get; set; } = string.Empty;
            public uint VirtualSize { get; set; }
            public uint VirtualAddress { get; set; }
            public uint SizeOfRawData { get; set; }
            public uint PointerToRawData { get; set; }
            public uint Characteristics { get; set; }
            
            public override string ToString() => Name.TrimEnd('\0');
        }

        public static PEInfo Extract(byte[] data)
        {
            var info = new PEInfo();

            if (data.Length < 64)
            {
                info.IsValid = false;
                return info;
            }

            // DOS Header
            info.DosSignature = BitConverter.ToUInt16(data, 0);
            if (info.DosSignature != 0x5A4D) // "MZ"
            {
                info.IsValid = false;
                return info;
            }

            info.PEOffset = BitConverter.ToUInt32(data, 0x3C);
            if (info.PEOffset + 24 > data.Length)
            {
                info.IsValid = false;
                return info;
            }

            // PE Signature
            uint peSignature = BitConverter.ToUInt32(data, (int)info.PEOffset);
            if (peSignature != 0x4550) // "PE\0\0"
            {
                info.IsValid = false;
                return info;
            }

            info.Signature = "PE";
            int coffOffset = (int)info.PEOffset + 4;

            // COFF Header (20 bytes)
            info.Machine = BitConverter.ToUInt16(data, coffOffset);
            info.NumberOfSections = BitConverter.ToUInt16(data, coffOffset + 2);
            info.TimeDateStamp = BitConverter.ToUInt32(data, coffOffset + 4);
            info.PointerToSymbolTable = BitConverter.ToUInt32(data, coffOffset + 8);
            info.NumberOfSymbols = BitConverter.ToUInt32(data, coffOffset + 12);
            info.SizeOfOptionalHeader = BitConverter.ToUInt16(data, coffOffset + 16);
            info.Characteristics = BitConverter.ToUInt16(data, coffOffset + 18);

            int optionalOffset = coffOffset + 20;
            if (optionalOffset + info.SizeOfOptionalHeader > data.Length)
            {
                info.IsValid = true; // Headers are valid, just truncated
                return info;
            }

            // Optional Header
            info.Magic = BitConverter.ToUInt16(data, optionalOffset);
            info.Is64Bit = info.Magic == 0x20B; // PE32+ vs PE32
            info.MajorLinkerVersion = data[optionalOffset + 2];
            info.MinorLinkerVersion = data[optionalOffset + 3];
            info.SizeOfCode = BitConverter.ToUInt32(data, optionalOffset + 4);
            info.SizeOfInitializedData = BitConverter.ToUInt32(data, optionalOffset + 8);
            info.SizeOfUninitializedData = BitConverter.ToUInt32(data, optionalOffset + 12);
            info.AddressOfEntryPoint = BitConverter.ToUInt32(data, optionalOffset + 16);
            info.BaseOfCode = BitConverter.ToUInt32(data, optionalOffset + 20);

            if (info.Is64Bit)
            {
                // PE32+ (x64)
                info.ImageBase = BitConverter.ToUInt64(data, optionalOffset + 24);
                info.SectionAlignment = BitConverter.ToUInt32(data, optionalOffset + 32);
                info.FileAlignment = BitConverter.ToUInt32(data, optionalOffset + 36);
                info.MajorOperatingSystemVersion = BitConverter.ToUInt16(data, optionalOffset + 40);
                info.MinorOperatingSystemVersion = BitConverter.ToUInt16(data, optionalOffset + 42);
                info.SizeOfImage = BitConverter.ToUInt32(data, optionalOffset + 56);
                info.SizeOfHeaders = BitConverter.ToUInt32(data, optionalOffset + 60);
            }
            else
            {
                // PE32 (x86)
                info.ImageBase = BitConverter.ToUInt32(data, optionalOffset + 28);
                info.SectionAlignment = BitConverter.ToUInt32(data, optionalOffset + 32);
                info.FileAlignment = BitConverter.ToUInt32(data, optionalOffset + 36);
                info.MajorOperatingSystemVersion = BitConverter.ToUInt16(data, optionalOffset + 40);
                info.MinorOperatingSystemVersion = BitConverter.ToUInt16(data, optionalOffset + 42);
                info.SizeOfImage = BitConverter.ToUInt32(data, optionalOffset + 56);
                info.SizeOfHeaders = BitConverter.ToUInt32(data, optionalOffset + 60);
            }

            // Parse sections
            int sectionsOffset = optionalOffset + info.SizeOfOptionalHeader;
            for (int i = 0; i < info.NumberOfSections; i++)
            {
                int sectionOffset = sectionsOffset + (i * 40);
                if (sectionOffset + 40 > data.Length)
                    break;

                var section = new SectionHeader
                {
                    Name = Encoding.ASCII.GetString(data, sectionOffset, 8),
                    VirtualSize = BitConverter.ToUInt32(data, sectionOffset + 8),
                    VirtualAddress = BitConverter.ToUInt32(data, sectionOffset + 12),
                    SizeOfRawData = BitConverter.ToUInt32(data, sectionOffset + 16),
                    PointerToRawData = BitConverter.ToUInt32(data, sectionOffset + 20),
                    Characteristics = BitConverter.ToUInt32(data, sectionOffset + 36)
                };
                info.Sections.Add(section);
            }

            info.IsValid = true;
            return info;
        }

        public static string GetMachineType(ushort machine)
        {
            return machine switch
            {
                0x14C => "i386 (x86)",
                0x8664 => "AMD64 (x86-64)",
                0xAA64 => "ARM64",
                0x1C0 => "ARM (Thumb-2)",
                _ => $"Unknown (0x{machine:X4})"
            };
        }

        public static string GetSubsystem(uint subsystem)
        {
            return subsystem switch
            {
                1 => "Native",
                2 => "Windows GUI",
                3 => "Windows CUI",
                7 => "POSIX CUI",
                _ => $"Unknown ({subsystem})"
            };
        }

        public static string GetCharacteristics(ushort flags)
        {
            var parts = new List<string>();
            if ((flags & 0x0001) != 0) parts.Add("Relocs Stripped");
            if ((flags & 0x0002) != 0) parts.Add("Executable");
            if ((flags & 0x0004) != 0) parts.Add("Line Nums Stripped");
            if ((flags & 0x0008) != 0) parts.Add("Local Syms Stripped");
            if ((flags & 0x0010) != 0) parts.Add("Aggr WS");
            if ((flags & 0x0020) != 0) parts.Add("Large Addr Aware");
            if ((flags & 0x0080) != 0) parts.Add("Bytes Reversed Lo");
            if ((flags & 0x0100) != 0) parts.Add("32-bit Machine");
            if ((flags & 0x0200) != 0) parts.Add("Debug Stripped");
            if ((flags & 0x1000) != 0) parts.Add("System");
            if ((flags & 0x2000) != 0) parts.Add("DLL");
            return string.Join(", ", parts);
        }

        public static string GetSectionCharacteristics(uint flags)
        {
            var parts = new List<string>();
            if ((flags & 0x00000020) != 0) parts.Add("Code");
            if ((flags & 0x00000040) != 0) parts.Add("InitData");
            if ((flags & 0x00000080) != 0) parts.Add("UninitData");
            if ((flags & 0x04000000) != 0) parts.Add("Executable");
            if ((flags & 0x40000000) != 0) parts.Add("Readable");
            if ((flags & 0x80000000) != 0) parts.Add("Writable");
            return string.Join(", ", parts);
        }
    }
}
