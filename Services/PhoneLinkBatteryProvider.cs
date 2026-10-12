using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BatteryIndicator.Models;

namespace BatteryIndicator.Services;

/// <summary>
/// Telemetry provider for Android smartphones (Google Pixel, Samsung Galaxy, OnePlus, etc.)
/// connected via Windows Phone Link / Link to Windows.
/// Reads live battery status from the Windows 11 Start Menu companion and metadata cache.
/// </summary>
public class PhoneLinkBatteryProvider : IDeviceBatteryProvider
{
    public string ProviderName => "Phone Link (Pixel & Android)";

    private static readonly string CompanionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "Microsoft.YourPhone_8wekyb3d8bbwe", "LocalState", "StartMenu", "StartMenuCompanion.json");

    private static readonly string MetadataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "Microsoft.YourPhone_8wekyb3d8bbwe", "LocalCache", "DeviceMetadataStorage.json");

    private static BatteryDeviceInfo? _cachedPhone;

    public Task<List<BatteryDeviceInfo>> PollDevicesAsync()
    {
        var result = new List<BatteryDeviceInfo>();

        try
        {
            if (File.Exists(CompanionPath))
            {
                string json;
                using (var fs = new FileStream(CompanionPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                // Parse battery percentage
                int? batteryPct = null;
                var battMatch = Regex.Match(json, @"Battery\s+(\d+)%\s+remaining", RegexOptions.IgnoreCase);
                if (!battMatch.Success)
                {
                    battMatch = Regex.Match(json, @"""text""\s*:\s*""(\d+)%""", RegexOptions.IgnoreCase);
                }

                if (battMatch.Success && int.TryParse(battMatch.Groups[1].Value, out int bVal))
                {
                    batteryPct = Math.Clamp(bVal, 0, 100);
                }

                // Parse device name
                string devName = "Pixel 7";
                var nameMatch = Regex.Match(json, @"""speak""\s*:\s*""(.*?)\s+-\s+Connected", RegexOptions.IgnoreCase);
                if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
                {
                    devName = nameMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // Fallback to checking metadata
                    string metaName = TryGetDeviceNameFromMetadata();
                    if (!string.IsNullOrWhiteSpace(metaName)) devName = metaName;
                }

                bool isCharging = json.Contains("charging", StringComparison.OrdinalIgnoreCase);
                bool isConnected = json.Contains("Connected", StringComparison.OrdinalIgnoreCase);

                if (batteryPct.HasValue)
                {
                    var dev = new BatteryDeviceInfo
                    {
                        Id = $"PHONE_LINK_{Regex.Replace(devName, @"\s+", "_").ToUpperInvariant()}",
                        Name = devName,
                        Category = DeviceCategory.PhoneOrTablet,
                        ConnectionType = DeviceConnectionType.Bluetooth,
                        BatteryPercentage = batteryPct.Value,
                        IsCharging = isCharging,
                        IsConnected = isConnected
                    };
                    _cachedPhone = dev;
                    result.Add(dev);
                    return Task.FromResult(result);
                }
            }
        }
        catch { }

        // Fallback: If companion momentarily busy, return last cached state
        if (_cachedPhone != null)
        {
            result.Add(_cachedPhone);
        }

        return Task.FromResult(result);
    }

    private static string TryGetDeviceNameFromMetadata()
    {
        try
        {
            if (File.Exists(MetadataPath))
            {
                string json = File.ReadAllText(MetadataPath);
                var match = Regex.Match(json, @"""DisplayName""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value;

                var modelMatch = Regex.Match(json, @"""ModelName""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (modelMatch.Success) return modelMatch.Groups[1].Value;
            }
        }
        catch { }
        return "Pixel 7";
    }
}
