using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.WinForms
{
    public class FunctionListControl : UserControl
    {
        private readonly ListView _list;
        private readonly List<Function> _functions = new();

        public event Action<Function>? FunctionSelected;

        public FunctionListControl()
        {
            Dock = DockStyle.Fill;

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                VirtualMode = true
            };

            _list.Columns.Add("Address", 100);
            _list.Columns.Add("Name", 200);
            _list.Columns.Add("Source", 100);
            _list.Columns.Add("Size", 60);

            _list.RetrieveVirtualItem += OnRetrieveVirtualItem;
            _list.SelectedIndexChanged += OnSelectedIndexChanged;

            Controls.Add(_list);
        }

        // -----------------------------
        // FULL LOAD
        // -----------------------------
        public void LoadFunctions(IReadOnlyList<Function> funcs)
        {
            _functions.Clear();
            _functions.AddRange(funcs);
            _list.VirtualListSize = _functions.Count;
            _list.Invalidate();
        }
        public void AddFunctionsBatch(List<Function> batch)
        {
            if (batch.Count == 0)
                return;

            _functions.AddRange(batch);

            // Update VirtualListSize ONCE per batch
            _list.VirtualListSize = _functions.Count;

            // Optional: repaint once
            _list.Invalidate();
        }

        // -----------------------------
        // INCREMENTAL ADD (lazy load)
        // -----------------------------
        public void AddFunction(Function fn)
        {
            _functions.Add(fn);

            // Update virtual list size
            //_list.VirtualListSize = _functions.Count;

            // Refresh only the new row
            //_list.RedrawItems(_functions.Count - 1, _functions.Count - 1, false);
        }

        // -----------------------------
        // VirtualMode callback
        // -----------------------------
        private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _functions.Count)
            {
                e.Item = new ListViewItem("?");
                return;
            }

            var f = _functions[e.ItemIndex];
            e.Item = BuildItem(f);
        }

        // -----------------------------
        // Helper to build a ListViewItem
        // -----------------------------
        private ListViewItem BuildItem(Function f)
        {
            var item = new ListViewItem($"0x{f.Address:X}");
            item.SubItems.Add(f.Name ?? $"sub_{f.Address:X}");
            item.SubItems.Add(f.Source ?? "");
            item.SubItems.Add(f.InstructionCount.ToString());
            item.Tag = f;
            return item;
        }

        private void OnSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_list.SelectedIndices.Count == 0)
                return;

            var index = _list.SelectedIndices[0];
            if (index < 0 || index >= _functions.Count)
                return;

            FunctionSelected?.Invoke(_functions[index]);
        }
    }
}