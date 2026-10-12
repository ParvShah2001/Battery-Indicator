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
/// Features smooth marquee horizontal text scrolling for long device names when hovered.
/// </summary>
public class ModernBatteryDeviceCard : Control
{
    private readonly BatteryDeviceInfo _device;
    private readonly Action<BatteryDeviceInfo>? _onPinToggled;
    private bool _isHovered;
    private bool _isPinHovered;

    // Marquee scrolling for long device names
    private float _marqueeOffset = 0f;
    private float _maxScroll = 0f;
    private int _marqueePauseFrames = 0;
    private bool _marqueeForward = true;
    private readonly System.Windows.Forms.Timer _marqueeTimer;

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

        // Marquee animation timer (25 FPS smooth ticker)
        _marqueeTimer = new System.Windows.Forms.Timer { Interval = 40 };
        _marqueeTimer.Tick += OnMarqueeTick;
    }

    private void OnMarqueeTick(object? sender, EventArgs e)
    {
        if (!_isHovered || _maxScroll <= 0)
        {
            if (_marqueeOffset != 0)
            {
                _marqueeOffset = 0;
                Invalidate(new Rectangle(54, 12, 185, 26));
            }
            _marqueeTimer.Stop();
            return;
        }

        if (_marqueePauseFrames > 0)
        {
            _marqueePauseFrames--;
            return;
        }

        if (_marqueeForward)
        {
            _marqueeOffset += 1.2f;
            if (_marqueeOffset >= _maxScroll)
            {
                _marqueeOffset = _maxScroll;
                _marqueeForward = false;
                _marqueePauseFrames = 25; // Pause ~1 sec at the end of the text
            }
        }
        else
        {
            _marqueeOffset -= 1.8f;
            if (_marqueeOffset <= 0)
            {
                _marqueeOffset = 0;
                _marqueeForward = true;
                _marqueePauseFrames = 20; // Pause ~0.8 sec at start
            }
        }

        Invalidate(new Rectangle(54, 12, 185, 26));
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
        if (_maxScroll > 0)
        {
            _marqueePauseFrames = 12; // Brief 500ms pause before scrolling
            _marqueeTimer.Start();
        }
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPinHovered = false;
        _marqueeOffset = 0f;
        _marqueeForward = true;
        _marqueePauseFrames = 0;
        _marqueeTimer.Stop();
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
        float radius = 10f; // Windows 11 rounded fillet radius

        var cardRect = new RectangleF(0.5f, 0.5f, w, h);
        using var cardPath = CreateRoundedPath(cardRect, radius);

        // 1. Dark Acrylic Card Base Background
        Color baseBg = _isHovered ? Color.FromArgb(38, 38, 38) : Color.FromArgb(29, 29, 29);
        using (var bgBrush = new SolidBrush(baseBg))
        {
            g.FillPath(bgBrush, cardPath);
        }

        // 2. Battery Percentage Gauge Bar (The entire card itself behaves as the tab progress bar)
        if (_device.BatteryPercentage.HasValue && _device.IsConnected)
        {
            int percentage = Math.Clamp(_device.BatteryPercentage.Value, 0, 100);
            float fillWidth = (percentage / 100.0f) * w;
            var fillRect = new RectangleF(0.5f, 0.5f, fillWidth, h);

            var oldClip = g.Clip;
            g.SetClip(cardPath);

            if (percentage > 0)
            {
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
        // Custom Image or Glyph Icon (Auto-cropped and white silhouette rendered)
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

        // Title: Device Name with Smooth Marquee Scrolling for Long Names
        string displayName = _device.Name;
        float maxTitleWidth = (w - 116f) - 54f - 6f; // Available title horizontal space
        var titleRect = new RectangleF(54f, 14f, maxTitleWidth, 22f);

        var measuredSize = g.MeasureString(displayName, TitleFont);
        _maxScroll = Math.Max(0, measuredSize.Width - maxTitleWidth + 6f);

        var prevClip = g.Clip;
        g.SetClip(titleRect);

        using var titleBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
        float titleDrawX = 54f - (_maxScroll > 0 ? _marqueeOffset : 0f);
        g.DrawString(displayName, TitleFont, titleBrush, titleDrawX, 14f);

        g.Clip = prevClip;

        // Subtitle: Connection type (e.g. "2.4 GHz", "Bluetooth", "Xbox Wireless")
        string connectionText = _device.ConnectionDisplay;
        using var subBrush = new SolidBrush(Color.FromArgb(145, 145, 145));
        g.DrawString(connectionText, SubFont, subBrush, 54f, 37f);

        // Battery Percentage on right (before plus button)
        string pctText = _device.BatteryPercentage.HasValue
            ? $"{_device.BatteryPercentage.Value}%" + (_device.IsCharging ? " ⚡" : "")
            : "— %";

        int curPct = _device.BatteryPercentage ?? 0;
        Color pctColor = !_device.BatteryPercentage.HasValue
            ? Color.FromArgb(140, 140, 140)
            : curPct <= 15 ? Color.FromArgb(245, 100, 100)
            : curPct <= 30 ? Color.FromArgb(250, 175, 70)
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _marqueeTimer.Stop();
            _marqueeTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
