using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core
{
    /// <summary>
    /// Optimizes disassembly performance for large binaries through intelligent caching,
    /// incremental analysis, and lazy loading of expensive metadata.
    /// </summary>
    public class DisassemblyOptimizer
    {
        private readonly Dictionary<ulong, Instruction> _addressToInstructionCache = [];
        private readonly Dictionary<int, Instruction> _offsetToInstructionCache = [];
        private bool _cacheValid;
        private ulong _cachedRangeStart;
        private ulong _cachedRangeEnd;

        public DisassemblyOptimizer()
        {
            InvalidateCache();
        }

        // ---------------------------------------------------------
        //  CACHE MANAGEMENT
        // ---------------------------------------------------------

        /// <summary>
        /// Mark all caches as invalid (call after major disassembly changes)
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
            _cachedRangeStart = 0;
            _cachedRangeEnd = 0;
        }

        /// <summary>
        /// Invalidate cache for a specific address range (call after patch)
        /// </summary>
        public void InvalidateCacheRange(ulong start, ulong end)
        {
            // If range overlaps with cached range, invalidate
            if (start <= _cachedRangeEnd && end >= _cachedRangeStart)
            {
                InvalidateCache();
            }
        }

        /// <summary>
        /// Build cache from instruction list (call once after disassembly)
        /// </summary>
        public void BuildCache(List<Instruction> instructions)
        {
            _addressToInstructionCache.Clear();
            _offsetToInstructionCache.Clear();

            if (instructions.Count == 0)
            {
                InvalidateCache();
                return;
            }

            foreach (var instr in instructions)
            {
                _addressToInstructionCache[instr.Address] = instr;
                _offsetToInstructionCache[instr.FileOffset] = instr;
            }

            _cacheValid = true;
            _cachedRangeStart = instructions.First().Address;
            _cachedRangeEnd = instructions.Last().EndAddress;
        }

        // ---------------------------------------------------------
        //  FAST LOOKUPS
        // ---------------------------------------------------------

        /// <summary>
        /// O(1) lookup: address → instruction
        /// </summary>
        public bool TryGetInstructionAt(ulong address, out Instruction? instruction)
        {
            if (_cacheValid)
            {
                return _addressToInstructionCache.TryGetValue(address, out instruction);
            }

            instruction = null;
            return false;
        }

        /// <summary>
        /// O(1) lookup: file offset → instruction
        /// </summary>
        public bool TryGetInstructionAtOffset(int offset, out Instruction? instruction)
        {
            if (_cacheValid)
            {
                return _offsetToInstructionCache.TryGetValue(offset, out instruction);
            }

            instruction = null;
            return false;
        }

        /// <summary>
        /// Get all instructions in address range
        /// </summary>
        public List<Instruction> GetInstructionsInRange(ulong start, ulong end)
        {
            if (!_cacheValid)
                return [];

            return _addressToInstructionCache.Values
                .Where(i => i.Address >= start && i.Address < end)
                .OrderBy(i => i.Address)
                .ToList();
        }

        // ---------------------------------------------------------
        //  BATCH OPERATIONS
        // ---------------------------------------------------------

        /// <summary>
        /// Batch update: Apply metadata to multiple instructions (e.g., function analysis results)
        /// More efficient than individual updates
        /// </summary>
        public void BatchUpdateMetadata(
            List<(ulong address, ulong? functionAddr, string? symbolName)> updates)
        {
            foreach (var (addr, funcAddr, symbol) in updates)
            {
                if (TryGetInstructionAt(addr, out var instr) && instr != null)
                {
                    instr.FunctionAddress = funcAddr;
                    instr.SymbolName = symbol;
                }
            }
        }

        /// <summary>
        /// Lazy load annotations: Only load when needed (not on every disassembly)
        /// </summary>
        public void LazyLoadAnnotations(
            Func<ulong, string?> annotationProvider)
        {
            if (!_cacheValid)
                return;

            foreach (var instr in _addressToInstructionCache.Values)
            {
                if (string.IsNullOrEmpty(instr.Annotation))
                {
                    instr.Annotation = annotationProvider(instr.Address);
                }
            }
        }

        // ---------------------------------------------------------
        //  PERFORMANCE METRICS
        // ---------------------------------------------------------

        public struct CacheStats
        {
            public int CachedInstructions { get; set; }
            public bool IsValid { get; set; }
            public ulong CachedRangeStart { get; set; }
            public ulong CachedRangeEnd { get; set; }
            public ulong CachedRangeSize => CachedRangeEnd - CachedRangeStart;

            public override string ToString()
            {
                return IsValid
                    ? $"Cache: {CachedInstructions} instructions, Range: 0x{CachedRangeStart:X}-0x{CachedRangeEnd:X} ({CachedRangeSize} bytes)"
                    : "Cache: Invalid";
            }
        }

        /// <summary>
        /// Get cache statistics for debugging/performance analysis
        /// </summary>
        public CacheStats GetStats()
        {
            return new CacheStats
            {
                CachedInstructions = _addressToInstructionCache.Count,
                IsValid = _cacheValid,
                CachedRangeStart = _cachedRangeStart,
                CachedRangeEnd = _cachedRangeEnd
            };
        }

        /// <summary>
        /// Clear all caches and free memory
        /// </summary>
        public void ClearAndDispose()
        {
            _addressToInstructionCache.Clear();
            _offsetToInstructionCache.Clear();
            InvalidateCache();
        }
    }

    /// <summary>
    /// Batch operand analyzer: Process multiple instructions for RIP-relative targets
    /// and other operand metadata (more efficient than analyzing one-by-one)
    /// </summary>
    public class BatchOperandAnalyzer
    {
        /// <summary>
        /// Analyze RIP-relative operands for a range of instructions
        /// </summary>
        public static void AnalyzeRIPRelativeOperands(
            List<Instruction> instructions,
            ulong imageBase,
            Func<ulong, string?> symbolResolver)
        {
            foreach (var instr in instructions)
            {
                if (instr.Raw == null)
                    continue;

                // Check for RIP-relative memory operand
                for (int i = 0; i < instr.Raw.Value.OpCount; i++)
                {
                    var op = instr.Raw.Value.GetOpKind(i);

                    if (op == Iced.Intel.OpKind.Memory)
                    {
                        var mem = instr.Raw.Value.MemoryBase;
                        
                        // RIP-relative on x64
                        if (mem == Iced.Intel.Register.RIP)
                        {
                            var displacement = instr.Raw.Value.MemoryDisplacement64;
                            var ripNext = instr.Address + (ulong)instr.Length;
                            var target = ripNext + (ulong)(long)displacement;

                            instr.RIPRelativeTarget = target;
                            instr.OperandType = symbolResolver(target) ?? "Data";
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Build a quick lookup table for operand types in a binary
        /// </summary>
        public static Dictionary<ulong, string> BuildOperandTypeLookup(
            List<Instruction> instructions)
        {
            var lookup = new Dictionary<ulong, string>();

            foreach (var instr in instructions)
            {
                if (instr.RIPRelativeTarget.HasValue && !string.IsNullOrEmpty(instr.OperandType))
                {
                    lookup[instr.Address] = instr.OperandType;
                }
            }

            return lookup;
        }
    }

    /// <summary>
    /// Memory-efficient instruction storage for very large binaries
    /// Uses packed representation instead of object per instruction
    /// </summary>
    public struct PackedInstruction
    {
        public ulong Address { get; set; }
        public int FileOffset { get; set; }
        public short Length { get; set; }  // Usually < 15 bytes
        public byte MnemonicHash { get; set; }  // Hash of mnemonic for quick comparison
        public byte[] Bytes { get; set; }

        public static PackedInstruction FromInstruction(Instruction instr)
        {
            return new PackedInstruction
            {
                Address = instr.Address,
                FileOffset = instr.FileOffset,
                Length = (short)instr.Length,
                Bytes = instr.Bytes,
                MnemonicHash = (byte)(instr.Mnemonic.GetHashCode() & 0xFF)
            };
        }

        public Instruction ToInstruction()
        {
            return new Instruction
            {
                Address = Address,
                FileOffset = FileOffset,
                Length = Length,
                Bytes = Bytes
            };
        }
    }
}
