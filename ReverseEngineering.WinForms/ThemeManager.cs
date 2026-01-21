// Project: ReverseEngineering.WinForms
// File: ThemeManager.cs
using ReverseEngineering.Core.ProjectSystem;
using ReverseEngineering.WinForms.HexEditor;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Centralized, static theme manager.
    /// Reads/writes theme selection from application settings.
    /// Single source of truth for UI theming across entire application.
    /// </summary>
    public static class ThemeManager
    {
        private static AppTheme _currentTheme = Themes.Dark;

        /// <summary>
        /// Current active theme. Read from settings on startup.
        /// </summary>
        public static AppTheme CurrentTheme => _currentTheme;

        /// <summary>
        /// Internal setter for theme preview (used by SettingsDialog for live preview).
        /// Does NOT persist to disk.
        /// </summary>
        internal static AppTheme _CurrentTheme 
        { 
            get => _currentTheme;
            set => _currentTheme = value;
        }

        /// <summary>
        /// Initialize theme from saved settings on application startup.
        /// Should be called once in Program.cs or FormMain constructor.
        /// </summary>
        public static void Initialize()
        {
            var themeName = SettingsManager.Current.UI.Theme ?? "Dark";
            _currentTheme = GetThemeFromName(themeName);
        }

        /// <summary>
        /// Change theme and persist to settings.
        /// Use this whenever user changes theme in UI.
        /// </summary>
        public static void SetTheme(AppTheme theme, string? themeName = null)
        {
            _currentTheme = theme;

            // Determine theme name for storage
            themeName ??= GetThemeName(theme);

            // Persist to application settings
            SettingsManager.Current.UI.Theme = themeName;
            SettingsManager.SaveSettings();
        }

        /// <summary>
        /// Apply current theme to entire control tree.
        /// Call once on form load, then on theme changes.
        /// </summary>
        public static void ApplyTheme(Control root)
        {
            if (root == null)
                return;

            ApplyRecursive(root, _currentTheme);
        }

        /// <summary>
        /// Apply current theme to a single control and children.
        /// Useful for dynamically created controls.
        /// </summary>
        public static void Apply(Control control)
        {
            if (control == null)
                return;

            ApplyRecursive(control, _currentTheme);
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
                    tb.BackColor = theme.BackColor;
                    tb.ForeColor = theme.ForeColor;
                    break;

                case RichTextBox rtb:
                    rtb.BackColor = theme.BackColor;
                    rtb.ForeColor = theme.ForeColor;
                    break;

                case Label lbl:
                    lbl.BackColor = theme.BackColor;
                    lbl.ForeColor = theme.ForeColor;
                    break;

                case TabControl tc:
                    tc.BackColor = theme.PanelColor;
                    tc.ForeColor = theme.ForeColor;
                    foreach (TabPage page in tc.TabPages)
                    {
                        page.BackColor = theme.PanelColor;
                        page.ForeColor = theme.ForeColor;
                    }
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

        /// <summary>
        /// Convert theme object to name for storage.
        /// </summary>
        private static string GetThemeName(AppTheme theme)
        {
            if (theme == Themes.Dark) return "Dark";
            if (theme == Themes.Light) return "Light";
            if (theme == Themes.Midnight) return "Midnight";
            if (theme == Themes.HackerGreen) return "HackerGreen";
            return "Dark"; // Default fallback
        }

        /// <summary>
        /// Convert theme name to theme object.
        /// Used when loading from settings.
        /// </summary>
        private static AppTheme GetThemeFromName(string? themeName)
        {
            return themeName switch
            {
                "Dark" => Themes.Dark,
                "Light" => Themes.Light,
                "Midnight" => Themes.Midnight,
                "HackerGreen" => Themes.HackerGreen,
                _ => Themes.Dark
            };
        }
    }
}