using ReverseEngineering.Core;

namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorState
    {
        public const int BytesPerRow = 16;

        // ---------------------------------------------------------
        //  BUFFER + MODIFIED TRACKING
        // ---------------------------------------------------------
        public HexBuffer? Buffer;

        // ---------------------------------------------------------
        //  LAYOUT METRICS
        // ---------------------------------------------------------
        public int TotalRows;
        public int LineHeight;
        public int CharWidth;

        public int OffsetColumnWidth;
        public int HexColumnWidth;
        public int AsciiColumnWidth;

        // ---------------------------------------------------------
        //  SCROLLING
        // ---------------------------------------------------------
        public int ScrollOffsetY;

        // ---------------------------------------------------------
        //  SELECTION + CARET
        // ---------------------------------------------------------
        public int SelectionStart = -1;
        public int SelectionEnd = -1;

        public int CaretIndex;
        public bool EditingHexNibble;

        // ---------------------------------------------------------
        //  HELPERS
        // ---------------------------------------------------------
        public void ResetSelection()
        {
            SelectionStart = -1;
            SelectionEnd = -1;
            CaretIndex = 0;
            EditingHexNibble = false;
        }
    }
}