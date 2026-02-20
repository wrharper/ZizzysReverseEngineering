using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.WinForms
{
    public class FunctionListControl : UserControl
    {
        private readonly ListView _list;
        private List<Function> _functions = new();

        public event Action<Function>? FunctionSelected;

        public FunctionListControl()
        {
            Dock = DockStyle.Fill;

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false
            };

            _list.Columns.Add("Address", 100);
            _list.Columns.Add("Name", 200);
            _list.Columns.Add("Source", 100);
            _list.Columns.Add("Size", 60);

            _list.SelectedIndexChanged += OnSelectedIndexChanged;

            Controls.Add(_list);
        }

        public void LoadFunctions(List<Function> funcs)
        {
            _functions = funcs;
            _list.Items.Clear();

            foreach (var f in funcs)
            {
                var item = new ListViewItem($"0x{f.Address:X}");
                item.SubItems.Add(f.Name ?? $"sub_{f.Address:X}");
                item.SubItems.Add(f.Source ?? "");
                item.SubItems.Add(f.InstructionCount.ToString());
                item.Tag = f;

                _list.Items.Add(item);
            }
        }

        private void OnSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0)
                return;

            var func = (Function)_list.SelectedItems[0].Tag!;
            FunctionSelected?.Invoke(func);
        }
    }
}