using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// User annotation for an address (function name, comment, etc).
    /// </summary>
    public class Annotation
    {
        public ulong Address { get; set; }
        public string? FunctionName { get; set; }
        public string? Comment { get; set; }
        public string? SymbolType { get; set; }
        public DateTime LastModified { get; set; }

        public override string ToString() => $"0x{Address:X}: {FunctionName ?? Comment ?? "(unnamed)"}";
    }

    /// <summary>
    /// Stores and manages user annotations per project.
    /// </summary>
    public class AnnotationStore
    {
        private readonly Dictionary<ulong, Annotation> _annotations = [];

        // ---------------------------------------------------------
        //  ANNOTATION OPERATIONS
        // ---------------------------------------------------------
        public void SetFunctionName(ulong address, string name)
        {
            if (!_annotations.ContainsKey(address))
                _annotations[address] = new Annotation { Address = address };

            _annotations[address].FunctionName = name;
            _annotations[address].LastModified = DateTime.Now;
        }

        public void SetComment(ulong address, string comment)
        {
            if (!_annotations.ContainsKey(address))
                _annotations[address] = new Annotation { Address = address };

            _annotations[address].Comment = comment;
            _annotations[address].LastModified = DateTime.Now;
        }

        public void SetSymbolType(ulong address, string symbolType)
        {
            if (!_annotations.ContainsKey(address))
                _annotations[address] = new Annotation { Address = address };

            _annotations[address].SymbolType = symbolType;
            _annotations[address].LastModified = DateTime.Now;
        }

        public void RemoveAnnotation(ulong address)
        {
            _annotations.Remove(address);
        }

        public Annotation? GetAnnotation(ulong address)
        {
            return _annotations.TryGetValue(address, out var ann) ? ann : null;
        }

        public string? GetFunctionName(ulong address)
        {
            return _annotations.TryGetValue(address, out var ann) ? ann.FunctionName : null;
        }

        public string? GetComment(ulong address)
        {
            return _annotations.TryGetValue(address, out var ann) ? ann.Comment : null;
        }

        public IReadOnlyDictionary<ulong, Annotation> GetAll() => _annotations;

        // ---------------------------------------------------------
        //  PERSISTENCE
        // ---------------------------------------------------------
        public string SerializeToJson()
        {
            return JsonSerializer.Serialize(_annotations.Values, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public void DeserializeFromJson(string json)
        {
            try
            {
                _annotations.Clear();

                var annotations = JsonSerializer.Deserialize<List<Annotation>>(json) ?? [];
                foreach (var ann in annotations)
                {
                    _annotations[ann.Address] = ann;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("AnnotationStore", "Failed to deserialize annotations", ex);
            }
        }

        public void SaveToFile(string path)
        {
            try
            {
                var json = SerializeToJson();
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Error("AnnotationStore", "Failed to save annotations", ex);
            }
        }

        public void LoadFromFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    DeserializeFromJson(json);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("AnnotationStore", "Failed to load annotations", ex);
            }
        }

        public void Clear()
        {
            _annotations.Clear();
        }
    }
}
