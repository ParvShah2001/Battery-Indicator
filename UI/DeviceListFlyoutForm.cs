using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BatteryIndicator.Models;
using BatteryIndicator.Services;

namespace BatteryIndicator.UI;

/// <summary>
/// Modern Windows 11 Fluent UI Flyout Window.
/// Fully customized, ultra-crisp, native Windows 11 dark theme with:
///  - Header: "Battery Indicator" and "⟳ Refresh" button
///  - Device Cards: Tab-style battery gauge cards (2.4 GHz / Bluetooth)
///  - Scrollbar-free design: No horizontal scrolling, no ugly vertical Win32 scrollbars.
///  - Centered bottom arrow indicator: Appears dynamically at bottom center if more devices are below.
///  - Smooth MouseWheel & click-to-scroll support.
///  - Footer: Left = "Last updated - HH:mm tt", Right = "Devices - N"
/// </summary>
public class DeviceListFlyoutForm : Form
{
    private readonly Panel _viewportPanel;
    private readonly Panel _cardsInnerPanel;
    private readonly Panel _bottomArrowPanel;
    private readonly BottomArrowIndicator _bottomArrowBtn;
    private readonly Label _headerTitle;
    private readonly ModernPillButton _refreshBtn;
    private readonly Label _footerLeft;
    private readonly Label _footerRight;
    private readonly Action _onRefreshRequested;
    private readonly Action<BatteryDeviceInfo>? _onPinToggled;

    private int _scrollOffset = 0;
    private const int WM_MOUSEWHEEL = 0x020A;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private readonly Action? _onCardChanged;

