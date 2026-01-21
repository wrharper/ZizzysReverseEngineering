using System;
using System.Drawing;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms.Utilities
{
    /// <summary>
    /// Utility class for creating responsive, percentage-based layouts in WinForms.
    /// This class provides helpers to make UI adapt to different window sizes and DPI scaling.
    /// 
    /// Usage Pattern:
    /// Instead of: new Point(150, y) with hardcoded positions
    /// Use: ResponsiveLayout.CalculateControlPosition(150, y, panelWidth, panelHeight)
    /// or use Anchoring/Docking with ResponsiveLayout helpers
    /// </summary>
    public static class ResponsiveLayout
    {
        // ---------------------------------------------------------
        //  COMMON LAYOUT CONSTANTS (in pixels, DPI-independent)
        // ---------------------------------------------------------
        
        /// <summary>Standard left margin for label column (pixels)</summary>
        public const int LabelMarginLeft = 10;
        
        /// <summary>Standard left position for control column (pixels)</summary>
        public const int ControlStartX = 150;
        
        /// <summary>Standard row height (pixels)</summary>
        public const int RowHeight = 30;
        
        /// <summary>Standard padding between elements (pixels)</summary>
        public const int StandardPadding = 15;
        
        /// <summary>Standard button width (pixels)</summary>
        public const int StandardButtonWidth = 120;

        // ---------------------------------------------------------
        //  ANCHORING HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// Configure a control to expand horizontally with its container.
        /// Use on TextBox, ComboBox, TrackBar, etc. in resizable panels.
        /// </summary>
        public static void SetResponsiveAnchor(Control control)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        }

        /// <summary>
        /// Configure a control to anchor left and top (default, non-expanding).
        /// Use on fixed-width controls like NumericUpDown, Button, CheckBox.
        /// </summary>
        public static void SetFixedAnchor(Control control)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        }

        /// <summary>
        /// Configure a control to fill available space (both horizontal and vertical).
        /// Use on main content panels.
        /// </summary>
        public static void SetFillAnchor(Control control)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        // ---------------------------------------------------------
        //  LABEL-CONTROL PAIR HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// Calculate the Y position for the next row after adding a control at the given Y.
        /// </summary>
        public static int NextRowY(int currentY) => currentY + RowHeight;

        /// <summary>
        /// Calculate the Y position for spacing (e.g., between sections).
        /// </summary>
        public static int SpacingY(int currentY, int spacing = 10) => currentY + spacing;

        // ---------------------------------------------------------
        //  PERCENTAGE-BASED POSITIONING
        // ---------------------------------------------------------

        /// <summary>
        /// Calculate a control's width based on a percentage of the container width.
        /// This is useful for making controls scale with their container.
        /// Example: ResponsiveLayout.CalculateWidthPercent(containerPanel.Width, 60) → 60% of panel width
        /// </summary>
        public static int CalculateWidthPercent(int containerWidth, double percentWidth)
        {
            return (int)(containerWidth * (percentWidth / 100.0));
        }

        /// <summary>
        /// Calculate a control's height based on a percentage of the container height.
        /// </summary>
        public static int CalculateHeightPercent(int containerHeight, double percentHeight)
        {
            return (int)(containerHeight * (percentHeight / 100.0));
        }

        /// <summary>
        /// Calculate X position as a percentage offset from left edge.
        /// Example: ResponsiveLayout.CalculateXPercent(panelWidth, 30) → 30% from left
        /// </summary>
        public static int CalculateXPercent(int containerWidth, double percentFromLeft)
        {
            return (int)(containerWidth * (percentFromLeft / 100.0));
        }

        /// <summary>
        /// Calculate Y position as a percentage offset from top edge.
        /// </summary>
        public static int CalculateYPercent(int containerHeight, double percentFromTop)
        {
            return (int)(containerHeight * (percentFromTop / 100.0));
        }

        // ---------------------------------------------------------
        //  BUTTON POSITIONING HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// Calculate button positions for a standard OK/Cancel button layout.
        /// Positions buttons from right to left with standard spacing.
        /// Returns tuple of (okButtonX, cancelButtonX, resetButtonX) positions.
        /// </summary>
        public static (int okX, int cancelX, int resetX) CalculateButtonPositions(int containerWidth)
        {
            int spacing = 10;
            int buttonWidth = 80;
            
            int resetX = spacing;
            int cancelX = containerWidth - spacing - buttonWidth;
            int okX = cancelX - spacing - buttonWidth;
            
            return (okX, cancelX, resetX);
        }

        // ---------------------------------------------------------
        //  SIZE CALCULATION HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// Calculate standard form size based on aspect ratio and minimum dimensions.
        /// For a typical settings dialog: 600x500 base, scales with screen.
        /// </summary>
        public static Size CalculateFormSize(Screen screen, double baseWidth = 600, double baseHeight = 500)
        {
            // Ensure form is at most 90% of screen size
            int maxWidth = (int)(screen.WorkingArea.Width * 0.9);
            int maxHeight = (int)(screen.WorkingArea.Height * 0.9);
            
            int width = Math.Min((int)baseWidth, maxWidth);
            int height = Math.Min((int)baseHeight, maxHeight);
            
            return new Size(width, height);
        }

        // ---------------------------------------------------------
        //  SCALING HELPERS (for DPI awareness)
        // ---------------------------------------------------------

        /// <summary>
        /// Scale a pixel value based on the current display DPI.
        /// WinForms normally handles this, but useful for custom calculations.
        /// </summary>
        public static int ScaleForDpi(int pixelValue, int currentDpi = 96)
        {
            // Standard DPI is 96; if different, scale proportionally
            return (int)(pixelValue * (currentDpi / 96.0));
        }

        // ---------------------------------------------------------
        //  LAYOUT DOCUMENTATION & BEST PRACTICES
        // ---------------------------------------------------------

        /// <summary>
        /// RESPONSIVE LAYOUT BEST PRACTICES FOR WINFORMS:
        /// 
        /// 1. USE DOCKING FOR CONTAINER PANELS:
        ///    - Main panel: Dock = DockStyle.Fill
        ///    - Button panel: Dock = DockStyle.Bottom, Height = 50
        ///    - This makes containers resize automatically
        /// 
        /// 2. USE ANCHORING FOR INDIVIDUAL CONTROLS:
        ///    - TextBox/ComboBox/TrackBar (expandable): SetResponsiveAnchor()
        ///    - NumericUpDown/Button/CheckBox (fixed): SetFixedAnchor()
        ///    - Major content panels: SetFillAnchor()
        /// 
        /// 3. USE RELATIVE POSITIONING WITH CONSTANTS:
        ///    - Labels at: LabelMarginLeft (10)
        ///    - Controls at: ControlStartX (150)
        ///    - Spacing: RowHeight (30) for vertical, StandardPadding (15) for sections
        /// 
        /// 4. AVOID HARDCODED PIXEL POSITIONS EXCEPT:
        ///    - Pre-calculated row/column positions using RowHeight constant
        ///    - Special layouts use percentage calculations
        /// 
        /// 5. FOR COMPLEX LAYOUTS, CONSIDER:
        ///    - TableLayoutPanel (recommended for forms with grid of controls)
        ///    - FlowLayoutPanel (recommended for sequential controls)
        ///    - GroupBox (for grouping related controls with borders)
        /// 
        /// 6. TEST LAYOUTS AT DIFFERENT SIZES:
        ///    - Minimize/maximize form to verify anchoring works
        ///    - Test with different DPI scaling (96, 120, 144 DPI)
        ///    - Verify no controls overlap or disappear
        /// 
        /// EXAMPLE - Converting hardcoded layout to responsive:
        /// 
        /// OLD (hardcoded):
        ///   _textBox = new TextBox { Location = new Point(150, 10), Width = 300 };
        ///   panel.Controls.Add(_textBox);
        /// 
        /// NEW (responsive):
        ///   _textBox = new TextBox 
        ///   { 
        ///       Location = new Point(ResponsiveLayout.ControlStartX, 10),
        ///       Width = panel.Width - ResponsiveLayout.ControlStartX - ResponsiveLayout.StandardPadding,
        ///       Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        ///   };
        ///   panel.Controls.Add(_textBox);
        /// 
        /// Or even better, use percentages:
        ///   _textBox.Width = ResponsiveLayout.CalculateWidthPercent(panel.Width, 70);
        /// </summary>
        public static string BestPracticesDocumentation => "See XML comments in this class";
    }
}
