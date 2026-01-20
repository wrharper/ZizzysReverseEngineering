// Project: ReverseEngineering.WinForms
// File: HexEditorTheme.cs

using System.Drawing;

namespace ReverseEngineering.WinForms.HexEditor
{
    public static class HexEditorTheme
    {
        public static Color Background { get; private set; }
        public static Color Foreground { get; private set; }
        public static Color Separator { get; private set; }

        public static Color OffsetColor { get; private set; }
        public static Color AsciiColor { get; private set; }

        public static Color SelectionBack { get; private set; }
        public static Color SelectionFore { get; private set; }

        public static Color ModifiedBack { get; private set; }

        public static Brush? BgBrush { get; private set; }
        public static Brush? FgBrush { get; private set; }
        public static Brush? AsciiBrush { get; private set; }
        public static Brush? OffsetBrush { get; private set; }
        public static Brush? SelectionBackBrush { get; private set; }
        public static Brush? SelectionForeBrush { get; private set; }
        public static Brush? ModifiedBackBrush { get; private set; }

        public static Pen? SeparatorPen { get; private set; }

        public static void Apply(AppTheme theme)
        {
            Background = theme.BackColor;
            Foreground = theme.ForeColor;
            Separator = theme.Separator;

            OffsetColor = theme.ForeColor;
            AsciiColor = theme.ForeColor;

            SelectionBack = Color.FromArgb(80, theme.Accent.R, theme.Accent.G, theme.Accent.B);
            SelectionFore = Color.White;

            ModifiedBack = Color.FromArgb(100, theme.Accent.R, theme.Accent.G, theme.Accent.B);

            // Dispose old brushes/pens
            BgBrush?.Dispose();
            FgBrush?.Dispose();
            AsciiBrush?.Dispose();
            OffsetBrush?.Dispose();
            SelectionBackBrush?.Dispose();
            SelectionForeBrush?.Dispose();
            ModifiedBackBrush?.Dispose();
            SeparatorPen?.Dispose();

            // Create new brushes/pens
            BgBrush = new SolidBrush(Background);
            FgBrush = new SolidBrush(Foreground);
            AsciiBrush = new SolidBrush(AsciiColor);
            OffsetBrush = new SolidBrush(OffsetColor);
            SelectionBackBrush = new SolidBrush(SelectionBack);
            SelectionForeBrush = new SolidBrush(SelectionFore);
            ModifiedBackBrush = new SolidBrush(ModifiedBack);

            SeparatorPen = new Pen(Separator);
        }
    }
}