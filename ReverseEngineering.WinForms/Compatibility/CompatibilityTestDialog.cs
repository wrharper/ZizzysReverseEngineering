#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms.Compatibility
{
    public class CompatibilityTestDialog : Form
    {
        private ListBox _testListBox;
        private TextBox _detailsTextBox;
        private Button _runAllButton;
        private Button _runSelectedButton;
        private Button _exportButton;
        private Button _closeButton;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private Label _summaryLabel;

        public CompatibilityTestDialog()
        {
            InitializeComponent();
            SetTheme();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Assembler/Disassembler Compatibility Verification";
            this.Width = 900;
            this.Height = 600;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;

            // Main split container
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                Orientation = Orientation.Vertical
            };

            // Left panel: Test list
            _testListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.One,
                Font = new Font("Consolas", 9)
            };
            _testListBox.SelectedIndexChanged += TestListBox_SelectedIndexChanged;
            mainSplit.Panel1.Controls.Add(_testListBox);

            // Right panel: Details and buttons
            var rightPanel = new Panel { Dock = DockStyle.Fill };

            // Details text box
            _detailsTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false,
                ScrollBars = ScrollBars.Both
            };
            rightPanel.Controls.Add(_detailsTextBox);

            mainSplit.Panel2.Controls.Add(rightPanel);

            // Status panel at bottom
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                BorderStyle = BorderStyle.FixedSingle
            };

            _statusLabel = new Label
            {
                Text = "Ready",
                Left = 10,
                Top = 10,
                Width = 500,
                Height = 20,
                Font = new Font("Segoe UI", 9)
            };
            statusPanel.Controls.Add(_statusLabel);

            _progressBar = new ProgressBar
            {
                Left = 10,
                Top = 35,
                Width = 500,
                Height = 20,
                Visible = false
            };
            statusPanel.Controls.Add(_progressBar);

            _summaryLabel = new Label
            {
                Text = "",
                Left = 10,
                Top = 60,
                Width = 500,
                Height = 20,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            statusPanel.Controls.Add(_summaryLabel);

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BorderStyle = BorderStyle.FixedSingle
            };

            _runAllButton = new Button
            {
                Text = "Run All Tests",
                Left = 10,
                Top = 10,
                Width = 120,
                Height = 25
            };
            _runAllButton.Click += RunAllButton_Click;
            buttonPanel.Controls.Add(_runAllButton);

            _runSelectedButton = new Button
            {
                Text = "Run Selected",
                Left = 140,
                Top = 10,
                Width = 120,
                Height = 25
            };
            _runSelectedButton.Click += RunSelectedButton_Click;
            buttonPanel.Controls.Add(_runSelectedButton);

            _exportButton = new Button
            {
                Text = "Export Report",
                Left = 270,
                Top = 10,
                Width = 120,
                Height = 25
            };
            _exportButton.Click += ExportButton_Click;
            buttonPanel.Controls.Add(_exportButton);

            _closeButton = new Button
            {
                Text = "Close",
                Left = 750,
                Top = 10,
                Width = 120,
                Height = 25,
                DialogResult = DialogResult.OK
            };
            buttonPanel.Controls.Add(_closeButton);

            // Add all controls to form
            this.Controls.Add(mainSplit);
            this.Controls.Add(statusPanel);
            this.Controls.Add(buttonPanel);

            this.ResumeLayout();
        }

        private void SetTheme()
        {
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.FromArgb(200, 200, 200);

            ApplyThemeToControl(_testListBox);
            ApplyThemeToControl(_detailsTextBox);
            ApplyThemeToControl(_statusLabel);
            ApplyThemeToControl(_summaryLabel);

            foreach (Button btn in new[] { _runAllButton, _runSelectedButton, _exportButton, _closeButton })
            {
                btn.BackColor = Color.FromArgb(60, 60, 60);
                btn.ForeColor = Color.FromArgb(200, 200, 200);
                btn.FlatStyle = FlatStyle.Flat;
            }
        }

        private void ApplyThemeToControl(Control control)
        {
            control.BackColor = Color.FromArgb(30, 30, 30);
            control.ForeColor = Color.FromArgb(200, 200, 200);
        }

        private void TestListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_testListBox.SelectedItem is string test)
            {
                _detailsTextBox.Text = $"Selected: {test}\n\n[Run a test to see details]";
            }
        }

        private async void RunAllButton_Click(object? sender, EventArgs e)
        {
            _runAllButton.Enabled = false;
            _runSelectedButton.Enabled = false;
            _statusLabel.Text = "Running all tests...";
            _progressBar.Visible = true;
            _progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                var results = await Task.Run(() => 
                    Core.Compatibility.AssemblerDisassemblerCompatibility.RunAllTests());

                DisplayResults(results);
                UpdateSummary(results);
                _statusLabel.Text = "All tests completed";
            }
            catch (Exception ex)
            {
                _detailsTextBox.Text = $"Error: {ex.Message}\n\n{ex.StackTrace}";
                _statusLabel.Text = "Error during tests";
            }
            finally
            {
                _progressBar.Visible = false;
                _runAllButton.Enabled = true;
                _runSelectedButton.Enabled = true;
            }
        }

        private async void RunSelectedButton_Click(object? sender, EventArgs e)
        {
            if (_testListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a test to run", "No Test Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _runAllButton.Enabled = false;
            _runSelectedButton.Enabled = false;
            _statusLabel.Text = "Running selected test...";
            _progressBar.Visible = true;
            _progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                var selected = _testListBox.SelectedItem.ToString() ?? "";
                var result = await Task.Run(() => RunSelectedTest(selected));
                _detailsTextBox.Text = result;
                _statusLabel.Text = "Test completed";
            }
            catch (Exception ex)
            {
                _detailsTextBox.Text = $"Error: {ex.Message}";
                _statusLabel.Text = "Error during test";
            }
            finally
            {
                _progressBar.Visible = false;
                _runAllButton.Enabled = true;
                _runSelectedButton.Enabled = true;
            }
        }

        private string RunSelectedTest(string testName)
        {
            // Map test names to methods
            return testName switch
            {
                "Keystone 64-bit Assembly" => FormatResult("Keystone 64-bit Assembly", 
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestKeystone64BitAssembly()),
                "Keystone 32-bit Assembly" => FormatResult("Keystone 32-bit Assembly",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestKeystone32BitAssembly()),
                "Keystone Complex Assembly" => FormatResult("Keystone Complex Assembly",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestKeystoneComplexAssembly()),
                "Iced 64-bit Disassembly" => FormatResult("Iced 64-bit Disassembly",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestIced64BitDisassembly()),
                "Iced 32-bit Disassembly" => FormatResult("Iced 32-bit Disassembly",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestIced32BitDisassembly()),
                "Iced RIP-relative Analysis" => FormatResult("Iced RIP-relative Analysis",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestIcedRIPRelativeAnalysis()),
                "Iced Operand Access" => FormatResult("Iced Operand Access",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestIcedOperandAccess()),
                "Round-trip Compatibility" => FormatResult("Round-trip Compatibility",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestRoundTripCompatibility()),
                "HexBuffer Optimization" => FormatResult("HexBuffer Optimization",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestHexBufferCompatibility()),
                "DisassemblyOptimizer Caching" => FormatResult("DisassemblyOptimizer Caching",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestDisassemblyOptimizerCompatibility()),
                "RIP-relative Enhancement" => FormatResult("RIP-relative Enhancement",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestRIPRelativeInstructionEnhancement()),
                "AI Logging" => FormatResult("AI Logging",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestAILoggingCompatibility()),
                "Settings System" => FormatResult("Settings System",
                    Core.Compatibility.AssemblerDisassemblerCompatibility.TestSettingsCompatibility()),
                _ => "Unknown test"
            };
        }

        private string FormatResult(string testName, (bool success, string message) result)
        {
            var icon = result.success ? "✓ PASS" : "✗ FAIL";
            return $"{icon}\n\nTest: {testName}\n\n{result.message}";
        }

        private void DisplayResults(List<(string test, bool success, string message)> results)
        {
            _testListBox.Items.Clear();
            var resultText = new System.Text.StringBuilder();
            resultText.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            resultText.AppendLine("║  COMPATIBILITY TEST RESULTS                                        ║");
            resultText.AppendLine("╠════════════════════════════════════════════════════════════════════╣");

            foreach (var (test, success, message) in results)
            {
                var icon = success ? "✓" : "✗";
                var status = success ? "PASS" : "FAIL";
                _testListBox.Items.Add($"{icon} [{status}] {test}");
                resultText.AppendLine($"║ {icon} [{status,-4}] {test,-45} ║");
                resultText.AppendLine($"║   → {message,-60} ║");
            }

            resultText.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            _detailsTextBox.Text = resultText.ToString();
        }

        private void UpdateSummary(List<(string test, bool success, string message)> results)
        {
            int passed = results.Count(r => r.success);
            int failed = results.Count(r => !r.success);
            _summaryLabel.Text = $"Results: {passed} passed, {failed} failed out of {results.Count} total tests";
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"compatibility_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var report = Core.Compatibility.AssemblerDisassemblerCompatibility.GenerateCompatibilityReport();
                        System.IO.File.WriteAllText(sfd.FileName, report);
                        MessageBox.Show($"Report saved to {sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving report: {ex.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
