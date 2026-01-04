// Project: ReverseEngineering.WinForms
// File: MainWindow/ThemeMenuController.cs

using System;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms.MainWindow
{
    public class ThemeMenuController
    {
        private readonly Form _form;
        private readonly MenuStrip _menu;

        public ThemeMenuController(Form form, MenuStrip menu)
        {
            _form = form;
            _menu = menu;

            BuildThemeMenu();
        }

        private void BuildThemeMenu()
        {
            var theme = new ToolStripMenuItem("Theme");

            theme.DropDownItems.Add(new ToolStripMenuItem("Dark", null, (s, e) => ThemeManager.ApplyTheme(_form, Themes.Dark)));
            theme.DropDownItems.Add(new ToolStripMenuItem("Light", null, (s, e) => ThemeManager.ApplyTheme(_form, Themes.Light)));
            theme.DropDownItems.Add(new ToolStripMenuItem("Midnight", null, (s, e) => ThemeManager.ApplyTheme(_form, Themes.Midnight)));
            theme.DropDownItems.Add(new ToolStripMenuItem("Hacker Green", null, (s, e) => ThemeManager.ApplyTheme(_form, Themes.HackerGreen)));

            _menu.Items.Add(theme);
        }
    }
}