using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryIndicator.Services;

/// <summary>
/// Manages Windows startup registration (HKCU\Software\Microsoft\Windows\CurrentVersion\Run)
/// allowing Battery Indicator to start automatically when the user logs in.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BatteryIndicator";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(ValueName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupManager] Error toggling startup: {ex.Message}");
        }
    }
}
