using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryIndicator.UI;

/// <summary>
/// Dark Fluent Windows 11 style ColorTable and Renderer for ContextMenuStrip.
/// </summary>
public class ModernFluentColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(48, 48, 48);
    public override Color ToolStripDropDownBackground => Color.FromArgb(28, 28, 28);
    public override Color MenuItemSelected => Color.FromArgb(44, 44, 44);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(44, 44, 44);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(44, 44, 44);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color ImageMarginGradientBegin => Color.FromArgb(28, 28, 28);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(28, 28, 28);
    public override Color ImageMarginGradientEnd => Color.FromArgb(28, 28, 28);
    public override Color SeparatorDark => Color.FromArgb(48, 48, 48);
    public override Color SeparatorLight => Color.Transparent;
    public override Color CheckBackground => Color.FromArgb(0, 185, 140);
    public override Color CheckSelectedBackground => Color.FromArgb(0, 215, 165);
}

public class ModernFluentMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernFluentMenuRenderer() : base(new ModernFluentColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.FromArgb(240, 245, 255) : Color.FromArgb(120, 130, 145);
        base.OnRenderItemText(e);
    }
}
