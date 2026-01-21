using System;
using System.Windows.Forms;
using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Displays parsed PE header information in a hierarchical tree view.
    /// Items are clickable to navigate to locations in the binary.
    /// </summary>
    public partial class PEInfoControl : UserControl
    {
        private PEHeaderExtractor.PEInfo? _peInfo;
        
        public event EventHandler<NavigateEventArgs>? Navigate;

        public class NavigateEventArgs : EventArgs
        {
            public ulong Address { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        private TreeView _treeView;

        public PEInfoControl()
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            _treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.CurrentTheme.BackColor,
                ForeColor = ThemeManager.CurrentTheme.ForeColor,
                LineColor = ThemeManager.CurrentTheme.Separator
            };
            _treeView.NodeMouseClick += TreeView_NodeMouseClick;
            _treeView.DoubleClick += TreeView_DoubleClick;
            
            Controls.Add(_treeView);
        }

        private void SetupUI()
        {
            // Already handled in InitializeComponent
        }

        public void LoadPEInfo(PEHeaderExtractor.PEInfo peInfo)
        {
            _peInfo = peInfo;
            _treeView.Nodes.Clear();

            if (!peInfo.IsValid)
            {
                _treeView.Nodes.Add("Invalid PE");
                return;
            }

            // DOS Header
            var dosNode = _treeView.Nodes.Add($"DOS Header @ 0x0");
            dosNode.Nodes.Add($"Signature: {peInfo.DosSignature:X4} (MZ)");
            dosNode.Nodes.Add($"PE Offset: 0x{peInfo.PEOffset:X}");

            // PE Signature
            var peNode = _treeView.Nodes.Add($"PE Signature @ 0x{peInfo.PEOffset:X}");
            peNode.Nodes.Add($"Signature: {peInfo.Signature}");

            // COFF Header
            int coffOffset = (int)peInfo.PEOffset + 4;
            var coffNode = _treeView.Nodes.Add($"COFF Header @ 0x{coffOffset:X}");
            coffNode.Nodes.Add($"Machine: {PEHeaderExtractor.GetMachineType(peInfo.Machine)}");
            coffNode.Nodes.Add($"Number of Sections: {peInfo.NumberOfSections}");
            coffNode.Nodes.Add($"TimeDateStamp: {peInfo.TimeDateStamp} (0x{peInfo.TimeDateStamp:X})");
            coffNode.Nodes.Add($"Characteristics: {PEHeaderExtractor.GetCharacteristics(peInfo.Characteristics)}");

            // Optional Header
            int optionalOffset = coffOffset + 20;
            var optNode = _treeView.Nodes.Add($"Optional Header @ 0x{optionalOffset:X}");
            optNode.Nodes.Add($"Magic: {(peInfo.Is64Bit ? "PE32+ (x64)" : "PE32 (x86)")}");
            optNode.Nodes.Add($"Linker Version: {peInfo.MajorLinkerVersion}.{peInfo.MinorLinkerVersion}");
            optNode.Nodes.Add($"Entry Point: 0x{peInfo.AddressOfEntryPoint:X}");
            optNode.Nodes.Add($"Image Base: 0x{peInfo.ImageBase:X}");
            optNode.Nodes.Add($"Section Alignment: 0x{peInfo.SectionAlignment:X}");
            optNode.Nodes.Add($"File Alignment: 0x{peInfo.FileAlignment:X}");
            optNode.Nodes.Add($"OS Version: {peInfo.MajorOperatingSystemVersion}.{peInfo.MinorOperatingSystemVersion}");
            optNode.Nodes.Add($"Image Size: 0x{peInfo.SizeOfImage:X}");
            optNode.Nodes.Add($"Headers Size: 0x{peInfo.SizeOfHeaders:X}");
            optNode.Nodes.Add($"Subsystem: {PEHeaderExtractor.GetSubsystem(peInfo.Subsystem)}");

            // Sections
            var sectionsNode = _treeView.Nodes.Add("Sections");
            foreach (var section in peInfo.Sections)
            {
                var sectionNode = sectionsNode.Nodes.Add($"{section.Name.TrimEnd('\0')} @ 0x{section.PointerToRawData:X}");
                sectionNode.Nodes.Add($"Virtual Address: 0x{section.VirtualAddress:X}");
                sectionNode.Nodes.Add($"Virtual Size: 0x{section.VirtualSize:X}");
                sectionNode.Nodes.Add($"Raw Size: 0x{section.SizeOfRawData:X}");
                sectionNode.Nodes.Add($"Raw Pointer: 0x{section.PointerToRawData:X}");
                sectionNode.Nodes.Add($"Characteristics: {PEHeaderExtractor.GetSectionCharacteristics(section.Characteristics)}");
            }
        }

        private void TreeView_DoubleClick(object? sender, EventArgs e)
        {
            if (_treeView.SelectedNode == null)
                return;

            var text = _treeView.SelectedNode.Text;

            // Try to extract address from various formats
            ulong address = ExtractAddress(text);
            if (address > 0)
            {
                Navigate?.Invoke(this, new NavigateEventArgs { Address = address, Description = text });
            }
        }

        private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // Just select the node
            _treeView.SelectedNode = e.Node;
        }

        private ulong ExtractAddress(string text)
        {
            // Look for patterns like "0x12345678" or "@ 0x..."
            var parts = text.Split(new[] { "0x", "@" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (ulong.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
                {
                    return address;
                }
            }
            return 0;
        }

        public void Clear()
        {
            _treeView.Nodes.Clear();
            _peInfo = null;
        }
    }
}
