using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using BatteryIndicator.Models;
using BatteryIndicator.Services;

namespace BatteryIndicator.UI;

/// <summary>
/// A sleek Windows 11 style card where the entire card itself functions as an elegant battery gauge tab.
/// Includes an interactive Plus (+) / Pin button to pin dedicated circular battery icons to the taskbar system tray.
/// </summary>
public class ModernBatteryDeviceCard : Control
{
    private readonly BatteryDeviceInfo _device;
    private readonly Action<BatteryDeviceInfo>? _onPinToggled;
    private bool _isHovered;
    private bool _isPinHovered;

    // Cached fonts to avoid GC pressure and ensure ultra-smooth rendering
    private static readonly Font IconFont = new Font("Segoe UI Emoji", 14.5f, FontStyle.Regular);
    private static readonly Font TitleFont = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold);
    private static readonly Font SubFont = new Font("Segoe UI Variable Small", 9f, FontStyle.Regular);
    private static readonly Font PctFont = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold);

    private static readonly Color DarkCanvasBg = Color.FromArgb(24, 24, 24);

    public ModernBatteryDeviceCard(BatteryDeviceInfo device, Action<BatteryDeviceInfo>? onPinToggled = null)
    {
        _device = device;
        _onPinToggled = onPinToggled;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true
        );
        Height = 68;
        Width = 356;
        Margin = new Padding(0, 0, 0, 10);
        BackColor = DarkCanvasBg;
        Cursor = Cursors.Default;
    }

    private RectangleF PinButtonRect => new RectangleF(Width - 38f, (Height - 28f) / 2f, 28f, 28f);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool insidePin = PinButtonRect.Contains(e.Location);
        if (insidePin != _isPinHovered)
        {
            _isPinHovered = insidePin;
            Cursor = insidePin ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
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
        _isPinHovered = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && PinButtonRect.Contains(e.Location))
        {
            _device.IsPinnedToTray = !_device.IsPinnedToTray;
            _onPinToggled?.Invoke(_device);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Fill background seamlessly with the dark flyout canvas color (NO WHITE CORNERS)
        g.Clear(DarkCanvasBg);

        float w = Width - 1.5f;
        float h = Height - 1.5f;
        float radius = 14f; // Smooth modern Windows 11 fillet
        var fullRect = new RectangleF(0.75f, 0.75f, w, h);
        using var cardPath = CreateRoundedPath(fullRect, radius);

        // 1. Windows 11 Dark Mode Card Base
        Color baseBg = _isHovered ? Color.FromArgb(42, 42, 42) : Color.FromArgb(32, 32, 32);
        using (var baseBrush = new SolidBrush(baseBg))
        {
            g.FillPath(baseBrush, cardPath);
        }

        // 2. Battery gauge fill tab inside the filleted card
        int percentage = Math.Clamp(_device.BatteryPercentage ?? 0, 0, 100);
        if (_device.BatteryPercentage.HasValue && percentage > 0)
        {
            // Clip to the filleted card path so the fill never bleeds outside the rounded corners
            var oldClip = g.Clip;
            g.SetClip(cardPath, CombineMode.Intersect);

            float fillWidth = (w * percentage) / 100.0f;
            if (fillWidth > 0)
            {
                var fillRect = new RectangleF(0.75f, 0.75f, fillWidth, h);

                Color energyColor;
                if (percentage <= 15)
                {
                    energyColor = Color.FromArgb(_isHovered ? 60 : 45, 235, 77, 75); // Red
                }
                else if (percentage <= 30)
                {
                    energyColor = Color.FromArgb(_isHovered ? 60 : 45, 240, 147, 43); // Amber
                }
                else
                {
                    energyColor = Color.FromArgb(_isHovered ? 60 : 45, 0, 185, 140); // Cyan/Mint
                }

                using var fillBrush = new SolidBrush(energyColor);
                g.FillRectangle(fillBrush, fillRect);

                // Subtle vertical energy marker line
                if (fillWidth < w - 2f)
                {
                    Color edgeColor = percentage <= 15 ? Color.FromArgb(235, 77, 75)
                        : percentage <= 30 ? Color.FromArgb(240, 147, 43)
                        : Color.FromArgb(0, 215, 165);

                    using var edgePen = new Pen(edgeColor, 2f);
                    g.DrawLine(edgePen, fillWidth, 4f, fillWidth, h - 4f);
                }
            }

            g.Clip = oldClip;
        }

        // 3. Filleted Card Border Outline
        Color borderColor = _isHovered ? Color.FromArgb(75, 75, 75) : Color.FromArgb(48, 48, 48);
        using (var borderPen = new Pen(borderColor, 1.2f))
        {
            g.DrawPath(borderPen, cardPath);
        }

        // 4. Content Typography & Icons
        // Custom Image or Glyph Icon
        var customIcon = IconManager.GetCustomDeviceImage(_device);
        if (customIcon != null)
        {
            var iconRect = new RectangleF(15f, 18f, 30f, 30f);
            IconManager.DrawCustomDeviceImage(g, _device, iconRect);
        }
        else
        {
            string iconText = _device.DefaultGlyph;
            var iconRect = new RectangleF(14f, 16f, 32f, 34f);
            var sfIcon = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(iconText, IconFont, Brushes.White, iconRect, sfIcon);
        }

        // Title: Device Name
        string displayName = _device.Name;
        using var titleBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
        var titleRect = new RectangleF(54f, 14f, 172f, 22f);
        var sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(displayName, TitleFont, titleBrush, titleRect, sfTitle);

        // Subtitle: Connection type (e.g. "2.4 GHz", "Bluetooth", "Xbox Wireless")
        string connectionText = _device.ConnectionDisplay;
        using var subBrush = new SolidBrush(Color.FromArgb(145, 145, 145));
        g.DrawString(connectionText, SubFont, subBrush, 54f, 37f);

        // Battery Percentage on right (before plus button)
        string pctText = _device.BatteryPercentage.HasValue
            ? $"{_device.BatteryPercentage.Value}%" + (_device.IsCharging ? " ⚡" : "")
            : "— %";

        Color pctColor = !_device.BatteryPercentage.HasValue
            ? Color.FromArgb(140, 140, 140)
            : percentage <= 15 ? Color.FromArgb(245, 100, 100)
            : percentage <= 30 ? Color.FromArgb(250, 175, 70)
            : Color.FromArgb(70, 225, 165);

        using var pctBrush = new SolidBrush(pctColor);
        var pctRect = new RectangleF(w - 116f, 0f, 74f, h);
        var sfPct = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        g.DrawString(pctText, PctFont, pctBrush, pctRect, sfPct);

        // 5. Plus / Pin Button (Toggle new system tray icon)
        var pinRect = PinButtonRect;
        using var pinPath = CreateRoundedPath(pinRect, 8f);

        bool isPinned = _device.IsPinnedToTray;
        Color pinBg = isPinned
            ? (_isPinHovered ? Color.FromArgb(0, 215, 165) : Color.FromArgb(0, 185, 140))
            : (_isPinHovered ? Color.FromArgb(55, 55, 55) : Color.FromArgb(38, 38, 38));

        using (var pinBrush = new SolidBrush(pinBg))
        {
            g.FillPath(pinBrush, pinPath);
        }

        Color pinBorder = isPinned ? Color.FromArgb(0, 240, 185) : (_isPinHovered ? Color.FromArgb(85, 85, 85) : Color.FromArgb(50, 50, 50));
        using (var pinPen = new Pen(pinBorder, 1f))
        {
            g.DrawPath(pinPen, pinPath);
        }

        string pinSymbol = isPinned ? "✓" : "+";
        Color pinTextColor = isPinned ? Color.FromArgb(18, 24, 20) : (_isPinHovered ? Color.White : Color.FromArgb(210, 210, 210));
        using var pinTextBrush = new SolidBrush(pinTextColor);
        using var pinFont = new Font("Segoe UI Variable Display", isPinned ? 10f : 12f, FontStyle.Bold);
        var sfPin = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(pinSymbol, pinFont, pinTextBrush, pinRect, sfPin);
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
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
