namespace ReverseEngineering.Core.ProjectSystem
{
    public class HexViewState
    {
        public int ScrollOffset { get; set; }
        public int CaretIndex { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionEnd { get; set; }
    }
}