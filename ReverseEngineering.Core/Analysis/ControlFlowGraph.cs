namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Control Flow Graph: tracks basic blocks and their relationships.
    /// </summary>
    public class ControlFlowGraph
    {
        private readonly Dictionary<ulong, BasicBlock> _blocks = [];
        private readonly List<ulong> _entryPoints = [];

        // NEW: sorted list of block ranges
        private readonly List<(ulong Start, ulong End, BasicBlock Block)> _sortedRanges = [];

        public IReadOnlyDictionary<ulong, BasicBlock> Blocks => _blocks;
        public IReadOnlyList<ulong> EntryPoints => _entryPoints;

        // ---------------------------------------------------------
        //  BLOCK MANAGEMENT
        // ---------------------------------------------------------
        public void AddBlock(BasicBlock block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            _blocks[block.StartAddress] = block;

            if (block.IsEntryPoint)
                _entryPoints.Add(block.StartAddress);

            // Add to range list
            _sortedRanges.Add((block.StartAddress, block.EndAddress, block));
        }

        // Call after all blocks are added
        public void FinalizeLayout()
        {
            _sortedRanges.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        // ---------------------------------------------------------
        //  FAST LOOKUPS
        // ---------------------------------------------------------
        public BasicBlock? GetBlock(ulong address)
        {
            return _blocks.TryGetValue(address, out var block) ? block : null;
        }

        public BasicBlock? GetBlockContainingAddress(ulong address)
        {
            // Binary search over sorted ranges
            int lo = 0;
            int hi = _sortedRanges.Count - 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                var (start, end, block) = _sortedRanges[mid];

                if (address < start)
                    hi = mid - 1;
                else if (address > end)
                    lo = mid + 1;
                else
                    return block;
            }

            return null;
        }

        // ---------------------------------------------------------
        //  GRAPH TRAVERSAL
        // ---------------------------------------------------------
        public IEnumerable<BasicBlock> GetSuccessors(BasicBlock block)
        {
            foreach (var succAddr in block.Successors)
                if (_blocks.TryGetValue(succAddr, out var succ))
                    yield return succ;
        }

        public IEnumerable<BasicBlock> GetPredecessors(BasicBlock block)
        {
            foreach (var predAddr in block.Predecessors)
                if (_blocks.TryGetValue(predAddr, out var pred))
                    yield return pred;
        }

        public IEnumerable<BasicBlock> TraverseDFS(ulong startAddress)
        {
            var visited = new HashSet<ulong>();
            var stack = new Stack<ulong>();
            stack.Push(startAddress);

            while (stack.Count > 0)
            {
                var addr = stack.Pop();
                if (!visited.Add(addr))
                    continue;

                if (_blocks.TryGetValue(addr, out var block))
                {
                    yield return block;

                    foreach (var succ in block.Successors.OrderByDescending(x => x))
                        if (!visited.Contains(succ))
                            stack.Push(succ);
                }
            }
        }

        public IEnumerable<BasicBlock> TraverseBFS(ulong startAddress)
        {
            var visited = new HashSet<ulong>();
            var queue = new Queue<ulong>();
            queue.Enqueue(startAddress);

            while (queue.Count > 0)
            {
                var addr = queue.Dequeue();
                if (!visited.Add(addr))
                    continue;

                if (_blocks.TryGetValue(addr, out var block))
                {
                    yield return block;

                    foreach (var succ in block.Successors)
                        if (!visited.Contains(succ))
                            queue.Enqueue(succ);
                }
            }
        }

        // ---------------------------------------------------------
        //  STATISTICS
        // ---------------------------------------------------------
        public int TotalBlocks => _blocks.Count;
        public int TotalInstructions => _blocks.Values.Sum(b => b.InstructionCount);

        public override string ToString() =>
            $"CFG: {_blocks.Count} blocks, {TotalInstructions} instructions";
    }
}
