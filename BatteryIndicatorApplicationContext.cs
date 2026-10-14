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

    private readonly GlobalHotkeyManager _hotkeyManager;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

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

        // 2. Initialize Flyout Form with onCardChanged callback
        _flyoutForm = new DeviceListFlyoutForm(
            onRefreshRequested: async () => await RefreshDevicesAsync(),
            onPinToggled: dev => ToggleDevicePin(dev),
            onCardChanged: () =>
            {
                _flyoutForm?.UpdateDevices(_currentDevices);
                BuildContextMenu();
            }
        );

        // 3. Initialize Global Hotkey (Win + Alt + B)
        _hotkeyManager = new GlobalHotkeyManager(() =>
        {
            if (_flyoutForm.Visible)
            {
                _flyoutForm.Hide();
            }
            else
            {
                _flyoutForm.ShowNearTray(Cursor.Position);
            }
        });
        if (SettingsManager.GlobalHotkeyEnabled)
        {
            _hotkeyManager.Register();
        }

        // 4. Initialize Context Menu with Windows 11 Dark Fluent Theme
        _contextMenu = new ContextMenuStrip
        {
            Renderer = new ModernFluentMenuRenderer(),
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
            ShowImageMargin = true
        };

        _contextMenu.Opening += (s, e) =>
        {
            if (_flyoutForm.IsHandleCreated)
            {
                SetForegroundWindow(_flyoutForm.Handle);
            }

            foreach (ToolStripItem item in _contextMenu.Items)
            {
                if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
                {
                    SafeConfigureSubmenu(mi);
                }
            }
        };

        _contextMenu.Opened += (s, e) =>
        {
            if (_contextMenu.Location.X <= 0 && _contextMenu.Location.Y <= 0)
            {
                Point cursor = Cursor.Position;
                var screen = Screen.FromPoint(cursor) ?? Screen.PrimaryScreen;
                if (screen != null)
                {
                    var work = screen.WorkingArea;
                    int x = Math.Clamp(cursor.X - _contextMenu.Width / 2, work.Left + 4, work.Right - _contextMenu.Width - 4);
                    int y = Math.Clamp(cursor.Y - _contextMenu.Height, work.Top + 4, work.Bottom - _contextMenu.Height - 4);
                    _contextMenu.Location = new Point(x, y);
                    if (_contextMenu.IsHandleCreated)
                    {
                        SetWindowPos(_contextMenu.Handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    }
                }
            }
        };

        _contextMenu.LocationChanged += (s, e) =>
        {
            if (_contextMenu.Visible && _contextMenu.Location.X <= 0 && _contextMenu.Location.Y <= 0)
            {
                Point cursor = Cursor.Position;
                var screen = Screen.FromPoint(cursor) ?? Screen.PrimaryScreen;
                if (screen != null)
                {
                    var work = screen.WorkingArea;
                    int x = Math.Clamp(cursor.X - _contextMenu.Width / 2, work.Left + 4, work.Right - _contextMenu.Width - 4);
                    int y = Math.Clamp(cursor.Y - _contextMenu.Height, work.Top + 4, work.Bottom - _contextMenu.Height - 4);
                    _contextMenu.Location = new Point(x, y);
                    if (_contextMenu.IsHandleCreated)
                    {
                        SetWindowPos(_contextMenu.Handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    }
                }
            }
        };

        BuildContextMenu();

        // 4. Initialize Master NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Text = "Battery Indicator for Windows: Initializing...",
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

        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = 20000
        };
        _pollTimer.Tick += async (s, e) => await RefreshDevicesAsync();
        _pollTimer.Start();

        // 5. Perform initial poll quietly in background
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

            var titleItem = new ToolStripMenuItem(dev.Name)
            {
                Image = IconManager.GetCustomDeviceMenuIcon(dev, 18),
                Enabled = false,
                Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
            };
            var statusItem = new ToolStripMenuItem($"{dev.BatteryDisplay}  •  {dev.ConnectionDisplay}") { Enabled = false };
            var unpinItem = new ToolStripMenuItem("✕ Unpin from System Tray");
            unpinItem.Click += (s, e) =>
            {
                dev.IsPinnedToTray = false;
                ToggleDevicePin(dev);
                _flyoutForm.UpdateDevices(_currentDevices);
            };
            var openItem = new ToolStripMenuItem("Show All Devices...");
            openItem.Click += (s, e) => _flyoutForm.ShowNearTray(Cursor.Position);

            var exitItem = new ToolStripMenuItem("Exit Battery Indicator for Windows");
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

        string displayName = SettingsManager.GetDeviceName(dev.Id, dev.Name);
        var oldIcon = icon.Icon;
        icon.Icon = TrayIconRenderer.CreateCircularDeviceTrayIcon(dev);
        oldIcon?.Dispose();

        string tip = $"{displayName}: {dev.BatteryDisplay} ({dev.ConnectionDisplay})";
        if (tip.Length >= 64) tip = tip.Substring(0, 60) + "...";
        icon.Text = tip;
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();

        // 1. Header (Compact app title)
        var titleItem = new ToolStripMenuItem("Battery Indicator for Windows")
        {
            Image = IconManager.GetAppMenuIcon(16),
            Enabled = false,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 2. Primary Actions
        var openItem = new ToolStripMenuItem("Open Flyout")
        {
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        openItem.Click += (s, e) => _flyoutForm.ShowNearTray(Cursor.Position);
        _contextMenu.Items.Add(openItem);

        var refreshItem = new ToolStripMenuItem("Refresh Devices");
        refreshItem.Click += async (s, e) => await RefreshDevicesAsync();
        _contextMenu.Items.Add(refreshItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 3. Settings Submenu (Consolidates all configuration into one tidy place)
        var settingsMenu = new ToolStripMenuItem("⚙️ Settings");

        // 3a. Low Battery Alert threshold
        string alertHeader = SettingsManager.LowBatteryAlertsEnabled
            ? $"Low Battery Alert: <{SettingsManager.LowBatteryThreshold}%"
            : "Low Battery Alert: Off";
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
        var toggleAlertItem = new ToolStripMenuItem("Disable Alerts")
        {
            Checked = !SettingsManager.LowBatteryAlertsEnabled
        };
        toggleAlertItem.Click += (s, e) =>
        {
            SettingsManager.LowBatteryAlertsEnabled = !SettingsManager.LowBatteryAlertsEnabled;
            BuildContextMenu();
        };
        alertMenu.DropDownItems.Add(toggleAlertItem);
        settingsMenu.DropDownItems.Add(alertMenu);

        // 3b. Customizable Shortcut Keybind
        var hotkeyMenu = new ToolStripMenuItem($"Shortcut: {SettingsManager.HotkeyDisplayText}");
        var customizeHotkey = new ToolStripMenuItem("Change Shortcut Keybind...");
        customizeHotkey.Click += (s, e) => PromptCustomHotkey();
        var toggleHotKey = new ToolStripMenuItem("Enable Shortcut")
        {
            Checked = SettingsManager.GlobalHotkeyEnabled
        };
        toggleHotKey.Click += (s, e) =>
        {
            SettingsManager.GlobalHotkeyEnabled = !SettingsManager.GlobalHotkeyEnabled;
            if (SettingsManager.GlobalHotkeyEnabled) _hotkeyManager.Register();
            else _hotkeyManager.Unregister();
            BuildContextMenu();
        };
        hotkeyMenu.DropDownItems.Add(customizeHotkey);
        hotkeyMenu.DropDownItems.Add(toggleHotKey);
        settingsMenu.DropDownItems.Add(hotkeyMenu);

        // 3c. Unhide All Devices
        var unhideItem = new ToolStripMenuItem("Unhide All Hidden Devices");
        unhideItem.Click += (s, e) =>
        {
            SettingsManager.UnhideAllDevices();
            _flyoutForm.UpdateDevices(_currentDevices);
            BuildContextMenu();
        };
        settingsMenu.DropDownItems.Add(unhideItem);

        // 3d. Start with Windows
        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = SettingsManager.StartWithWindows
        };
        startupItem.Click += (s, e) =>
        {
            SettingsManager.StartWithWindows = !SettingsManager.StartWithWindows;
            BuildContextMenu();
        };
        settingsMenu.DropDownItems.Add(startupItem);

        settingsMenu.DropDownItems.Add(new ToolStripSeparator());

        // 3e. Open Device Profiles (Crowdsourced JSON plugin database)
        var profilesItem = new ToolStripMenuItem("📂 Custom Device Profiles Folder...");
        profilesItem.Click += (s, e) =>
        {
            try
            {
                if (!Directory.Exists(DeviceProfileManager.UserProfilesDir))
                {
                    Directory.CreateDirectory(DeviceProfileManager.UserProfilesDir);
                }
                Process.Start("explorer.exe", DeviceProfileManager.UserProfilesDir);
            }
            catch { }
        };
        settingsMenu.DropDownItems.Add(profilesItem);

        // 3f. Open Crash Reports Folder
        var crashLogItem = new ToolStripMenuItem("📋 Local Crash Reports Folder...");
        crashLogItem.Click += (s, e) =>
        {
            try
            {
                string crashDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryIndicatorForWindows", "CrashReports");
                if (!Directory.Exists(crashDir)) Directory.CreateDirectory(crashDir);
                Process.Start("explorer.exe", crashDir);
            }
            catch { }
        };
        settingsMenu.DropDownItems.Add(crashLogItem);

        _contextMenu.Items.Add(settingsMenu);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 4. Exit
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitThread();
        _contextMenu.Items.Add(exitItem);

        // Attach layout/position fix to all nested dropdown menus to prevent (0,0) top-left jump
        foreach (ToolStripItem item in _contextMenu.Items)
        {
            if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
            {
                SafeConfigureSubmenu(mi);
            }
        }
    }

    private static void SafeConfigureSubmenu(ToolStripMenuItem item)
    {
        var dropDown = item.DropDown;
        if (dropDown == null) return;

        if (dropDown.Tag as string == "ConfiguredSafeSubmenu") return;
        dropDown.Tag = "ConfiguredSafeSubmenu";

        // Pre-configure direction BEFORE WinForms computes DropDownLocation
        item.DropDownOpening += (s, e) =>
        {
            PreConfigureDropDownDirection(item);
            FixSubmenuPosition(item);
        };

        item.DropDownOpened += (s, e) =>
        {
            FixSubmenuPosition(item);
        };

        dropDown.Opening += (s, e) =>
        {
            PreConfigureDropDownDirection(item);
            FixSubmenuPosition(item);
        };

        dropDown.Opened += (s, e) =>
        {
            FixSubmenuPosition(item);
        };

        dropDown.VisibleChanged += (s, e) =>
        {
            if (dropDown.Visible)
            {
                FixSubmenuPosition(item);
            }
        };

        dropDown.LocationChanged += (s, e) =>
        {
            if (dropDown.Location.X <= 0 && dropDown.Location.Y <= 0)
            {
                FixSubmenuPosition(item);
            }
        };

        dropDown.Paint += (s, e) =>
        {
            if (dropDown.Location.X <= 0 && dropDown.Location.Y <= 0)
            {
                FixSubmenuPosition(item);
            }
        };

        foreach (ToolStripItem child in item.DropDownItems)
        {
            if (child is ToolStripMenuItem childItem && childItem.HasDropDownItems)
            {
                SafeConfigureSubmenu(childItem);
            }
        }
    }

    private static void PreConfigureDropDownDirection(ToolStripMenuItem item)
    {
        var parent = item.Owner;
        Point pt = (parent != null && parent.IsHandleCreated) ? parent.PointToScreen(item.Bounds.Location) : Cursor.Position;
        if (pt.X <= 0 && pt.Y <= 0) pt = Cursor.Position;

        var screen = Screen.FromPoint(pt) ?? Screen.PrimaryScreen;
        if (screen == null) return;

        int subWidth = Math.Max(item.DropDown?.PreferredSize.Width ?? 180, 180);
        if (pt.X + item.Bounds.Width + subWidth > screen.WorkingArea.Right - 8)
        {
            item.DropDownDirection = ToolStripDropDownDirection.Left;
        }
        else
        {
            item.DropDownDirection = ToolStripDropDownDirection.Right;
        }
    }

    private static void FixSubmenuPosition(ToolStripMenuItem item)
    {
        var parent = item.Owner;
        var dropDown = item.DropDown;
        if (parent == null || dropDown == null) return;

        try
        {
            Point itemScreenPt;
            if (parent.IsHandleCreated && parent.Visible)
            {
                itemScreenPt = parent.PointToScreen(new Point(item.Bounds.Left, item.Bounds.Top));
            }
            else
            {
                itemScreenPt = Cursor.Position;
            }

            if (itemScreenPt.X <= 0 && itemScreenPt.Y <= 0)
            {
                itemScreenPt = Cursor.Position;
            }

            var screen = Screen.FromPoint(itemScreenPt) ?? Screen.PrimaryScreen;
            if (screen == null) return;

            var workArea = screen.WorkingArea;
            int subWidth = Math.Max(dropDown.PreferredSize.Width, dropDown.Width);
            if (subWidth < 140) subWidth = 180;
            int subHeight = Math.Max(dropDown.PreferredSize.Height, dropDown.Height);
            if (subHeight < 40) subHeight = 120;

            // Submenu positioning: Taskbar tray is typically at the bottom right.
            // If opening to the right would overflow the right edge of the screen, open to the left.
            int targetX;
            if (itemScreenPt.X + item.Bounds.Width + subWidth > workArea.Right - 8)
            {
                item.DropDownDirection = ToolStripDropDownDirection.Left;
                targetX = itemScreenPt.X - subWidth + 2;
            }
            else
            {
                item.DropDownDirection = ToolStripDropDownDirection.Right;
                targetX = itemScreenPt.X + item.Bounds.Width - 2;
            }

            int targetY = itemScreenPt.Y;

            // Clamp coordinates strictly within the monitor's working area
            if (targetX < workArea.Left + 8) targetX = workArea.Left + 8;
            if (targetX + subWidth > workArea.Right - 8) targetX = workArea.Right - subWidth - 8;

            if (targetY + subHeight > workArea.Bottom - 8) targetY = workArea.Bottom - subHeight - 8;
            if (targetY < workArea.Top + 8) targetY = workArea.Top + 8;

            // Never allow (0,0) or negative
            if (targetX < workArea.Left) targetX = workArea.Left + 8;
            if (targetY < workArea.Top) targetY = workArea.Top + 8;

            dropDown.Location = new Point(targetX, targetY);

            if (dropDown.IsHandleCreated)
            {
                SetWindowPos(dropDown.Handle, IntPtr.Zero, targetX, targetY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }
        catch
        {
            // Fallback gracefully without throwing
        }
    }

    private void PromptCustomHotkey()
    {
        using var form = new Form
        {
            Text = "Customize Shortcut Keybind",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(340, 160),
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White
        };

        var lbl = new Label
        {
            Text = "Select your desired shortcut modifiers and key:",
            Location = new Point(20, 14),
            AutoSize = true,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };

        var chkWin = new CheckBox { Text = "Win", Location = new Point(24, 42), AutoSize = true, Checked = (SettingsManager.HotkeyModifiers & GlobalHotkeyManager.MOD_WIN) != 0 };
        var chkAlt = new CheckBox { Text = "Alt", Location = new Point(80, 42), AutoSize = true, Checked = (SettingsManager.HotkeyModifiers & GlobalHotkeyManager.MOD_ALT) != 0 };
        var chkCtrl = new CheckBox { Text = "Ctrl", Location = new Point(130, 42), AutoSize = true, Checked = (SettingsManager.HotkeyModifiers & GlobalHotkeyManager.MOD_CONTROL) != 0 };
        var chkShift = new CheckBox { Text = "Shift", Location = new Point(185, 42), AutoSize = true, Checked = (SettingsManager.HotkeyModifiers & GlobalHotkeyManager.MOD_SHIFT) != 0 };

        var lblPlus = new Label { Text = "+ Key:", Location = new Point(22, 74), AutoSize = true, Font = new Font("Segoe UI Variable Text", 9.5f) };
        var cmbKey = new ComboBox
        {
            Location = new Point(80, 72),
            Size = new Size(120, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White
        };

        string[] commonKeys = { "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space", "Tab" };
        foreach (var k in commonKeys) cmbKey.Items.Add(k);

        char curChar = (char)SettingsManager.HotkeyKey;
        string curKeyStr = curChar.ToString().ToUpper();
        if (cmbKey.Items.Contains(curKeyStr)) cmbKey.SelectedItem = curKeyStr;
        else cmbKey.SelectedIndex = 0; // Default 'B'

        var btnOk = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(160, 115),
            Size = new Size(76, 30),
            BackColor = Color.FromArgb(0, 185, 140),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(245, 115),
            Size = new Size(76, 30),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        form.Controls.AddRange(new Control[] { lbl, chkWin, chkAlt, chkCtrl, chkShift, lblPlus, cmbKey, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK)
        {
            uint mods = 0;
            var parts = new List<string>();
            if (chkWin.Checked) { mods |= GlobalHotkeyManager.MOD_WIN; parts.Add("Win"); }
            if (chkCtrl.Checked) { mods |= GlobalHotkeyManager.MOD_CONTROL; parts.Add("Ctrl"); }
            if (chkAlt.Checked) { mods |= GlobalHotkeyManager.MOD_ALT; parts.Add("Alt"); }
            if (chkShift.Checked) { mods |= GlobalHotkeyManager.MOD_SHIFT; parts.Add("Shift"); }

            if (mods == 0) mods = GlobalHotkeyManager.MOD_WIN | GlobalHotkeyManager.MOD_ALT; // At least one modifier

            string selected = cmbKey.SelectedItem?.ToString() ?? "B";
            parts.Add(selected);

            uint vkKey = (uint)selected[0];
            if (selected.StartsWith("F") && int.TryParse(selected.Substring(1), out int fNum))
            {
                vkKey = (uint)(0x70 + (fNum - 1)); // VK_F1 is 0x70
            }
            else if (selected == "Space") vkKey = 0x20;
            else if (selected == "Tab") vkKey = 0x09;

            SettingsManager.HotkeyModifiers = mods;
            SettingsManager.HotkeyKey = vkKey;
            SettingsManager.HotkeyDisplayText = string.Join(" + ", parts);

            if (SettingsManager.GlobalHotkeyEnabled)
            {
                _hotkeyManager.Register(mods, vkKey);
            }
            BuildContextMenu();
        }
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
            ? $"Battery Indicator for Windows: {primaryDevice.Name} ({primaryDevice.BatteryDisplay})"
            : $"Battery Indicator for Windows: {connectedCount} device{(connectedCount == 1 ? "" : "s")}";

        if (tooltip.Length >= 64) tooltip = tooltip.Substring(0, 60) + "...";
        _notifyIcon.Text = tooltip;
    }

    private void RefreshPinnedIcons()
    {
        foreach (var id in _pinnedDeviceIds.ToList())
        {
            var pinnedDev = _currentDevices.FirstOrDefault(d => d.Id == id);
            if (pinnedDev != null)
            {
                CreateOrUpdatePinnedTrayIcon(pinnedDev);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyManager.Dispose();
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
