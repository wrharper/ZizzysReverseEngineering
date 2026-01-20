using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Represents a named symbol (function, data, import, export, etc.).
    /// </summary>
    public class Symbol
    {
        public ulong Address { get; set; }
        public string Name { get; set; } = "";
        public string SymbolType { get; set; } = ""; // "function", "data", "import", "export", "string", etc.
        public string? Section { get; set; }
        public uint Size { get; set; }
        public bool IsImported { get; set; }
        public bool IsExported { get; set; }
        public string? SourceDLL { get; set; } // For imports

        public override string ToString() => $"Symbol @ 0x{Address:X}: {Name} ({SymbolType})";
    }

    /// <summary>
    /// Collects and normalizes symbols from various sources:
    /// - Imported functions (IAT)
    /// - Exported functions (EAT)
    /// - Named data sections
    /// - User annotations
    /// </summary>
    public static class SymbolResolver
    {
        private static readonly Dictionary<string, Symbol> _symbolCache = [];

        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------
        public static Dictionary<ulong, Symbol> ResolveSymbols(
            List<Instruction> disassembly,
            CoreEngine engine,
            bool includeImports = true,
            bool includeExports = true,
            bool includeStrings = false)
        {
            var symbols = new Dictionary<ulong, Symbol>();

            // Step 1: Add imported functions
            if (includeImports)
                AddImportedSymbols(engine, symbols);

            // Step 2: Add exported functions
            if (includeExports)
                AddExportedSymbols(engine, symbols);

            // Step 3: Add functions discovered by FunctionFinder
            AddDiscoveredFunctions(disassembly, symbols);

            // Step 4: Add strings (optional, slow)
            if (includeStrings)
                AddStringSymbols(engine, symbols);

            return symbols;
        }

        /// <summary>
        /// Lookup symbol name by address.
        /// </summary>
        public static string? GetSymbolName(ulong address, Dictionary<ulong, Symbol> symbols)
        {
            return symbols.TryGetValue(address, out var sym) ? sym.Name : null;
        }

        /// <summary>
        /// Lookup symbol by name (case-insensitive).
        /// </summary>
        public static Symbol? FindSymbolByName(string name, Dictionary<ulong, Symbol> symbols)
        {
            return symbols.Values.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // ---------------------------------------------------------
        //  SYMBOL COLLECTION
        // ---------------------------------------------------------
        private static void AddImportedSymbols(CoreEngine engine, Dictionary<ulong, Symbol> symbols)
        {
            // Extract from PE import table (IAT)
            // Scan the import address table for function pointers
            if (engine?.HexBuffer?.Bytes == null) return;

            var fileBytes = engine.HexBuffer.Bytes;
            
            try
            {
                ExtractImportAddressTable(fileBytes, symbols, engine.Is64Bit);
            }
            catch
            {
                // Silently fail if PE parsing fails
            }
        }

        private static void AddExportedSymbols(CoreEngine engine, Dictionary<ulong, Symbol> symbols)
        {
            // Extract from PE export table (EAT)
            if (engine?.HexBuffer?.Bytes == null) return;

            var fileBytes = engine.HexBuffer.Bytes;
            
            try
            {
                ExtractExportAddressTable(fileBytes, symbols);
            }
            catch
            {
                // Silently fail if PE parsing fails
            }
        }

        private static void ExtractImportAddressTable(byte[] fileBytes, Dictionary<ulong, Symbol> symbols, bool is64Bit)
        {
            using var stream = new System.IO.MemoryStream(fileBytes);
            using var reader = new System.IO.BinaryReader(stream);

            // Read DOS header to get PE offset
            stream.Position = 0x3C;
            if (stream.Position + 4 > fileBytes.Length) return;
            int peOffset = reader.ReadInt32();

            stream.Position = peOffset;
            if (stream.Position + 4 > fileBytes.Length) return;
            uint signature = reader.ReadUInt32();
            if (signature != 0x4550) return; // "PE\0\0"

            // Skip COFF header
            stream.Position += 2; // machine
            ushort numberOfSections = reader.ReadUInt16();
            stream.Position += 12;
            ushort sizeOfOptionalHeader = reader.ReadUInt16();

            // Read magic
            stream.Position = peOffset + 24;
            ushort magic = reader.ReadUInt16();
            if (magic != (is64Bit ? 0x20B : 0x10B)) return;

            // Get import table RVA from data directories
            int importTableIndex = 1; // Import Table is directory entry 1
            stream.Position = peOffset + 24 + sizeOfOptionalHeader - 16 + (importTableIndex * 8);
            if (stream.Position + 8 > fileBytes.Length) return;

            uint importTableRVA = reader.ReadUInt32();
            uint importTableSize = reader.ReadUInt32();

            if (importTableRVA == 0 || importTableSize == 0) return;

            // Find section containing import table
            stream.Position = peOffset + 24 + sizeOfOptionalHeader;
            ulong sectionOffset = (ulong)stream.Position;

            for (int i = 0; i < numberOfSections; i++)
            {
                byte[] sectionName = reader.ReadBytes(8);
                uint virtualSize = reader.ReadUInt32();
                uint virtualAddress = reader.ReadUInt32();
                uint rawSize = reader.ReadUInt32();
                uint rawPointer = reader.ReadUInt32();

                if (importTableRVA >= virtualAddress && importTableRVA < virtualAddress + Math.Max(virtualSize, rawSize))
                {
                    // Found the section with import table
                    uint offsetInSection = importTableRVA - virtualAddress;
                    stream.Position = rawPointer + offsetInSection;

                    // Parse Import Directory Table
                    for (int j = 0; j < 100; j++) // Safety limit
                    {
                        if (stream.Position + 20 > fileBytes.Length) break;

                        uint importNameTableRVA = reader.ReadUInt32();
                        reader.ReadUInt32(); // timestamp
                        reader.ReadUInt32(); // forwarder chain
                        uint nameRVA = reader.ReadUInt32();
                        uint importAddressTableRVA = reader.ReadUInt32();

                        if (importNameTableRVA == 0) break; // End of table

                        // Extract DLL name
                        string dllName = ExtractStringAtRVA(fileBytes, nameRVA, peOffset, numberOfSections, sectionOffset);

                        // Parse imported functions from IAT
                        int iatOffset = FindSectionOffset(fileBytes, importAddressTableRVA, peOffset, numberOfSections, sectionOffset);
                        if (iatOffset >= 0)
                        {
                            stream.Position = iatOffset;
                            for (int k = 0; k < 1000; k++) // Safety limit
                            {
                                if (stream.Position + (is64Bit ? 8 : 4) > fileBytes.Length) break;

                                ulong entry = is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
                                if (entry == 0) break; // End of IAT

                                ulong entryAddress = (ulong)iatOffset - (ulong)FindSectionOffset(fileBytes, importAddressTableRVA - 0x1000, peOffset, numberOfSections, sectionOffset) + (ulong)k * (is64Bit ? 8u : 4u);

                                // Add symbol for this import
                                if (!symbols.ContainsKey(entryAddress))
                                {
                                    symbols[entryAddress] = new Symbol
                                    {
                                        Address = entryAddress,
                                        Name = $"imp_{dllName}",
                                        SymbolType = "import",
                                        SourceDLL = dllName,
                                        IsImported = true
                                    };
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        private static void ExtractExportAddressTable(byte[] fileBytes, Dictionary<ulong, Symbol> symbols)
        {
            // Similar to IAT but for exports
            // This is a simplified version - full implementation would parse EAT completely
            using var stream = new System.IO.MemoryStream(fileBytes);
            using var reader = new System.IO.BinaryReader(stream);

            stream.Position = 0x3C;
            if (stream.Position + 4 > fileBytes.Length) return;
            int peOffset = reader.ReadInt32();

            stream.Position = peOffset;
            if (stream.Position + 4 > fileBytes.Length) return;
            uint signature = reader.ReadUInt32();
            if (signature != 0x4550) return;

            stream.Position += 2;
            ushort numberOfSections = reader.ReadUInt16();
            stream.Position += 12;
            ushort sizeOfOptionalHeader = reader.ReadUInt16();

            // Export table is directory entry 0
            stream.Position = peOffset + 24 + sizeOfOptionalHeader - 16;
            if (stream.Position + 8 > fileBytes.Length) return;

            uint exportTableRVA = reader.ReadUInt32();
            uint exportTableSize = reader.ReadUInt32();

            if (exportTableRVA == 0 || exportTableSize == 0) return;

            // Placeholder: Actual export table parsing would go here
            // For now, we log that exports are present
        }

        private static string ExtractStringAtRVA(byte[] fileBytes, uint rva, int peOffset, ushort numberOfSections, ulong sectionOffset)
        {
            int offset = FindSectionOffset(fileBytes, rva, peOffset, numberOfSections, sectionOffset);
            if (offset < 0 || offset >= fileBytes.Length) return "unknown";

            var str = new System.Text.StringBuilder();
            while (offset < fileBytes.Length && fileBytes[offset] != 0)
            {
                str.Append((char)fileBytes[offset]);
                offset++;
                if (str.Length > 256) break; // Safety limit
            }
            return str.ToString();
        }

        private static int FindSectionOffset(byte[] fileBytes, uint rva, int peOffset, ushort numberOfSections, ulong sectionOffset)
        {
            using var stream = new System.IO.MemoryStream(fileBytes);
            using var reader = new System.IO.BinaryReader(stream);

            stream.Position = (long)sectionOffset;
            for (int i = 0; i < numberOfSections; i++)
            {
                byte[] sectionName = reader.ReadBytes(8);
                uint virtualSize = reader.ReadUInt32();
                uint virtualAddress = reader.ReadUInt32();
                uint rawSize = reader.ReadUInt32();
                uint rawPointer = reader.ReadUInt32();

                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                {
                    return (int)(rawPointer + (rva - virtualAddress));
                }
            }
            return -1;
        }


        private static void AddDiscoveredFunctions(List<Instruction> disassembly, Dictionary<ulong, Symbol> symbols)
        {
            // Discover functions via FunctionFinder and add as symbols
            var functions = FunctionFinder.FindFunctions(disassembly, null!, includePrologues: true, includeCallGraph: false);

            foreach (var func in functions)
            {
                if (!symbols.ContainsKey(func.Address))
                {
                    symbols[func.Address] = new Symbol
                    {
                        Address = func.Address,
                        Name = func.Name ?? $"sub_{func.Address:X}",
                        SymbolType = "function",
                        Size = (uint)FunctionFinder.CalculateFunctionSize(disassembly, func.Address)
                    };
                }
            }
        }

        private static void AddStringSymbols(CoreEngine engine, Dictionary<ulong, Symbol> symbols)
        {
            // Scan data sections for null-terminated strings
            if (engine?.HexBuffer?.Bytes == null) return;

            var fileBytes = engine.HexBuffer.Bytes;
            var stringMatches = PatternMatcher.FindAllStrings(fileBytes, minLength: 4);

            foreach (var match in stringMatches)
            {
                if (!symbols.ContainsKey(match.Address))
                {
                    // Only add if string is in data section (rough heuristic: not in first 4KB which usually contains code)
                    if (match.Offset > 4096)
                    {
                        symbols[match.Address] = new Symbol
                        {
                            Address = match.Address,
                            Name = $"str_{match.Address:X}",
                            SymbolType = "string",
                            Size = (uint)(match.MatchedBytes?.Length ?? 0)
                        };
                    }
                }
            }
        }

        // ---------------------------------------------------------
        //  USER ANNOTATIONS
        // ---------------------------------------------------------
        public static void AddUserAnnotation(ulong address, string name, string symbolType, Dictionary<ulong, Symbol> symbols)
        {
            symbols[address] = new Symbol
            {
                Address = address,
                Name = name,
                SymbolType = symbolType
            };
        }

        public static void RemoveAnnotation(ulong address, Dictionary<ulong, Symbol> symbols)
        {
            symbols.Remove(address);
        }

        // ---------------------------------------------------------
        //  SYMBOL STATISTICS
        // ---------------------------------------------------------
        public static int CountByType(string symbolType, Dictionary<ulong, Symbol> symbols)
        {
            return symbols.Values.Count(s => s.SymbolType == symbolType);
        }

        public static IEnumerable<Symbol> GetSymbolsByType(string symbolType, Dictionary<ulong, Symbol> symbols)
        {
            return symbols.Values.Where(s => s.SymbolType == symbolType).OrderBy(s => s.Address);
        }
    }
}
