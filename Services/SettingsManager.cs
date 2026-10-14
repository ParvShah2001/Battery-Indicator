using System;
using System.IO;
using System.Text.Json;

namespace BatteryIndicator.Services;

public class AppSettings
{
    public int LowBatteryThreshold { get; set; } = 20;
    public bool LowBatteryAlertsEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool GlobalHotkeyEnabled { get; set; } = true;
    public uint HotkeyModifiers { get; set; } = 0x0008 | 0x0001; // Win + Alt (MOD_WIN | MOD_ALT)
    public uint HotkeyKey { get; set; } = 0x42; // 'B'
    public string HotkeyDisplayText { get; set; } = "Win + Alt + B";
    public Dictionary<string, string> DeviceAliases { get; set; } = new();
    public List<string> HiddenDeviceIds { get; set; } = new();
}

/// <summary>
/// Manages application configuration and user preferences (e.g. Low Battery Alert threshold).
/// </summary>
public static class SettingsManager
{
    private static readonly string LegacyAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryIndicator");
    private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryIndicatorForWindows");
    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");
    private static readonly string LegacySettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private static AppSettings _currentSettings = new();

    static SettingsManager()
    {
        try
        {
            if (!Directory.Exists(AppDataDir))
            {
                Directory.CreateDirectory(AppDataDir);
                string oldSettings = Path.Combine(LegacyAppDataDir, "settings.json");
                if (File.Exists(oldSettings) && !File.Exists(SettingsFile))
                {
                    File.Copy(oldSettings, SettingsFile, true);
                }
            }
        }
        catch { }
        Load();
    }

    public static int LowBatteryThreshold
    {
        get => _currentSettings.LowBatteryThreshold;
        set
        {
            _currentSettings.LowBatteryThreshold = Math.Clamp(value, 5, 50);
            Save();
        }
    }

    public static bool LowBatteryAlertsEnabled
    {
        get => _currentSettings.LowBatteryAlertsEnabled;
        set
        {
            _currentSettings.LowBatteryAlertsEnabled = value;
            Save();
        }
    }

    public static bool StartWithWindows
    {
        get => StartupManager.IsStartupEnabled();
        set
        {
            _currentSettings.StartWithWindows = value;
            StartupManager.SetStartup(value);
            Save();
        }
    }

    public static bool GlobalHotkeyEnabled
    {
        get => _currentSettings.GlobalHotkeyEnabled;
        set
        {
            _currentSettings.GlobalHotkeyEnabled = value;
            Save();
        }
    }

    public static uint HotkeyModifiers
    {
        get => _currentSettings.HotkeyModifiers;
        set
        {
            _currentSettings.HotkeyModifiers = value;
            Save();
        }
    }

    public static uint HotkeyKey
    {
        get => _currentSettings.HotkeyKey;
        set
        {
            _currentSettings.HotkeyKey = value;
            Save();
        }
    }

    public static string HotkeyDisplayText
    {
        get => string.IsNullOrWhiteSpace(_currentSettings.HotkeyDisplayText) ? "Win + Alt + B" : _currentSettings.HotkeyDisplayText;
        set
        {
            _currentSettings.HotkeyDisplayText = value;
            Save();
        }
    }

    public static string GetDeviceName(string deviceId, string defaultName)
    {
        if (_currentSettings.DeviceAliases.TryGetValue(deviceId, out var alias) && !string.IsNullOrWhiteSpace(alias))
        {
            return alias;
        }
        return defaultName;
    }

    public static void SetDeviceAlias(string deviceId, string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            _currentSettings.DeviceAliases.Remove(deviceId);
        }
        else
        {
            _currentSettings.DeviceAliases[deviceId] = alias.Trim();
        }
        Save();
    }

    public static bool IsDeviceHidden(string deviceId)
    {
        return _currentSettings.HiddenDeviceIds.Contains(deviceId);
    }

    public static void SetDeviceHidden(string deviceId, bool hide)
    {
        if (hide)
        {
            if (!_currentSettings.HiddenDeviceIds.Contains(deviceId))
                _currentSettings.HiddenDeviceIds.Add(deviceId);
        }
        else
        {
            _currentSettings.HiddenDeviceIds.Remove(deviceId);
        }
        Save();
    }

    public static void UnhideAllDevices()
    {
        _currentSettings.HiddenDeviceIds.Clear();
        Save();
    }

    public static void Load()
    {
        try
        {
            if (!Directory.Exists(AppDataDir)) Directory.CreateDirectory(AppDataDir);

            // Automatically migrate from legacy base directory if present
            if (!File.Exists(SettingsFile) && File.Exists(LegacySettingsFile))
            {
                try { File.Copy(LegacySettingsFile, SettingsFile, true); } catch { }
            }

            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                }
            }
            else
            {
                Save();
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
