using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReverseEngineering.Core.Analysis;
using ReverseEngineering.Core.ProjectSystem;

namespace ReverseEngineering.Core
{
    public class CoreEngine
    {
        public bool Is64Bit { get; private set; }
        public HexBuffer HexBuffer { get; private set; } = new HexBuffer([]);
        public List<Instruction> Disassembly { get; private set; } = [];
        public PEHeaderExtractor.PEInfo? PEInfo { get; private set; }

        private PatchEngine? _patchEngine;

        // NEW: Fast lookup for incremental disassembly
        private readonly Dictionary<ulong, int> _addressToIndex = [];

        // Base address for PE files (you can adjust this if needed)
        public ulong ImageBase { get; private set; } = 0;

        // ---------------------------------------------------------
        //  UNDO/REDO (Phase 5)
        // ---------------------------------------------------------
        public UndoRedoManager UndoRedo { get; private set; } = new();

        // ---------------------------------------------------------
        //  ANALYSIS RESULTS (Phase 2)
        // ---------------------------------------------------------
        public ControlFlowGraph? CFG { get; private set; }
        public List<Function> Functions { get; private set; } = [];
        public Dictionary<ulong, List<CrossReference>> CrossReferences { get; private set; } = [];
        public Dictionary<ulong, Symbol> Symbols { get; private set; } = [];
        public List<PatternMatch> Strings { get; private set; } = [];
        public bool AnalysisInProgress { get; private set; }
        public bool DisassemblyComplete { get; private set; }

        /// <summary>
        /// Progress callback for disassembly: (processed, total)
        /// </summary>
        public delegate void DisassemblyProgressCallback(int processed, int total);

        public DisassemblyProgressCallback? OnDisassemblyProgress { get; set; }

        public void LoadFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Binary not found", path);

            // Reset completion flag for new binary
            DisassemblyComplete = false;

            // Report initial progress (file read)
            OnDisassemblyProgress?.Invoke(0, 100);
            System.Threading.Thread.Sleep(50);  // Ensure UI updates

            var bytes = File.ReadAllBytes(path);

            // Report after file read (10%)
            OnDisassemblyProgress?.Invoke(10, 100);
            System.Threading.Thread.Sleep(50);

            // Detect PE32 vs PE32+
            Is64Bit = DetectBitness(bytes);

            // Extract PE header info
            PEInfo = PEHeaderExtractor.Extract(bytes);

            // Report after header parsing (20%)
            OnDisassemblyProgress?.Invoke(20, 100);
            System.Threading.Thread.Sleep(50);

            // Create buffer
            HexBuffer = new HexBuffer(bytes, path);

            // Patch engine for future undo/redo
            _patchEngine = new PatchEngine(HexBuffer);

            // Report before disassembly (30%)
            OnDisassemblyProgress?.Invoke(30, 100);
            System.Threading.Thread.Sleep(50);

            // Decode full disassembly (will report 30-95% during this phase)
            // DecodePE now reports progress as (percentage, 100)
            Disassembly = Disassembler.DecodePE(bytes, (progressPercent, _) => 
            {
                // Scale from 0-100% to 30-95% range
                int scaledProgress = 30 + (progressPercent * 65 / 100);
                OnDisassemblyProgress?.Invoke(scaledProgress, 100);
            });

            // Report before address indexing (95%)
            OnDisassemblyProgress?.Invoke(95, 100);
            System.Threading.Thread.Sleep(50);

            // Build address map
            RebuildAddressIndex();

            // Report completion (100%)
            OnDisassemblyProgress?.Invoke(100, 100);

            // Mark disassembly as complete
            DisassemblyComplete = true;
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

            // Binary search for instruction at this address (O(log n) instead of O(n))
            int left = 0, right = Disassembly.Count - 1;
            
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var ins = Disassembly[mid];
                
                if (ins.Address == address)
                {
                    return ins.FileOffset;
                }
                else if (address < ins.Address)
                {
                    right = mid - 1;
                }
                else
                {
                    // address > ins.Address - check if it's within this instruction
                    if (address < ins.EndAddress)
                    {
                        return ins.FileOffset + (int)(address - ins.Address);
                    }
                    left = mid + 1;
                }
            }

