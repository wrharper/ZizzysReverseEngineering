using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.Search
{
    /// <summary>
    /// Search dialog for finding bytes, instructions, functions, symbols, xrefs.
    /// </summary>
    public partial class SearchDialog : Form
    {
        private readonly CoreEngine _core;
        private List<SearchResult> _currentResults = [];

        public event Action<SearchResult>? ResultSelected;

        public SearchDialog(CoreEngine core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Window properties
            Text = "Search";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;

            // ---------------------------------------------------------
            //  LAYOUT
            // ---------------------------------------------------------
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };

            // Row 0: Search type selector
            var typePanel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            typePanel.Controls.Add(new Label { Text = "Search Type:", AutoSize = true, Margin = new Padding(0, 0, 4, 0) });

            var typeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Items = { "Bytes", "Instruction Mnemonic", "Function Name", "Symbol Name", "Xref To Address" },
                SelectedIndex = 0,
                Width = 150
            };
            typeCombo.SelectedIndexChanged += (s, e) => { };
            typePanel.Controls.Add(typeCombo);

            mainPanel.Controls.Add(typePanel, 0, 0);

            // Row 1: Search input
            var inputPanel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            inputPanel.Controls.Add(new Label { Text = "Search Query:", AutoSize = true, Margin = new Padding(0, 0, 4, 0) });

            var inputBox = new TextBox
            {
                Width = 350,
                Height = 24,
                Multiline = false
            };
            inputPanel.Controls.Add(inputBox);

            var searchBtn = new Button { Text = "Search", Width = 80, Height = 24 };
            searchBtn.Click += (s, e) => PerformSearch(typeCombo.SelectedIndex, inputBox.Text);
            inputPanel.Controls.Add(searchBtn);

            mainPanel.Controls.Add(inputPanel, 0, 1);

            // Row 2: Results list
            var resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            resultsGrid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Address", DataPropertyName = "Address", Width = 100 },
                new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "ResultType", Width = 80 },
                new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }
            );

            resultsGrid.DoubleClick += (s, e) =>
            {
                if (resultsGrid.SelectedRows.Count > 0)
                {
                    var idx = resultsGrid.SelectedRows[0].Index;
                    if (idx >= 0 && idx < _currentResults.Count)
                        ResultSelected?.Invoke(_currentResults[idx]);
                }
            };

            mainPanel.Controls.Add(resultsGrid, 0, 2);
            mainPanel.RowStyles[2] = new RowStyle(SizeType.Percent, 100);

            // Row 3: Close button
            var closeBtn = new Button { Text = "Close", Dock = DockStyle.Right, Width = 100 };
            closeBtn.Click += (s, e) => Close();
            mainPanel.Controls.Add(closeBtn, 0, 3);

            Controls.Add(mainPanel);
            ResumeLayout(false);
        }

        private void PerformSearch(int searchType, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Please enter a search query.");
                return;
            }

            _currentResults = searchType switch
            {
                0 => SearchBytePattern(query),
                1 => SearchInstruction(query),
                2 => SearchFunction(query),
                3 => SearchSymbol(query),
                4 => SearchXRef(query),
                _ => []
            };

            // Update grid
            if (Controls.OfType<TableLayoutPanel>().FirstOrDefault()?.Controls.OfType<DataGridView>().FirstOrDefault() is DataGridView grid)
            {
                grid.DataSource = _currentResults.Select(r => new
                {
                    r.Address,
                    r.ResultType,
                    r.Description
                }).ToList();
            }
        }

        private List<SearchResult> SearchBytePattern(string query)
        {
            var bytes = SearchManager.HexStringToBytes(query);
            if (bytes == null)
            {
                MessageBox.Show("Invalid hex format. Use format like: 48 89 E5 or 4889E5");
                return [];
            }

            return SearchManager.SearchBytes(_core.HexBuffer, bytes);
        }

        private List<SearchResult> SearchInstruction(string query)
        {
            return SearchManager.SearchInstructionsByMnemonic(_core.Disassembly, query);
        }

        private List<SearchResult> SearchFunction(string query)
        {
            return SearchManager.SearchFunctionsByName(_core.Functions, query);
        }

        private List<SearchResult> SearchSymbol(string query)
        {
            return SearchManager.SearchSymbolsByName(_core.Symbols, query);
        }

        private List<SearchResult> SearchXRef(string query)
        {
            if (!ulong.TryParse(query, System.Globalization.NumberStyles.HexNumber, null, out var addr))
            {
                MessageBox.Show("Please enter a hex address.");
                return [];
            }

            return SearchManager.FindReferencesToAddress(addr, _core.CrossReferences);
        }
    }
}
