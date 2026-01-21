#nullable enable

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// Tracks progress of each analysis step, enabling resumption after interruption
    /// </summary>
    public class AnalysisProgress
    {
        // Step 1: Functions (0-100%)
        public int FunctionsProcessed { get; set; } = 0;
        public int FunctionsTotal { get; set; } = 0;
        public bool FunctionsStarted { get; set; } = false;
        public bool FunctionsCompleted { get; set; } = false;

        // Step 2: CFG (per-function progress)
        public int CFGFunctionsProcessed { get; set; } = 0;
        public int CFGFunctionsTotal { get; set; } = 0;
        public bool CFGStarted { get; set; } = false;
        public bool CFGCompleted { get; set; } = false;

        // Step 3: XRefs (0-100%)
        public int XRefsProcessed { get; set; } = 0;
        public int XRefsTotal { get; set; } = 0;
        public bool XRefsStarted { get; set; } = false;
        public bool XRefsCompleted { get; set; } = false;

        // Step 4: Symbols (0-100%)
        public int SymbolsProcessed { get; set; } = 0;
        public int SymbolsTotal { get; set; } = 0;
        public bool SymbolsStarted { get; set; } = false;
        public bool SymbolsCompleted { get; set; } = false;

        // Step 5: Strings (0-100%)
        public int StringsProcessed { get; set; } = 0;
        public int StringsTotal { get; set; } = 0;
        public bool StringsStarted { get; set; } = false;
        public bool StringsCompleted { get; set; } = false;

        // Step 6: Annotations (0-100%)
        public int AnnotationsProcessed { get; set; } = 0;
        public int AnnotationsTotal { get; set; } = 0;
        public bool AnnotationsStarted { get; set; } = false;
        public bool AnnotationsCompleted { get; set; } = false;

        /// <summary>
        /// Total number of completed steps (0-6)
        /// </summary>
        public int CompletedSteps =>
            (FunctionsCompleted ? 1 : 0) +
            (CFGCompleted ? 1 : 0) +
            (XRefsCompleted ? 1 : 0) +
            (SymbolsCompleted ? 1 : 0) +
            (StringsCompleted ? 1 : 0) +
            (AnnotationsCompleted ? 1 : 0);

        /// <summary>
        /// Get readable progress summary
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (FunctionsStarted)
                parts.Add($"Functions: {FunctionsProcessed}/{FunctionsTotal}" + (FunctionsCompleted ? " ✓" : ""));
            
            if (CFGStarted)
                parts.Add($"CFG: {CFGFunctionsProcessed}/{CFGFunctionsTotal}" + (CFGCompleted ? " ✓" : ""));
            
            if (XRefsStarted)
                parts.Add($"XRefs: {XRefsProcessed}/{XRefsTotal}" + (XRefsCompleted ? " ✓" : ""));
            
            if (SymbolsStarted)
                parts.Add($"Symbols: {SymbolsProcessed}/{SymbolsTotal}" + (SymbolsCompleted ? " ✓" : ""));
            
            if (StringsStarted)
                parts.Add($"Strings: {StringsProcessed}/{StringsTotal}" + (StringsCompleted ? " ✓" : ""));
            
            if (AnnotationsStarted)
                parts.Add($"Annotations: {AnnotationsProcessed}/{AnnotationsTotal}" + (AnnotationsCompleted ? " ✓" : ""));

            return string.Join(" | ", parts);
        }
    }
}
