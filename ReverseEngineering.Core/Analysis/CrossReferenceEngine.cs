using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.Analysis
{
    /// <summary>
    /// Represents a cross-reference: a source instruction referencing a target.
    /// </summary>
    public class CrossReference
    {
        public ulong SourceAddress { get; set; }
        public ulong TargetAddress { get; set; }
        public string RefType { get; set; } = ""; // "code", "data", "import", "string", etc.
        public string? Description { get; set; }

        public override string ToString() => $"0x{SourceAddress:X} -> 0x{TargetAddress:X} [{RefType}]";
    }

    /// <summary>
    /// Builds cross-reference database from disassembly.
    /// Tracks:
    /// - Code → Code (JMP, CALL, conditional branches)
    /// - Code → Data (MOV, LEA, indirect references)
    /// - Data → Code (function pointers, vtables)
    /// </summary>
    public static class CrossReferenceEngine
    {
        private const ulong DefaultImageBase = 0x140000000; // x64 typical base

        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------
        public static Dictionary<ulong, List<CrossReference>> BuildXRefs(
            List<Instruction> disassembly,
            ulong imageBase = DefaultImageBase)
        {
            var xrefs = new Dictionary<ulong, List<CrossReference>>();

            // Step 1: Code → Code references (control flow)
            FindCodeToCodeRefs(disassembly, imageBase, xrefs);

            // Step 2: Code → Data references (data access)
            FindCodeToDataRefs(disassembly, imageBase, xrefs);

            // Step 3: Code → String references (string literals)
            FindCodeToStringRefs(disassembly, imageBase, xrefs);

            return xrefs;
        }

        /// <summary>
        /// Get all references FROM a given address.
        /// </summary>
        public static List<CrossReference> GetOutgoingRefs(ulong address, Dictionary<ulong, List<CrossReference>> xrefs)
        {
            return xrefs.TryGetValue(address, out var refs) ? refs : [];
        }

        /// <summary>
        /// Get all references TO a given address (reverse xref).
        /// </summary>
        public static List<CrossReference> GetIncomingRefs(ulong address, Dictionary<ulong, List<CrossReference>> xrefs)
        {
            return xrefs.Values.SelectMany(refs => refs.Where(r => r.TargetAddress == address)).ToList();
        }

        // ---------------------------------------------------------
        //  CODE → CODE REFERENCES
        // ---------------------------------------------------------
        private static void FindCodeToCodeRefs(List<Instruction> disassembly, ulong imageBase, Dictionary<ulong, List<CrossReference>> xrefs)
        {
            foreach (var ins in disassembly)
            {
                if (ins.Raw == null)
                    continue;

                var mnemonic = ins.Raw.Value.Mnemonic;
                var refType = "";

                // JMP
                if (mnemonic == Mnemonic.Jmp)
                    refType = "jump";
                // Conditional branches
                else if (IsConditionalJump(mnemonic))
                    refType = "cond_jump";
                // CALL
                else if (mnemonic == Mnemonic.Call)
                    refType = "call";
                else
                    continue;

                var target = GetJumpTarget(ins);
                if (target.HasValue)
                {
                    AddXRef(xrefs, ins.Address, target.Value, refType);
                }
            }
        }

        // ---------------------------------------------------------
        //  CODE → DATA REFERENCES
        // ---------------------------------------------------------
        private static void FindCodeToDataRefs(List<Instruction> disassembly, ulong imageBase, Dictionary<ulong, List<CrossReference>> xrefs)
        {
            foreach (var ins in disassembly)
            {
                if (ins.Raw == null)
                    continue;

                var raw = ins.Raw.Value;
                var mnemonic = raw.Mnemonic;

                // MOV r64, imm64 (likely a data reference)
                if (mnemonic == Mnemonic.Mov && raw.OpCount > 0 && raw.Op1Kind == OpKind.Immediate64)
                {
                    var imm = raw.Immediate64;
                    if (IsLikelyAddress(imm, imageBase))
                    {
                        AddXRef(xrefs, ins.Address, imm, "mov_imm64");
                    }
                }

                // LEA r64, [rip + offset] (RIP-relative, common in x64)
                // Note: Simplified RIP-relative handling
                if (mnemonic == Mnemonic.Lea && raw.Op0Kind == OpKind.Register && raw.Op1Kind == OpKind.Memory && raw.MemoryBase == Register.RIP)
                {
                    // For now, skip complex memory address calculation
                    // TODO: Properly calculate RIP-relative targets
                }

                // MOV r64, [rip + offset]
                if (mnemonic == Mnemonic.Mov && raw.Op0Kind == OpKind.Register && raw.Op1Kind == OpKind.Memory && raw.MemoryBase == Register.RIP)
                {
                    // For now, skip complex memory address calculation
                    // TODO: Properly calculate RIP-relative targets
                }
            }
        }

        private static bool IsConditionalJump(Mnemonic mnemonic)
        {
            return mnemonic switch
            {
                Mnemonic.Jo or Mnemonic.Jno or Mnemonic.Jb or Mnemonic.Jae or
                Mnemonic.Je or Mnemonic.Jne or Mnemonic.Jbe or Mnemonic.Ja or
                Mnemonic.Js or Mnemonic.Jns or Mnemonic.Jp or Mnemonic.Jnp or
                Mnemonic.Jl or Mnemonic.Jge or Mnemonic.Jle or Mnemonic.Jg or
                Mnemonic.Jcxz or Mnemonic.Jecxz or Mnemonic.Jrcxz => true,
                _ => false
            };
        }

        // ---------------------------------------------------------
        //  CODE → STRING REFERENCES
        // ---------------------------------------------------------
        private static void FindCodeToStringRefs(List<Instruction> disassembly, ulong imageBase, Dictionary<ulong, List<CrossReference>> xrefs)
        {
            // TODO: This would require section scanning for string literals
            // For now, placeholder
        }

        // ---------------------------------------------------------
        //  HELPERS
        // ---------------------------------------------------------
        private static ulong? GetJumpTarget(Instruction ins)
        {
            if (ins.Raw == null)
                return null;

            var raw = ins.Raw.Value;
            if (raw.OpCount == 0)
                return null;

            var op = raw.Op0Kind;
            if (op == OpKind.NearBranch64)
                return raw.NearBranch64;
            if (op == OpKind.NearBranch32)
                return raw.NearBranch32;

            return null;
        }

        private static void AddXRef(Dictionary<ulong, List<CrossReference>> xrefs, ulong source, ulong target, string refType)
        {
            if (!xrefs.ContainsKey(source))
                xrefs[source] = [];

            xrefs[source].Add(new CrossReference
            {
                SourceAddress = source,
                TargetAddress = target,
                RefType = refType
            });
        }

        private static bool IsLikelyAddress(ulong value, ulong imageBase)
        {
            // Heuristic: values in certain ranges are likely addresses
            // For typical x64: addresses are in range [imageBase, imageBase + 0xFFFFFFFF]
            if (value == 0)
                return false;

            if (value < 0x1000)
                return false; // Likely small immediates

            if (value > imageBase + 0x100000000)
                return false; // Likely outside typical module space

            return true;
        }
    }
}
