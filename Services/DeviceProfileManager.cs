using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BatteryIndicator.Models;
using HidSharp;

namespace BatteryIndicator.Services;

/// <summary>
/// Crowdsourced Device Profiles Manager.
/// Automatically discovers and merges built-in and user-contributed JSON device profiles.
/// Allows zero-code peripheral telemetry expansion for mechanical keyboards, mice, and dongles.
/// </summary>
public static class DeviceProfileManager
{
    private static readonly List<DeviceProfile> _profiles = new();
    private static readonly object _lock = new();

    public static string UserProfilesDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BatteryIndicator",
        "Profiles"
    );

    public static IReadOnlyList<DeviceProfile> Profiles
    {
        get
        {
            lock (_lock)
            {
                if (_profiles.Count == 0) LoadProfiles();
                return _profiles.AsReadOnly();
            }
        }
    }

    public static void ReloadProfiles()
    {
        lock (_lock)
        {
            _profiles.Clear();
            LoadProfiles();
        }
    }

    private static void LoadProfiles()
    {
        // 1. Built-in default JSON database
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string defaultPath = Path.Combine(appDir, "Resources", "device_profiles.json");
        if (!File.Exists(defaultPath))
        {
            defaultPath = Path.Combine(appDir, "device_profiles.json");
        }

        if (File.Exists(defaultPath))
        {
            LoadProfilesFromFile(defaultPath);
        }

        // 2. User & Community %APPDATA% directory
        try
        {
            if (!Directory.Exists(UserProfilesDir))
            {
                Directory.CreateDirectory(UserProfilesDir);
            }

            foreach (var jsonFile in Directory.GetFiles(UserProfilesDir, "*.json"))
            {
                LoadProfilesFromFile(jsonFile);
            }

            string userSingleFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BatteryIndicator",
                "device_profiles.json"
            );
            if (File.Exists(userSingleFile))
            {
                LoadProfilesFromFile(userSingleFile);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Profiles] Failed to load user profiles: {ex.Message}");
        }
    }

    private static void LoadProfilesFromFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            var items = JsonSerializer.Deserialize<List<DeviceProfile>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Id)) continue;
                    _profiles.RemoveAll(p => p.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
                    _profiles.Add(item);
                }
                Debug.WriteLine($"[Profiles] Loaded {_profiles.Count} profiles from {Path.GetFileName(filePath)}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Profiles] Error reading {filePath}: {ex.Message}");
        }
    }

    public static DeviceProfile? Match(int vid, int? pid, string? prodName, string? devicePath)
    {
        var list = Profiles;
        foreach (var p in list)
        {
            if (p.Vid.HasValue && p.Vid.Value != vid) continue;
            if (p.Pid.HasValue && pid.HasValue && p.Pid.Value != pid.Value) continue;

            if (!string.IsNullOrWhiteSpace(p.NameContains))
            {
                if (string.IsNullOrWhiteSpace(prodName) ||
                    !prodName.Contains(p.NameContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(p.DevicePathContains))
            {
                if (string.IsNullOrWhiteSpace(devicePath) ||
                    !devicePath.Contains(p.DevicePathContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            return p;
        }

        return null;
    }

    /// <summary>
    /// Queries battery percentage from a device matching a JSON profile using HID Feature/Input reports.
    /// </summary>
    public static BatteryDeviceInfo? ExecuteProfileQuery(DeviceProfile profile, List<HidDevice> candidateEndpoints)
    {
        foreach (var dev in candidateEndpoints)
        {
            try
            {
                if (!dev.TryOpen(out var stream)) continue;
                using (stream)
                {
                    stream.ReadTimeout = 150;
                    stream.WriteTimeout = 150;

                    byte[]? cmdBytes = ParseHex(profile.CommandHex);
                    byte[] buf = new byte[Math.Max(profile.ReportLength, 64)];

                    if (cmdBytes != null && cmdBytes.Length > 0)
                    {
                        if (profile.Protocol.Equals("HidOutputReport", StringComparison.OrdinalIgnoreCase))
                        {
                            stream.Write(cmdBytes, 0, cmdBytes.Length);
                        }
                        else
                        {
                            stream.SetFeature(cmdBytes);
                        }
                    }

                    if (profile.Protocol.Equals("HidInputReport", StringComparison.OrdinalIgnoreCase))
                    {
                        int read = stream.Read(buf, 0, buf.Length);
                        if (read <= 0) continue;
                    }
                    else
                    {
                        buf[0] = profile.ReportId;
                        stream.GetFeature(buf);
                    }

                    // Validate header response if specified
                    if (!string.IsNullOrWhiteSpace(profile.ResponseHeaderHex))
                    {
                        byte[] expectedHeader = ParseHex(profile.ResponseHeaderHex) ?? Array.Empty<byte>();
                        bool matches = true;
                        for (int i = 0; i < expectedHeader.Length && i < buf.Length; i++)
                        {
                            if (buf[i] != expectedHeader[i])
                            {
                                matches = false;
                                break;
                            }
                        }
                        if (!matches) continue;
                    }

                    // Extract battery raw value
                    if (profile.BatteryByteOffset >= 0 && profile.BatteryByteOffset < buf.Length)
                    {
                        int raw = buf[profile.BatteryByteOffset];
                        if (raw >= profile.MinRawValue && raw <= profile.MaxRawValue)
                        {
                            int pct = profile.MaxRawValue == profile.MinRawValue ? raw :
                                (int)Math.Round(((double)(raw - profile.MinRawValue) / (profile.MaxRawValue - profile.MinRawValue)) * 100.0);
                            pct = Math.Clamp(pct, 0, 100);

                            bool isCharging = false;
                            if (profile.ChargingByteOffset.HasValue && profile.ChargingByteOffset.Value < buf.Length)
                            {
                                int chgByte = buf[profile.ChargingByteOffset.Value];
                                int mask = profile.ChargingBitMask ?? 0xFF;
                                isCharging = (chgByte & mask) != 0;
                            }

                            string name = !string.IsNullOrWhiteSpace(profile.Name) ? profile.Name :
                                (dev.GetProductName() ?? profile.Vendor);

                            return new BatteryDeviceInfo
                            {
                                Id = $"PROFILE_{profile.Id}_{dev.VendorID:X4}_{dev.ProductID:X4}",
                                Name = name,
                                Category = profile.ParsedCategory,
                                ConnectionType = profile.ParsedConnectionType,
                                BatteryPercentage = pct,
                                IsCharging = isCharging,
                                IsConnected = true
                            };
                        }
                    }
                }
            }
            catch
            {
                // Try next endpoint
            }
        }

        return null;
    }

    private static byte[]? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        string clean = Regex.Replace(hex, @"[^0-9A-Fa-f]", "");
        if (clean.Length % 2 != 0) return null;

        byte[] bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
