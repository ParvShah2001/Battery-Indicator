using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using BatteryIndicator.Models;

namespace BatteryIndicator.Services;

/// <summary>
/// Manages custom user-defined application and device icons.
/// Looks in `Resources\Icons\` and application base directory for custom PNG or ICO files.
/// Automatically falls back to built-in Fluent glyphs if no custom image is placed.
/// </summary>
public static class IconManager
{
    private static readonly ConcurrentDictionary<string, Image?> _imageCache = new();
    private static readonly string IconsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons");

    private static readonly ImageAttributes WhiteSilhouetteAttributes;

    static IconManager()
    {
        try
        {
            if (!Directory.Exists(IconsDirectory))
            {
                Directory.CreateDirectory(IconsDirectory);
            }
        }
        catch { }

        // Setup ColorMatrix that maps dark/black pixels to crisp bright white (255, 255, 255)
        // while preserving exact alpha channel transparency
        var cm = new System.Drawing.Imaging.ColorMatrix(new float[][]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 1f, 0 },
            new float[] { 1f, 1f, 1f, 0, 1f }
        });
        WhiteSilhouetteAttributes = new ImageAttributes();
        WhiteSilhouetteAttributes.SetColorMatrix(cm);
    }

    /// <summary>
    /// Returns the directory path where users should place custom device icons.
    /// </summary>
    public static string GetIconsDirectory() => IconsDirectory;

    /// <summary>
    /// Gets a custom image for the specified device if available.
    /// Checks device-specific filename first (e.g. "atk_vxe_r1.png"), then category filename (e.g. "mouse.png").
    /// </summary>
    public static Image? GetCustomDeviceImage(BatteryDeviceInfo device)
    {
        string sanitizedName = SanitizeFilename(device.Name);
        string categoryName = GetCategoryDefaultFilename(device.Category);

        // 1. Check specific device name
        if (_imageCache.TryGetValue(sanitizedName, out var cachedSpecific))
        {
            return cachedSpecific;
        }

        Image? specificImg = TryLoadImageFromDisk(sanitizedName);
        if (specificImg != null)
        {
            _imageCache[sanitizedName] = specificImg;
            return specificImg;
        }

        // 2. Check category name
        if (_imageCache.TryGetValue(categoryName, out var cachedCategory))
        {
            return cachedCategory;
        }

        Image? catImg = TryLoadImageFromDisk(categoryName);
        _imageCache[categoryName] = catImg;
        return catImg;
    }

    private static readonly ConcurrentDictionary<string, Rectangle> _contentBoundsCache = new();

    private static Rectangle GetContentBounds(Image img, string key)
    {
        return _contentBoundsCache.GetOrAdd(key, _ =>
        {
            if (img is Bitmap bmp)
            {
                int minX = bmp.Width, maxX = 0, minY = bmp.Height, maxY = 0;
                bool found = false;

                int stepX = Math.Max(1, bmp.Width / 100);
                int stepY = Math.Max(1, bmp.Height / 100);

                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    for (int x = 0; x < bmp.Width; x += stepX)
                    {
                        if (bmp.GetPixel(x, y).A > 20)
                        {
                            found = true;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (found && maxX >= minX && maxY >= minY)
                {
                    int padX = Math.Max(2, (maxX - minX) / 25);
                    int padY = Math.Max(2, (maxY - minY) / 25);
                    int x0 = Math.Max(0, minX - padX);
                    int y0 = Math.Max(0, minY - padY);
                    int w = Math.Min(bmp.Width - x0, (maxX - minX + 1) + padX * 2);
                    int h = Math.Min(bmp.Height - y0, (maxY - minY + 1) + padY * 2);
                    return new Rectangle(x0, y0, w, h);
                }
            }
            return new Rectangle(0, 0, img.Width, img.Height);
        });
    }

    /// <summary>
    /// Draws the custom device image as a crisp, bright white silhouette for Windows 11 dark theme
    /// and dark taskbar system tray discs. Automatically auto-crops transparent margins and scales
    /// preserving aspect ratio so the device artwork appears large, prominent, and easily identifiable.
    /// </summary>
    public static void DrawCustomDeviceImage(Graphics g, BatteryDeviceInfo device, RectangleF destRect)
    {
        var img = GetCustomDeviceImage(device);
        if (img == null) return;

        string key = SanitizeFilename(device.Name);
        var srcRect = GetContentBounds(img, key);

        float aspect = (float)srcRect.Width / Math.Max(1, srcRect.Height);
        float targetW = destRect.Width;
        float targetH = destRect.Height;

        if (aspect > 1f)
        {
            targetH = destRect.Width / aspect;
        }
        else
        {
            targetW = destRect.Height * aspect;
        }

        float drawX = destRect.X + (destRect.Width - targetW) / 2f;
        float drawY = destRect.Y + (destRect.Height - targetH) / 2f;
        var finalRect = new RectangleF(drawX, drawY, targetW, targetH);

        var oldInterpolation = g.InterpolationMode;
        var oldSmoothing = g.SmoothingMode;
        var oldPixelOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var dest = Rectangle.Round(finalRect);
        g.DrawImage(
            img,
            dest,
            srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height,
            GraphicsUnit.Pixel,
            WhiteSilhouetteAttributes
        );

        g.InterpolationMode = oldInterpolation;
        g.SmoothingMode = oldSmoothing;
        g.PixelOffsetMode = oldPixelOffset;
    }

    private static readonly ConcurrentDictionary<string, Image?> _menuIconCache = new();

    /// <summary>
    /// Gets a high-contrast menu icon of the app logo for context menus.
    /// </summary>
    public static Image? GetAppMenuIcon(int size = 18)
    {
        string cacheKey = $"APP_MENU_{size}";
        return _menuIconCache.GetOrAdd(cacheKey, _ =>
        {
            var logo = GetAppLogo();
            if (logo == null) return null;

            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(logo, new Rectangle(0, 0, size, size));
            }
            return bmp;
        });
    }

    /// <summary>
    /// Gets a crisp white silhouette icon of the custom device artwork for context menus.
    /// </summary>
    public static Image? GetCustomDeviceMenuIcon(BatteryDeviceInfo device, int size = 18)
    {
        string key = $"MENU_{SanitizeFilename(device.Name)}_{size}";
        return _menuIconCache.GetOrAdd(key, _ =>
        {
            var img = GetCustomDeviceImage(device);
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                if (img != null)
                {
                    DrawCustomDeviceImage(g, device, new RectangleF(0, 0, size, size));
                }
                else
                {
                    using var font = new Font("Segoe UI Emoji", 9.5f, FontStyle.Regular);
                    var rect = new RectangleF(0, 0, size, size);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(device.DefaultGlyph, font, Brushes.White, rect, sf);
                }
            }
            return bmp;
        });
    }

    /// <summary>
    /// Gets the custom application logo if available (e.g. app.png).
    /// </summary>
    public static Image? GetAppLogo()
    {
        return TryLoadImageFromDisk("app");
    }

    /// <summary>
    /// Gets the custom application icon for the system tray (app.ico or generated from app.png).
    /// </summary>
    public static Icon? GetAppIcon()
    {
        string[] searchPaths = new string[] {
            Path.Combine(IconsDirectory, "app.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico")
        };

        foreach (var p in searchPaths)
        {
            if (File.Exists(p))
            {
                try
                {
                    return new Icon(p, 32, 32);
                }
                catch { }
            }
        }

        var png = GetAppLogo();
        if (png != null)
        {
            try
            {
                using var bmp = new Bitmap(png, new Size(32, 32));
                IntPtr hIcon = bmp.GetHicon();
                var ico = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return ico;
            }
            catch { }
        }

        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// Returns the base filename for a device category (e.g. "mouse", "gamepad", etc.)
    /// </summary>
    public static string GetCategoryDefaultFilename(DeviceCategory category) => category switch
    {
        DeviceCategory.Mouse => "mouse",
        DeviceCategory.Keyboard => "keyboard",
        DeviceCategory.Headphones => "headphones",
        DeviceCategory.Earbuds => "earbuds",
        DeviceCategory.Speaker => "speaker",
        DeviceCategory.Gamepad => "gamepad",
        DeviceCategory.Stylus => "stylus",
        DeviceCategory.FlightStickOrWheel => "wheel",
        DeviceCategory.VrController => "vr",
        DeviceCategory.PhoneOrTablet => "phone",
        _ => "device"
    };

    private static Image? TryLoadImageFromDisk(string baseName)
    {
        string[] extensions = { ".png", ".ico", ".jpg", ".jpeg" };
        string[] searchDirs = { IconsDirectory, AppDomain.CurrentDomain.BaseDirectory };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var ext in extensions)
            {
                string path = Path.Combine(dir, baseName + ext);
                if (File.Exists(path))
                {
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        return Image.FromStream(stream);
                    }
                    catch { }
                }
            }
        }

        return null;
    }

    private static string SanitizeFilename(string name)
    {
        string clean = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(clean) ? "device" : clean;
    }
}
