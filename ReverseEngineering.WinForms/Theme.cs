// Project: ReverseEngineering.WinForms
// File: Theme.cs
using System.Drawing;

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Complete theme definition with syntax highlighting colors for disassembly
    /// </summary>
    public class AppTheme
    {
        // Main UI colors
        public Color BackColor { get; set; }
        public Color PanelColor { get; set; }
        public Color ForeColor { get; set; }

        // Button colors
        public Color ButtonBack { get; set; }
        public Color ButtonFore { get; set; }
        public Color ButtonBorder { get; set; }
        public Color ButtonHoverBack { get; set; }

        // Accent and UI elements
        public Color Accent { get; set; }
        public Color Separator { get; set; }
        
        // Disassembly syntax highlighting
        public Color SyntaxAddress { get; set; }      // 0x0000 addresses
        public Color SyntaxMnemonic { get; set; }     // mov, push, ret, etc.
        public Color SyntaxOperand { get; set; }      // register/memory operands
        public Color SyntaxImmediate { get; set; }    // hex immediates
        public Color SyntaxComment { get; set; }      // annotations
        public Color SyntaxSelectedBg { get; set; }   // selected line background
        
        // Progress dialog colors
        public Color ProgressBarBack { get; set; }
        public Color ProgressBarFill { get; set; }
        public Color DialogBorder { get; set; }
    }

    public static class Themes
    {
        /// <summary>
        /// Dark theme - professional, low contrast
        /// </summary>
        public static readonly AppTheme Dark = new AppTheme
        {
            BackColor = Color.FromArgb(20, 20, 20),
            PanelColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            
            ButtonBack = Color.FromArgb(50, 50, 50),
            ButtonFore = Color.FromArgb(220, 220, 220),
            ButtonBorder = Color.FromArgb(80, 80, 80),
            ButtonHoverBack = Color.FromArgb(70, 70, 70),
            
            Accent = Color.DeepSkyBlue,
            Separator = Color.FromArgb(60, 60, 60),
            
            SyntaxAddress = Color.FromArgb(100, 180, 255),      // Bright blue
            SyntaxMnemonic = Color.FromArgb(150, 220, 100),     // Lime green
            SyntaxOperand = Color.FromArgb(220, 150, 100),      // Warm orange
            SyntaxImmediate = Color.FromArgb(200, 200, 100),    // Yellow
            SyntaxComment = Color.FromArgb(100, 150, 100),      // Muted green
            SyntaxSelectedBg = Color.FromArgb(60, 90, 160),     // Deep blue
            
            ProgressBarBack = Color.FromArgb(40, 40, 40),
            ProgressBarFill = Color.FromArgb(100, 180, 255),
            DialogBorder = Color.FromArgb(80, 80, 80)
        };

        /// <summary>
        /// Light theme - bright, high contrast
        /// </summary>
        public static readonly AppTheme Light = new AppTheme
        {
            BackColor = Color.White,
            PanelColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(30, 30, 30),
            
            ButtonBack = Color.FromArgb(225, 225, 225),
            ButtonFore = Color.FromArgb(30, 30, 30),
            ButtonBorder = Color.FromArgb(150, 150, 150),
            ButtonHoverBack = Color.FromArgb(210, 210, 210),
            
            Accent = Color.RoyalBlue,
            Separator = Color.FromArgb(200, 200, 200),
            
            SyntaxAddress = Color.FromArgb(0, 100, 180),        // Dark blue
            SyntaxMnemonic = Color.FromArgb(0, 128, 0),         // Forest green
            SyntaxOperand = Color.FromArgb(180, 80, 0),         // Dark orange
            SyntaxImmediate = Color.FromArgb(150, 120, 0),      // Dark yellow
            SyntaxComment = Color.FromArgb(100, 150, 100),      // Muted green
            SyntaxSelectedBg = Color.FromArgb(200, 220, 255),   // Light blue
            
            ProgressBarBack = Color.FromArgb(220, 220, 220),
            ProgressBarFill = Color.FromArgb(30, 120, 180),
            DialogBorder = Color.FromArgb(150, 150, 150)
        };

        /// <summary>
        /// Midnight theme - deep blue, elegant
        /// </summary>
        public static readonly AppTheme Midnight = new AppTheme
        {
            BackColor = Color.FromArgb(10, 10, 30),
            PanelColor = Color.FromArgb(20, 20, 50),
            ForeColor = Color.FromArgb(180, 200, 255),
            
            ButtonBack = Color.FromArgb(30, 30, 70),
            ButtonFore = Color.FromArgb(200, 220, 255),
            ButtonBorder = Color.FromArgb(80, 80, 150),
            ButtonHoverBack = Color.FromArgb(50, 50, 100),
            
            Accent = Color.MediumPurple,
            Separator = Color.FromArgb(40, 40, 80),
            
            SyntaxAddress = Color.FromArgb(150, 200, 255),      // Light blue
            SyntaxMnemonic = Color.FromArgb(180, 255, 180),     // Light green
            SyntaxOperand = Color.FromArgb(255, 200, 120),      // Light orange
            SyntaxImmediate = Color.FromArgb(255, 255, 150),    // Light yellow
            SyntaxComment = Color.FromArgb(150, 200, 150),      // Light muted green
            SyntaxSelectedBg = Color.FromArgb(60, 60, 150),     // Purple-tinted blue
            
            ProgressBarBack = Color.FromArgb(30, 30, 60),
            ProgressBarFill = Color.FromArgb(150, 200, 255),
            DialogBorder = Color.FromArgb(80, 80, 150)
        };

        /// <summary>
        /// Hacker Green theme - classic terminal aesthetic (IMPROVED: darker, more readable)
        /// </summary>
        public static readonly AppTheme HackerGreen = new AppTheme
        {
            BackColor = Color.Black,
            PanelColor = Color.FromArgb(5, 20, 5),
            ForeColor = Color.FromArgb(100, 200, 100),           // Muted green (was bright Lime)
            
            ButtonBack = Color.FromArgb(10, 30, 10),
            ButtonFore = Color.FromArgb(120, 220, 120),
            ButtonBorder = Color.FromArgb(60, 120, 60),
            ButtonHoverBack = Color.FromArgb(20, 50, 20),
            
            Accent = Color.FromArgb(100, 200, 100),
            Separator = Color.FromArgb(0, 80, 0),
            
            SyntaxAddress = Color.FromArgb(100, 200, 100),      // Primary green
            SyntaxMnemonic = Color.FromArgb(150, 255, 150),     // Bright green
            SyntaxOperand = Color.FromArgb(80, 180, 80),        // Dark green
            SyntaxImmediate = Color.FromArgb(120, 220, 120),    // Medium green
            SyntaxComment = Color.FromArgb(60, 150, 60),        // Dark muted green
            SyntaxSelectedBg = Color.FromArgb(0, 60, 0),        // Very dark green
            
            ProgressBarBack = Color.FromArgb(10, 30, 10),
            ProgressBarFill = Color.FromArgb(100, 200, 100),
            DialogBorder = Color.FromArgb(60, 120, 60)
        };
    }
}