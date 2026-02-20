using System.Text;

#nullable enable

namespace ReverseEngineering.Core.LLM
{
    /// <summary>
    /// Converts CoreEngine analysis data into BinaryContextData for LLM consumption
    /// Generates system prompt from context
    /// </summary>
    public class BinaryContextGenerator(CoreEngine engine)
    {
        private readonly CoreEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        /// <summary>
        /// Generate complete binary context from current engine state
        /// </summary>
        public BinaryContextData GenerateContext(SystemContextData scd)
        {
            var entryPointAddress = _engine.Disassembly.FirstOrDefault()?.Address ?? 0;
            
            var context = new BinaryContextData
            {
                SCD = scd,
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

            return context;
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
        public static string GenerateSystemPrompt(BinaryContextData context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert reverse engineering assistant analyzing a binary executable file.");
            sb.AppendLine("Your goal is to help find, understand, and modify specific code patterns.");
            sb.AppendLine();

            // Binary metadata section (static info only)
            sb.AppendLine("═══ BINARY METADATA ═══");
            sb.AppendLine($"File: {Path.GetFileName(context.BinaryPath) ?? "Unknown"}");
            if (context.SCD.SendPE)
                GetPEInfo(sb, context.SCD.PEInfoControl);
            if (context.SCD.SendBytes)
                GetHexBytes(sb, context.SCD.HexEditor);
            if (context.SCD.SendDisasm)
                GetDisassembly(sb, context.SCD.DisasmView);
            sb.AppendLine($"Total Size: {FormatSize(context.ImageSize)} ({context.TotalBytes:N0} bytes)");
            if (context.ModifiedBytes > 0)
                sb.AppendLine($"Modified: {context.ModifiedBytes} bytes ({(context.ModifiedBytes * 100.0 / context.TotalBytes):F2}%)");
            sb.AppendLine();

            // Capabilities and guidelines (static)
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

        public static void GetPEInfo(StringBuilder sb, object? peInfoControl)
        {
            sb.AppendLine("\n[PE Info]:");
            if (peInfoControl != null)
            {
                var method = peInfoControl.GetType().GetMethod("GetVisibleText");
                if (method != null)
                    sb.AppendLine(method.Invoke(peInfoControl, null)?.ToString() ?? "  (No PE info visible)");
                else
                    sb.AppendLine("  (No PE info visible)");
            }
            else
            {
                sb.AppendLine("  (No PE info visible)");
            }
        }

        public static void GetHexBytes(StringBuilder sb, object? hexEditor)
        {
            sb.AppendLine("\n[Hex Bytes]:");

            if (hexEditor != null)
            {
                var fullMethod = hexEditor.GetType().GetMethod("GetAllText");
                if (fullMethod != null)
                {
                    sb.AppendLine(fullMethod.Invoke(hexEditor, null)?.ToString()
                                  ?? "  (No hex bytes available)");
                    return;
                }

                var visibleMethod = hexEditor.GetType().GetMethod("GetVisibleText");
                if (visibleMethod != null)
                {
                    sb.AppendLine(visibleMethod.Invoke(hexEditor, null)?.ToString()
                                  ?? "  (No hex bytes visible)");
                    return;
                }

                sb.AppendLine("  (No hex bytes available)");
            }
            else
            {
                sb.AppendLine("  (No hex bytes visible)");
            }
        }

        public static void GetDisassembly(StringBuilder sb, object? disasmView)
        {
            sb.AppendLine("\n[Disassembly]:");

            if (disasmView != null)
            {
                // Prefer full disassembly if available
                var fullMethod = disasmView.GetType().GetMethod("GetFullDisassemblyText");
                if (fullMethod != null)
                {
                    sb.AppendLine(fullMethod.Invoke(disasmView, null)?.ToString()
                                  ?? "  (No disassembly available)");
                    return;
                }

                // Fallback to visible text
                var visibleMethod = disasmView.GetType().GetMethod("GetVisibleText");
                if (visibleMethod != null)
                {
                    sb.AppendLine(visibleMethod.Invoke(disasmView, null)?.ToString()
                                  ?? "  (No disassembly visible)");
                    return;
                }

                sb.AppendLine("  (No disassembly available)");
            }
            else
            {
                sb.AppendLine("  (No disassembly visible)");
            }
        }
    }
}
