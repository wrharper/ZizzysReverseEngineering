using System;
using System.Windows.Forms;
using System.Drawing;

#nullable enable

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Control that displays AI, Trainer, and SQL statistics.
    /// Shows context usage, token consumption, pattern indexing, and cache metrics.
    /// </summary>
    public class AIStatsControl : UserControl
    {
        private TableLayoutPanel _mainLayout;
        private GroupBox _aiGroup;
        private GroupBox _trainerGroup;
        private GroupBox _sqlGroup;

        // AI Stats controls
        private Label _contextUsageLabel;
        private ProgressBar _contextUsageBar;
        private Label _tokensLabel;
        private Label _modelLabel;

        // Trainer Stats controls
        private Label _patternsIndexedLabel;
        private Label _embeddingsLabel;
        private Label _trainerStatusLabel;

        // SQL Stats controls
        private Label _cacheHitsLabel;
        private Label _queriesLabel;
        private Label _dbSizeLabel;

        public AIStatsControl()
        {
            InitializeComponent();
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        private void InitializeComponent()
        {
            // Main layout - 3 columns, force sizing mode
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = false,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            // Set column widths equally - CRITICAL: Use percentage to divide space
            _mainLayout.ColumnStyles.Clear();
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            _mainLayout.RowStyles.Clear();
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // AI Stats Group - set to fill its cell
            _aiGroup = CreateAIStatsGroup();
            _aiGroup.AutoSize = false;
            _mainLayout.Controls.Add(_aiGroup, 0, 0);

            // Trainer Stats Group - set to fill its cell
            _trainerGroup = CreateTrainerStatsGroup();
            _trainerGroup.AutoSize = false;
            _mainLayout.Controls.Add(_trainerGroup, 1, 0);

            // SQL Stats Group - set to fill its cell
            _sqlGroup = CreateSQLStatsGroup();
            _sqlGroup.AutoSize = false;
            _mainLayout.Controls.Add(_sqlGroup, 2, 0);

            this.Controls.Add(_mainLayout);
            this.BackColor = Color.White;
        }

        private GroupBox CreateAIStatsGroup()
        {
            var group = new GroupBox
            {
                Text = "AI Stats",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(8),
                ForeColor = Color.FromArgb(0, 100, 200),
                Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold),
                MinimumSize = new Size(150, 100)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false,
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            layout.RowStyles.Clear();
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Model
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Context %
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25f));  // Progress bar
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Tokens

            // Model label
            _modelLabel = new Label 
            { 
                Text = "Model: (not loaded)", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_modelLabel, 0, 0);

            // Context usage percentage
            _contextUsageLabel = new Label 
            { 
                Text = "Context Usage: 0%", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_contextUsageLabel, 0, 1);

            // Context usage bar
            _contextUsageBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Maximum = 100,
                Value = 0,
                Margin = new Padding(0, 5, 0, 5)
            };
            layout.Controls.Add(_contextUsageBar, 0, 2);

            // Tokens label
            _tokensLabel = new Label 
            { 
                Text = "Tokens: 0 / 4096", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_tokensLabel, 0, 3);

            group.Controls.Add(layout);
            return group;
        }

        private GroupBox CreateTrainerStatsGroup()
        {
            var group = new GroupBox
            {
                Text = "Trainer Stats",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(8),
                ForeColor = Color.FromArgb(200, 100, 0),
                Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold),
                MinimumSize = new Size(150, 100)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = false,
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            layout.RowStyles.Clear();
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Trainer status
            _trainerStatusLabel = new Label 
            { 
                Text = "Status: Idle", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_trainerStatusLabel, 0, 0);

            // Patterns indexed
            _patternsIndexedLabel = new Label 
            { 
                Text = "Patterns Indexed: 0", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_patternsIndexedLabel, 0, 1);

            // Embeddings generated
            _embeddingsLabel = new Label 
            { 
                Text = "Embeddings: 0", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_embeddingsLabel, 0, 2);

            group.Controls.Add(layout);
            return group;
        }

        private GroupBox CreateSQLStatsGroup()
        {
            var group = new GroupBox
            {
                Text = "SQL / Cache Stats",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(8),
                ForeColor = Color.FromArgb(0, 150, 0),
                Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold),
                MinimumSize = new Size(150, 100)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = false,
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            layout.RowStyles.Clear();
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Cache hits
            _cacheHitsLabel = new Label 
            { 
                Text = "Cache Hits: 0", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_cacheHitsLabel, 0, 0);

            // Queries
            _queriesLabel = new Label 
            { 
                Text = "Queries: 0", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_queriesLabel, 0, 1);

            // DB size
            _dbSizeLabel = new Label 
            { 
                Text = "DB Size: 0 KB", 
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5),
                Font = new Font(this.Font.FontFamily, this.Font.Size - 1)
            };
            layout.Controls.Add(_dbSizeLabel, 0, 2);

            group.Controls.Add(layout);
            return group;
        }

        /// <summary>
        /// Update AI stats display
        /// </summary>
        public void UpdateAIStats(string model, float contextUsagePercent, int currentTokens, int maxTokens)
        {
            _modelLabel.Text = $"Model: {model}";
            _contextUsageLabel.Text = $"Context Usage: {contextUsagePercent:F1}%";
            _contextUsageBar.Value = Math.Min(100, (int)contextUsagePercent);
            _tokensLabel.Text = $"Tokens: {currentTokens} / {maxTokens}";

            // Change color based on usage
            if (contextUsagePercent > 80)
                _contextUsageBar.ForeColor = Color.Red;
            else if (contextUsagePercent > 60)
                _contextUsageBar.ForeColor = Color.Orange;
            else
                _contextUsageBar.ForeColor = Color.Green;
        }

        /// <summary>
        /// Update trainer stats display
        /// </summary>
        public void UpdateTrainerStats(string status, int patternsIndexed, int embeddingsGenerated)
        {
            _trainerStatusLabel.Text = $"Status: {status}";
            _patternsIndexedLabel.Text = $"Patterns Indexed: {patternsIndexed}";
            _embeddingsLabel.Text = $"Embeddings: {embeddingsGenerated}";
        }

        /// <summary>
        /// Update SQL/cache stats display
        /// </summary>
        public void UpdateSQLStats(long cacheHits, long totalQueries, long dbSizeKB)
        {
            _cacheHitsLabel.Text = $"Cache Hits: {cacheHits:N0}";
            _queriesLabel.Text = $"Queries: {totalQueries:N0}";
            _dbSizeLabel.Text = $"DB Size: {dbSizeKB:N0} KB";
            
            // Calculate hit rate percentage
            if (totalQueries > 0)
            {
                float hitRate = (cacheHits / (float)totalQueries) * 100f;
                _queriesLabel.Text += $" ({hitRate:F1}% hit rate)";
            }
        }

        /// <summary>
        /// Reset all stats to initial state
        /// </summary>
        public void ResetStats()
        {
            UpdateAIStats("(not loaded)", 0, 0, 4096);
            UpdateTrainerStats("Idle", 0, 0);
            UpdateSQLStats(0, 0, 0);
        }
    }
}
