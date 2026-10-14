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

    // Cached fonts to avoid GC pressure and ensure ultra-smooth rendering
    private static readonly Font IconFont = new Font("Segoe UI Emoji", 14.5f, FontStyle.Regular);
    private static readonly Font TitleFont = new Font("Segoe UI Variable Display", 10.5f, FontStyle.Bold);
    private static readonly Font SubFont = new Font("Segoe UI Variable Small", 9f, FontStyle.Regular);
    private static readonly Font PctFont = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold);

    private static readonly Color DarkCanvasBg = Color.FromArgb(24, 24, 24);

    private readonly Action? _onCardChanged;

    public ModernBatteryDeviceCard(BatteryDeviceInfo device, Action<BatteryDeviceInfo>? onPinToggled = null, Action? onCardChanged = null)
    {
        _device = device;
        _onPinToggled = onPinToggled;
        _onCardChanged = onCardChanged;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true
        );

        // Check if device display name requires multi-line wrapping
        string displayName = SettingsManager.GetDeviceName(_device.Id, _device.Name);
        using var tempBmp = new Bitmap(1, 1);
        using var tempG = Graphics.FromImage(tempBmp);
        float availableWidth = 196f; // Roomy horizontal space for title
        var unconstrained = tempG.MeasureString(displayName, TitleFont);
        bool isMultiLine = unconstrained.Width > availableWidth;

        Height = isMultiLine ? 82 : 68;
        Width = 356;
        Margin = new Padding(0, 0, 0, 10);
        BackColor = DarkCanvasBg;
        Cursor = Cursors.Default;

        // Context Menu for Rename and Hide
        ContextMenuStrip = CreateCardContextMenu();
    }

    private ContextMenuStrip CreateCardContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ModernFluentMenuRenderer(),
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
            ShowImageMargin = true
        };

        menu.LocationChanged += (s, e) =>
        {
            if (menu.Visible && menu.Location.X <= 0 && menu.Location.Y <= 0)
            {
                Point cursor = Cursor.Position;
                var screen = Screen.FromPoint(cursor) ?? Screen.PrimaryScreen;
                if (screen != null)
                {
                    var work = screen.WorkingArea;
                    int x = Math.Clamp(cursor.X - menu.Width / 2, work.Left + 4, work.Right - menu.Width - 4);
                    int y = Math.Clamp(cursor.Y - menu.Height, work.Top + 4, work.Bottom - menu.Height - 4);
                    menu.Location = new Point(x, y);
                }
            }
        };

        var titleItem = new ToolStripMenuItem(SettingsManager.GetDeviceName(_device.Id, _device.Name))
        {
            Image = IconManager.GetCustomDeviceMenuIcon(_device, 18),
            Enabled = false,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new ToolStripSeparator());

        var renameItem = new ToolStripMenuItem("✏️ Rename Device...");
        renameItem.Click += (s, e) => PromptRenameDevice();
        menu.Items.Add(renameItem);

        var pinItem = new ToolStripMenuItem(_device.IsPinnedToTray ? "✕ Unpin from System Tray" : "📌 Pin to System Tray");
        pinItem.Click += (s, e) =>
        {
            _device.IsPinnedToTray = !_device.IsPinnedToTray;
            _onPinToggled?.Invoke(_device);
            Invalidate();
        };
        menu.Items.Add(pinItem);

        var hideItem = new ToolStripMenuItem("👁️ Hide This Device");
        hideItem.Click += (s, e) =>
        {
            SettingsManager.SetDeviceHidden(_device.Id, true);
            _onCardChanged?.Invoke();
        };
        menu.Items.Add(hideItem);

        return menu;
    }

    private void PromptRenameDevice()
    {
        string currentName = SettingsManager.GetDeviceName(_device.Id, _device.Name);
        using var form = new Form
        {
            Text = "Rename Device",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(340, 130),
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White
        };

        var label = new Label
        {
            Text = "Enter custom display name:",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };

        var txt = new TextBox
        {
            Text = currentName,
            Location = new Point(22, 42),
            Size = new Size(296, 26),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnOk = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(160, 84),
            Size = new Size(72, 30),
            BackColor = Color.FromArgb(0, 185, 140),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat
        };

        var btnReset = new Button
        {
            Text = "Reset",
            Location = new Point(80, 84),
            Size = new Size(72, 30),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnReset.Click += (s, e) => { txt.Text = _device.Name; };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(240, 84),
            Size = new Size(72, 30),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        form.Controls.AddRange(new Control[] { label, txt, btnReset, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsManager.SetDeviceAlias(_device.Id, txt.Text);
            _onCardChanged?.Invoke();
            Invalidate();
        }
    }

    private RectangleF PinButtonRect => new RectangleF(Width - 36f, (Height - 28f) / 2f, 28f, 28f);

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
        // Custom Image or Glyph Icon (Vertically centered)
        float iconY = (h - 32f) / 2f;
        var customIcon = IconManager.GetCustomDeviceImage(_device);
        if (customIcon != null)
        {
            var iconRect = new RectangleF(15f, iconY, 30f, 30f);
            IconManager.DrawCustomDeviceImage(g, _device, iconRect);
        }
        else
        {
            string iconText = _device.DefaultGlyph;
            var iconRect = new RectangleF(14f, iconY, 32f, 32f);
            var sfIcon = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(iconText, IconFont, Brushes.White, iconRect, sfIcon);
        }

        // Title: Multi-Line Wrapping for Long Device Names (No Marquee, Full 2 Lines)
        string displayName = SettingsManager.GetDeviceName(_device.Id, _device.Name);
        float maxTitleWidth = (w - 100f) - 54f; // ~200px available
        bool isMultiLine = Height > 72;

        var titleRect = isMultiLine
            ? new RectangleF(54f, 8f, maxTitleWidth, 44f)
            : new RectangleF(54f, 14f, maxTitleWidth, 24f);

        using (var titleBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
        using (var sfTitle = new StringFormat
        {
            Trimming = StringTrimming.EllipsisWord,
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        })
        {
            g.DrawString(displayName, TitleFont, titleBrush, titleRect, sfTitle);
        }

        // Subtitle: Always displays Connection Type (e.g. "Bluetooth", "2.4 GHz") + TWS Earbuds / Health Details
        string connType = _device.ConnectionDisplay;
        string subtitleText;
        if (_device.SubBatteries != null && _device.SubBatteries.Count > 0)
        {
            string subs = string.Join("   ", _device.SubBatteries.Select(s => $"{s.Label}: {s.Percentage}%{(s.IsCharging ? "⚡" : "")}"));
            subtitleText = $"{connType}  •  {subs}";
        }
        else if (_device.HealthPercentage.HasValue && _device.Category == DeviceCategory.Laptop)
        {
            subtitleText = $"{connType}  •  Health: {_device.HealthPercentage.Value}%";
        }
        else
        {
            subtitleText = connType;
        }

        var subRect = isMultiLine
            ? new RectangleF(54f, 54f, maxTitleWidth + 50f, 20f)
            : new RectangleF(54f, 38f, maxTitleWidth + 50f, 20f);

        using (var subBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
        using (var sfSub = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
        {
            g.DrawString(subtitleText, SubFont, subBrush, subRect, sfSub);
        }

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
        var pctRect = new RectangleF(w - 98f, 0f, 60f, h);
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
        base.Dispose(disposing);
    }
}
