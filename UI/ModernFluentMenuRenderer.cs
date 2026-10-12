using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BatteryIndicator.UI;

/// <summary>
/// Dark Fluent Windows 11 style ColorTable for ContextMenuStrip.
/// </summary>
public class ModernFluentColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(58, 58, 58);
    public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);
    public override Color MenuItemSelected => Color.FromArgb(48, 50, 56);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(48, 50, 56);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(48, 50, 56);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
    public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);
    public override Color SeparatorDark => Color.FromArgb(48, 48, 48);
    public override Color SeparatorLight => Color.Transparent;
    public override Color CheckBackground => Color.FromArgb(0, 185, 140);
    public override Color CheckSelectedBackground => Color.FromArgb(0, 215, 165);
}

/// <summary>
/// Modern Windows 11 ContextMenuStrip Renderer with:
///  - Filleted pill-shaped hover highlights
///  - High-DPI ClearType text rendering
///  - Subtle dark acrylic borders and seamless icon margins
///  - Elegant accent checkmark glyphs
/// </summary>
public class ModernFluentMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernFluentMenuRenderer() : base(new ModernFluentColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(58, 58, 58), 1f);
        var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        g.DrawRectangle(pen, r);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Keep the margin completely seamless with the dark dropdown background
        var g = e.Graphics;
        using var brush = new SolidBrush(Color.FromArgb(30, 30, 30));
        g.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled && !(e.Item is ToolStripMenuItem mi && mi.Checked)) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (e.Item.Selected && e.Item.Enabled)
        {
            // Filleted capsule hover highlight (Windows 11 style)
            var pillRect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var path = CreatePillPath(pillRect, 5f);
            using var brush = new SolidBrush(Color.FromArgb(50, 54, 62));
            g.FillPath(brush, path);

            using var borderPen = new Pen(Color.FromArgb(70, 75, 85), 1f);
            g.DrawPath(borderPen, path);
        }
        else if (e.Item is ToolStripMenuItem item && item.Checked)
        {
            var pillRect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var path = CreatePillPath(pillRect, 5f);
            using var brush = new SolidBrush(Color.FromArgb(36, 42, 44));
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image == null) return;

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = e.ImageRectangle;
        g.DrawImage(e.Image, rect);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(e.ImageRectangle.X, e.ImageRectangle.Y + (e.ImageRectangle.Height - 16) / 2, 16, 16);
        using var path = CreatePillPath(rect, 4f);

        using (var brush = new SolidBrush(Color.FromArgb(0, 185, 140)))
        {
            g.FillPath(brush, path);
        }

        // Crisp white checkmark '✓'
        PointF[] check = new PointF[]
        {
            new PointF(rect.X + 3.5f, rect.Y + 8f),
            new PointF(rect.X + 6.5f, rect.Y + 11.5f),
            new PointF(rect.X + 12.5f, rect.Y + 4.5f)
        };
        using var pen = new Pen(Color.FromArgb(16, 20, 24), 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(pen, check);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color textColor;
        if (!e.Item.Enabled)
        {
            textColor = Color.FromArgb(130, 135, 145);
        }
        else if (e.Item.Selected)
        {
            textColor = Color.White;
        }
        else
        {
            textColor = Color.FromArgb(235, 235, 235);
        }

        e.TextColor = textColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        int y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(48, 48, 48), 1f);
        g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float x = e.ArrowRectangle.X + e.ArrowRectangle.Width / 2f - 2f;
        float y = e.ArrowRectangle.Y + e.ArrowRectangle.Height / 2f;

        PointF[] arrow = new PointF[]
        {
            new PointF(x, y - 4f),
            new PointF(x + 4f, y),
            new PointF(x, y + 4f)
        };

        bool isSelected = e.Item?.Selected ?? false;
        using var pen = new Pen(isSelected ? Color.White : Color.FromArgb(170, 175, 185), 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(pen, arrow);
    }

    private static GraphicsPath CreatePillPath(Rectangle rect, float radius)
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
