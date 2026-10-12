using System;
using System.IO;
using System.Text.Json;

namespace BatteryIndicator.Services;

public class AppSettings
{
    public int LowBatteryThreshold { get; set; } = 20;
    public bool LowBatteryAlertsEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
}

/// <summary>
/// Manages application configuration and user preferences (e.g. Low Battery Alert threshold).
/// </summary>
public static class SettingsManager
{
    private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryIndicator");
    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");
    private static readonly string LegacySettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private static AppSettings _currentSettings = new();

    static SettingsManager()
    {
        try
        {
            if (!Directory.Exists(AppDataDir)) Directory.CreateDirectory(AppDataDir);
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
