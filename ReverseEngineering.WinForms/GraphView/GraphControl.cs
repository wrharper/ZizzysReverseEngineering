using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.WinForms.GraphView
{
    /// <summary>
    /// Control to visualize CFG (Control Flow Graph) as a node-graph diagram.
    /// Shows basic blocks and their control flow relationships.
    /// </summary>
    public class GraphControl : UserControl
    {
        private readonly CoreEngine _core;
        private ControlFlowGraph? _cfg;
        private readonly Dictionary<ulong, Rectangle> _nodeRects = [];
        private float _scale = 1.0f;
        private Point _panOffset = Point.Zero;

        public event Action<ulong>? BlockSelected;

        public GraphControl(CoreEngine core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));

            DoubleBuffered = true;
            Font = new Font("Consolas", 9);

            MouseWheel += (s, e) =>
            {
                _scale *= e.Delta > 0 ? 1.1f : 0.9f;
                _scale = Math.Max(0.5f, Math.Min(3.0f, _scale));
                Invalidate();
            };

            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    var addr = HitTestNode(e.Location);
                    if (addr.HasValue)
                        BlockSelected?.Invoke(addr.Value);
                }
            };
        }

        /// <summary>
        /// Display CFG for a function.
        /// </summary>
        public void DisplayCFG(ControlFlowGraph cfg)
        {
            _cfg = cfg;
            _nodeRects.Clear();
            LayoutGraph();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_cfg == null || _cfg.Blocks.Count == 0)
            {
                e.Graphics.DrawString("No CFG loaded", Font, Brushes.Gray, 10, 10);
                return;
            }

            e.Graphics.Clear(BackColor);
            e.Graphics.TranslateTransform(_panOffset.X, _panOffset.Y);
            e.Graphics.ScaleTransform(_scale, _scale);

            // Draw edges
            DrawEdges(e.Graphics);

            // Draw nodes
            DrawNodes(e.Graphics);
        }

        private void DrawNodes(Graphics g)
        {
            foreach (var (addr, block) in _cfg!.Blocks)
            {
                if (_nodeRects.TryGetValue(addr, out var rect))
                {
                    // Draw node
                    var brush = block.IsEntryPoint ? Brushes.LimeGreen : Brushes.LightBlue;
                    g.FillRectangle(brush, rect);
                    g.DrawRectangle(Pens.Black, rect);

                    // Draw text
                    var text = $"0x{addr:X}\n[{block.InstructionCount} instrs]";
                    var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, Font, Brushes.Black, rect, fmt);
                }
            }
        }

        private void DrawEdges(Graphics g)
        {
            var drawnEdges = new HashSet<(ulong, ulong)>();

            foreach (var (addr, block) in _cfg!.Blocks)
            {
                if (!_nodeRects.TryGetValue(addr, out var fromRect))
                    continue;

                foreach (var succAddr in block.Successors)
                {
                    if (_nodeRects.TryGetValue(succAddr, out var toRect))
                    {
                        if (drawnEdges.Add((addr, succAddr)))
                        {
                            var from = new Point(
                                (int)(fromRect.Right + _nodeRects.Values.Average(r => r.Width) / 2),
                                (int)(fromRect.Top + fromRect.Height / 2)
                            );

                            var to = new Point(
                                (int)(toRect.Left - _nodeRects.Values.Average(r => r.Width) / 2),
                                (int)(toRect.Top + toRect.Height / 2)
                            );

                            // Draw arrow
                            g.DrawLine(Pens.Black, from, to);
                            DrawArrowhead(g, from, to);
                        }
                    }
                }
            }
        }

        private void DrawArrowhead(Graphics g, Point from, Point to)
        {
            float angle = (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
            float size = 10;

            var p1 = new PointF(
                to.X - size * (float)Math.Cos(angle - Math.PI / 6),
                to.Y - size * (float)Math.Sin(angle - Math.PI / 6)
            );

            var p2 = new PointF(
                to.X - size * (float)Math.Cos(angle + Math.PI / 6),
                to.Y - size * (float)Math.Sin(angle + Math.PI / 6)
            );

            g.DrawLine(Pens.Black, to, p1);
            g.DrawLine(Pens.Black, to, p2);
        }

        private void LayoutGraph()
        {
            if (_cfg?.Blocks.Count == 0)
                return;

            // Simple hierarchical layout
            var levels = new Dictionary<ulong, int>();
            var queue = new Queue<ulong>();

            // BFS to assign levels
            foreach (var entry in _cfg.EntryPoints)
            {
                levels[entry] = 0;
                queue.Enqueue(entry);
            }

            while (queue.Count > 0)
            {
                var addr = queue.Dequeue();
                if (_cfg.Blocks.TryGetValue(addr, out var block))
                {
                    var currentLevel = levels[addr];
                    foreach (var succ in block.Successors)
                    {
                        if (!levels.ContainsKey(succ))
                        {
                            levels[succ] = currentLevel + 1;
                            queue.Enqueue(succ);
                        }
                    }
                }
            }

            // Position nodes
            int nodeWidth = 120;
            int nodeHeight = 60;
            int xSpacing = 150;
            int ySpacing = 100;

            var levelCounts = new Dictionary<int, int>();
            foreach (var (addr, level) in levels)
            {
                if (!levelCounts.ContainsKey(level))
                    levelCounts[level] = 0;

                var x = levelCounts[level] * xSpacing;
                var y = level * ySpacing;

                _nodeRects[addr] = new Rectangle(x, y, nodeWidth, nodeHeight);
                levelCounts[level]++;
            }
        }

        private ulong? HitTestNode(Point p)
        {
            var adjusted = new Point(
                (int)((p.X - _panOffset.X) / _scale),
                (int)((p.Y - _panOffset.Y) / _scale)
            );

            foreach (var (addr, rect) in _nodeRects)
            {
                if (rect.Contains(adjusted))
                    return addr;
            }

            return null;
        }
    }
}
