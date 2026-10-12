using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BatteryIndicator.Models;
using BatteryIndicator.Services;
using BatteryIndicator.UI;

namespace BatteryIndicator;

/// <summary>
/// Main Application Context running in the Windows system tray.
/// Manages tray icon rendering, tooltip updates, device polling loops, multi-device pinning,
/// customizable low battery notifications, and popup flyout.
/// </summary>
public class BatteryIndicatorApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly List<IDeviceBatteryProvider> _providers;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly DeviceListFlyoutForm _flyoutForm;
    private readonly Icon? _appIcon;

    private readonly Dictionary<string, NotifyIcon> _pinnedTrayIcons = new();
    private readonly HashSet<string> _pinnedDeviceIds = new();
    private readonly HashSet<string> _notifiedLowBatteryDeviceIds = new();
    private List<BatteryDeviceInfo> _currentDevices = new();
    private bool _isPolling;

    public BatteryIndicatorApplicationContext()
    {
        // 0. Load custom application icon (app.png or app.ico)
        _appIcon = IconManager.GetAppIcon();

        // 1. Initialize providers across all device categories
        _providers = new List<IDeviceBatteryProvider>
        {
            new HidDongleBatteryProvider(),
            new BluetoothBatteryProvider(),
            new PhoneLinkBatteryProvider(),
            new WindowsGamepadBatteryProvider()
        };

        // 2. Initialize Flyout Form
        _flyoutForm = new DeviceListFlyoutForm(
            onRefreshRequested: async () => await RefreshDevicesAsync(),
            onPinToggled: dev => ToggleDevicePin(dev)
        );

        // 3. Initialize Context Menu with Windows 11 Dark Fluent Theme
        _contextMenu = new ContextMenuStrip
        {
            Renderer = new ModernFluentMenuRenderer(),
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular),
            ShowImageMargin = false
        };
        BuildContextMenu();

        // 4. Initialize Master NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Text = "Battery Indicator: Initializing...",
            Icon = _appIcon ?? TrayIconRenderer.CreateBatteryIcon(null, false, false),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_flyoutForm.Visible)
                {
                    _flyoutForm.Hide();
                }
                else
                {
                    _flyoutForm.ShowNearTray(Cursor.Position);
                }
            }
        };

        // 5. Polling Timer (every 20 seconds)
        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = 20000
        };
        _pollTimer.Tick += async (s, e) => await RefreshDevicesAsync();
        _pollTimer.Start();

        // 6. Perform initial poll immediately
        _ = RefreshDevicesAsync();
    }

    public void ToggleDevicePin(BatteryDeviceInfo dev)
    {
        if (dev.IsPinnedToTray)
        {
            _pinnedDeviceIds.Add(dev.Id);
            CreateOrUpdatePinnedTrayIcon(dev);
        }
        else
        {
            _pinnedDeviceIds.Remove(dev.Id);
            if (_pinnedTrayIcons.TryGetValue(dev.Id, out var trayIcon))
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                _pinnedTrayIcons.Remove(dev.Id);
            }
        }

        BuildContextMenu();
    }

    private void CreateOrUpdatePinnedTrayIcon(BatteryDeviceInfo dev)
    {
        if (!_pinnedTrayIcons.TryGetValue(dev.Id, out var icon))
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new ModernFluentMenuRenderer(),
                Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular),
                ShowImageMargin = false
            };

            var titleItem = new ToolStripMenuItem($"{dev.DefaultGlyph} {dev.Name}") { Enabled = false };
            var statusItem = new ToolStripMenuItem($"Battery: {dev.BatteryDisplay} ({dev.ConnectionDisplay})") { Enabled = false };
            var unpinItem = new ToolStripMenuItem("✕ Unpin from System Tray");
            unpinItem.Click += (s, e) =>
            {
                dev.IsPinnedToTray = false;
                ToggleDevicePin(dev);
                _flyoutForm.UpdateDevices(_currentDevices);
            };
            var openItem = new ToolStripMenuItem("Show All Devices...");
            openItem.Click += (s, e) => _flyoutForm.ShowNearTray(Cursor.Position);

            var exitItem = new ToolStripMenuItem("Exit Battery Indicator");
            exitItem.Click += (s, e) => ExitThread();

            menu.Items.Add(titleItem);
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(unpinItem);
            menu.Items.Add(openItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            icon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Visible = true
            };

            icon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (_flyoutForm.Visible) _flyoutForm.Hide();
                    else _flyoutForm.ShowNearTray(Cursor.Position);
                }
            };

            _pinnedTrayIcons[dev.Id] = icon;
        }

        var oldIcon = icon.Icon;
        icon.Icon = TrayIconRenderer.CreateCircularDeviceTrayIcon(dev);
        oldIcon?.Dispose();

        string tip = $"{dev.Name}: {dev.BatteryDisplay} ({dev.ConnectionDisplay})";
        if (tip.Length >= 64) tip = tip.Substring(0, 60) + "...";
        icon.Text = tip;
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();

        var titleItem = new ToolStripMenuItem("🔋 Battery Indicator") { Enabled = false };
        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        if (_currentDevices.Count == 0)
        {
            _contextMenu.Items.Add(new ToolStripMenuItem("No active devices found") { Enabled = false });
        }
        else
        {
            foreach (var dev in _currentDevices)
            {
                string label = $"{dev.DefaultGlyph} {dev.Name}: {dev.BatteryDisplay}";
                var item = new ToolStripMenuItem(label)
                {
                    Checked = dev.IsPinnedToTray
                };
                item.Click += (s, e) =>
                {
                    dev.IsPinnedToTray = !dev.IsPinnedToTray;
                    ToggleDevicePin(dev);
                    _flyoutForm.UpdateDevices(_currentDevices);
                };
                _contextMenu.Items.Add(item);
            }
        }

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Low Battery Alert threshold submenu
        string alertHeader = SettingsManager.LowBatteryAlertsEnabled
            ? $"⚠️ Low Battery Alert (<{SettingsManager.LowBatteryThreshold}%)"
            : "⚠️ Low Battery Alert (Off)";
        var alertMenu = new ToolStripMenuItem(alertHeader);

        int[] thresholds = { 10, 15, 20, 25, 30 };
        foreach (int t in thresholds)
        {
            var item = new ToolStripMenuItem($"{t}%" + (t == 20 ? " (Default)" : ""))
            {
                Checked = SettingsManager.LowBatteryAlertsEnabled && SettingsManager.LowBatteryThreshold == t
            };
            int captured = t;
            item.Click += (s, e) =>
            {
                SettingsManager.LowBatteryAlertsEnabled = true;
                SettingsManager.LowBatteryThreshold = captured;
                BuildContextMenu();
            };
            alertMenu.DropDownItems.Add(item);
        }

        alertMenu.DropDownItems.Add(new ToolStripSeparator());

        var customItem = new ToolStripMenuItem("Custom Percentage...");
        customItem.Click += (s, e) =>
        {
            int? customVal = PromptCustomThreshold(SettingsManager.LowBatteryThreshold);
            if (customVal.HasValue)
            {
                SettingsManager.LowBatteryAlertsEnabled = true;
                SettingsManager.LowBatteryThreshold = customVal.Value;
                BuildContextMenu();
            }
        };
        alertMenu.DropDownItems.Add(customItem);

        var toggleItem = new ToolStripMenuItem("Disable Low Battery Alerts")
        {
            Checked = !SettingsManager.LowBatteryAlertsEnabled
        };
        toggleItem.Click += (s, e) =>
        {
            SettingsManager.LowBatteryAlertsEnabled = !SettingsManager.LowBatteryAlertsEnabled;
            BuildContextMenu();
        };
        alertMenu.DropDownItems.Add(toggleItem);

        _contextMenu.Items.Add(alertMenu);

        var refreshItem = new ToolStripMenuItem("Refresh");
        refreshItem.Click += async (s, e) => await RefreshDevicesAsync();
        _contextMenu.Items.Add(refreshItem);

        var showAllItem = new ToolStripMenuItem("Show All Devices...");
        showAllItem.Click += (s, e) => _flyoutForm.ShowNearTray(Cursor.Position);
        _contextMenu.Items.Add(showAllItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit Battery Indicator");
        exitItem.Click += (s, e) => ExitThread();
        _contextMenu.Items.Add(exitItem);
    }

    private static int? PromptCustomThreshold(int current)
    {
        using var form = new Form
        {
            Text = "Low Battery Alert",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(320, 130),
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White
        };

        var label = new Label
        {
            Text = "Notify when battery drops below (%):",
            Location = new Point(20, 18),
            AutoSize = true,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };

        var num = new NumericUpDown
        {
            Location = new Point(24, 48),
            Size = new Size(110, 26),
            Minimum = 5,
            Maximum = 50,
            Value = Math.Clamp(current, 5, 50),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 10f)
        };

        var btnOk = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(154, 46),
            Size = new Size(72, 30),
            BackColor = Color.FromArgb(0, 185, 140),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(234, 46),
            Size = new Size(72, 30),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        form.Controls.AddRange(new Control[] { label, num, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK)
        {
            return (int)num.Value;
        }
        return null;
    }

    private void CheckLowBatteryAlerts(List<BatteryDeviceInfo> devices)
    {
        if (!SettingsManager.LowBatteryAlertsEnabled) return;
        int threshold = SettingsManager.LowBatteryThreshold;

        foreach (var dev in devices)
        {
            if (dev.BatteryPercentage.HasValue && dev.IsConnected)
            {
                if (dev.BatteryPercentage.Value <= threshold && !dev.IsCharging)
                {
                    if (!_notifiedLowBatteryDeviceIds.Contains(dev.Id))
                    {
                        _notifiedLowBatteryDeviceIds.Add(dev.Id);
                        _notifyIcon.ShowBalloonTip(
                            5000,
                            "Low Battery Alert",
                            $"{dev.Name} battery has dropped to {dev.BatteryPercentage.Value}%!",
                            ToolTipIcon.Warning
                        );
                    }
                }
                else if (dev.BatteryPercentage.Value > threshold + 5 || dev.IsCharging)
                {
                    _notifiedLowBatteryDeviceIds.Remove(dev.Id);
                }
            }
        }
    }

    public async Task RefreshDevicesAsync()
    {
        if (_isPolling) return;
        _isPolling = true;

        try
        {
            var detected = new List<BatteryDeviceInfo>();
            foreach (var provider in _providers)
            {
                try
                {
                    var devices = await provider.PollDevicesAsync();
                    detected.AddRange(devices);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error polling provider {provider.ProviderName}: {ex.Message}");
                }
            }

            // Deduplicate devices across providers by normalized name and category
            var deduplicated = new List<BatteryDeviceInfo>();
            foreach (var dev in detected)
            {
                var existing = deduplicated.FirstOrDefault(d =>
                    string.Equals(d.Name.Trim(), dev.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (d.Category == DeviceCategory.PhoneOrTablet && dev.Category == DeviceCategory.PhoneOrTablet &&
                     (d.Name.Contains("Pixel", StringComparison.OrdinalIgnoreCase) && dev.Name.Contains("Pixel", StringComparison.OrdinalIgnoreCase))));

                if (existing == null)
                {
                    deduplicated.Add(dev);
                }
                else
                {
                    // If candidate has battery percentage and existing doesn't, candidate takes priority!
                    if (!existing.BatteryPercentage.HasValue && dev.BatteryPercentage.HasValue)
                    {
                        int idx = deduplicated.IndexOf(existing);
                        deduplicated[idx] = dev;
                    }
                }
            }

            _currentDevices = deduplicated;

            // Preserve pin states
            foreach (var dev in _currentDevices)
            {
                dev.IsPinnedToTray = _pinnedDeviceIds.Contains(dev.Id);
            }

            // Check low battery notification thresholds
            CheckLowBatteryAlerts(_currentDevices);

            // Update dedicated circular tray icons for pinned devices
            foreach (var id in _pinnedDeviceIds.ToList())
            {
                var pinnedDev = _currentDevices.FirstOrDefault(d => d.Id == id);
                if (pinnedDev != null)
                {
                    CreateOrUpdatePinnedTrayIcon(pinnedDev);
                }
            }

            UpdateTrayStatus();
            BuildContextMenu();
            _flyoutForm.UpdateDevices(_currentDevices);
        }
        finally
        {
            _isPolling = false;
        }
    }

    private void UpdateTrayStatus()
    {
        // The master system tray icon ALWAYS remains the official app logo icon
        _notifyIcon.Icon = _appIcon ?? TrayIconRenderer.CreateBatteryIcon(null, false, false);

        int connectedCount = _currentDevices.Count(d => d.IsConnected);
        var primaryDevice = _currentDevices
            .Where(d => d.BatteryPercentage.HasValue && d.IsConnected)
            .OrderBy(d => d.BatteryPercentage!.Value)
            .FirstOrDefault();

        string tooltip = primaryDevice != null
            ? $"Battery Indicator: {primaryDevice.Name} ({primaryDevice.BatteryDisplay})"
            : $"Battery Indicator: {connectedCount} device{(connectedCount == 1 ? "" : "s")}";

        if (tooltip.Length >= 64) tooltip = tooltip.Substring(0, 60) + "...";
        _notifyIcon.Text = tooltip;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _appIcon?.Dispose();

            foreach (var kvp in _pinnedTrayIcons)
            {
                kvp.Value.Visible = false;
                kvp.Value.Dispose();
            }
            _pinnedTrayIcons.Clear();

            _contextMenu.Dispose();
            _flyoutForm.Dispose();
        }
        base.Dispose(disposing);
    }
}
