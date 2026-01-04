namespace ReverseEngineering.Core.ProjectSystem
{
    public sealed class PatchEntry
    {
        // Absolute offset into the file/buffer
        public int Offset { get; set; }

        // Original byte value (for audit/validation)
        public byte OldValue { get; set; }

        // New byte value
        public byte NewValue { get; set; }
    }
}