    public DeviceListFlyoutForm(Action onRefreshRequested, Action<BatteryDeviceInfo>? onPinToggled = null, Action? onCardChanged = null)
    {
        _onRefreshRequested = onRefreshRequested;
        _onPinToggled = onPinToggled;
        _onCardChanged = onCardChanged;

        Text = "Battery Indicator for Windows";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(418, 400);
        BackColor = Color.FromArgb(24, 24, 24); // Windows 11 Dark Mica / Shell base
        ForeColor = Color.White;
        DoubleBuffered = true;

        ApplyWindows11DwmStyling();

        // Main layout container (locks width to 386px inside padding)
        var mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 10),
            BackColor = Color.FromArgb(24, 24, 24)
        };

        // 1. Header section (Dock: Top)
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        _refreshBtn = new ModernPillButton
        {
            Text = "Refresh",
            Size = new Size(76, 28),
            Location = new Point(310, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            CornerRadius = 7,
            NormalColor = Color.FromArgb(34, 34, 34),
            HoverColor = Color.FromArgb(48, 48, 48)
        };
        _refreshBtn.Click += (s, e) => _onRefreshRequested();

        _headerTitle = new Label
        {
            Text = "Battery Indicator for Windows",
            Font = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 240, 240),
            AutoSize = false,
            Location = new Point(0, 6),
            Size = new Size(300, 28),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        headerPanel.Resize += (s, e) =>
        {
            _refreshBtn.Location = new Point(headerPanel.ClientSize.Width - _refreshBtn.Width, 6);
            _headerTitle.Size = new Size(Math.Max(50, headerPanel.ClientSize.Width - _refreshBtn.Width - 10), 28);
        };

        headerPanel.Controls.Add(_headerTitle);
        headerPanel.Controls.Add(_refreshBtn);

        // 2. Footer section (Dock: Bottom)
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        _footerLeft = new Label
        {
            Font = new Font("Segoe UI Variable Small", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(0, 3),
            Size = new Size(200, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Last updated - " + DateTime.Now.ToString("hh:mm tt")
        };

        _footerRight = new Label
        {
            Font = new Font("Segoe UI Variable Small", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(236, 3),
            Size = new Size(150, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Devices - 0"
        };

        footerPanel.Resize += (s, e) =>
        {
            _footerRight.Location = new Point(footerPanel.ClientSize.Width - _footerRight.Width, 3);
            _footerLeft.Size = new Size(Math.Max(50, footerPanel.ClientSize.Width - _footerRight.Width - 10), 20);
        };

        footerPanel.Controls.Add(_footerLeft);
        footerPanel.Controls.Add(_footerRight);

        // 3. Bottom Arrow panel (Dock: Bottom, sits directly above the footer)
        _bottomArrowPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = Color.FromArgb(24, 24, 24),
            Visible = false
        };

        _bottomArrowBtn = new BottomArrowIndicator
        {
            Location = new Point((386 - 54) / 2, 2)
        };
        _bottomArrowPanel.Resize += (s, e) =>
        {
            _bottomArrowBtn.Location = new Point((_bottomArrowPanel.ClientSize.Width - _bottomArrowBtn.Width) / 2, 2);
        };
        _bottomArrowBtn.ArrowClicked += (s, e) => ScrollDelta(76); // Scrolls down by 1 card step
        _bottomArrowPanel.Controls.Add(_bottomArrowBtn);

        // 4. Viewport & Inner Cards Panel (Dock: Fill)
        // No AutoScroll to strictly prevent horizontal scrolling and native scrollbars
        _viewportPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 24, 24),
            AutoScroll = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _cardsInnerPanel = new Panel
        {
            Location = new Point(0, 0),
            Width = 386,
            Height = 0,
            BackColor = Color.FromArgb(24, 24, 24),
            AutoScroll = false
        };

        _viewportPanel.Controls.Add(_cardsInnerPanel);

        // MouseWheel event routing
        _viewportPanel.MouseWheel += (s, e) => ScrollDelta(e.Delta > 0 ? -60 : 60);
        _cardsInnerPanel.MouseWheel += (s, e) => ScrollDelta(e.Delta > 0 ? -60 : 60);
        mainContainer.MouseWheel += (s, e) => ScrollDelta(e.Delta > 0 ? -60 : 60);

        // Add docked controls in correct order
        mainContainer.Controls.Add(_viewportPanel);
        mainContainer.Controls.Add(_bottomArrowPanel);
        mainContainer.Controls.Add(footerPanel);
        mainContainer.Controls.Add(headerPanel);

        Controls.Add(mainContainer);

        Deactivate += (s, e) => Hide();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEWHEEL)
        {
            short delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            ScrollDelta(delta > 0 ? -60 : 60);
            return;
        }
        base.WndProc(ref m);
    }

    private void ScrollDelta(int delta)
    {
        int visibleHeight = _viewportPanel.ClientSize.Height;
        int totalHeight = _cardsInnerPanel.Height;
        int maxScroll = Math.Max(0, totalHeight - visibleHeight);

        int newOffset = Math.Clamp(_scrollOffset + delta, 0, maxScroll);
        if (newOffset != _scrollOffset)
        {
            _scrollOffset = newOffset;
            _cardsInnerPanel.Top = -_scrollOffset;
            UpdateArrowVisibility();
        }
    }

    private void UpdateArrowVisibility()
    {
        int visibleHeight = _viewportPanel.ClientSize.Height;
        int totalHeight = _cardsInnerPanel.Height;
        int maxScroll = Math.Max(0, totalHeight - visibleHeight);

        // Show arrow indicator only if more devices exist below the visible boundary
        bool hasMoreBelow = maxScroll > 0 && (_scrollOffset < maxScroll - 6);
        _bottomArrowPanel.Visible = hasMoreBelow;
    }

    private void ApplyWindows11DwmStyling()
    {
        try
        {
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        }
        catch { }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(48, 48, 48), 1.2f);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    public void UpdateDevices(List<BatteryDeviceInfo> devices)
    {
        _cardsInnerPanel.SuspendLayout();
        _cardsInnerPanel.Controls.Clear();

        int contentWidth = _viewportPanel.ClientSize.Width > 0 ? _viewportPanel.ClientSize.Width : 386;
        _cardsInnerPanel.Width = contentWidth;

        // Filter out user-hidden devices
        var visibleDevices = devices.Where(d => !SettingsManager.IsDeviceHidden(d.Id)).ToList();

        if (visibleDevices.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = devices.Count > 0 ? "All devices are currently hidden" : "No devices currently connected",
                Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(130, 130, 130),
                AutoSize = false,
                Size = new Size(contentWidth, 80),
                Location = new Point(0, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _cardsInnerPanel.Controls.Add(emptyLabel);
            _cardsInnerPanel.Height = 120;
        }
        else
        {
            int y = 0;
            foreach (var dev in visibleDevices)
            {
                var card = new ModernBatteryDeviceCard(dev, onPinToggled: _onPinToggled, onCardChanged: _onCardChanged)
                {
                    Location = new Point(0, y),
                    Width = contentWidth
                };
                card.MouseWheel += (s, e) => ScrollDelta(e.Delta > 0 ? -60 : 60);
                _cardsInnerPanel.Controls.Add(card);
                y += card.Height + 8; // Card height (68) + vertical spacing (8)
            }
            _cardsInnerPanel.Height = y;
        }

        _scrollOffset = 0;
        _cardsInnerPanel.Top = 0;
        _cardsInnerPanel.ResumeLayout(true);

        _footerLeft.Text = "Last updated - " + DateTime.Now.ToString("hh:mm tt");
        _footerRight.Text = $"Devices - {visibleDevices.Count}";

        UpdateArrowVisibility();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public void ShowNearTray(Point trayLocation)
    {
        var screen = Screen.FromPoint(trayLocation) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var workingArea = screen.WorkingArea;

        int x = Math.Max(workingArea.Left + 8, Math.Min(workingArea.Right - Width - 8, trayLocation.X - Width / 2));
        int y = Math.Max(workingArea.Top + 8, workingArea.Bottom - Height - 12);

        Location = new Point(x, y);
        TopMost = true;
        Show();
        BringToFront();
        Activate();
        SetForegroundWindow(Handle);
    }
}

/// <summary>
/// A sleek Windows 11 Fluent chevron indicator shown at bottom center
/// when more devices are available below the current viewport.
/// </summary>
public class BottomArrowIndicator : Control
{
    private bool _isHovered;
    public event EventHandler? ArrowClicked;

    public BottomArrowIndicator()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true
        );
        Size = new Size(54, 18);
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(24, 24, 24);
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

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ArrowClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(24, 24, 24));

        var rect = new RectangleF(1, 1, Width - 2, Height - 2);
        using var path = CreatePillPath(rect, 7f);

        Color bg = _isHovered ? Color.FromArgb(48, 48, 48) : Color.FromArgb(34, 34, 34);
        using var brush = new SolidBrush(bg);
        g.FillPath(brush, path);

        Color border = _isHovered ? Color.FromArgb(70, 70, 70) : Color.FromArgb(46, 46, 46);
        using var pen = new Pen(border, 1f);
        g.DrawPath(pen, path);

        // Draw crisp downward chevron
        float cx = Width / 2f;
        float cy = Height / 2f - 1f;
        PointF[] chevron = new PointF[]
        {
            new PointF(cx - 5f, cy - 2f),
            new PointF(cx, cy + 3f),
            new PointF(cx + 5f, cy - 2f)
        };
        using var chevPen = new Pen(_isHovered ? Color.White : Color.FromArgb(200, 200, 200), 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(chevPen, chevron);
    }

    private static GraphicsPath CreatePillPath(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
