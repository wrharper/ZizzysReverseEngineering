using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReverseEngineering.Core;
using System.Text.RegularExpressions;
using ReverseEngineering.WinForms.HexEditor;

namespace ReverseEngineering.WinForms
{
    public class DebugLogControl : UserControl
    {
        private readonly RichTextBox _output;
        private readonly Panel _buttonPanel;
        private readonly Button _runButton;
        private readonly Button _clearButton;
        private readonly Button _saveButton;
        private readonly Label _statusLabel;
        private Process? _process;
        private HexEditorControl? _hexEditor;
        private ulong _lastCrashVirtualAddress = 0;  // Store crash VA for navigation

        public event EventHandler? RunRequested;

        public DebugLogControl()
        {
            BackColor = System.Drawing.SystemColors.Control;

            // Status label
            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "Ready",
                BackColor = System.Drawing.SystemColors.ControlDarkDark,
                ForeColor = System.Drawing.Color.White,
                Padding = new Padding(5)
            };

            // Output text box
            _output = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 10),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.Lime
            };

            // Button panel
            _buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = System.Drawing.SystemColors.Control
            };

            _runButton = new Button
            {
                Text = "Run",
                Width = 80,
                Height = 30,
                Left = 5,
                Top = 5
            };
            _runButton.Click += (s, e) => RunRequested?.Invoke(this, EventArgs.Empty);

            _clearButton = new Button
            {
                Text = "Clear",
                Width = 80,
                Height = 30,
                Left = 90,
                Top = 5
            };
            _clearButton.Click += (s, e) => Clear();

            _saveButton = new Button
            {
                Text = "Save Log",
                Width = 80,
                Height = 30,
                Left = 175,
                Top = 5
            };
            _saveButton.Click += (s, e) => SaveLog();

            _buttonPanel.Controls.Add(_runButton);
            _buttonPanel.Controls.Add(_clearButton);
            _buttonPanel.Controls.Add(_saveButton);

            Controls.Add(_output);
            Controls.Add(_statusLabel);
            Controls.Add(_buttonPanel);
        }

        public void Clear()
        {
            _output.Clear();
            _statusLabel.Text = "Ready";
        }

        public void AppendOutput(string text, bool isError = false)
        {
            _output.Invoke((Action)(() =>
            {
                if (isError)
                {
                    _output.ForeColor = System.Drawing.Color.Red;
                    _output.AppendText("[ERROR] ");
                }
                else
                {
                    _output.ForeColor = System.Drawing.Color.Lime;
                }

                _output.AppendText(text + Environment.NewLine);
            }));
        }

        public void SetStatus(string status)
        {
            _statusLabel.Invoke((Action)(() =>
            {
                _statusLabel.Text = status;
            }));
        }

        /// <summary>
        /// Set the hex editor control for crash navigation.
        /// </summary>
        public void SetHexEditor(HexEditorControl hexEditor)
        {
            _hexEditor = hexEditor;
        }

        /// <summary>
        /// Get the last crash virtual address (for pre-populating dialogs).
        /// </summary>
        public ulong GetLastCrashVirtualAddress()
        {
            return _lastCrashVirtualAddress;
        }

        public async Task RunBinaryAsync(string binaryPath)
        {
            if (string.IsNullOrEmpty(binaryPath))
            {
                AppendOutput("Error: No binary path specified", true);
                return;
            }

            _runButton.Enabled = false;
            SetStatus("Running...");
            AppendOutput($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Executing: {binaryPath}");

            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = binaryPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(binaryPath)
                    }
                };

                _process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        AppendOutput(e.Data);
                };

                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        AppendOutput(e.Data, isError: true);
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                await Task.Run(() => _process.WaitForExit());

                _output.Invoke((Action)(() =>
                {
                    _output.ForeColor = System.Drawing.Color.Yellow;
                    _output.AppendText($"\n[Process exited with code: {_process.ExitCode}]" + Environment.NewLine);
                    _output.ForeColor = System.Drawing.Color.Lime;
                }));

                SetStatus($"Exited with code: {_process.ExitCode}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error running binary: {ex.Message}", isError: true);
                SetStatus("Error");
            }
            finally
            {
                _runButton.Enabled = true;
                _process?.Dispose();
            }
        }

        public void LaunchDebugger(string debuggerPath, string binaryPath)
        {
            try
            {
                SetStatus($"Launching {System.IO.Path.GetFileName(debuggerPath)}...");
                AppendOutput($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Launching debugger: {debuggerPath}");
                AppendOutput($"With binary: {binaryPath}");
                Process.Start(debuggerPath, binaryPath);
            }
            catch (Exception ex)
            {
                AppendOutput($"Error launching debugger: {ex.Message}", isError: true);
                SetStatus("Error launching debugger");
            }
        }

        private void SaveLog()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"debug_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
                DefaultExt = ".txt"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllText(dialog.FileName, _output.Text);
                        SetStatus($"Log saved to {System.IO.Path.GetFileName(dialog.FileName)}");
                    }
                    catch (Exception ex)
                    {
                        AppendOutput($"Error saving log: {ex.Message}", isError: true);
                    }
                }
            }
        }

        /// <summary>
        /// Run binary with Windows Debugger API to capture crash information
        /// </summary>
        public async Task RunBinaryWithDebuggerAsync(string binaryPath, CoreEngine? coreEngine = null)
        {
            if (string.IsNullOrEmpty(binaryPath))
            {
                AppendOutput("Error: No binary path specified", true);
                return;
            }

            _runButton.Enabled = false;
            SetStatus("Debugging...");
            AppendOutput($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DEBUG MODE: Executing: {binaryPath}");

            try
            {
                string result;
                
                // Try Advanced Windows Debugger first (uses Windows Debug API)
                var advancedDebugger = new Debug.AdvancedWindowsDebugger();
                result = await advancedDebugger.DebugBinaryAsync(binaryPath, (msg) =>
                {
                    AppendOutput(msg);
                });

                // If crash info captured, display and return
                if (result.Contains("0x"))
                {
                    _output.Invoke((Action)(() =>
                    {
                        _output.ForeColor = System.Drawing.Color.Yellow;
                        _output.AppendText($"\n[Debug Summary] {result}" + Environment.NewLine);
                        _output.ForeColor = System.Drawing.Color.Lime;
                    }));

                    // Try to extract VA and store for navigation
                    if (coreEngine != null && coreEngine.Disassembly.Count > 0)
                    {
                        var match = Regex.Match(result, @"0x([0-9A-Fa-f]{1,16})");
                        if (match.Success && ulong.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out ulong crashVA))
                        {
                            AppendOutput($"[VA Extraction] Matched: 0x{crashVA:X16}\n");
                            _lastCrashVirtualAddress = crashVA;
                            _output.Invoke((Action)(() =>
                            {
                                _output.ForeColor = System.Drawing.Color.Cyan;
                                _output.AppendText($"[Navigable] VA 0x{crashVA:X16}\n");
                                _output.ForeColor = System.Drawing.Color.Lime;
                            }));
                            SetStatus($"Crash @ VA:0x{crashVA:X16} - Use Debug > Go to Crash Location to navigate");
                        }
                        else
                        {
                            AppendOutput($"[Warning] Could not parse VA from result\n");
                        }
                    }
                    else
                    {
                        AppendOutput($"[Info] CoreEngine not available for VA navigation\n");
                    }

                    SetStatus($"Debug session complete: {result}");
                    return;
                }

                // Fallback to x64dbg if Advanced debugger didn't work
                AppendOutput("\n[Fallback] Trying x64dbg...\n");
                var externalDebugger = new Debug.ExternalDebugger();
                result = await externalDebugger.DebugWithX64dbgAsync(binaryPath, (msg) =>
                {
                    AppendOutput(msg);
                });

                // Fallback to basic Windows debugger if x64dbg not available
                if (result.Contains("not installed"))
                {
                    AppendOutput("\n[Fallback] Using basic Windows debugger\n");
                    var basicDebugger = new Debug.WindowsDebugger();
                    result = await basicDebugger.DebugBinaryAsync(binaryPath, (msg) =>
                    {
                        AppendOutput(msg);
                    });
                }

                _output.Invoke((Action)(() =>
                {
                    _output.ForeColor = System.Drawing.Color.Yellow;
                    _output.AppendText($"\n[Debug Summary] {result}" + Environment.NewLine);
                    _output.ForeColor = System.Drawing.Color.Lime;
                }));

                SetStatus($"Debug session complete: {result}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error during debug: {ex.Message}", isError: true);
                SetStatus("Debug error");
            }
            finally
            {
                _runButton.Enabled = true;
            }
        }
    }
}
