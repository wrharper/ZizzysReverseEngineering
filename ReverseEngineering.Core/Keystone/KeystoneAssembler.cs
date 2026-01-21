using Keystone.Net;
using System;
using System.Collections.Generic;

namespace ReverseEngineering.Core.Keystone
{
    public static class KeystoneAssembler
    {
        private static readonly object _lock = new();

        public static byte[] Assemble(string asmText, ulong address, bool is64Bit)
        {
            var (bytes, _) = AssembleWithInfo(asmText, address, is64Bit);
            return bytes;
        }

        /// <summary>
        /// Assemble with detailed error information.
        /// Useful for debugging assembly failures and providing user feedback.
        /// </summary>
        public static (byte[] bytes, AssemblyResult result) AssembleWithInfo(string asmText, ulong address, bool is64Bit)
        {
            lock (_lock)
            {
                var arch = Architecture.X86;
                var mode = is64Bit ? Mode.X64 : Mode.X32;

                using var ks = new Engine(arch, mode);
                ks.SetOption(OptionType.SYNTAX, (uint)OptionValue.SYNTAX_INTEL);

                try
                {
                    var encoded = ks.Assemble(asmText, address);
                    if (encoded?.Buffer != null)
                    {
                        return (encoded.Buffer, new AssemblyResult
                        {
                            Success = true,
                            Bytes = encoded.Buffer,
                            ByteCount = encoded.Buffer.Length,
                            StatementCount = asmText.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries).Length
                        });
                    }
                }
                catch (KeystoneException ex)
                {
                    return (Array.Empty<byte>(), new AssemblyResult
                    {
                        Success = false,
                        Error = ex.Message,
                        ErrorLine = -1
                    });
                }

                return (Array.Empty<byte>(), new AssemblyResult
                {
                    Success = false,
                    Error = "Assembly returned null buffer"
                });
            }
        }

        /// <summary>
        /// Batch assemble multiple instruction sequences.
        /// Useful for patch generation with multiple alternatives.
        /// </summary>
        public static List<(string code, byte[] bytes, bool success)> AssembleMultiple(
            List<string> asmCodes, 
            ulong baseAddress, 
            bool is64Bit)
        {
            var results = new List<(string, byte[], bool)>();
            ulong currentAddr = baseAddress;

            foreach (var code in asmCodes)
            {
                var (bytes, result) = AssembleWithInfo(code, currentAddr, is64Bit);
                results.Add((code, bytes, result.Success));
                currentAddr += (ulong)bytes.Length;
            }

            return results;
        }

        /// <summary>
        /// Validate assembly syntax without actually assembling.
        /// Returns true if valid, false if not.
        /// </summary>
        public static bool ValidateSyntax(string asmText, bool is64Bit)
        {
            var (_, result) = AssembleWithInfo(asmText, 0, is64Bit);
            return result.Success;
        }

        /// <summary>
        /// Get detailed statistics about assembled code.
        /// </summary>
        public static AssemblyStatistics GetAssemblyStatistics(string asmText, ulong address, bool is64Bit)
        {
            var (bytes, result) = AssembleWithInfo(asmText, address, is64Bit);
            
            if (!result.Success)
                return new AssemblyStatistics { Success = false };

            // Parse lines for statistics
            var lines = asmText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int instructionCount = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith(';'))
                    instructionCount++;
            }

            return new AssemblyStatistics
            {
                Success = true,
                TotalBytes = bytes.Length,
                EstimatedInstructions = instructionCount,
                AvgBytesPerInstruction = instructionCount > 0 ? bytes.Length / instructionCount : 0
            };
        }
    }

    /// <summary>
    /// Details about an assembly operation result.
    /// </summary>
    public class AssemblyResult
    {
        public bool Success { get; set; }
        public byte[]? Bytes { get; set; }
        public int ByteCount { get; set; }
        public int StatementCount { get; set; }
        public string? Error { get; set; }
        public int ErrorLine { get; set; } = -1;

        public override string ToString() =>
            Success 
                ? $"✓ Assembled {ByteCount} bytes from {StatementCount} statements"
                : $"✗ Assembly failed: {Error}" + (ErrorLine >= 0 ? $" (line {ErrorLine})" : "");
    }

    /// <summary>
    /// Statistics about assembled code for analysis.
    /// </summary>
    public class AssemblyStatistics
    {
        public bool Success { get; set; }
        public int TotalBytes { get; set; }
        public int EstimatedInstructions { get; set; }
        public int AvgBytesPerInstruction { get; set; }

        public override string ToString() =>
            Success
                ? $"{TotalBytes} bytes, ~{EstimatedInstructions} instructions, avg {AvgBytesPerInstruction} bytes/instr"
                : "Failed to assemble";
    }
}