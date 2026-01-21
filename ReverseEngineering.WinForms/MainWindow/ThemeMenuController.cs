// Project: ReverseEngineering.WinForms
// File: MainWindow/ThemeMenuController.cs

using System;
using System.Windows.Forms;
using ReverseEngineering.WinForms.Settings;

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

            // Single menu item: opens Settings dialog to UI tab (index 2)
            theme.DropDownItems.Add(new ToolStripMenuItem("Settings...", null, (s, e) => 
            {
                using (var dialog = new SettingsDialog(2))  // No need to pass form - uses this.Owner automatically
                {
                    if (dialog.ShowDialog(_form) == DialogResult.OK)
                    {
                        // Theme changes were applied live, just ensure main form updates
                        ThemeManager.ApplyTheme(_form);
                        _form.Refresh();
                    }
                }
            }));

            _menu.Items.Add(theme);
        }

        private static void OnThemeSelected(Form form, AppTheme theme, string themeName)
        {
            // Set theme and persist to settings
            ThemeManager.SetTheme(theme, themeName);

            // Apply to form
            ThemeManager.ApplyTheme(form);
        }
    }
}