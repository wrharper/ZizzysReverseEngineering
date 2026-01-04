using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReverseEngineering.Core;
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.WinForms.HexEditor;

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

        private readonly DisassemblyController _disasmController;

        private CancellationTokenSource? _hexToAsmCts;
        private bool _suppressEvents;

        public MainMenuController(
            Form form,
            MenuStrip menu,
            HexEditorControl hex,
            LogControl log,
            DisassemblyController disasmController,
            ToolStripStatusLabel statusFile,
            CoreEngine core)
        {
            _form = form;
            _menu = menu;
            _hex = hex;
            _log = log;
            _statusFile = statusFile;
            _core = core;
            _disasmController = disasmController;

            BuildMenu();

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
            file.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (s, e) => _form.Close()));

            _menu.Items.Add(file);
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

            _core.LoadFile(ofd.FileName);

            _suppressEvents = true;
            _hex.SetBuffer(_core.HexBuffer);
            _disasmController.Load(_core);
            _suppressEvents = false;

            _log.Append($"Loaded binary: {ofd.FileName}");
            _statusFile.Text = Path.GetFileName(ofd.FileName);
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
    }
}