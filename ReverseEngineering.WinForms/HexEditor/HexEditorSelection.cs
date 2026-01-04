namespace ReverseEngineering.WinForms.HexEditor
{
    public class HexEditorSelection
    {
        private readonly HexEditorState _s;

        public HexEditorSelection(HexEditorState state)
        {
            _s = state;
        }

        // ---------------------------------------------------------
        //  SET SELECTION
        // ---------------------------------------------------------
        public void SetSelection(int start, int end)
        {
            if (_s.Buffer == null || _s.Buffer.Bytes.Length == 0)
            {
                _s.SelectionStart = -1;
                _s.SelectionEnd = -1;
                return;
            }

            int max = _s.Buffer.Bytes.Length - 1;

            start = Clamp(start, 0, max);
            end = Clamp(end, 0, max);

            _s.SelectionStart = start;
            _s.SelectionEnd = end;
        }

        // ---------------------------------------------------------
        //  GET SELECTION RANGE
        // ---------------------------------------------------------
        public (int start, int end) GetSelectionRange()
        {
            if (_s.SelectionStart < 0 || _s.SelectionEnd < 0)
                return (-1, -1);

            if (_s.SelectionStart <= _s.SelectionEnd)
                return (_s.SelectionStart, _s.SelectionEnd);

            return (_s.SelectionEnd, _s.SelectionStart);
        }

        public int GetSelectionLength()
        {
            var (start, end) = GetSelectionRange();
            if (start < 0 || end < 0)
                return 0;

            return (end - start) + 1;
        }

        // ---------------------------------------------------------
        //  HELPERS
        // ---------------------------------------------------------
        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}