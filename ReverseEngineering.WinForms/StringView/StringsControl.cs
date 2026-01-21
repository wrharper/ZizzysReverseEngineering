using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.StringView
{
    /// <summary>
    /// Control for displaying extracted strings from binary analysis.
    /// </summary>
    public class StringsControl : UserControl
    {
        private readonly CoreEngine _core;
        private readonly DataGridView _grid;
        private readonly TextBox _searchBox;
        private readonly Label _statusLabel;

        public event Action<ulong>? StringSelected;

        public StringsControl(CoreEngine core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));

            // Search box at top
            _searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Search strings...",
                Margin = new Padding(4)
            };
            _searchBox.TextChanged += OnSearchChanged;

            // Status label
            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = "No strings found",
                AutoSize = false
            };

            // Data grid for strings
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None
            };

            // Apply initial theme
            ApplyTheme();

            // Configure columns
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Address",
                HeaderText = "Address",
                Width = 80,
                DataPropertyName = "Address"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Offset",
                HeaderText = "Offset",
                Width = 80,
                DataPropertyName = "Offset"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "String",
                HeaderText = "String",
                Width = 300,
                DataPropertyName = "String"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Length",
                HeaderText = "Length",
                Width = 60,
                DataPropertyName = "Length"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Bytes",
                HeaderText = "Hex Bytes",
                Width = 150,
                DataPropertyName = "Bytes"
            });

            _grid.DoubleClick += (s, e) =>
            {
                if (_grid.CurrentRow?.DataBoundItem is StringEntry entry)
                    StringSelected?.Invoke(entry.RawAddress);
            };

            // Layout
            Controls.Add(_grid);
            Controls.Add(_statusLabel);
            Controls.Add(_searchBox);

            _grid.BringToFront();
            _statusLabel.BringToFront();
            _searchBox.BringToFront();
        }

        /// <summary>
        /// Populate grid with extracted strings.
        /// </summary>
        public void PopulateFromAnalysis()
        {
            _grid.DataSource = null;

            if (_core.Strings == null || _core.Strings.Count == 0)
            {
                _statusLabel.Text = "No strings found";
                ApplyTheme();
                return;
            }

            var entries = _core.Strings
                .Where(str => str.MatchedBytes != null && str.MatchedBytes.Length > 0)
                .Select(str =>
                {
                    // Sanitize string for display
                    string displayString = SanitizeString(Encoding.ASCII.GetString(str.MatchedBytes!));
                    
                    return new StringEntry
                    {
                        Address = $"0x{str.Address:X8}",
                        Offset = $"0x{str.Offset:X8}",
                        String = displayString,
                        Length = str.MatchedBytes!.Length,
                        Bytes = BytesToHex(str.MatchedBytes!),
                        RawAddress = str.Address,
                        RawString = displayString
                    };
                })
                .ToList();

            if (entries.Count == 0)
            {
                _statusLabel.Text = "No strings found";
                ApplyTheme();
                return;
            }

            _grid.DataSource = entries;
            _statusLabel.Text = $"{entries.Count} strings found";
            ApplyTheme();
        }

        /// <summary>
        /// Apply current theme to all controls.
        /// </summary>
        private void ApplyTheme()
        {
            var theme = ThemeManager.CurrentTheme;

            // Grid background and header
            _grid.BackgroundColor = theme.BackColor;
            _grid.GridColor = theme.BackColor;

            // Apply theme to all columns
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                col.DefaultCellStyle.BackColor = theme.BackColor;
                col.DefaultCellStyle.ForeColor = theme.ForeColor;
                col.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
                col.DefaultCellStyle.SelectionForeColor = Color.White;
                col.HeaderCell.Style.BackColor = theme.BackColor;
                col.HeaderCell.Style.ForeColor = theme.ForeColor;
            }

            // Search box and status label
            _statusLabel.Text = _statusLabel.Text;  // Preserve existing text
            _statusLabel.ForeColor = theme.ForeColor;
            _statusLabel.BackColor = theme.BackColor;
            _searchBox.BackColor = theme.BackColor;
            _searchBox.ForeColor = theme.ForeColor;
            BackColor = theme.BackColor;
        }

        /// <summary>
        /// Filter strings by search text.
        /// </summary>
        private void OnSearchChanged(object? sender, EventArgs e)
        {
            if (_grid.DataSource is not List<StringEntry> entries)
                return;

            string searchText = _searchBox.Text.ToLower();

            var filtered = entries
                .Where(e => e.RawString.ToLower().Contains(searchText) ||
                            e.Address.ToLower().Contains(searchText) ||
                            e.Bytes.ToLower().Contains(searchText))
                .ToList();

            _grid.DataSource = new List<StringEntry>(filtered);
            _statusLabel.Text = $"{filtered.Count} of {entries.Count} strings";
        }

        /// <summary>
        /// Sanitize string for display (remove unprintables, truncate long strings).
        /// </summary>
        private static string SanitizeString(string str)
        {
            const int maxDisplay = 100;
            
            var sb = new StringBuilder();
            foreach (char c in str)
            {
                if (char.IsControl(c))
                    sb.Append($"\\x{(int)c:X2}");
                else
                    sb.Append(c);
            }

            string result = sb.ToString();
            return result.Length > maxDisplay ? result.Substring(0, maxDisplay) + "..." : result;
        }

        /// <summary>
        /// Convert bytes to hex string.
        /// </summary>
        private static string BytesToHex(byte[] bytes)
        {
            const int maxDisplay = 16;
            var hex = string.Join(" ", bytes.Take(maxDisplay).Select(b => $"{b:X2}"));
            return bytes.Length > maxDisplay ? hex + "..." : hex;
        }

        /// <summary>
        /// Data binding class for grid.
        /// </summary>
        private class StringEntry
        {
            public required string Address { get; set; }
            public required string Offset { get; set; }
            public required string String { get; set; }
            public required int Length { get; set; }
            public required string Bytes { get; set; }
            public required ulong RawAddress { get; set; }
            public required string RawString { get; set; }
        }
    }
}
