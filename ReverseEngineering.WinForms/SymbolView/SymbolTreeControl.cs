using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.WinForms.SymbolView
{
    /// <summary>
    /// Tree view control for functions, symbols, and analysis results.
    /// </summary>
    public class SymbolTreeControl : UserControl
    {
        private readonly CoreEngine _core;
        private readonly TreeView _tree;

        public event Action<ulong>? SymbolSelected;

        public SymbolTreeControl(CoreEngine core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));

            // Create tree view
            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowRootLines = true,
                ShowNodeToolTips = true
            };

            _tree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node?.Tag is ulong addr)
                    SymbolSelected?.Invoke(addr);
            };

            Controls.Add(_tree);
        }

        /// <summary>
        /// Populate tree with analysis results.
        /// </summary>
        public void PopulateFromAnalysis()
        {
            _tree.Nodes.Clear();

            // Root nodes
            var functionsNode = new TreeNode("Functions") { Tag = "functions" };
            var symbolsNode = new TreeNode("Symbols") { Tag = "symbols" };
            var xrefsNode = new TreeNode("Cross-References") { Tag = "xrefs" };

            // Functions
            if (_core.Functions.Count > 0)
            {
                foreach (var func in _core.Functions.OrderBy(f => f.Address))
                {
                    var funcNode = new TreeNode($"0x{func.Address:X}: {func.Name ?? "unnamed"}")
                    {
                        Tag = func.Address,
                        ImageIndex = 0
                    };

                    if (func.CFG != null)
                    {
                        var blockCount = func.CFG.Blocks.Count;
                        funcNode.Nodes.Add(new TreeNode($"  {blockCount} blocks"));
                    }

                    functionsNode.Nodes.Add(funcNode);
                }
            }

            // Symbols
            if (_core.Symbols.Count > 0)
            {
                var grouped = _core.Symbols.Values.GroupBy(s => s.SymbolType);
                foreach (var group in grouped)
                {
                    var typeNode = new TreeNode($"{group.Key} ({group.Count()})", 
                        group.Select(s => new TreeNode($"0x{s.Address:X}: {s.Name}") { Tag = s.Address }).ToArray())
                    {
                        Tag = group.Key
                    };

                    symbolsNode.Nodes.Add(typeNode);
                }
            }

            // Cross-references (summary)
            if (_core.CrossReferences.Count > 0)
            {
                var refTypes = _core.CrossReferences.Values
                    .SelectMany(refs => refs.Select(r => r.RefType))
                    .Distinct();

                foreach (var refType in refTypes)
                {
                    var count = _core.CrossReferences.Values
                        .SelectMany(refs => refs.Where(r => r.RefType == refType))
                        .Count();

                    xrefsNode.Nodes.Add(new TreeNode($"{refType} ({count})"));
                }
            }

            _tree.Nodes.Add(functionsNode);
            _tree.Nodes.Add(symbolsNode);
            _tree.Nodes.Add(xrefsNode);
        }

        /// <summary>
        /// Refresh tree when analysis changes.
        /// </summary>
        public void Refresh()
        {
            PopulateFromAnalysis();
        }
    }
}
