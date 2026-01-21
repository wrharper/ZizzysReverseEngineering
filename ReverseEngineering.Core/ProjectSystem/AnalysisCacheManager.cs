using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReverseEngineering.Core;
using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// Manages persistent caching of binary analysis results.
    /// Allows resuming large file analysis across app sessions.
    /// Cache stored relative to executable directory.
    /// </summary>
    public class AnalysisCacheManager
    {
        private readonly string _cachePath;
        private string _currentCacheDir = "";
        private readonly JsonSerializerOptions _jsonOptions;

        public AnalysisCacheManager()
        {
            // Get exe directory and create Cache subfolder
            string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
                exeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            _cachePath = Path.Combine(exeDir, "Cache");
            Directory.CreateDirectory(_cachePath);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Initialize cache for a binary file
        /// </summary>
        public void InitializeForBinary(string binaryPath)
        {
            if (!File.Exists(binaryPath))
                throw new FileNotFoundException($"Binary file not found: {binaryPath}");

            string fileHash = ComputeFileHash(binaryPath);
            string fileName = Path.GetFileNameWithoutExtension(binaryPath);
            _currentCacheDir = Path.Combine(_cachePath, $"{fileName}_{fileHash}");
            Directory.CreateDirectory(_currentCacheDir);
            Logger.Debug("AnalysisCache", $"Initialized cache: {_currentCacheDir}");
        }

        /// <summary>
        /// Get analysis status - which steps have completed
        /// </summary>
        public AnalysisStatus GetAnalysisStatus()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return new AnalysisStatus();

            var status = new AnalysisStatus();

            // Check if files exist AND have content (non-empty = not corrupted/incomplete)
            status.FunctionsCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "functions.json"), minBytes: 10);
            status.CFGCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "cfg.json"), minBytes: 10);
            status.XRefsCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "xrefs.json"), minBytes: 2);
            status.SymbolsCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "symbols.json"), minBytes: 2);
            status.StringsCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "strings.json"), minBytes: 10);
            status.AnnotationsCompleted = IsValidCacheFile(Path.Combine(_currentCacheDir, "annotations.bin"), minBytes: 4);

            Logger.Debug("AnalysisCache", $"Cache status: Functions={status.FunctionsCompleted}, CFG={status.CFGCompleted}, XRefs={status.XRefsCompleted}, Symbols={status.SymbolsCompleted}, Strings={status.StringsCompleted}, Annotations={status.AnnotationsCompleted}");

            return status;
        }

        /// <summary>
        /// Validate cache file exists and has minimum content
        /// </summary>
        private static bool IsValidCacheFile(string path, int minBytes = 1)
        {
            if (!File.Exists(path))
            {
                Logger.Debug("AnalysisCache", $"Cache file missing: {Path.GetFileName(path)}");
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(path);
                bool isValid = fileInfo.Length >= minBytes;
                if (!isValid)
                    Logger.Debug("AnalysisCache", $"Cache file too small ({fileInfo.Length} bytes, need {minBytes}): {Path.GetFileName(path)}");
                return isValid;
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Error validating cache file {Path.GetFileName(path)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save functions to cache
        /// </summary>
        public void SaveFunctions(List<Function> functions)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveFunctions: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "functions.json");
                string json = JsonSerializer.Serialize(functions, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved {functions.Count} functions to {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save functions: {ex.Message}");
            }
        }

        /// <summary>
        /// Load functions from cache
        /// </summary>
        public List<Function>? LoadFunctions()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "functions.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Function>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save control flow graphs
        /// </summary>
        public void SaveCFG(ControlFlowGraph cfg)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveCFG: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "cfg.json");
                string json = JsonSerializer.Serialize(cfg, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved CFG to {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save CFG: {ex.Message}");
            }
        }

        /// <summary>
        /// Load control flow graph from cache
        /// </summary>
        public ControlFlowGraph? LoadCFG()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "cfg.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ControlFlowGraph>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save cross-references
        /// </summary>
        public void SaveXRefs(Dictionary<ulong, List<CrossReference>> xrefs)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveXRefs: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "xrefs.json");
                string json = JsonSerializer.Serialize(xrefs, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved {xrefs.Count} xref groups to {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save xrefs: {ex.Message}");
            }
        }

        /// <summary>
        /// Load cross-references from cache
        /// </summary>
        public Dictionary<ulong, List<CrossReference>>? LoadXRefs()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "xrefs.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<ulong, List<CrossReference>>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save symbols
        /// </summary>
        public void SaveSymbols(Dictionary<ulong, Symbol> symbols)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveSymbols: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "symbols.json");
                string json = JsonSerializer.Serialize(symbols, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved {symbols.Count} symbols to {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save symbols: {ex.Message}");
            }
        }

        /// <summary>
        /// Load symbols from cache
        /// </summary>
        public Dictionary<ulong, Symbol>? LoadSymbols()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "symbols.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<ulong, Symbol>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save found strings
        /// </summary>
        public void SaveStrings(List<string> strings)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveStrings: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "strings.json");
                string json = JsonSerializer.Serialize(strings, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved {strings.Count} strings to {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save strings: {ex.Message}");
            }
        }

        /// <summary>
        /// Load strings from cache
        /// </summary>
        public List<string>? LoadStrings()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "strings.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<string>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save patterns found
        /// </summary>
        public void SavePatterns(List<PatternMatch> patterns)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return;

            string path = Path.Combine(_currentCacheDir, "patterns.json");
            string json = JsonSerializer.Serialize(patterns, _jsonOptions);
            WriteAtomicFile(path, json);
        }

        /// <summary>
        /// Load patterns from cache
        /// </summary>
        public List<PatternMatch>? LoadPatterns()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "patterns.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<PatternMatch>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save annotation state (step 6 - instruction annotations)
        /// Uses binary format to avoid JSON memory issues on huge binaries
        /// Only saves addresses with non-empty annotations to minimize file size
        /// </summary>
        public void SaveAnnotationState(List<Instruction> annotatedInstructions)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveAnnotationState: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "annotations.bin");
                
                // Filter to only instructions with annotations
                var annotated = annotatedInstructions
                    .Where(i => !string.IsNullOrEmpty(i.SymbolName) || !string.IsNullOrEmpty(i.Annotation))
                    .ToList();

                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new BinaryWriter(stream);
                
                writer.Write(annotated.Count);
                
                foreach (var inst in annotated)
                {
                    writer.Write(inst.Address);
                    writer.Write(inst.SymbolName ?? "");
                    writer.Write(inst.Annotation ?? "");
                }
                
                Logger.Debug("AnalysisCache", $"Saved {annotated.Count} annotations to {path} ({stream.Length:N0} bytes)");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save annotations: {ex.Message}");
            }
        }

        /// <summary>
        /// Load annotation state from cache and apply to disassembly
        /// </summary>
        public void LoadAnnotationState(List<Instruction> disassembly)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return;

            string path = Path.Combine(_currentCacheDir, "annotations.bin");
            if (!File.Exists(path))
                return;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);
                
                int count = reader.ReadInt32();
                
                // Build address map for fast lookup
                var addressMap = new Dictionary<ulong, Instruction>();
                foreach (var inst in disassembly)
                    addressMap[inst.Address] = inst;
                
                // Apply cached annotations
                for (int i = 0; i < count; i++)
                {
                    ulong address = reader.ReadUInt64();
                    string symbolName = reader.ReadString();
                    string annotation = reader.ReadString();
                    
                    if (addressMap.TryGetValue(address, out var inst))
                    {
                        if (!string.IsNullOrEmpty(symbolName))
                            inst.SymbolName = symbolName;
                        if (!string.IsNullOrEmpty(annotation))
                            inst.Annotation = annotation;
                    }
                }
                
                Logger.Debug("AnalysisCache", $"Loaded {count} annotations from {path}");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to load annotations: {ex.Message}");
            }
        }

        /// <summary>
        /// Save disassembly with file hash for change detection
        /// Uses compressed binary format to avoid JSON memory issues on huge binaries
        /// </summary>
        public void SaveDisassembly(List<Instruction> disassembly, string binaryPath)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveDisassembly: No cache directory");
                return;
            }

            try
            {
                string fileHash = ComputeFileHash(binaryPath);
                string path = Path.Combine(_currentCacheDir, "disassembly.bin");
                
                // Write binary format: hash (string) + instruction count + instructions
                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new BinaryWriter(stream);
                
                // Write metadata
                writer.Write(fileHash);
                writer.Write(DateTime.UtcNow.Ticks);
                writer.Write(disassembly.Count);
                
                // Write instructions in compact format
                foreach (var inst in disassembly)
                {
                    writer.Write(inst.Address);
                    writer.Write(inst.FileOffset);
                    writer.Write(inst.RVA);
                    writer.Write((byte)(inst.Bytes?.Length ?? 0));
                    if (inst.Bytes != null)
                        writer.Write(inst.Bytes);
                    writer.Write(inst.Mnemonic ?? "");
                    writer.Write(inst.Operands ?? "");
                }
                
                Logger.Debug("AnalysisCache", $"Saved {disassembly.Count} instructions to {path} ({stream.Length:N0} bytes)");
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to save disassembly: {ex.Message}");
            }
        }

        /// <summary>
        /// Load disassembly from cache if file hash matches (no changes detected)
        /// </summary>
        public List<Instruction>? LoadDisassembly(string binaryPath)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "LoadDisassembly: No cache directory");
                return null;
            }

            string path = Path.Combine(_currentCacheDir, "disassembly.bin");
            if (!File.Exists(path))
            {
                Logger.Debug("AnalysisCache", "LoadDisassembly: Cache file not found");
                return null;
            }

            try
            {
                // Compute current file hash
                string currentHash = ComputeFileHash(binaryPath);

                // Read binary format
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);
                
                // Read metadata
                string cachedHash = reader.ReadString();
                long timestamp = reader.ReadInt64();
                int instructionCount = reader.ReadInt32();
                
                // Check if file hash matches
                if (cachedHash != currentHash)
                {
                    Logger.Info("AnalysisCache", $"File changed (hash mismatch). Re-disassembly needed.");
                    return null;
                }

                // Hash matches - read instructions
                var disassembly = new List<Instruction>(instructionCount);
                for (int i = 0; i < instructionCount; i++)
                {
                    var inst = new Instruction
                    {
                        Address = reader.ReadUInt64(),
                        FileOffset = (int)reader.ReadUInt32(),
                        RVA = reader.ReadUInt32(),
                    };
                    
                    byte byteCount = reader.ReadByte();
                    if (byteCount > 0)
                        inst.Bytes = reader.ReadBytes(byteCount);
                    
                    inst.Mnemonic = reader.ReadString();
                    inst.Operands = reader.ReadString();
                    disassembly.Add(inst);
                }

                Logger.Info("AnalysisCache", $"✓ Loaded {disassembly.Count:N0} cached instructions (file unchanged)");
                return disassembly;
            }
            catch (Exception ex)
            {
                Logger.Error("AnalysisCache", $"Failed to load disassembly: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear all cache for current binary
        /// </summary>
        public void ClearCache()
        {
            if (!string.IsNullOrEmpty(_currentCacheDir) && Directory.Exists(_currentCacheDir))
            {
                Directory.Delete(_currentCacheDir, true);
                Directory.CreateDirectory(_currentCacheDir);
            }
        }

        /// <summary>
        /// Get cache directory size
        /// </summary>
        public long GetCacheSize()
        {
            if (string.IsNullOrEmpty(_currentCacheDir) || !Directory.Exists(_currentCacheDir))
                return 0;

            long size = 0;
            foreach (var file in Directory.GetFiles(_currentCacheDir))
            {
                size += new FileInfo(file).Length;
            }
            return size;
        }

        // ---------------------------------------------------------
        //  PRIVATE HELPERS
        // ---------------------------------------------------------
        
        /// <summary>
        /// Atomically write JSON file by writing to temp file first, then renaming
        /// This ensures file is never left in a corrupted/incomplete state
        /// </summary>
        private static void WriteAtomicFile(string path, string content)
        {
            string tempPath = path + ".tmp";
            try
            {
                // Write to temp file first
                File.WriteAllText(tempPath, content);
                
                // Atomic replace (Windows deletes old file and renames temp in one operation)
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);
            }
            catch
            {
                // Cleanup temp file if something went wrong
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash)[..8]; // First 8 chars
        }

        // ---------------------------------------------------------
        //  PROGRESS CHECKPOINT METHODS (for resumable analysis)
        // ---------------------------------------------------------

        /// <summary>
        /// Load analysis progress checkpoint
        /// </summary>
        public AnalysisProgress? LoadProgress()
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
                return null;

            string path = Path.Combine(_currentCacheDir, "progress.json");
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AnalysisProgress>(json);
            }
            catch (Exception ex)
            {
                Logger.Debug("AnalysisCache", $"Failed to load progress: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save analysis progress checkpoint
        /// </summary>
        public void SaveProgress(AnalysisProgress progress)
        {
            if (string.IsNullOrEmpty(_currentCacheDir))
            {
                Logger.Debug("AnalysisCache", "SaveProgress: No cache directory");
                return;
            }

            try
            {
                string path = Path.Combine(_currentCacheDir, "progress.json");
                string json = JsonSerializer.Serialize(progress, _jsonOptions);
                WriteAtomicFile(path, json);
                Logger.Debug("AnalysisCache", $"Saved progress: {progress}");
            }
            catch (Exception ex)
            {
                Logger.Debug("AnalysisCache", $"Failed to save progress: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Analysis progress tracking
    /// </summary>
    public class AnalysisStatus
    {
        public bool FunctionsCompleted { get; set; }
        public bool CFGCompleted { get; set; }
        public bool XRefsCompleted { get; set; }
        public bool SymbolsCompleted { get; set; }
        public bool StringsCompleted { get; set; }
        public bool AnnotationsCompleted { get; set; }

        public bool IsFullyAnalyzed => FunctionsCompleted && CFGCompleted && XRefsCompleted && 
                                       SymbolsCompleted && StringsCompleted && AnnotationsCompleted;

        public int CompletedSteps => 
            (FunctionsCompleted ? 1 : 0) +
            (CFGCompleted ? 1 : 0) +
            (XRefsCompleted ? 1 : 0) +
            (SymbolsCompleted ? 1 : 0) +
            (StringsCompleted ? 1 : 0) +
            (AnnotationsCompleted ? 1 : 0);

        public override string ToString()
        {
            return $"Analysis: {CompletedSteps}/6 steps complete";
        }
    }

    /// <summary>
    /// Cached pattern match result (different from Analysis.PatternMatch for strings)
    /// </summary>
    public class CachedPatternMatch
    {
        public ulong Address { get; set; }
        public string PatternName { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
