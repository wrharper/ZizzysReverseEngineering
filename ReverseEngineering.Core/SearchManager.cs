using System;
using System.Collections.Generic;
using System.Linq;
using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.Core
{
    /// <summary>
    /// Search result from various search operations.
    /// </summary>
    public class SearchResult
    {
        public ulong Address { get; set; }
        public int Offset { get; set; }
        public string ResultType { get; set; } = ""; // "byte", "instruction", "function", "symbol", etc.
        public string? Description { get; set; }
        public byte[]? Data { get; set; }

        public override string ToString() => $"0x{Address:X}: {Description} ({ResultType})";
    }

    /// <summary>
    /// Unified search interface for binary, disassembly, and analysis results.
    /// </summary>
    public static class SearchManager
    {
        // ---------------------------------------------------------
        //  BYTE SEARCH
        // ---------------------------------------------------------
        /// <summary>
        /// Search for byte sequence in buffer.
        /// </summary>
        public static List<SearchResult> SearchBytes(HexBuffer buffer, byte[] pattern)
        {
            var results = new List<SearchResult>();

            if (pattern.Length == 0 || pattern.Length > buffer.Bytes.Length)
                return results;

            for (int i = 0; i <= buffer.Bytes.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer.Bytes[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    results.Add(new SearchResult
                    {
                        Address = (ulong)i,
                        Offset = i,
                        ResultType = "byte",
                        Description = $"Byte sequence match",
                        Data = (byte[])pattern.Clone()
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Search for byte pattern with wildcards.
        /// </summary>
        public static List<SearchResult> SearchBytePattern(HexBuffer buffer, string pattern)
        {
            var matches = PatternMatcher.FindBytePattern(buffer.Bytes, pattern, "pattern");
            return matches.Select(m => new SearchResult
            {
                Address = m.Address,
                Offset = (int)m.Address,
                ResultType = "pattern",
                Description = m.Description,
                Data = m.MatchedBytes
            }).ToList();
        }

        // ---------------------------------------------------------
        //  INSTRUCTION SEARCH
        // ---------------------------------------------------------
        /// <summary>
        /// Search for instructions by mnemonic.
        /// </summary>
        public static List<SearchResult> SearchInstructionsByMnemonic(List<Instruction> disassembly, string mnemonic)
        {
            var results = new List<SearchResult>();

            foreach (var ins in disassembly)
            {
                if (ins.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        Address = ins.Address,
                        Offset = ins.FileOffset,
                        ResultType = "instruction",
                        Description = $"{ins.Mnemonic} {ins.Operands}"
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Search for all instructions matching a predicate.
        /// </summary>
        public static List<SearchResult> SearchInstructions(
            List<Instruction> disassembly,
            Func<Instruction, bool> predicate,
            string resultType = "instruction")
        {
            var results = new List<SearchResult>();

            foreach (var ins in disassembly)
            {
                if (predicate(ins))
                {
                    results.Add(new SearchResult
                    {
                        Address = ins.Address,
                        Offset = ins.FileOffset,
                        ResultType = resultType,
                        Description = $"{ins.Mnemonic} {ins.Operands}"
                    });
                }
            }

            return results;
        }

        // ---------------------------------------------------------
        //  FUNCTION SEARCH
        // ---------------------------------------------------------
        /// <summary>
        /// Search for functions by name (case-insensitive).
        /// </summary>
        public static List<SearchResult> SearchFunctionsByName(List<Function> functions, string name)
        {
            var results = new List<SearchResult>();

            foreach (var func in functions)
            {
                if (func.Name != null && func.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        Address = func.Address,
                        Offset = -1,
                        ResultType = "function",
                        Description = $"{func.Name} ({func.InstructionCount} instrs)"
                    });
                }
            }

            return results;
        }

        // ---------------------------------------------------------
        //  SYMBOL SEARCH
        // ---------------------------------------------------------
        /// <summary>
        /// Search for symbols by name (case-insensitive).
        /// </summary>
        public static List<SearchResult> SearchSymbolsByName(Dictionary<ulong, Symbol> symbols, string name)
        {
            var results = new List<SearchResult>();

            foreach (var sym in symbols.Values)
            {
                if (sym.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        Address = sym.Address,
                        Offset = -1,
                        ResultType = "symbol",
                        Description = $"{sym.Name} ({sym.SymbolType})"
                    });
                }
            }

            return results;
        }

        // ---------------------------------------------------------
        //  CROSS-REFERENCE SEARCH
        // ---------------------------------------------------------
        /// <summary>
        /// Find all references to an address.
        /// </summary>
        public static List<SearchResult> FindReferencesToAddress(
            ulong address,
            Dictionary<ulong, List<CrossReference>> xrefs)
        {
            var results = new List<SearchResult>();

            foreach (var (source, refs) in xrefs)
            {
                foreach (var xref in refs.Where(r => r.TargetAddress == address))
                {
                    results.Add(new SearchResult
                    {
                        Address = source,
                        Offset = -1,
                        ResultType = $"xref_{xref.RefType}",
                        Description = $"Reference from 0x{source:X} ({xref.RefType})"
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Find all references FROM an address.
        /// </summary>
        public static List<SearchResult> FindReferencesFromAddress(
            ulong address,
            Dictionary<ulong, List<CrossReference>> xrefs)
        {
            if (!xrefs.TryGetValue(address, out var refs))
                return [];

            return refs.Select(r => new SearchResult
            {
                Address = r.TargetAddress,
                Offset = -1,
                ResultType = $"xref_{r.RefType}",
                Description = $"Reference to 0x{r.TargetAddress:X} ({r.RefType})"
            }).ToList();
        }

        // ---------------------------------------------------------
        //  HEX CONVERSION
        // ---------------------------------------------------------
        /// <summary>
        /// Convert hex string to bytes.
        /// Example: "48 89 E5" or "4889E5"
        /// </summary>
        public static byte[]? HexStringToBytes(string hexString)
        {
            var cleaned = hexString.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "");

            if (cleaned.Length % 2 != 0)
                return null;

            var bytes = new byte[cleaned.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(cleaned.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    return null;
            }

            return bytes;
        }
    }
}
