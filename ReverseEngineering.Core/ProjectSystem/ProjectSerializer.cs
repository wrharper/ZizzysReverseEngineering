// Project: ReverseEngineering.Core
// File: ProjectSystem/ProjectSerializer.cs

using System;
using System.IO;
using System.Text.Json;

namespace ReverseEngineering.Core.ProjectSystem
{
    public static class ProjectSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true
        };

        // ---------------------------------------------------------
        //  SAVE PROJECT → JSON FILE
        // ---------------------------------------------------------
        public static void Save(string path, ProjectModel project)
        {
            if (project != null)
            {
                string json = JsonSerializer.Serialize(project, _options);
                File.WriteAllText(path, json);
            }
            else
                throw new ArgumentNullException(nameof(project));
        }

        // ---------------------------------------------------------
        //  LOAD PROJECT ← JSON FILE
        // ---------------------------------------------------------
        public static ProjectModel Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Project file not found.", path);

            string json = File.ReadAllText(path);
            var project = JsonSerializer.Deserialize<ProjectModel>(json, _options);

            if (project != null)
            {
                // Versioning hook (future-proof)
                if (project.ProjectVersion < 1)
                    project.ProjectVersion = 1;

                return project;
            }

            throw new InvalidOperationException("Failed to deserialize project file.");
        }
    }
}