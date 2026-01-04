// Project: ReverseEngineering.Core
// File: ProjectSystem/PatchExporter.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ReverseEngineering.Core.ProjectSystem
{
    public static class PatchExporter
    {
        // ---------------------------------------------------------
        //  EXPORT AS HUMAN-READABLE TEXT DIFF
        // ---------------------------------------------------------
        public static void ExportText(string path, List<PatchEntry> patches)
        {
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            var sb = new StringBuilder();

            sb.AppendLine("# ReverseEngineering Patch File");
            sb.AppendLine("# Format: offset old new");
            sb.AppendLine();

            foreach (var p in patches)
            {
                sb.AppendLine($"{p.Offset:X8} {p.OldValue:X2} {p.NewValue:X2}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        // ---------------------------------------------------------
        //  EXPORT AS JSON PATCH
        // ---------------------------------------------------------
        public static void ExportJson(string path, List<PatchEntry> patches)
        {
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(patches, options);
            File.WriteAllText(path, json);
        }

        // ---------------------------------------------------------
        //  FUTURE: EXPORT AS IPS/BPS/UPS
        // ---------------------------------------------------------
        public static void ExportIPS(string path, List<PatchEntry> patches)
        {
            throw new NotImplementedException("IPS export not implemented yet.");
        }

        public static void ExportBPS(string path, List<PatchEntry> patches)
        {
            throw new NotImplementedException("BPS export not implemented yet.");
        }
    }
}