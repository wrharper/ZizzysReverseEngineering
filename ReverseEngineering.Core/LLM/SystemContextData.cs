namespace ReverseEngineering.Core.LLM
{
    public class SystemContextData
    {
        public bool SendPE { get; set; }
        public bool SendBytes { get; set; }
        public bool SendDisasm { get; set; }
        public object? PEInfoControl { get; set; }
        public object? HexEditor { get; set; }
        public object? DisasmView { get; set; }
    }
}
