using System;
using System.Collections.Generic;
using System.Text;

namespace ReverseEngineering.Core
{
    public class HexBuffer(byte[] bytes, string filePath)
    {
        public const int BytesPerRow = 16;

        public byte[] Bytes { get; } = bytes ?? throw new ArgumentNullException(nameof(bytes));
        public byte[] OriginalBytes { get; } = (byte[])bytes.Clone();
        public bool[] Modified { get; } = new bool[bytes.Length];
        public string FilePath { get; } = filePath ?? string.Empty;

        public HexBuffer(byte[] bytes)
            : this(bytes, string.Empty)
        {
        }

        public byte this[int index] => Bytes[index];

        // ---------------------------------------------------------
        //  BYTE WRITING
        // ---------------------------------------------------------
        public void WriteByte(int offset, byte value)
        {
            if (offset < 0 || offset >= Bytes.Length)
                return;

            Bytes[offset] = value;
            Modified[offset] = true;
        }

        public void WriteBytes(int offset, byte[] values)
        {
            if (values == null || values.Length == 0)
                return;

            if (offset < 0 || offset + values.Length > Bytes.Length)
                return;

            Array.Copy(values, 0, Bytes, offset, values.Length);

            for (int i = 0; i < values.Length; i++)
                Modified[offset + i] = true;
        }

        // ---------------------------------------------------------
        //  PATCH EXTRACTION (UPGRADED)
        // ---------------------------------------------------------
        public IEnumerable<(int offset, byte original, byte value)> GetModifiedBytes()
        {
            for (int i = 0; i < Bytes.Length; i++)
            {
                if (Modified[i])
                {
                    byte oldB = OriginalBytes[i];
                    byte newB = Bytes[i];

                    if (oldB != newB)
                        yield return (i, oldB, newB);
                }
            }
        }

        // ---------------------------------------------------------
        //  ROW / COLUMN MATH
        // ---------------------------------------------------------
        public static int GetRow(int index) => index / BytesPerRow;
        public static int GetColumn(int index) => index % BytesPerRow;
        public static int GetOffset(int row, int col) => row * BytesPerRow + col;

        // ---------------------------------------------------------
        //  FORMATTING HELPERS
        // ---------------------------------------------------------
        public string GetHexString(int start, int end)
        {
            if (Bytes.Length == 0)
                return string.Empty;

            if (start < 0) start = 0;
            if (end < 0) return string.Empty;

            if (start >= Bytes.Length)
                return string.Empty;

            end = Math.Min(end, Bytes.Length - 1);

            var sb = new StringBuilder();
            for (int i = start; i <= end; i++)
            {
                sb.Append(Bytes[i].ToString("X2"));
                if (i < end)
                    sb.Append(' ');
            }

            return sb.ToString();
        }

        public string GetAsciiString(int start, int end)
        {
            if (Bytes.Length == 0)
                return string.Empty;

            if (start < 0) start = 0;
            if (end < 0) return string.Empty;

            if (start >= Bytes.Length)
                return string.Empty;

            end = Math.Min(end, Bytes.Length - 1);

            var sb = new StringBuilder();
            for (int i = start; i <= end; i++)
            {
                byte b = Bytes[i];
                sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
            }

            return sb.ToString();
        }

        public string GetFullLineString(int offset)
        {
            if (Bytes.Length == 0)
                return string.Empty;

            if (offset < 0 || offset >= Bytes.Length)
                return string.Empty;

            int row = GetRow(offset);
            int startIndex = row * BytesPerRow;
            int endIndex = Math.Min(startIndex + BytesPerRow - 1, Bytes.Length - 1);

            var sb = new StringBuilder();

            // Offset
            sb.Append(startIndex.ToString("X8"));
            sb.Append("  ");

            // Hex bytes
            for (int i = startIndex; i <= endIndex; i++)
            {
                sb.Append(Bytes[i].ToString("X2"));
                sb.Append(' ');
            }

            // Padding
            int missing = BytesPerRow - (endIndex - startIndex + 1);
            for (int m = 0; m < missing; m++)
                sb.Append("   ");

            // ASCII
            sb.Append(" |");
            for (int i = startIndex; i <= endIndex; i++)
            {
                byte b = Bytes[i];
                sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
            }
            sb.Append('|');

            return sb.ToString();
        }

        // ---------------------------------------------------------
        //  PERFORMANCE & OPTIMIZATION
        // ---------------------------------------------------------

        /// <summary>
        /// Get count of modified bytes (for diagnostics)
        /// </summary>
        public int GetModifiedCount()
        {
            int count = 0;
            for (int i = 0; i < Modified.Length; i++)
            {
                if (Modified[i])
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Check if a range has any modifications
        /// </summary>
        public bool HasModificationsInRange(int start, int end)
        {
            for (int i = Math.Max(0, start); i <= Math.Min(Modified.Length - 1, end); i++)
            {
                if (Modified[i])
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Reset modifications (for re-patching)
        /// </summary>
        public void ResetModifications()
        {
            Array.Clear(Modified, 0, Modified.Length);
        }

        /// <summary>
        /// Restore buffer to original state
        /// </summary>
        public void RevertToOriginal()
        {
            Array.Copy(OriginalBytes, 0, Bytes, 0, Bytes.Length);
            ResetModifications();
        }

        /// <summary>
        /// Get modified byte ranges (for incremental operations)
        /// </summary>
        public List<(int start, int end)> GetModifiedRanges()
        {
            var ranges = new List<(int start, int end)>();
            int? rangeStart = null;

            for (int i = 0; i < Modified.Length; i++)
            {
                if (Modified[i])
                {
                    if (!rangeStart.HasValue)
                        rangeStart = i;
                }
                else
                {
                    if (rangeStart.HasValue)
                    {
                        ranges.Add((rangeStart.Value, i - 1));
                        rangeStart = null;
                    }
                }
            }

            if (rangeStart.HasValue)
                ranges.Add((rangeStart.Value, Modified.Length - 1));

            return ranges;
        }

        /// <summary>
        /// Get size in MB for diagnostics
        /// </summary>
        public double GetSizeInMB() => Bytes.Length / (1024.0 * 1024.0);

        /// <summary>
        /// Get memory overhead (for optimization)
        /// </summary>
        public int GetMemoryOverheadBytes()
        {
            // Estimate: Modified array + object overhead
            return Modified.Length + 64;  // Rough estimate
        }
    }
}
