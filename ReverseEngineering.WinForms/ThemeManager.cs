// Project: ReverseEngineering.WinForms
// File: ThemeManager.cs
using ReverseEngineering.WinForms.HexEditor;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    public static class ThemeManager
    {
        public static AppTheme CurrentTheme { get; private set; }

        public static void ApplyTheme(Control root, AppTheme theme)
        {
            if (root == null || theme == null)
                return;

            CurrentTheme = theme;
            ApplyRecursive(root, theme);
        }

        // Call this for ad-hoc controls created in code (e.g., btnApply)
        public static void Apply(Control control)
        {
            if (control == null || CurrentTheme == null)
                return;

            ApplyRecursive(control, CurrentTheme);
        }

        // ⭐ This MUST be inside the class, not at namespace level
        private static void ApplyRecursive(Control c, AppTheme theme)
        {
            switch (c)
            {
                case Button b:
                    b.BackColor = theme.ButtonBack;
                    b.ForeColor = theme.ButtonFore;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = theme.ButtonBorder;
                    break;

                case TextBox tb:
                    tb.BackColor = theme.PanelColor;
                    tb.ForeColor = theme.ForeColor;
                    break;

                case RichTextBox rtb:
                    rtb.BackColor = theme.BackColor;
                    rtb.ForeColor = theme.ForeColor;
                    break;

                case Panel p:
                    p.BackColor = theme.PanelColor;
                    p.ForeColor = theme.ForeColor;

                    // Treat very thin panels as separators
                    if (p.Height <= 2 || p.Width <= 2)
                        p.BackColor = theme.Separator;
                    break;

                case SplitContainer sc:
                    sc.BackColor = theme.Separator;
                    sc.Panel1.BackColor = theme.PanelColor;
                    sc.Panel2.BackColor = theme.PanelColor;
                    break;

                case GroupBox gb:
                    gb.BackColor = theme.PanelColor;
                    gb.ForeColor = theme.ForeColor;
                    break;

                case HexEditorControl hex:
                    HexEditorTheme.Apply(theme);
                    hex.Invalidate();
                    break;

                default:
                    c.BackColor = theme.BackColor;
                    c.ForeColor = theme.ForeColor;
                    break;
            }

            foreach (Control child in c.Controls)
                ApplyRecursive(child, theme);
        }
    }
}