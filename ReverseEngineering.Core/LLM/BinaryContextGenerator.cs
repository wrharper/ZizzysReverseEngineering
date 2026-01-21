using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReverseEngineering.Core.Analysis;

#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Converts CoreEngine analysis data into BinaryContextData for LLM consumption
    /// Generates system prompt from context
    /// </summary>
    public class BinaryContextGenerator
    {
        private readonly CoreEngine _engine;

        public BinaryContextGenerator(CoreEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// Generate complete binary context from current engine state
        /// </summary>
        public BinaryContextData GenerateContext()
        {
            var entryPointAddress = _engine.Disassembly.FirstOrDefault()?.Address ?? 0;
            
            var context = new BinaryContextData
            {
                BinaryPath = _engine.HexBuffer.FilePath,
                BinaryFormat = _engine.Is64Bit ? "PE (x64)" : "PE (x86)",
                Is64Bit = _engine.Is64Bit,
                ImageBase = (uint)(_engine.ImageBase & 0xFFFFFFFF),
                ImageSize = (uint)_engine.HexBuffer.Bytes.Length,
                EntryPoint = (uint)(entryPointAddress & 0xFFFFFFFF),
                TotalBytes = _engine.HexBuffer.Bytes.Length,
                ModifiedBytes = _engine.HexBuffer.GetModifiedCount(),
                LastUpdated = DateTime.UtcNow
            };

            // Add recent patches (limit to last 20)
            var patches = _engine.HexBuffer.GetModifiedBytes().Take(20).ToList();
            context.RecentPatches = patches.Select(p => ((uint)p.offset, p.original, p.value)).ToList();

            // Add comprehensive function summaries
            ExtractFunctionAnalysis(context);

            // Add cross-reference analysis
            ExtractCrossReferenceAnalysis(context);

            // Add symbol analysis
            ExtractSymbolAnalysis(context);

            // Add string analysis
            ExtractStringAnalysis(context);

            // Add pattern detection results
            ExtractPatternAnalysis(context);

            return context;
        }

        private void ExtractFunctionAnalysis(BinaryContextData context)
        {
            context.TotalFunctions = _engine.Functions.Count;

            // Build function relationship map
            var funcToXrefCount = new Dictionary<ulong, int>();
            foreach (var xrefList in _engine.CrossReferences.Values)
            {
                foreach (var xref in xrefList)
                {
                    if (!funcToXrefCount.ContainsKey(xref.TargetAddress))
                        funcToXrefCount[xref.TargetAddress] = 0;
                    funcToXrefCount[xref.TargetAddress]++;
                }
            }

            // Add top 50 functions by complexity (size * xref count)
            var rankedFunctions = _engine.Functions
                .OrderByDescending(f => (long)f.InstructionCount * funcToXrefCount.GetValueOrDefault(f.Address, 0))
                .Take(50)
                .ToList();

            foreach (var func in rankedFunctions)
            {
                // Find end address
                var funcInstructions = _engine.Disassembly
                    .Where(i => i.Address >= func.Address)
                    .Take(func.InstructionCount);
                
                var lastInstruction = funcInstructions.LastOrDefault();
                var endAddress = lastInstruction?.EndAddress ?? func.Address;

                // Find functions called by this one
                var calledAddrs = _engine.CrossReferences
                    .Where(kvp => kvp.Key >= func.Address && kvp.Key < endAddress)
                    .SelectMany(kvp => kvp.Value)
                    .Where(xref => xref.RefType == "call")
                    .Select(xref => xref.TargetAddress)
                    .Distinct()
                    .Take(10)
                    .ToList();

                // Find functions that call this one
                var calledByAddrs = _engine.CrossReferences
                    .SelectMany(kvp => kvp.Value)
                    .Where(xref => xref.TargetAddress == func.Address && xref.RefType == "call")
                    .Select(xref => xref.SourceAddress)
                    .Distinct()
                    .Take(10)
                    .ToList();

                context.Functions.Add(new FunctionSummary
                {
                    Address = func.Address,
                    Name = func.Name,
                    Size = (int)(endAddress - func.Address),
                    BlockCount = func.CFG?.Blocks?.Count ?? 1,
                    InstructionCount = func.InstructionCount,
                    CalledAddresses = calledAddrs,
                    CalledByAddresses = calledByAddrs,
                    XRefCount = funcToXrefCount.GetValueOrDefault(func.Address, 0),
                    IsEntryPoint = func.IsEntryPoint,
                    IsImported = func.IsImported
                });
            }

            // Extract top call chains
            ExtractTopCallChains(context);
        }

        private void ExtractTopCallChains(BinaryContextData context)
        {
            // Find most complex call paths (for understanding control flow)
            var callChains = new List<CallChainSummary>();
            
            // For each exported/entry function, trace call path
            var rootFuncs = _engine.Functions
                .Where(f => f.IsEntryPoint || f.IsExported)
                .Take(5)
                .ToList();

            foreach (var rootFunc in rootFuncs)
            {
                var chain = new List<ulong> { rootFunc.Address };
                var visited = new HashSet<ulong> { rootFunc.Address };
                
                TraceCallChain(rootFunc.Address, chain, visited, maxDepth: 5);
                
                if (chain.Count > 1)
                {
                    callChains.Add(new CallChainSummary
                    {
                        Chain = chain,
                        Depth = chain.Count
                    });
                }
            }

            context.TopCallChains = callChains.OrderByDescending(c => c.Depth).Take(10).ToList();
        }

        private void TraceCallChain(ulong currentAddr, List<ulong> chain, HashSet<ulong> visited, int maxDepth)
        {
            if (chain.Count >= maxDepth || !_engine.CrossReferences.TryGetValue(currentAddr, out var xrefs))
                return;

            var callTargets = xrefs
                .Where(x => x.RefType == "call" && !visited.Contains(x.TargetAddress))
                .Take(1)
                .ToList();

            foreach (var target in callTargets)
            {
                chain.Add(target.TargetAddress);
                visited.Add(target.TargetAddress);
                TraceCallChain(target.TargetAddress, chain, visited, maxDepth);
            }
        }

        private void ExtractCrossReferenceAnalysis(BinaryContextData context)
        {
            context.TotalCrossReferences = _engine.CrossReferences.Count;

            // Categorize xrefs
            var codeToCode = 0;
            var codeToData = 0;

            // Get top 30 most-referenced addresses
            var topXrefs = _engine.CrossReferences
                .OrderByDescending(x => x.Value.Count)
                .Take(30)
                .ToList();

            foreach (var xrefGroup in topXrefs)
            {
                foreach (var xref in xrefGroup.Value.Take(3))
                {
                    if (xref.RefType == "call" || xref.RefType.StartsWith("j"))
                        codeToCode++;
                    else
                        codeToData++;

                    context.CrossReferences.Add(new CrossReferenceSummary
                    {
                        From = xref.SourceAddress,
                        To = xref.TargetAddress,
                        RefType = xref.RefType,
                        Description = GetXrefDescription(xref.RefType)
                    });
                }
            }

            context.CodeToCodeRefs = codeToCode;
            context.CodeToDataRefs = codeToData;
        }

        private string GetXrefDescription(string refType)
        {
            return refType switch
            {
                "call" => "Function call",
                "jmp" => "Unconditional jump",
                "je" or "jne" or "jz" or "jnz" or "ja" or "jb" => "Conditional branch",
                "lea" => "Load effective address",
                "mov" => "Move/Load data",
                "push" or "pop" => "Stack operation",
                _ => refType
            };
        }

        private void ExtractSymbolAnalysis(BinaryContextData context)
        {
            context.TotalSymbols = _engine.Symbols.Count;

            var allSymbols = _engine.Symbols.Values.ToList();

            // Categorize symbols
            var imported = allSymbols.Where(s => s.IsImported).ToList();
            var exported = allSymbols.Where(s => s.IsExported).ToList();

            context.ImportedFunctions = imported
                .Take(20)
                .Select(s => new SymbolSummary
                {
                    Address = s.Address,
                    Name = s.Name,
                    SymbolType = s.SymbolType,
                    IsImport = s.IsImported,
                    IsExport = s.IsExported,
                    Section = s.Section,
                    Size = s.Size,
                    SourceDLL = s.SourceDLL
                })
                .ToList();

            context.ExportedFunctions = exported
                .Take(20)
                .Select(s => new SymbolSummary
                {
                    Address = s.Address,
                    Name = s.Name,
                    SymbolType = s.SymbolType,
                    IsImport = s.IsImported,
                    IsExport = s.IsExported,
                    Section = s.Section,
                    Size = s.Size,
                    SourceDLL = s.SourceDLL
                })
                .ToList();

            // Add top general symbols
            context.Symbols = allSymbols
                .Take(50)
                .Select(s => new SymbolSummary
                {
                    Address = s.Address,
                    Name = s.Name,
                    SymbolType = s.SymbolType,
                    IsImport = s.IsImported,
                    IsExport = s.IsExported,
                    Section = s.Section,
                    Size = s.Size,
                    SourceDLL = s.SourceDLL
                })
                .ToList();
        }

        private void ExtractStringAnalysis(BinaryContextData context)
        {
            // Find strings in binary (ASCII and Unicode)
            var strings = new List<StringReferenceSummary>();
            
            try
            {
                var bytes = _engine.HexBuffer.Bytes;
                var currentString = new List<byte>();
                var stringStart = 0;

                for (int i = 0; i < bytes.Length; i++)
                {
                    byte b = bytes[i];
                    if (b >= 0x20 && b < 0x7F) // Printable ASCII
                    {
                        if (currentString.Count == 0)
                            stringStart = i;
                        currentString.Add(b);
                    }
                    else
                    {
                        if (currentString.Count >= 4) // Minimum string length
                        {
                            var str = System.Text.Encoding.ASCII.GetString(currentString.ToArray());
                            strings.Add(new StringReferenceSummary
                            {
                                Address = (ulong)stringStart,
                                Content = str,
                                IsUnicode = false
                            });
                        }
                        currentString.Clear();
                    }
                }

                context.TotalStringsFound = strings.Count;
                context.Strings = strings.Take(50).ToList();
            }
            catch
            {
                context.TotalStringsFound = 0;
            }
        }

        private void ExtractPatternAnalysis(BinaryContextData context)
        {
            // Placeholder for pattern detection
            // In a full implementation, this would use PatternMatcher
            // For now, just identify common patterns
            context.DetectedPatterns = new List<PatternDetectionSummary>();

            // Check for common crypto/compression patterns
            var patterns = new List<PatternDetectionSummary>();

            // Look for XOR operations (common in encryption)
            var xorCount = _engine.Disassembly.Count(i => i.Mnemonic == "xor");
            if (xorCount > 10)
            {
                patterns.Add(new PatternDetectionSummary
                {
                    PatternName = "XOR operations",
                    Address = _engine.Disassembly.First(i => i.Mnemonic == "xor").Address,
                    Confidence = 0.6f,
                    Description = $"Found {xorCount} XOR instructions - possible encryption/obfuscation"
                });
            }

            // Look for loop patterns (common in compression/encoding)
            var loopCount = _engine.Disassembly.Count(i => i.Mnemonic.StartsWith("loop"));
            if (loopCount > 5)
            {
                patterns.Add(new PatternDetectionSummary
                {
                    PatternName = "Loop operations",
                    Address = _engine.Disassembly.First(i => i.Mnemonic.StartsWith("loop")).Address,
                    Confidence = 0.5f,
                    Description = $"Found {loopCount} LOOP instructions - possible compression/encoding"
                });
            }

            context.DetectedPatterns = patterns;
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        /// <summary>
        /// Generate comprehensive system prompt with ALL binary analysis context
        /// This is sent as the system role to establish AI context (2-3 KB typical)
        /// </summary>
        public string GenerateSystemPrompt(BinaryContextData context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert reverse engineering assistant analyzing a binary executable file.");
            sb.AppendLine("Your goal is to help find, understand, and modify specific code patterns.");
            sb.AppendLine();

            // Binary metadata section
            sb.AppendLine("═══ BINARY METADATA ═══");
            sb.AppendLine($"File: {Path.GetFileName(context.BinaryPath) ?? "Unknown"}");
            sb.AppendLine($"Format: {context.BinaryFormat}");
            sb.AppendLine($"Image Base: 0x{context.ImageBase:X}");
            sb.AppendLine($"Entry Point: 0x{context.EntryPoint:X}");
            sb.AppendLine($"Total Size: {FormatSize(context.ImageSize)} ({context.TotalBytes:N0} bytes)");
            if (context.ModifiedBytes > 0)
                sb.AppendLine($"Modified: {context.ModifiedBytes} bytes ({(context.ModifiedBytes * 100.0 / context.TotalBytes):F2}%)");
            sb.AppendLine();

            // Summary statistics
            sb.AppendLine("═══ ANALYSIS SUMMARY ═══");
            sb.AppendLine($"Functions: {context.TotalFunctions}");
            sb.AppendLine($"Cross-References: {context.TotalCrossReferences} (Code→Code: {context.CodeToCodeRefs}, Code→Data: {context.CodeToDataRefs})");
            sb.AppendLine($"Symbols: {context.TotalSymbols} (Imports: {context.ImportedFunctions.Count}, Exports: {context.ExportedFunctions.Count})");
            sb.AppendLine($"Strings: {context.TotalStringsFound}");
            if (context.DetectedPatterns.Count > 0)
                sb.AppendLine($"Patterns: {context.DetectedPatterns.Count} detected");
            sb.AppendLine();

            // Top functions
            if (context.Functions.Count > 0)
            {
                sb.AppendLine("═══ KEY FUNCTIONS ═══");
                foreach (var func in context.Functions.Take(15))
                {
                    sb.Append($"• 0x{func.Address:X}: {func.Name ?? "[unnamed]"}");
                    sb.Append($" | {func.Size} bytes | {func.InstructionCount} instructions | {func.BlockCount} blocks");
                    
                    if (func.IsEntryPoint)
                        sb.Append(" [ENTRY]");
                    if (func.IsImported)
                        sb.Append(" [IMPORT]");
                    
                    if (func.XRefCount > 0 || func.CalledByAddresses.Count > 0 || func.CalledAddresses.Count > 0)
                    {
                        sb.Append(" |");
                        if (func.CalledByAddresses.Count > 0)
                            sb.Append($" called-by:{func.CalledByAddresses.Count}");
                        if (func.CalledAddresses.Count > 0)
                            sb.Append($" calls:{func.CalledAddresses.Count}");
                        if (func.XRefCount > 0)
                            sb.Append($" xref:{func.XRefCount}");
                    }
                    
                    sb.AppendLine();
                }
                if (context.Functions.Count > 15)
                    sb.AppendLine($"... and {context.Functions.Count - 15} more functions");
                sb.AppendLine();
            }

            // Imported functions
            if (context.ImportedFunctions.Count > 0)
            {
                sb.AppendLine("═══ IMPORTED FUNCTIONS ═══");
                var importsByDll = context.ImportedFunctions.GroupBy(i => i.SourceDLL ?? "Unknown");
                foreach (var dllGroup in importsByDll.Take(5))
                {
                    sb.AppendLine($"From {dllGroup.Key}:");
                    foreach (var import in dllGroup.Take(10))
                    {
                        sb.AppendLine($"  • 0x{import.Address:X}: {import.Name}");
                    }
                    if (dllGroup.Count() > 10)
                        sb.AppendLine($"  ... and {dllGroup.Count() - 10} more");
                }
                if (importsByDll.Count() > 5)
                    sb.AppendLine($"... and {importsByDll.Count() - 5} more DLLs");
                sb.AppendLine();
            }

            // Call chains (execution paths)
            if (context.TopCallChains.Count > 0)
            {
                sb.AppendLine("═══ CALL CHAINS (Execution Paths) ═══");
                foreach (var chain in context.TopCallChains.Take(5))
                {
                    var chainAddrs = chain.Chain.Take(6).Select(a => $"0x{a:X}").ToList();
                    var chainStr = string.Join(" → ", chainAddrs);
                    if (chain.Chain.Count > 6)
                        chainStr += $" → ... (total depth: {chain.Depth})";
                    sb.AppendLine($"• {chainStr}");
                }
                sb.AppendLine();
            }

            // Top cross-references
            if (context.CrossReferences.Count > 0)
            {
                sb.AppendLine("═══ KEY CROSS-REFERENCES ═══");
                foreach (var xref in context.CrossReferences.Take(12))
                {
                    sb.AppendLine($"• 0x{xref.From:X} → 0x{xref.To:X} ({xref.RefType}: {xref.Description})");
                }
                if (context.CrossReferences.Count > 12)
                    sb.AppendLine($"... and {context.CrossReferences.Count - 12} more references");
                sb.AppendLine();
            }

            // Strings
            if (context.Strings.Count > 0)
            {
                sb.AppendLine("═══ DISCOVERED STRINGS ═══");
                foreach (var str in context.Strings.Take(15))
                {
                    var display = str.Content.Length > 70 
                        ? str.Content.Substring(0, 67) + "..." 
                        : str.Content;
                    sb.AppendLine($"• 0x{str.Address:X}: \"{display}\"");
                }
                if (context.Strings.Count > 15)
                    sb.AppendLine($"... and {context.Strings.Count - 15} more strings");
                sb.AppendLine();
            }

            // Pattern detection
            if (context.DetectedPatterns.Count > 0)
            {
                sb.AppendLine("═══ DETECTED PATTERNS ═══");
                foreach (var pattern in context.DetectedPatterns)
                {
                    sb.AppendLine($"• {pattern.PatternName} @ 0x{pattern.Address:X}");
                    sb.AppendLine($"  Confidence: {pattern.Confidence * 100:F0}%");
                    sb.AppendLine($"  {pattern.Description}");
                }
                sb.AppendLine();
            }

            // Recent patches
            if (context.RecentPatches.Count > 0)
            {
                sb.AppendLine("═══ RECENT PATCHES ═══");
                foreach (var patch in context.RecentPatches.Take(15))
                {
                    var (offset, original, value) = patch;
                    sb.AppendLine($"• 0x{offset:X}: 0x{original:X2} → 0x{value:X2}");
                }
                if (context.RecentPatches.Count > 15)
                    sb.AppendLine($"... and {context.RecentPatches.Count - 15} more patches");
                sb.AppendLine();
            }

            // Instructions
            sb.AppendLine("═══ YOUR CAPABILITIES ═══");
            sb.AppendLine("✓ Analyze code patterns and assembly logic");
            sb.AppendLine("✓ Suggest patch locations (NOP, jumps, calls, writes)");
            sb.AppendLine("✓ Explain function behavior and data structures");
            sb.AppendLine("✓ Identify API usage and system calls");
            sb.AppendLine("✓ Locate string references and their usage");
            sb.AppendLine("✓ Find encryption, compression, or obfuscation patterns");
            sb.AppendLine();
            sb.AppendLine("GUIDELINES:");
            sb.AppendLine("• Always reference addresses in hex (0xADDRESS)");
            sb.AppendLine("• Provide byte sequences for suggested patches");
            sb.AppendLine("• Focus on specific addresses when analyzing");
            sb.AppendLine("• Use the analysis data to understand binary structure");

            return sb.ToString();
        }

        /// <summary>
        /// Generate brief context update (for changes since last context)
        /// </summary>
        public string GenerateContextUpdatePrompt(BinaryContextData previous, BinaryContextData current)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== BINARY CONTEXT UPDATE ===");
            
            if (current.ModifiedBytes > previous.ModifiedBytes)
            {
                var newPatches = current.ModifiedBytes - previous.ModifiedBytes;
                sb.AppendLine($"New patches applied: {newPatches} bytes modified");
                
                var recentlyAdded = current.RecentPatches
                    .Take(Math.Min(5, current.RecentPatches.Count))
                    .ToList();
                    
                foreach (var patch in recentlyAdded)
                {
                    sb.AppendLine($"  0x{patch.offset:X}: {patch.original:X2} → {patch.Item3:X2}");
                }
            }

            if (current.TotalFunctions != previous.TotalFunctions)
            {
                var diff = current.TotalFunctions - previous.TotalFunctions;
                sb.AppendLine($"Functions: {previous.TotalFunctions} → {current.TotalFunctions} ({(diff > 0 ? "+" : "")}{diff})");
            }

            if (current.TotalCrossReferences != previous.TotalCrossReferences)
            {
                var diff = current.TotalCrossReferences - previous.TotalCrossReferences;
                sb.AppendLine($"Cross-References: {previous.TotalCrossReferences} → {current.TotalCrossReferences} ({(diff > 0 ? "+" : "")}{diff})");
            }

            sb.AppendLine("Context is now up to date. Ready for analysis queries.");

            return sb.ToString();
        }
    }
}
