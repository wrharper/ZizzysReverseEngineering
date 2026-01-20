using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Represents a match result from pattern matching.
    /// </summary>
    public class PatternMatch
    {
        public ulong Address { get; set; }
        public int Offset { get; set; }
        public byte[]? MatchedBytes { get; set; }
        public string? Description { get; set; }

        public override string ToString() => $"Match @ 0x{Address:X}+{Offset}: {Description}";
    }

    /// <summary>
    /// Pattern matching for byte signatures and instruction patterns.
    /// Supports:
    /// - Byte signatures with wildcards
    /// - Instruction pattern matching
    /// - Function prologue detection
    /// </summary>
    public static class PatternMatcher
    {
        // ---------------------------------------------------------
        //  BYTE PATTERN MATCHING
        // ---------------------------------------------------------
        /// <summary>
        /// Find byte pattern in buffer with wildcard support.
        /// Wildcard format: "??" represents any byte
        /// Example: "55 8B EC" (PUSH RBP; MOV RBP, RSP)
        ///          "48 89 E5 48 83 EC ??" (RBP prologue with any stack allocation)
        /// </summary>
        public static List<PatternMatch> FindBytePattern(byte[] buffer, string pattern, string? description = null)
        {
            var matches = new List<PatternMatch>();
            var patternBytesOpt = ParseBytePattern(pattern);

            if (!patternBytesOpt.HasValue)
                return matches;

            var patternBytes = patternBytesOpt.Value;
            if (patternBytes.Item1.Length == 0)
                return matches;

            for (int i = 0; i <= buffer.Length - patternBytes.Item1.Length; i++)
            {
                if (MatchesPattern(buffer, i, patternBytes))
                {
                    var matched = new byte[patternBytes.Item1.Length];
                    Array.Copy(buffer, i, matched, 0, patternBytes.Item1.Length);

                    matches.Add(new PatternMatch
                    {
                        Address = (ulong)i,
                        Offset = i,
                        MatchedBytes = matched,
                        Description = description
                    });
                }
            }

            return matches;
        }

        /// <summary>
        /// Find multiple patterns in buffer.
        /// </summary>
        public static Dictionary<string, List<PatternMatch>> FindMultiplePatterns(
            byte[] buffer,
            Dictionary<string, string> patterns)
        {
            var results = new Dictionary<string, List<PatternMatch>>();

            foreach (var (name, pattern) in patterns)
            {
                results[name] = FindBytePattern(buffer, pattern, name);
            }

            return results;
        }

        // ---------------------------------------------------------
        //  INSTRUCTION PATTERN MATCHING
        // ---------------------------------------------------------
        /// <summary>
        /// Find instructions matching a predicate.
        /// </summary>
        public static List<PatternMatch> FindInstructionPattern(
            List<Instruction> disassembly,
            Func<Instruction, bool> predicate,
            string? description = null)
        {
            var matches = new List<PatternMatch>();

            for (int i = 0; i < disassembly.Count; i++)
            {
                var ins = disassembly[i];
                if (predicate(ins))
                {
                    matches.Add(new PatternMatch
                    {
                        Address = ins.Address,
                        Offset = ins.FileOffset,
                        MatchedBytes = ins.Bytes,
                        Description = description
                    });
                }
            }

            return matches;
        }

        // ---------------------------------------------------------
        //  COMMON PATTERNS
        // ---------------------------------------------------------
        /// <summary>
        /// Find function prologues (x64 calling convention).
        /// Pattern: PUSH RBP; MOV RBP, RSP
        /// Bytes: 55 48 89 E5
        /// </summary>
        public static List<PatternMatch> FindX64Prologues(byte[] buffer)
        {
            return FindBytePattern(buffer, "55 48 89 E5", "x64_prologue_rbp");
        }

        /// <summary>
        /// Find stack setup pattern.
        /// Pattern: SUB RSP, imm
        /// Bytes: 48 83 EC ??
        /// </summary>
        public static List<PatternMatch> FindStackSetup(byte[] buffer)
        {
            return FindBytePattern(buffer, "48 83 EC ??", "stack_setup");
        }

        /// <summary>
        /// Find RET instructions.
        /// Bytes: C3 (near return)
        /// </summary>
        public static List<PatternMatch> FindReturnInstructions(byte[] buffer)
        {
            return FindBytePattern(buffer, "C3", "ret");
        }

        /// <summary>
        /// Find NOP sleds (alignment padding).
        /// Bytes: 90 90 90 90 ...
        /// </summary>
        public static List<PatternMatch> FindNOPSled(byte[] buffer, int minLength = 4)
        {
            var matches = new List<PatternMatch>();

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 0x90)
                {
                    int sledLength = 1;
                    while (i + sledLength < buffer.Length && buffer[i + sledLength] == 0x90)
                        sledLength++;

                    if (sledLength >= minLength)
                    {
                        matches.Add(new PatternMatch
                        {
                            Address = (ulong)i,
                            Offset = i,
                            MatchedBytes = new byte[sledLength],
                            Description = $"nop_sled_{sledLength}"
                        });

                        i += sledLength - 1;
                    }
                }
            }

            return matches;
        }

        // ---------------------------------------------------------
        //  PATTERN PARSING
        // ---------------------------------------------------------
        private static (byte[], bool[])? ParseBytePattern(string pattern)
        {
            var parts = pattern.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            var bytes = new List<byte>();
            var wildcards = new List<bool>();

            foreach (var part in parts)
            {
                if (part.Equals("??", StringComparison.OrdinalIgnoreCase))
                {
                    bytes.Add(0);
                    wildcards.Add(true);
                }
                else if (byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    bytes.Add(b);
                    wildcards.Add(false);
                }
                else
                {
                    return null; // Invalid pattern
                }
            }

            return (bytes.ToArray(), wildcards.ToArray());
        }

        private static bool MatchesPattern(byte[] buffer, int offset, (byte[], bool[]) pattern)
        {
            var (bytes, wildcards) = pattern;

            if (offset + bytes.Length > buffer.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (!wildcards[i] && buffer[offset + i] != bytes[i])
                    return false;
            }

            return true;
        }

        // ---------------------------------------------------------
        //  STRING SCANNING
        // ---------------------------------------------------------
        /// <summary>
        /// Find null-terminated ASCII strings in binary.
        /// </summary>
        public static List<PatternMatch> FindStrings(byte[] buffer, int minLength = 4)
        {
            var matches = new List<PatternMatch>();
            var currentString = new StringBuilder();
            int startOffset = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                byte b = buffer[i];

                // Printable ASCII (32-126) or newline/tab (9, 10, 13)
                if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
                {
                    if (currentString.Length == 0)
                        startOffset = i;

                    currentString.Append((char)b);
                }
                else
                {
                    // End of string
                    if (currentString.Length >= minLength)
                    {
                        var str = currentString.ToString();
                        var matched = Encoding.ASCII.GetBytes(str);

                        matches.Add(new PatternMatch
                        {
                            Address = (ulong)startOffset,
                            Offset = startOffset,
                            MatchedBytes = matched,
                            Description = $"String: {str}"
                        });
                    }
                    currentString.Clear();
                }
            }

            // Check last string
            if (currentString.Length >= minLength)
            {
                var str = currentString.ToString();
                var matched = Encoding.ASCII.GetBytes(str);

                matches.Add(new PatternMatch
                {
                    Address = (ulong)startOffset,
                    Offset = startOffset,
                    MatchedBytes = matched,
                    Description = $"String: {str}"
                });
            }

            return matches;
        }

        /// <summary>
        /// Find wide (UTF-16) strings in binary.
        /// </summary>
        public static List<PatternMatch> FindWideStrings(byte[] buffer, int minLength = 4)
        {
            var matches = new List<PatternMatch>();
            var currentString = new StringBuilder();
            int startOffset = 0;
            int charCount = 0;

            for (int i = 0; i < buffer.Length - 1; i += 2)
            {
                byte b1 = buffer[i];
                byte b2 = buffer[i + 1];

                // Valid wide char (low byte is printable, high byte is 0 or low)
                if ((b1 >= 32 && b1 <= 126) && (b2 == 0 || b2 < 32))
                {
                    if (charCount == 0)
                        startOffset = i;

                    currentString.Append((char)b1);
                    charCount++;
                }
                else if (b1 == 0 && b2 == 0)
                {
                    // Null terminator
                    if (charCount >= minLength / 2)
                    {
                        var str = currentString.ToString();
                        var matched = Encoding.Unicode.GetBytes(str);

                        matches.Add(new PatternMatch
                        {
                            Address = (ulong)startOffset,
                            Offset = startOffset,
                            MatchedBytes = matched,
                            Description = $"WideString: {str}"
                        });
                    }
                    currentString.Clear();
                    charCount = 0;
                }
                else
                {
                    if (charCount >= minLength / 2)
                    {
                        var str = currentString.ToString();
                        var matched = Encoding.Unicode.GetBytes(str);

                        matches.Add(new PatternMatch
                        {
                            Address = (ulong)startOffset,
                            Offset = startOffset,
                            MatchedBytes = matched,
                            Description = $"WideString: {str}"
                        });
                    }
                    currentString.Clear();
                    charCount = 0;
                }
            }

            return matches;
        }

        /// <summary>
        /// Find both ASCII and wide strings.
        /// </summary>
        public static List<PatternMatch> FindAllStrings(byte[] buffer, int minLength = 4)
        {
            var matches = new List<PatternMatch>();
            matches.AddRange(FindStrings(buffer, minLength));
            matches.AddRange(FindWideStrings(buffer, minLength));
            
            // Sort by offset
            return matches.OrderBy(m => m.Offset).ToList();
        }
    }
}
