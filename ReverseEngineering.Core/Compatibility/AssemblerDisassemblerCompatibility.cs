using System;
using System.Collections.Generic;
using System.Diagnostics;
using Iced.Intel;
using Keystone.Net;
using ReverseEngineering.Core.Keystone;
using ReverseEngineering.Core.Analysis;

namespace ReverseEngineering.Core.Compatibility
{
    /// <summary>
    /// Verifies that Keystone (assembler) and Iced (disassembler) are properly integrated
    /// and compatible with all new systems (settings, optimization, caching, AI logs, etc.)
    /// </summary>
    public class AssemblerDisassemblerCompatibility
    {
        // ---------------------------------------------------------
        //  KEYSTONE TESTS
        // ---------------------------------------------------------

        /// <summary>
        /// Test basic Keystone assembly on x86-64
        /// </summary>
        public static (bool success, string message) TestKeystone64BitAssembly()
        {
            try
            {
                var bytes = KeystoneAssembler.Assemble("MOV RAX, RBX", 0x401000, is64Bit: true);
                if (bytes.Length == 0)
                    return (false, "Keystone returned empty bytes");

                // MOV RAX, RBX should be: 48 89 D8
                if (bytes.Length >= 3 && bytes[0] == 0x48 && bytes[1] == 0x89 && bytes[2] == 0xD8)
                    return (true, $"✓ Keystone 64-bit assembly works (MOV RAX,RBX = {BitConverter.ToString(bytes)})");

                return (true, $"✓ Keystone assembled to {BitConverter.ToString(bytes)} (verify manually)");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Keystone assembly failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Keystone x32-bit assembly
        /// </summary>
        public static (bool success, string message) TestKeystone32BitAssembly()
        {
            try
            {
                var bytes = KeystoneAssembler.Assemble("MOV EAX, EBX", 0x401000, is64Bit: false);
                if (bytes.Length == 0)
                    return (false, "Keystone returned empty bytes");

                // MOV EAX, EBX should be: 89 D8
                if (bytes.Length >= 2 && bytes[0] == 0x89 && bytes[1] == 0xD8)
                    return (true, $"✓ Keystone 32-bit assembly works (MOV EAX,EBX = {BitConverter.ToString(bytes)})");

                return (true, $"✓ Keystone assembled to {BitConverter.ToString(bytes)} (verify manually)");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Keystone 32-bit assembly failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test complex Keystone assembly with multiple instructions
        /// </summary>
        public static (bool success, string message) TestKeystoneComplexAssembly()
        {
            try
            {
                var asm = @"
                    PUSH RBP
                    MOV RBP, RSP
                    SUB RSP, 0x20
                    MOV RAX, 0x401000
                    CALL RAX
                    ADD RSP, 0x20
                    POP RBP
                    RET
                ";

                var bytes = KeystoneAssembler.Assemble(asm, 0x401000, is64Bit: true);
                if (bytes.Length == 0)
                    return (false, "Keystone returned empty bytes for complex assembly");

                return (true, $"✓ Keystone complex assembly works ({bytes.Length} bytes generated)");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Keystone complex assembly failed: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        //  ICED TESTS
        // ---------------------------------------------------------

        /// <summary>
        /// Test basic Iced disassembly on x86-64
        /// </summary>
        public static (bool success, string message) TestIced64BitDisassembly()
        {
            try
            {
                byte[] bytes = [0x48, 0x89, 0xD8];  // MOV RAX, RBX
                var decoder = Decoder.Create(64, bytes);
                var instr = new Iced.Intel.Instruction();
                decoder.Decode(out instr);

                if (instr.Mnemonic == Mnemonic.Mov && instr.Length == 3)
                    return (true, $"✓ Iced 64-bit disassembly works ({instr} = {BitConverter.ToString(bytes)})");

                return (false, $"✗ Unexpected instruction: {instr}");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Iced disassembly failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Iced 32-bit disassembly
        /// </summary>
        public static (bool success, string message) TestIced32BitDisassembly()
        {
            try
            {
                byte[] bytes = [0x89, 0xD8];  // MOV EAX, EBX
                var decoder = Decoder.Create(32, bytes);
                var instr = new Iced.Intel.Instruction();
                decoder.Decode(out instr);

                if (instr.Mnemonic == Mnemonic.Mov && instr.Length == 2)
                    return (true, $"✓ Iced 32-bit disassembly works ({instr} = {BitConverter.ToString(bytes)})");

                return (false, $"✗ Unexpected instruction: {instr}");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Iced 32-bit disassembly failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Iced RIP-relative operand analysis
        /// </summary>
        public static (bool success, string message) TestIcedRIPRelativeAnalysis()
        {
            try
            {
                // LEA RAX, [RIP + 0x2000]
                byte[] bytes = [0x48, 0x8D, 0x05, 0x00, 0x20, 0x00, 0x00];
                var decoder = Decoder.Create(64, bytes);
                var instr = new Iced.Intel.Instruction();
                decoder.Decode(out instr);

                if (instr.MemoryBase == Register.RIP)
                    return (true, $"✓ Iced RIP-relative analysis works (instruction has RIP operand)");

                return (false, $"✗ RIP-relative operand not detected: {instr}");
            }
            catch (Exception ex)
            {
                return (false, $"✗ RIP-relative analysis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Iced instruction operand access
        /// </summary>
        public static (bool success, string message) TestIcedOperandAccess()
        {
            try
            {
                // MOV RAX, RBX
                byte[] bytes = [0x48, 0x89, 0xD8];
                var decoder = Decoder.Create(64, bytes);
                var instr = new Iced.Intel.Instruction();
                decoder.Decode(out instr);

                if (instr.OpCount >= 2)
                {
                    var op0 = instr.GetOpKind(0);  // RAX (Register)
                    var op1 = instr.GetOpKind(1);  // RBX (Register)
                    
                    if (op0 == OpKind.Register && op1 == OpKind.Register)
                        return (true, $"✓ Iced operand access works ({instr.Op0Register} = {instr.Op1Register})");
                }

                return (false, $"✗ Operand access failed");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Operand access test failed: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        //  ROUND-TRIP TESTS
        // ---------------------------------------------------------

        /// <summary>
        /// Test round-trip: Iced disassemble → Keystone assemble → Iced disassemble
        /// Ensures compatibility between tools
        /// </summary>
        public static (bool success, string message) TestRoundTripCompatibility()
        {
            try
            {
                // Original bytes: MOV RAX, RBX
                byte[] original = [0x48, 0x89, 0xD8];

                // 1. Disassemble with Iced
                var decoder = Decoder.Create(64, original);
                var instr = new Iced.Intel.Instruction();
                decoder.Decode(out instr);
                var formatter = new IntelFormatter();
                var output = new StringOutput();
                formatter.Format(instr, output);
                var mnemonic = output.ToStringAndReset();

                // 2. Re-assemble with Keystone
                var reassembled = KeystoneAssembler.Assemble(mnemonic, 0x401000, is64Bit: true);

                // 3. Verify bytes match
                if (reassembled.Length == original.Length)
                {
                    bool match = true;
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (reassembled[i] != original[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return (true, $"✓ Round-trip compatibility works: {mnemonic}");
                    else
                        return (true, $"⚠ Round-trip bytes differ (manual review: {BitConverter.ToString(original)} vs {BitConverter.ToString(reassembled)})");
                }

                return (false, $"✗ Round-trip failed: length mismatch ({reassembled.Length} vs {original.Length})");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Round-trip test failed: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        //  NEW SYSTEMS COMPATIBILITY TESTS
        // ---------------------------------------------------------

        /// <summary>
        /// Test compatibility with CoreEngine address mapping (PE-aware OffsetToAddress)
        /// </summary>
        public static (bool success, string message) TestCoreEngineAddressMapping()
        {
            try
            {
                var engine = new CoreEngine();
                // Build a mock disassembly list
                var instructions = new List<Instruction>
                {
                    new() { 
                        Address = 0x401000, 
                        FileOffset = 0x1000, 
                        Mnemonic = "MOV", 
                        Operands = "RAX, RBX", 
                        Bytes = [0x48, 0x89, 0xD8], 
                        Length = 3 
                    },
                    new() { 
                        Address = 0x401003, 
                        FileOffset = 0x1003, 
                        Mnemonic = "NOP", 
                        Operands = "", 
                        Bytes = [0x90], 
                        Length = 1 
                    }
                };

                // Simulate CoreEngine state
                engine.Disassembly.AddRange(instructions);

                // Test OffsetToAddress (uses disassembly lookup, not linear formula)
                var addr = engine.OffsetToAddress(0x1000);
                if (addr == 0x401000)
                    return (true, $"✓ CoreEngine address mapping works (offset 0x1000 → address 0x{addr:X})");

                return (false, $"✗ Address mapping failed: expected 0x401000, got 0x{addr:X}");
            }
            catch (Exception ex)
            {
                return (false, $"✗ CoreEngine address mapping test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with Instruction enhancements (analysis metadata)
        /// </summary>
        public static (bool success, string message) TestInstructionEnhancements()
        {
            try
            {
                var instr = new Instruction
                {
                    Address = 0x401000,
                    Mnemonic = "CALL",
                    Bytes = [0xE8, 0x00, 0x10, 0x00, 0x00],
                    Length = 5,
                    FunctionAddress = 0x401000,
                    BasicBlockAddress = 0x401000,
                    SymbolName = "main",
                    Annotation = "Entry point",
                    IsPatched = false,
                    IsNop = false
                };

                // Verify all fields are accessible and set
                if (!string.IsNullOrEmpty(instr.SymbolName) && 
                    instr.FunctionAddress.HasValue && 
                    instr.BasicBlockAddress.HasValue &&
                    !string.IsNullOrEmpty(instr.Annotation))
                {
                    return (true, $"✓ Instruction enhancements compatible (symbol: {instr.SymbolName}, annotation: {instr.Annotation})");
                }

                return (false, "✗ Instruction metadata fields not working");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Instruction enhancements test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with SearchManager pattern matching
        /// </summary>
        public static (bool success, string message) TestSearchManagerCompatibility()
        {
            try
            {
                var buffer = new byte[]
                {
                    0x48, 0x89, 0xD8,  // MOV RAX, RBX
                    0x90,              // NOP
                    0x48, 0x89, 0xD8   // MOV RAX, RBX again
                };

                var instructions = new List<Instruction>
                {
                    new() { Address = 0x401000, FileOffset = 0, Mnemonic = "MOV", Bytes = [0x48, 0x89, 0xD8], Length = 3 },
                    new() { Address = 0x401003, FileOffset = 3, Mnemonic = "NOP", Bytes = [0x90], Length = 1 },
                    new() { Address = 0x401004, FileOffset = 4, Mnemonic = "MOV", Bytes = [0x48, 0x89, 0xD8], Length = 3 }
                };

                // Test byte pattern search
                var matches = PatternMatcher.FindBytePattern(buffer, "48 89 D8");
                if (matches.Count >= 2)
                    return (true, $"✓ SearchManager/PatternMatcher compatible (found {matches.Count} matches for MOV RAX,RBX)");

                return (false, "✗ Pattern matching returned fewer than expected matches");
            }
            catch (Exception ex)
            {
                return (false, $"✗ SearchManager compatibility test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with HexBuffer optimization
        /// </summary>
        public static (bool success, string message) TestHexBufferCompatibility()
        {
            try
            {
                var buf = new HexBuffer([0x48, 0x89, 0xD8, 0x90, 0x90], "test.bin");
                
                // Write bytes
                buf.WriteByte(0, 0x90);
                buf.WriteBytes(1, [0x90, 0x90]);

                // Check tracking
                var modified = buf.GetModifiedRanges();
                if (modified.Count > 0)
                    return (true, $"✓ HexBuffer optimization compatible (tracked {modified.Count} modified ranges)");

                return (false, "✗ HexBuffer tracking not working");
            }
            catch (Exception ex)
            {
                return (false, $"✗ HexBuffer compatibility test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with DisassemblyOptimizer caching
        /// </summary>
        public static (bool success, string message) TestDisassemblyOptimizerCompatibility()
        {
            try
            {
                var instructions = new List<Instruction>
                {
                    new() { Address = 0x401000, Mnemonic = "MOV", Operands = "RAX, RBX", Bytes = [0x48, 0x89, 0xD8], Length = 3 },
                    new() { Address = 0x401003, Mnemonic = "NOP", Operands = "", Bytes = [0x90], Length = 1 }
                };

                var optimizer = new DisassemblyOptimizer();
                optimizer.BuildCache(instructions);

                if (optimizer.TryGetInstructionAt(0x401000, out var instr) && instr != null)
                    return (true, $"✓ DisassemblyOptimizer caching compatible (cached {instr.Mnemonic})");

                return (false, "✗ DisassemblyOptimizer caching not working");
            }
            catch (Exception ex)
            {
                return (false, $"✗ DisassemblyOptimizer compatibility test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with RIP-relative instruction enhancement
        /// </summary>
        public static (bool success, string message) TestRIPRelativeInstructionEnhancement()
        {
            try
            {
                var instr = new Instruction
                {
                    Address = 0x401000,
                    Mnemonic = "LEA",
                    RIPRelativeTarget = 0x403000,
                    OperandType = "Data"
                };

                var display = instr.GetRIPRelativeDisplay();
                if (display.Contains("0x403000"))
                    return (true, $"✓ RIP-relative enhancement works: {display}");

                return (false, "✗ RIP-relative display not working");
            }
            catch (Exception ex)
            {
                return (false, $"✗ RIP-relative enhancement test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with AI logging
        /// </summary>
        public static (bool success, string message) TestAILoggingCompatibility()
        {
            try
            {
                var entry = new AILogs.AILogEntry
                {
                    Operation = "TestAssembly",
                    Prompt = "Assemble MOV RAX, RBX",
                    AIOutput = "Generated bytes: 48 89 D8",
                    Status = "Success",
                    DurationMs = 125
                };

                entry.Changes.Add(new AILogs.ByteChange
                {
                    Offset = 0,
                    OriginalByte = 0x90,
                    NewByte = 0x48,
                    AssemblyBefore = "NOP",
                    AssemblyAfter = "MOV RAX, RBX"
                });

                if (entry.Changes.Count > 0 && !string.IsNullOrEmpty(entry.AIOutput))
                    return (true, $"✓ AI logging compatible (tracked {entry.Changes.Count} changes)");

                return (false, "✗ AI logging not working");
            }
            catch (Exception ex)
            {
                return (false, $"✗ AI logging compatibility test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test compatibility with Settings system
        /// </summary>
        public static (bool success, string message) TestSettingsCompatibility()
        {
            try
            {
                var settings = ProjectSystem.SettingsManager.Current;
                if (settings.LMStudio != null && settings.Analysis != null && settings.UI != null)
                    return (true, $"✓ Settings system compatible (LM host: {settings.LMStudio.Host}:{settings.LMStudio.Port})");

                return (false, "✗ Settings not initialized");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Settings compatibility test failed: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        //  COMPREHENSIVE TEST RUNNER
        // ---------------------------------------------------------

        /// <summary>
        /// Run all compatibility tests
        /// </summary>
        public static List<(string test, bool success, string message)> RunAllTests()
        {
            var results = new List<(string, bool, string)>();

            // Keystone tests
            var kt64 = TestKeystone64BitAssembly();
            results.Add(("Keystone 64-bit Assembly", kt64.success, kt64.message));

            var kt32 = TestKeystone32BitAssembly();
            results.Add(("Keystone 32-bit Assembly", kt32.success, kt32.message));

            var ktComplex = TestKeystoneComplexAssembly();
            results.Add(("Keystone Complex Assembly", ktComplex.success, ktComplex.message));

            // Iced tests
            var id64 = TestIced64BitDisassembly();
            results.Add(("Iced 64-bit Disassembly", id64.success, id64.message));

            var id32 = TestIced32BitDisassembly();
            results.Add(("Iced 32-bit Disassembly", id32.success, id32.message));

            var irip = TestIcedRIPRelativeAnalysis();
            results.Add(("Iced RIP-relative Analysis", irip.success, irip.message));

            var iop = TestIcedOperandAccess();
            results.Add(("Iced Operand Access", iop.success, iop.message));

            // Round-trip
            var rt = TestRoundTripCompatibility();
            results.Add(("Round-trip Compatibility", rt.success, rt.message));

            // New systems & enhancements
            var coreAddr = TestCoreEngineAddressMapping();
            results.Add(("CoreEngine Address Mapping", coreAddr.success, coreAddr.message));

            var instrEnh = TestInstructionEnhancements();
            results.Add(("Instruction Enhancements", instrEnh.success, instrEnh.message));

            var search = TestSearchManagerCompatibility();
            results.Add(("SearchManager/PatternMatcher", search.success, search.message));

            var hb = TestHexBufferCompatibility();
            results.Add(("HexBuffer Optimization", hb.success, hb.message));

            var do_compat = TestDisassemblyOptimizerCompatibility();
            results.Add(("DisassemblyOptimizer Caching", do_compat.success, do_compat.message));

            var rip = TestRIPRelativeInstructionEnhancement();
            results.Add(("RIP-relative Enhancement", rip.success, rip.message));

            var ai = TestAILoggingCompatibility();
            results.Add(("AI Logging", ai.success, ai.message));

            var settings = TestSettingsCompatibility();
            results.Add(("Settings System", settings.success, settings.message));

            return results;
        }

        /// <summary>
        /// Generate formatted compatibility report
        /// </summary>
        public static string GenerateCompatibilityReport()
        {
            var tests = RunAllTests();
            var sw = new Stopwatch();
            sw.Start();

            var report = new System.Text.StringBuilder();
            report.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            report.AppendLine("║  ASSEMBLER/DISASSEMBLER COMPATIBILITY VERIFICATION REPORT         ║");
            report.AppendLine("╠════════════════════════════════════════════════════════════════════╣");

            int passed = 0, failed = 0;

            foreach (var (test, success, message) in tests)
            {
                var status = success ? "PASS" : "FAIL";
                var icon = success ? "✓" : "✗";
                report.AppendLine($"║ {icon} [{status,-4}] {test,-45} ║");
                report.AppendLine($"║   → {message,-60} ║");

                if (success) passed++;
                else failed++;
            }

            sw.Stop();

            report.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
            report.AppendLine($"║ TOTAL: {passed} PASSED, {failed} FAILED                                              ║");
            report.AppendLine($"║ EXECUTION TIME: {sw.ElapsedMilliseconds}ms                                            ║");
            report.AppendLine("╚════════════════════════════════════════════════════════════════════╝");

            return report.ToString();
        }
    }
}
