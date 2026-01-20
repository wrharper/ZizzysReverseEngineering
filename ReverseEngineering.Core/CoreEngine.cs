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
        public bool AnalysisInProgress { get; private set; }

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

            AnalysisInProgress = true;

            try
            {
                // Step 1: Find functions
                Functions = FunctionFinder.FindFunctions(Disassembly, this);

                // Step 2: Build CFG from entry point
                if (Disassembly.Count > 0)
                {
                    var entryPoint = Disassembly[0].Address;
                    CFG = BasicBlockBuilder.BuildCFG(Disassembly, entryPoint);
                }

                // Step 3: Find cross-references
                CrossReferences = CrossReferenceEngine.BuildXRefs(Disassembly, ImageBase);

                // Step 4: Resolve symbols
                Symbols = SymbolResolver.ResolveSymbols(Disassembly, this);

                // Step 5: Annotate instructions with metadata
                AnnotateInstructions();
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