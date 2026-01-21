using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.Core.AILogs;
using ReverseEngineering.WinForms.HexEditor;
using ReverseEngineering.WinForms.LLM;
using ReverseEngineering.WinForms.AILogs;
using ReverseEngineering.WinForms.Compatibility;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class MainMenuController
    {
        private readonly Form _form;
        private readonly MenuStrip _menu;
        private readonly HexEditorControl _hex;
        private readonly LogControl _log;
        private readonly ToolStripStatusLabel _statusFile;
        private readonly CoreEngine _core;
        private readonly PEInfoControl? _peInfoControl;
        private readonly LLMPane? _llmPane;

        private readonly DisassemblyController _disasmController;
        private readonly AnalysisController? _analysisController;
        private readonly AILogsManager? _aiLogsManager;

        private CancellationTokenSource? _hexToAsmCts;
        private bool _suppressEvents;

        public MainMenuController(
            Form form,
            MenuStrip menu,
            HexEditorControl hex,
            LogControl log,
            DisassemblyController disasmController,
            ToolStripStatusLabel statusFile,
            CoreEngine core,
            AnalysisController? analysisController = null,
            AILogsManager? aiLogsManager = null,
            PEInfoControl? peInfoControl = null,
            LLMPane? llmPane = null)
        {
            _form = form;
            _menu = menu;
            _hex = hex;
            _log = log;
            _statusFile = statusFile;
            _core = core;
            _disasmController = disasmController;
            _analysisController = analysisController;
            _aiLogsManager = aiLogsManager ?? new AILogsManager();
            _peInfoControl = peInfoControl;
            _llmPane = llmPane;

            BuildMenu();

            // Wire up app close to clear LLM context
            _form.FormClosing += (s, e) => _llmPane?.Clear();

            // ⭐ CRITICAL: async HEX → ASM sync
            _hex.ByteChanged += OnHexByteChanged;
        }

        private void BuildMenu()
        {
            var file = new ToolStripMenuItem("File");

            file.DropDownItems.Add(new ToolStripMenuItem("Open Binary", null, OpenBinary));
            file.DropDownItems.Add(new ToolStripMenuItem("Open Project", null, OpenProject));
            file.DropDownItems.Add(new ToolStripMenuItem("Save Project", null, SaveProject));
            file.DropDownItems.Add(new ToolStripMenuItem("Export Patch", null, ExportPatch));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (s, e) => _form.Close()));

            _menu.Items.Add(file);

            // ---------------------------------------------------------
            //  EDIT MENU (undo/redo)
            // ---------------------------------------------------------
            var edit = new ToolStripMenuItem("Edit");

            var undoItem = new ToolStripMenuItem("Undo", null, UndoClick);
            undoItem.ShortcutKeys = Keys.Control | Keys.Z;
            edit.DropDownItems.Add(undoItem);

            var redoItem = new ToolStripMenuItem("Redo", null, RedoClick);
            redoItem.ShortcutKeys = Keys.Control | Keys.Y;
            edit.DropDownItems.Add(redoItem);

            edit.DropDownItems.Add(new ToolStripSeparator());

            var findItem = new ToolStripMenuItem("Find...", null, FindClick);
            findItem.ShortcutKeys = Keys.Control | Keys.F;
            edit.DropDownItems.Add(findItem);

            _menu.Items.Add(edit);

            // ---------------------------------------------------------
            //  ANALYSIS MENU
            // ---------------------------------------------------------
            if (_analysisController != null)
            {
                var analysis = new ToolStripMenuItem("Analysis");

                var runAnalysisItem = new ToolStripMenuItem("Run Analysis", null, (s, e) => RunAnalysisClick());
                runAnalysisItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.A;
                analysis.DropDownItems.Add(runAnalysisItem);

                _menu.Items.Add(analysis);
            }

            // ---------------------------------------------------------
            //  AI MENU (Logs, debugging)
            // ---------------------------------------------------------
            var aiMenu = new ToolStripMenuItem("AI");

            var viewLogsItem = new ToolStripMenuItem("View Logs...", null, ShowAILogsViewer);
            aiMenu.DropDownItems.Add(viewLogsItem);

            aiMenu.DropDownItems.Add(new ToolStripSeparator());

            var clearLogsItem = new ToolStripMenuItem("Clear All Logs", null, (s, e) =>
            {
                if (MessageBox.Show("Clear all AI logs? This cannot be undone.", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _aiLogsManager?.ClearAllLogs();
                    _log.Append("AI logs cleared");
                }
            });
            aiMenu.DropDownItems.Add(clearLogsItem);

            _menu.Items.Add(aiMenu);

            // ---------------------------------------------------------
            //  TOOLS MENU (Settings, compatibility test, etc.)
            // ---------------------------------------------------------
            var tools = new ToolStripMenuItem("Tools");

            var settingsItem = new ToolStripMenuItem("Settings...", null, ShowSettingsDialog);
            settingsItem.ShortcutKeys = Keys.Control | Keys.Oemcomma;
            tools.DropDownItems.Add(settingsItem);

            tools.DropDownItems.Add(new ToolStripSeparator());

            var compatItem = new ToolStripMenuItem("Compatibility Tests", null, ShowCompatibilityDialog);
            tools.DropDownItems.Add(compatItem);

            _menu.Items.Add(tools);

            // Subscribe to undo/redo changes to update menu
            _core.UndoRedo.HistoryChanged += UpdateUndoRedoMenu;
            UpdateUndoRedoMenu();
        }

        private void UpdateUndoRedoMenu()
        {
            if (_menu.Items.Count < 2)
                return;

            var edit = _menu.Items[1] as ToolStripMenuItem;
            if (edit?.DropDownItems.Count < 2)
                return;

            var undoItem = edit.DropDownItems[0] as ToolStripMenuItem;
            var redoItem = edit.DropDownItems[1] as ToolStripMenuItem;

            if (undoItem != null)
            {
                undoItem.Enabled = _core.UndoRedo.CanUndo;
                var undoDesc = _core.UndoRedo.GetNextUndoDescription();
                undoItem.Text = _core.UndoRedo.CanUndo && !string.IsNullOrEmpty(undoDesc)
                    ? $"Undo {undoDesc}"
                    : "Undo";
            }

            if (redoItem != null)
            {
                redoItem.Enabled = _core.UndoRedo.CanRedo;
                redoItem.Text = _core.UndoRedo.CanRedo
                    ? $"Redo {_core.UndoRedo.GetNextRedoDescription()}"
                    : "Redo";
            }
        }

        private void UndoClick(object? s, EventArgs e)
        {
            _core.UndoRedo.Undo();
            _core.RebuildDisassemblyFromBuffer();
            _hex.Invalidate();
            _disasmController.RefreshDisassembly();
        }

        private void RedoClick(object? s, EventArgs e)
        {
            _core.UndoRedo.Redo();
            _core.RebuildDisassemblyFromBuffer();
            _hex.Invalidate();
            _disasmController.RefreshDisassembly();
        }

        private void FindClick(object? s, EventArgs e)
        {
            var searchDialog = new ReverseEngineering.WinForms.Search.SearchDialog(_core);
            searchDialog.ResultSelected += (result) =>
            {
                if (result.Offset >= 0)
                {
                    _suppressEvents = true;
                    _hex.SetSelection(result.Offset, result.Offset);
                    _hex.ScrollTo(result.Offset);
                    _suppressEvents = false;
                }
                else if (result.Address > 0)
                {
                    int offset = _core.AddressToOffset(result.Address);
                    if (offset >= 0)
                    {
                        _suppressEvents = true;
                        _hex.SetSelection(offset, offset);
                        _hex.ScrollTo(offset);
                        _suppressEvents = false;
                    }
                }
            };
            searchDialog.Show(_form);
        }
        
        // ---------------------------------------------------------
        //  HEX → ASM SYNC (async)
        // ---------------------------------------------------------
        private async void OnHexByteChanged(int offset, byte oldValue, byte newValue)
        {
            if (_suppressEvents)
                return;

            _hexToAsmCts?.Cancel();
            _hexToAsmCts = new CancellationTokenSource();
            var token = _hexToAsmCts.Token;

            try
            {
                await Task.Delay(80, token);

                await Task.Run(() => _core.RebuildInstructionAtOffset(offset), token);

                _suppressEvents = true;
                _disasmController.Load(_core);
                _suppressEvents = false;
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }

        // ---------------------------------------------------------
        //  EXPORT PATCH
        // ---------------------------------------------------------
        private void ExportPatch(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Text Patch|*.txt|JSON Patch|*.json";

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            if (_hex.Buffer == null)
                return;

            var patches = _hex.Buffer
                .GetModifiedBytes()
                .Select(p => new PatchEntry
                {
                    Offset = p.offset,
                    OldValue = p.original,
                    NewValue = p.value
                })
                .ToList();

            if (sfd.FilterIndex == 1)
                PatchExporter.ExportText(sfd.FileName, patches);
            else
                PatchExporter.ExportJson(sfd.FileName, patches);

            _log.Append($"Exported patch: {sfd.FileName}");
        }

        // ---------------------------------------------------------
        //  OPEN BINARY
        // ---------------------------------------------------------
        private void OpenBinary(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Executable files|*.exe;*.dll;*.sys|All files|*.*";

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            // Clear LLM context for new binary
            _llmPane?.Clear();

            _core.LoadFile(ofd.FileName);

            _suppressEvents = true;
            _hex.SetBuffer(_core.HexBuffer);
            _disasmController.Load(_core);
            _suppressEvents = false;

            Logger.Info("UI", $"Loaded binary: {ofd.FileName}");
            _statusFile.Text = Path.GetFileName(ofd.FileName);

            // Load and display PE info
            if (_peInfoControl != null && _core.PEInfo != null)
            {
                _peInfoControl.LoadPEInfo(_core.PEInfo);
                Logger.Info("UI", $"PE: {(_core.PEInfo.Is64Bit ? "x64" : "x86")} @ 0x{_core.PEInfo.ImageBase:X}, Entry: 0x{_core.PEInfo.AddressOfEntryPoint:X}");
            }

            // Auto-run analysis if enabled in settings
            if (_analysisController != null && ReverseEngineering.Core.ProjectSystem.SettingsManager.Current.Analysis.AutoAnalyzeOnLoad)
            {
                Logger.Info("Analysis", "Starting analysis...");
                _ = _analysisController.RunAnalysisAsync();
            }
        }

        // ---------------------------------------------------------
        //  OPEN PROJECT
        // ---------------------------------------------------------
        private void OpenProject(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Hex Project|*.hexproj";

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            // Clear LLM context for new project
            _llmPane?.Clear();

            var project = ProjectSerializer.Load(ofd.FileName);

            ProjectManager.RestoreState(
                project,
                out string filePath,
                out string theme,
                out var hexState,
                out var asmState,
                out var patches
            );

            // Load binary
            _core.LoadFile(filePath);
            _hex.SetBuffer(_core.HexBuffer);

            // Apply patches
            ProjectManager.ApplyPatches(_core.HexBuffer, patches);

            // Rebuild disassembly to reflect patched bytes
            _core.RebuildDisassemblyFromBuffer();

            // Reload disassembly UI
            _disasmController.Load(_core);

            // Restore view state
            _hex.SetViewState(hexState);
            _disasmController.SetViewState(asmState);

            _log.Append($"Loaded project: {ofd.FileName}");
            _statusFile.Text = Path.GetFileName(ofd.FileName);

            // Auto-run analysis on project load
            if (_analysisController != null)
            {
                Logger.Info("Analysis", "Starting analysis...");
                _ = _analysisController.RunAnalysisAsync();
            }
        }

        // ---------------------------------------------------------
        //  SAVE PROJECT
        // ---------------------------------------------------------
        private void SaveProject(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Hex Project|*.hexproj";

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            if (_hex.Buffer == null)
                return;

            var patches = _hex.Buffer
                .GetModifiedBytes()
                .Select(p => new PatchEntry
                {
                    Offset = p.offset,
                    OldValue = p.original,
                    NewValue = p.value
                })
                .ToList();

            var project = ProjectManager.CaptureState(
                filePath: _hex.CurrentFilePath,
                theme: "Default",
                hexView: _hex.GetViewState(),
                asmView: _disasmController.GetViewState(),
                patches: patches
            );

            ProjectSerializer.Save(sfd.FileName, project);

            _log.Append($"Saved project: {sfd.FileName}");
            _statusFile.Text = Path.GetFileName(sfd.FileName);
        }

        // ---------------------------------------------------------
        //  ANALYSIS MENU HANDLERS
        // ---------------------------------------------------------
        private void RunAnalysisClick()
        {
            if (_analysisController == null)
            {
                MessageBox.Show("Analysis controller not initialized");
                return;
            }

            if (_core.Disassembly == null || _core.Disassembly.Count == 0)
            {
                MessageBox.Show("No binary loaded. Please open a binary file first.");
                return;
            }

            Logger.Info("Analysis", "Manual analysis start requested");
            _ = _analysisController.RunAnalysisAsync();
        }

        // ---------------------------------------------------------
        //  SETTINGS
        // ---------------------------------------------------------
        private void ShowSettingsDialog(object? sender, EventArgs e)
        {
            var settingsDialog = new Settings.SettingsDialog
            {
                StartPosition = FormStartPosition.CenterParent
            };

            if (settingsDialog.ShowDialog(_form) == DialogResult.OK)
            {
                _log.Append("Settings saved successfully");
                // TODO: Apply theme changes immediately if user changed theme
                var theme = SettingsManager.GetTheme();
                // Theme will be reloaded on next application restart or via ThemeManager
            }
        }

        // ---------------------------------------------------------
        //  AI LOGS
        // ---------------------------------------------------------
        private void ShowAILogsViewer(object? sender, EventArgs e)
        {
            if (_aiLogsManager == null)
            {
                MessageBox.Show("AI logs manager not initialized");
                return;
            }

            var viewer = new AILogsViewer(_aiLogsManager);
            viewer.ShowDialog(_form);
        }

        // ---------------------------------------------------------
        //  COMPATIBILITY TESTS
        // ---------------------------------------------------------
        private void ShowCompatibilityDialog(object? sender, EventArgs e)
        {
            var dialog = new Compatibility.CompatibilityTestDialog();
            dialog.ShowDialog(_form);
        }
    }
}
