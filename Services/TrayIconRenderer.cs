using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using BatteryIndicator.Models;

namespace BatteryIndicator.Services;

/// <summary>
/// Futuristic Windows 11 style battery tray icon generator.
/// Features:
///  - Pill/capsule shaped sleek battery shell with rounded contour
///  - Glow/accent energy fills with gradient
///  - Cyan/Neon-Green futuristic charging bolt or clean Segoe UI percentage typography
/// </summary>
public static class TrayIconRenderer
{
    private const int Size = 32;

    public static Icon CreateBatteryIcon(int? percentage, bool isCharging, bool isConnected)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            if (!isConnected || !percentage.HasValue)
            {
                // Futuristic disconnected state: subtle translucent pill with cyan dash/dot
                DrawPillBatteryShell(g, Color.FromArgb(140, 150, 160));
                using var font = new Font("Segoe UI Variable Display", 11, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.FromArgb(200, 220, 240));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("—", font, brush, new RectangleF(1, 9, 25, 14), sf);
            }
            else
            {
                int val = Math.Clamp(percentage.Value, 0, 100);

                // Futuristic Windows 11 accent colors:
                // Healthy: Vibrant Cyber Mint / Emerald (#00F298 -> #00C853)
                // Warning: Cyber Amber / Orange (#FFA726 -> #FF9100)
                // Critical: Neon Coral / Red (#FF5252 -> #D50000)
                Color colorStart, colorEnd;
                if (val <= 15)
                {
                    colorStart = Color.FromArgb(255, 82, 82);
                    colorEnd = Color.FromArgb(229, 57, 53);
                }
                else if (val <= 30)
                {
                    colorStart = Color.FromArgb(255, 183, 77);
                    colorEnd = Color.FromArgb(255, 152, 0);
                }
                else
                {
                    colorStart = Color.FromArgb(0, 242, 180);
                    colorEnd = Color.FromArgb(0, 190, 120);
                }

                // 1. Draw rounded sleek battery shell
                DrawPillBatteryShell(g, Color.FromArgb(240, 245, 250));

                // 2. Battery fill inside rounded capsule
                // Inner bounds: X=3, Y=9, W=23, H=14
                int fillWidth = (int)Math.Round((23.0 * val) / 100.0);
                if (fillWidth > 2)
                {
                    var fillRect = new Rectangle(3, 9, fillWidth, 14);
                    using var brush = new LinearGradientBrush(
                        new Point(3, 9),
                        new Point(3 + fillWidth, 23),
                        colorStart,
                        colorEnd
                    );
                    using var path = CreateRoundedRectPath(fillRect, 2);
                    g.FillPath(brush, path);
                }

                // 3. Charging status or clean percentage digits
                if (isCharging)
                {
                    // Glowing cyber charging bolt
                    using var boltBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
                    using var boltGlowBrush = new SolidBrush(Color.FromArgb(255, 230, 0));
                    using var borderPen = new Pen(Color.FromArgb(20, 20, 25), 1.2f);

                    PointF[] bolt = new PointF[]
                    {
                        new PointF(16, 4),
                        new PointF(10, 16),
                        new PointF(15, 16),
                        new PointF(13, 27),
                        new PointF(22, 14),
                        new PointF(16, 14)
                    };
                    g.FillPolygon(boltGlowBrush, bolt);
                    g.DrawPolygon(borderPen, bolt);
                }
                else
                {
                    // Sharp modern typography with drop shadow
                    string text = val.ToString();
                    float fontSize = val >= 100 ? 7.5f : 9.5f;
                    using var font = new Font("Segoe UI Variable Small", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var shadowBrush = new SolidBrush(Color.FromArgb(210, 10, 15, 20));
                    using var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    var rect = new RectangleF(2, 8, 25, 16);
                    var shadowRect = new RectangleF(rect.X + 0.8f, rect.Y + 0.8f, rect.Width, rect.Height);

                    g.DrawString(text, font, shadowBrush, shadowRect, sf);
                    g.DrawString(text, font, textBrush, rect, sf);
                }
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var cloned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return cloned;
    }

    /// <summary>
    /// Renders a dedicated circular system tray icon for a pinned device.
    /// Features:
    ///  - Outer circular progress ring showing exact battery percentage (color-coded)
    ///  - Dark circular glass backing
    ///  - In the middle: custom device icon or crisp vector glyph
    ///  - Charging indicator bolt
    /// </summary>
    public static Icon CreateCircularDeviceTrayIcon(BatteryDeviceInfo device)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            float cx = 15.5f;
            float cy = 15.5f;
            float diameter = 28f;
            var circleRect = new RectangleF(cx - diameter / 2f, cy - diameter / 2f, diameter, diameter);

            // 1. Dark circular backing disc
            using (var discBrush = new SolidBrush(Color.FromArgb(235, 18, 22, 28)))
            {
                g.FillEllipse(discBrush, circleRect);
            }

            // 2. Battery percentage color & sweep
            int val = Math.Clamp(device.BatteryPercentage ?? 0, 0, 100);
            Color ringColor;
            if (!device.BatteryPercentage.HasValue || !device.IsConnected)
            {
                ringColor = Color.FromArgb(120, 130, 140);
            }
            else if (val <= 15)
            {
                ringColor = Color.FromArgb(255, 82, 82);
            }
            else if (val <= 30)
            {
                ringColor = Color.FromArgb(255, 170, 40);
            }
            else
            {
                ringColor = Color.FromArgb(0, 240, 165);
            }

            // Outer ring track
            using (var trackPen = new Pen(Color.FromArgb(70, 60, 70, 85), 2.2f))
            {
                g.DrawEllipse(trackPen, circleRect);
            }

            // Active progress ring arc (clockwise starting from top: -90 degrees)
            if (device.BatteryPercentage.HasValue && val > 0)
            {
                float sweepAngle = (val / 100.0f) * 360f;
                using var progressPen = new Pen(ringColor, 2.5f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawArc(progressPen, circleRect, -90f, sweepAngle);
            }

            // 3. Center icon: Custom device image enlarged & clearly recognizable, or bold vector glyph
            var customImg = IconManager.GetCustomDeviceImage(device);
            if (customImg != null)
            {
                var imgRect = new RectangleF(cx - 9.5f, cy - 9.5f, 19f, 19f);
                IconManager.DrawCustomDeviceImage(g, device, imgRect);
            }
            else
            {
                string glyph = device.DefaultGlyph;
                using var glyphFont = new Font("Segoe UI Emoji", 11f, FontStyle.Regular);
                var glyphRect = new RectangleF(cx - 10f, cy - 10f, 20f, 20f);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, glyphFont, Brushes.White, glyphRect, sf);
            }

            // 4. Charging lightning bolt badge
            if (device.IsCharging)
            {
                using var boltBrush = new SolidBrush(Color.FromArgb(255, 235, 0));
                using var boltBorder = new Pen(Color.FromArgb(20, 20, 20), 1f);
                PointF[] bolt = new PointF[]
                {
                    new PointF(24, 2),
                    new PointF(19, 10),
                    new PointF(23, 10),
                    new PointF(21, 17),
                    new PointF(27, 8),
                    new PointF(23, 8)
                };
                g.FillPolygon(boltBrush, bolt);
                g.DrawPolygon(boltBorder, bolt);
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var cloned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return cloned;
    }

    private static void DrawPillBatteryShell(Graphics g, Color outlineColor)
    {
        // Dark translucent outer pill backing
        using var backBrush = new SolidBrush(Color.FromArgb(140, 15, 20, 30));
        using var backPath = CreateRoundedRectPath(new Rectangle(2, 8, 25, 16), 3);
        g.FillPath(backBrush, backPath);

        // Crisp border outline
        using var pen = new Pen(outlineColor, 1.6f);
        g.DrawPath(pen, backPath);

        // Terminal nipple (right side) with rounded pill cap
        using var capBrush = new SolidBrush(outlineColor);
        using var capPath = CreateRoundedRectPath(new Rectangle(27, 12, 3, 8), 1);
        g.FillPath(capBrush, capPath);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
