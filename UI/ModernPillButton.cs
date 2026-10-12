using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BatteryIndicator.UI;

/// <summary>
/// Windows 11 Fluent styled pill button with razor-sharp ClearType typography
/// and smooth hover states with zero text jitter.
/// </summary>
public class ModernPillButton : Button
{
    public int CornerRadius { get; set; } = 8;
    public Color NormalColor { get; set; } = Color.FromArgb(36, 36, 36);
    public Color HoverColor { get; set; } = Color.FromArgb(50, 50, 50);
    public Color ActiveColor { get; set; } = Color.FromArgb(0, 185, 140);
    public bool IsActive { get; set; }

    private bool _isHovered;

    public ModernPillButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true
        );
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        ForeColor = Color.FromArgb(235, 235, 235);
        BackColor = Color.FromArgb(24, 24, 24);
        Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(24, 24, 24));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedPath(rect, CornerRadius);

        Color fill = IsActive ? ActiveColor : (_isHovered ? HoverColor : NormalColor);
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        // Crisp border outline
        Color border = IsActive ? Color.FromArgb(0, 220, 160) : (_isHovered ? Color.FromArgb(70, 70, 70) : Color.FromArgb(48, 48, 48));
        using (var pen = new Pen(border, 1f))
        {
            g.DrawPath(pen, path);
        }

        // Native Windows ClearType text rendering for zero-jitter, crystal-clear typography
        Color textColor = IsActive ? Color.Black : (_isHovered ? Color.White : Color.FromArgb(220, 220, 220));
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
        );
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
