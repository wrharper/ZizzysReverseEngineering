// Project: ReverseEngineering.WinForms
// File: Theme.cs
using System.Drawing;

namespace ReverseEngineering.WinForms
{
    public class AppTheme
    {
        public Color BackColor { get; set; }
        public Color PanelColor { get; set; }
        public Color ForeColor { get; set; }

        public Color ButtonBack { get; set; }
        public Color ButtonFore { get; set; }
        public Color ButtonBorder { get; set; }

        public Color Accent { get; set; }
        public Color Separator { get; set; }
    }

    public static class Themes
    {
        public static readonly AppTheme Dark = new AppTheme
        {
            BackColor = Color.FromArgb(20, 20, 20),
            PanelColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            ButtonBack = Color.FromArgb(50, 50, 50),
            ButtonFore = Color.White,
            ButtonBorder = Color.Gray,
            Accent = Color.DeepSkyBlue,
            Separator = Color.FromArgb(60, 60, 60)
        };

        public static readonly AppTheme Light = new AppTheme
        {
            BackColor = Color.White,
            PanelColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.Black,
            ButtonBack = Color.FromArgb(225, 225, 225),
            ButtonFore = Color.Black,
            ButtonBorder = Color.DarkGray,
            Accent = Color.RoyalBlue,
            Separator = Color.FromArgb(200, 200, 200)
        };

        public static readonly AppTheme Midnight = new AppTheme
        {
            BackColor = Color.FromArgb(10, 10, 30),
            PanelColor = Color.FromArgb(20, 20, 50),
            ForeColor = Color.FromArgb(180, 200, 255),
            ButtonBack = Color.FromArgb(30, 30, 70),
            ButtonFore = Color.White,
            ButtonBorder = Color.FromArgb(80, 80, 150),
            Accent = Color.MediumPurple,
            Separator = Color.FromArgb(40, 40, 80)
        };

        public static readonly AppTheme HackerGreen = new AppTheme
        {
            BackColor = Color.Black,
            PanelColor = Color.FromArgb(10, 10, 10),
            ForeColor = Color.Lime,
            ButtonBack = Color.FromArgb(20, 20, 20),
            ButtonFore = Color.Lime,
            ButtonBorder = Color.Green,
            Accent = Color.Lime,
            Separator = Color.FromArgb(0, 80, 0)
        };
    }
}