            return -1;
        }

        public ulong OffsetToAddress(int offset)
        {
            // Binary search for instruction containing this offset (O(log n) instead of O(n))
            int left = 0, right = Disassembly.Count - 1;
            
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var ins = Disassembly[mid];
                
                if (offset == ins.FileOffset)
                {
                    return ins.Address;
                }
                else if (offset < ins.FileOffset)
                {
                    right = mid - 1;
                }
                else
                {
                    // offset > ins.FileOffset - check if it's within this instruction
                    if (offset < ins.FileOffset + ins.Length)
                    {
                        return ins.Address + (ulong)(offset - ins.FileOffset);
                    }
                    left = mid + 1;
                }
            }
            
            // Fallback: if no instruction found, use linear mapping (shouldn't happen for valid offsets)
            return ImageBase + (ulong)offset;
        }

        public int OffsetToInstructionIndex(int offset)
        {
            if (Disassembly.Count == 0)
                return -1;

            // Binary search for instruction containing this offset (O(log n) instead of O(n))
            int left = 0, right = Disassembly.Count - 1;
            
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var ins = Disassembly[mid];
                int start = ins.FileOffset;
                int end = start + ins.Length;
                
                if (offset >= start && offset < end)
                {
                    return mid;
                }
                else if (offset < start)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return -1;
        }

        // ---------------------------------------------------------
        //  PATCHING
        // ---------------------------------------------------------
        public void ApplyPatch(int offset, byte[] newBytes, string description = "")
        {
            _patchEngine ??= new PatchEngine(HexBuffer);

            // Capture original bytes
            byte[] originalBytes = new byte[newBytes.Length];
            Array.Copy(HexBuffer.Bytes, offset, originalBytes, 0, newBytes.Length);

            // Create and execute patch command
            var command = new PatchCommand(HexBuffer, offset, originalBytes, newBytes, description);
            UndoRedo.Execute(command);

            // Full rebuild is correct for bulk patching
            RebuildDisassemblyFromBuffer();

            // Also record in PatchEngine for project saving
            _patchEngine.ApplyPatch(offset, newBytes, description);
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

            // Fast path: exact address match
            if (_addressToIndex.TryGetValue(address, out int idx))
                return idx;

            // Slow path: binary search for instruction whose span contains this address
            int left = 0, right = Disassembly.Count - 1;
            
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var ins = Disassembly[mid];
                
                if (address >= ins.Address && address < ins.EndAddress)
                {
                    return mid;
                }
                else if (address < ins.Address)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
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

        // ---------------------------------------------------------
        //  ANALYSIS LAYER (Phase 2)
        // ---------------------------------------------------------
        /// <summary>
        /// Run full analysis on loaded disassembly.
        /// Discovers functions, builds CFG, finds xrefs, resolves symbols.
        /// </summary>
        public void RunAnalysis()
        {
            if (Disassembly.Count == 0)
                return;

            // Warn if running analysis on partial disassembly
            if (!DisassemblyComplete)
            {
                Logger.Info("Analysis", "⚠️ Running analysis on PARTIAL disassembly. Results will be incomplete until full disassembly completes.");
            }

            AnalysisInProgress = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Step 1: Find functions
                Logger.Info("Analysis", "Step 1/6: Finding functions...");
                Functions = FunctionFinder.FindFunctions(Disassembly, this);
                Logger.Info("Analysis", $"✓ Found {Functions.Count} functions ({sw.ElapsedMilliseconds}ms)");

                // Step 2: Build CFG from entry point
                Logger.Info("Analysis", "Step 2/6: Building control flow graph...");
                if (Disassembly.Count > 0)
                {
                    var entryPoint = Disassembly[0].Address;
                    CFG = BasicBlockBuilder.BuildCFG(Disassembly, entryPoint);
                    if (CFG != null)
                        Logger.Debug("Analysis", $"  Main CFG: {CFG.Blocks.Count} blocks");
                }
                Logger.Info("Analysis", $"✓ Built CFG ({sw.ElapsedMilliseconds}ms)");

                // Step 3: Find cross-references
                Logger.Info("Analysis", "Step 3/6: Finding cross-references...");
                CrossReferences = CrossReferenceEngine.BuildXRefs(Disassembly, ImageBase);
                Logger.Info("Analysis", $"✓ Found {CrossReferences.Count} cross-reference locations ({sw.ElapsedMilliseconds}ms)");

                // Step 4: Resolve symbols
                Logger.Info("Analysis", "Step 4/6: Resolving symbols...");
                Symbols = SymbolResolver.ResolveSymbols(Disassembly, this);
                Logger.Info("Analysis", $"✓ Resolved {Symbols.Count} symbols ({sw.ElapsedMilliseconds}ms)");

                // Step 5: Extract strings from binary
                Logger.Info("Analysis", "Step 5/6: Extracting strings...");
                var rawStrings = PatternMatcher.FindStrings(HexBuffer.Bytes, minLength: 3);
                Logger.Info("Analysis", $"Found {rawStrings.Count} raw strings");
                // Convert file offsets to virtual addresses
                Strings = rawStrings.Select(s =>
                {
                    // For now, use file offset + ImageBase as approximation
                    // In a full PE parser, would convert via section headers
                    return new PatternMatch
                    {
                        Address = ImageBase + (ulong)s.Offset,
                        Offset = s.Offset,
                        MatchedBytes = s.MatchedBytes,
                        Description = s.Description
                    };
                }).ToList();
                Logger.Info("Analysis", $"✓ Extracted {Strings.Count} strings ({sw.ElapsedMilliseconds}ms)");

                // Step 6: Annotate instructions with metadata
                Logger.Info("Analysis", "Step 6/6: Annotating instructions...");
                AnnotateInstructions();
                Logger.Info("Analysis", $"✓ Annotated instructions ({sw.ElapsedMilliseconds}ms)");

                sw.Stop();
                Logger.Info("Analysis", $"✅ Analysis complete in {sw.ElapsedMilliseconds}ms");
            }
            finally
            {
                AnalysisInProgress = false;
            }
        }

        /// <summary>
        /// Annotate instructions with analysis results.
        /// </summary>
        private void AnnotateInstructions()
        {
            // Map function addresses to instructions
            var functionMap = new Dictionary<ulong, ulong>();
            foreach (var func in Functions)
            {
                functionMap[func.Address] = func.Address;
            }

            // Map basic block addresses to instructions
            var blockMap = new Dictionary<ulong, ulong>();
            if (CFG != null)
            {
                foreach (var block in CFG.Blocks.Values)
                {
                    blockMap[block.StartAddress] = block.StartAddress;
                }
            }

            // Annotate each instruction
            foreach (var ins in Disassembly)
            {
                // Set function address
                foreach (var func in Functions)
                {
                    if (CFG?.GetBlockContainingAddress(ins.Address) is BasicBlock block
                        && block.ParentFunctionAddress == func.Address)
                    {
                        ins.FunctionAddress = func.Address;
                        break;
                    }
                }

                // Set basic block address
                if (CFG?.GetBlockContainingAddress(ins.Address) is BasicBlock containingBlock)
                {
                    ins.BasicBlockAddress = containingBlock.StartAddress;
                }

                // Set xrefs
                if (CrossReferences.TryGetValue(ins.Address, out var xrefs))
                {
                    ins.XRefsFrom = xrefs;
                }

                // Set symbol name
                if (Symbols.TryGetValue(ins.Address, out var sym))
                {
                    ins.SymbolName = sym.Name;
                }

                // Set patched flag
                ins.IsPatched = HexBuffer.Modified[ins.FileOffset];
            }
        }

        /// <summary>
        /// Find function containing an address.
        /// </summary>
        public Function? FindFunctionAtAddress(ulong address)
        {
            return Functions.FirstOrDefault(f => f.Address == address);
        }

        /// <summary>
        /// Get symbol name for an address.
        /// </summary>
        public string? GetSymbolName(ulong address)
        {
            return SymbolResolver.GetSymbolName(address, Symbols);
        }

        /// <summary>
        /// Add user annotation for an address.
        /// </summary>
        public void AnnotateAddress(ulong address, string name, string symbolType)
        {
            SymbolResolver.AddUserAnnotation(address, name, symbolType, Symbols);

            // Update instruction metadata
            var ins = Disassembly.FirstOrDefault(i => i.Address == address);
            if (ins != null)
                ins.SymbolName = name;
        }

        /// <summary>
        /// Search for byte pattern in disassembly.
        /// </summary>
        public List<PatternMatch> FindBytePattern(string pattern, string? description = null)
        {
            return PatternMatcher.FindBytePattern(HexBuffer.Bytes, pattern, description);
        }

        /// <summary>
        /// Search for common patterns (prologues, NOPs, etc).
        /// </summary>
        public List<PatternMatch> FindPrologues()
        {
            return PatternMatcher.FindX64Prologues(HexBuffer.Bytes);
        }
    }
}