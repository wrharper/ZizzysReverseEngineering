// Project: ReverseEngineering.Core
// File: ProjectSystem/ProjectModel.cs

using System.Collections.Generic;

namespace ReverseEngineering.Core.ProjectSystem
{
    public sealed class ProjectModel
    {
        public int ProjectVersion { get; set; } = 1;

        // Path to the main binary this project is associated with
        public string FilePath { get; set; } = string.Empty;

        // Theme identifier (e.g., "Dark", "Light", "MatrixDark")
        public string Theme { get; set; } = "Default";

        // View state for hex and disassembly
        public HexViewState HexView { get; set; } = new HexViewState();
        public AsmViewState AsmView { get; set; } = new AsmViewState();

        // All edits made in this project (audit-friendly patch list)
        public List<PatchEntry> Patches { get; set; } = [];
    }

}