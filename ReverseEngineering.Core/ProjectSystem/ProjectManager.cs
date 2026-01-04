// Project: ReverseEngineering.Core
// File: ProjectSystem/ProjectManager.cs

using System;
using System.Collections.Generic;

namespace ReverseEngineering.Core.ProjectSystem
{
    public static class ProjectManager
    {
        // ---------------------------------------------------------
        //  CAPTURE PROJECT STATE (called by UI layer)
        // ---------------------------------------------------------
        public static ProjectModel CaptureState(
            string filePath,
            string theme,
            HexViewState hexView,
            AsmViewState asmView,
            List<PatchEntry> patches)
        {
            return new ProjectModel
            {
                ProjectVersion = 1,
                FilePath = filePath,
                Theme = theme,
                HexView = hexView ?? new HexViewState(),
                AsmView = asmView ?? new AsmViewState(),
                Patches = patches ?? []
            };
        }

        // ---------------------------------------------------------
        //  RESTORE PROJECT STATE (UI layer applies returned values)
        // ---------------------------------------------------------
        public static void RestoreState(
            ProjectModel project,
            out string filePath,
            out string theme,
            out HexViewState hexView,
            out AsmViewState asmView,
            out List<PatchEntry> patches)
        {
            if (project != null)
            {
                filePath = project.FilePath;
                theme = project.Theme;
                hexView = project.HexView ?? new HexViewState();
                asmView = project.AsmView ?? new AsmViewState();
                patches = project.Patches ?? [];
            }
            else
            {
                throw new ArgumentNullException(nameof(project));
            }
        }

        // ---------------------------------------------------------
        //  APPLY PATCHES TO A BUFFER
        // ---------------------------------------------------------
        public static void ApplyPatches(HexBuffer buffer, List<PatchEntry> patches)
        {
            if (buffer != null)
            {
                if (patches == null)
                    return;

                foreach (var p in patches)
                {
                    if (p.Offset < 0 || p.Offset >= buffer.Bytes.Length)
                        continue;

                    buffer.WriteByte(p.Offset, p.NewValue);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(buffer));
            }
        }

        // ---------------------------------------------------------
        //  GENERATE PATCH LIST FROM BUFFER DIFF
        // ---------------------------------------------------------
        public static List<PatchEntry> GeneratePatchList(HexBuffer buffer)
        {
            var list = new List<PatchEntry>();

            if (buffer == null)
                return list;

            foreach (var (offset, original, value) in buffer.GetModifiedBytes())
            {
                list.Add(new PatchEntry
                {
                    Offset = offset,
                    OldValue = original,
                    NewValue = value
                });
            }

            return list;
        }
    }
}