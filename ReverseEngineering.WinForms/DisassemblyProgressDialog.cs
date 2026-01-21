#nullable enable

using System;
using System.Windows.Forms;

namespace ReverseEngineering.WinForms
{
    /// <summary>
    /// Dialog showing disassembly progress with cancel button
    /// </summary>
    public partial class DisassemblyProgressDialog : Form
    {
        private bool _cancelRequested = false;
        private AppTheme _theme;

        public bool CancelRequested => _cancelRequested;

        public DisassemblyProgressDialog(AppTheme? theme = null)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
            this.ControlBox = false;  // Disable close button
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            
            _theme = theme ?? ThemeManager.CurrentTheme;
            ApplyTheme(_theme);
        }

        private void ApplyTheme(AppTheme theme)
        {
            // Form colors
            this.BackColor = theme.PanelColor;
            this.ForeColor = theme.ForeColor;
            
            // Apply to all controls
            foreach (Control ctrl in this.Controls)
            {
                ctrl.BackColor = theme.BackColor;
                ctrl.ForeColor = theme.ForeColor;
                
                if (ctrl is Button btn)
                {
                    btn.BackColor = theme.ButtonBack;
                    btn.ForeColor = theme.ButtonFore;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = theme.ButtonBorder;
                    btn.FlatAppearance.BorderSize = 1;
                }
                else if (ctrl is ProgressBar pb)
                {
                    // ProgressBar is limited in theming, apply what we can
                    pb.BackColor = theme.ProgressBarBack;
                }
                else if (ctrl is Label lbl)
                {
                    lbl.BackColor = theme.PanelColor;
                    lbl.ForeColor = theme.ForeColor;
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Progress bar
            var progressBar = new ProgressBar
            {
                Name = "progressBar",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(360, 23),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            // Status label
            var lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "Disassembling binary...",
                Location = new System.Drawing.Point(12, 45),
                Size = new System.Drawing.Size(360, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            // Progress text (percentage)
            var lblProgress = new Label
            {
                Name = "lblProgress",
                Text = "0%",
                Location = new System.Drawing.Point(12, 75),
                Size = new System.Drawing.Size(360, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Regular)
            };

            // Cancel button
            var btnCancel = new Button
            {
                Name = "btnCancel",
                Text = "Cancel",
                Location = new System.Drawing.Point(297, 110),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += BtnCancel_Click;

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 145);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblStatus);
            this.Controls.Add(lblProgress);
            this.Controls.Add(btnCancel);
            this.Text = "Disassembling Binary";
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            _cancelRequested = true;
            this.DialogResult = DialogResult.Cancel;
        }

        /// <summary>
        /// Update progress bar and status text
        /// </summary>
        public void UpdateProgress(int processed, int total, string? status = null)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => UpdateProgress(processed, total, status));
                return;
            }

            var progressBar = this.Controls["progressBar"] as ProgressBar;
            var lblProgress = this.Controls["lblProgress"] as Label;
            var lblStatus = this.Controls["lblStatus"] as Label;

            if (progressBar != null)
            {
                // processed and total are already in 0-100 scale from LoadFile
                // If they're the raw instruction counts, we calculate percentage
                int percentage;
                if (total > 0)
                {
                    percentage = (int)((processed * 100L) / total);
                }
                else
                {
                    percentage = processed;  // Already a percentage
                }
                // Clamp percentage to valid range [0, 100] to prevent ArgumentOutOfRangeException
                progressBar.Value = Math.Clamp(percentage, 0, 100);
            }

            if (lblProgress != null)
            {
                // Display just the percentage if total == 100 (scaled values)
                if (total == 100)
                {
                    // Clamp displayed percentage to valid range
                    int displayPercentage = Math.Clamp(processed, 0, 100);
                    lblProgress.Text = $"{displayPercentage}%";
                }
                else if (total > 0)
                {
                    int percentage = (int)((processed * 100L) / total);
                    // Clamp displayed percentage to valid range
                    percentage = Math.Clamp(percentage, 0, 100);
                    // Clamp processed count to non-negative
                    int displayProcessed = Math.Max(processed, 0);
                    lblProgress.Text = $"{percentage}% ({displayProcessed:N0} / {total:N0} instructions)";
                }
            }

            if (lblStatus != null && !string.IsNullOrEmpty(status))
            {
                lblStatus.Text = status;
            }

            Application.DoEvents();  // Allow UI to update and detect cancel clicks
        }

        /// <summary>
        /// Mark as complete
        /// </summary>
        public void SetComplete()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => SetComplete());
                return;
            }

            var progressBar = this.Controls["progressBar"] as ProgressBar;
            var lblProgress = this.Controls["lblProgress"] as Label;

            if (progressBar != null)
                progressBar.Value = 100;

            if (lblProgress != null)
                lblProgress.Text = "100% - Complete";
        }
    }
}
