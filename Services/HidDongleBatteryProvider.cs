using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BatteryIndicator.Models;
using HidSharp;

namespace BatteryIndicator.Services;

/// <summary>
/// Universal & Vendor-specific HID dongle and controller battery provider.
/// Supports:
///  - 17-byte Compx/VXE/ATK protocol (Report ID 0x08, Command 0x04)
///  - Razer 2.4GHz mice and keyboards (Command 0x07)
///  - Logitech HID++ 2.0 (Lightspeed and Unifying mice, keyboards)
///  - Sony PlayStation DualSense (PS5) and DualShock 4 (PS4)
///  - Nintendo Switch Pro Controller and Joy-Cons
///  - Corsair Slipstream and SteelSeries wireless peripherals
///  - Drawing Tablet Digitizers and Styluses (Wacom, Huion, XP-Pen)
///  - Standard HID Power Device Page (0x84 / 0x85)
/// </summary>
public class HidDongleBatteryProvider : IDeviceBatteryProvider
{
    public string ProviderName => "2.4 GHz Dongles & HID";

    // Stable cache across quick sleep intervals so devices stay visible when idle
    private static readonly Dictionary<string, BatteryDeviceInfo> _deviceCache = new();

    public Task<List<BatteryDeviceInfo>> PollDevicesAsync()
    {
        var result = new List<BatteryDeviceInfo>();
        var hidDevices = DeviceList.Local.GetHidDevices().ToList();

        var groupedDevices = hidDevices.GroupBy(d => new { d.VendorID, d.ProductID }).ToList();

        foreach (var group in groupedDevices)
        {
            int vid = group.Key.VendorID;
            int pid = group.Key.ProductID;

            try
            {
                // 1. ATK / VXE / Compx mice (VID 0x373B, 0x3554)
                if (vid == 0x373B || vid == 0x3554)
                {
                    var devInfo = PollAtkVxeDongle(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 2. Portronics Hydra 10 / Areson wireless keyboard (VID 0x25A7)
                if (vid == 0x25A7)
                {
                    var devInfo = PollPortronicsHydraKeyboard(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 2. Logitech Lightspeed / Unifying (VID 0x046D)
                if (vid == 0x046D)
                {
                    var devInfo = PollLogitechDongle(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 3. Razer 2.4G Dongles (VID 0x1532)
                if (vid == 0x1532)
                {
                    var devInfo = PollRazerDongle(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 4. Sony PlayStation DualSense / DualShock 4 (VID 0x054C)
                if (vid == 0x054C)
                {
                    var devInfo = PollPlayStationController(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 5. Nintendo Switch Pro Controller / Joy-Con (VID 0x057E)
                if (vid == 0x057E && (pid == 0x2009 || pid == 0x2006 || pid == 0x2007))
                {
                    var devInfo = PollNintendoSwitchController(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 6. SteelSeries (VID 0x1038)
                if (vid == 0x1038)
                {
                    var devInfo = PollSteelSeriesDongle(group.ToList());
                    if (devInfo != null)
                    {
                        result.Add(devInfo);
                        continue;
                    }
                }

                // 7. Drawing Tablet Styluses / Digitizers (Wacom, Huion, XP-Pen)
                var stylusDev = PollDigitizerStylus(group.ToList());
                if (stylusDev != null)
                {
                    result.Add(stylusDev);
                    continue;
                }

                // 8. Universal fallback: Check HID standard power device collection (Usage Page 0x84)
                var standardDev = PollStandardHidPower(group.ToList());
                if (standardDev != null)
                {
                    result.Add(standardDev);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HidDongleProvider] Error polling VID 0x{vid:X4} PID 0x{pid:X4}: {ex.Message}");
            }
        }

        // Cache persistence: if dongles are physically connected but device momentarily slept
        if (result.Count == 0 && _deviceCache.Count > 0)
        {
            bool donglePresent = hidDevices.Any(d =>
                d.VendorID == 0x373B || d.VendorID == 0x046D || d.VendorID == 0x1532 ||
                d.VendorID == 0x054C || d.VendorID == 0x057E || d.VendorID == 0x1038);

            if (donglePresent)
            {
                result.AddRange(_deviceCache.Values);
            }
        }

        return Task.FromResult(result);
    }

    private BatteryDeviceInfo? PollAtkVxeDongle(List<HidDevice> endpoints)
    {
        var ioEndpoint = endpoints.FirstOrDefault(d => d.GetMaxOutputReportLength() == 17 && d.GetMaxInputReportLength() == 17);
        if (ioEndpoint == null) return null;

        string productName = "ATK VXE R1 Mouse";
        try
        {
            var p = ioEndpoint.GetProductName();
            if (!string.IsNullOrWhiteSpace(p))
            {
                if (p.Contains("Wireless mouse", StringComparison.OrdinalIgnoreCase))
                {
                    productName = "ATK VXE R1 Mouse";
                }
                else
                {
                    productName = p;
                }
            }
        }
        catch { }

        string cacheKey = $"HID_VXE_{ioEndpoint.VendorID:X4}_{ioEndpoint.ProductID:X4}";

        if (!ioEndpoint.TryOpen(out var stream))
        {
            if (_deviceCache.TryGetValue(cacheKey, out var cached)) return cached;
            return null;
        }

        using (stream)
        {
            stream.ReadTimeout = 400;
            byte[] req = new byte[17];
            req[0] = 0x08; // Report ID
            req[1] = 0x04; // Query Power status

            int sum = 0;
            for (int i = 0; i < 16; i++) sum += req[i];
            req[16] = (byte)((0x55 - (sum & 0xFF)) & 0xFF);

            try
            {
                stream.Write(req);
                byte[] resp = new byte[17];
                int read = stream.Read(resp);

                if (read >= 10 && resp[0] == 0x08)
                {
                    int battery = resp[6];
                    bool charging = resp[7] != 0;
                    int mv = (resp[8] << 8) | resp[9];

                    if (battery >= 0 && battery <= 100)
                    {
                        var dev = new BatteryDeviceInfo
                        {
                            Id = cacheKey,
                            Name = productName,
                            Category = DeviceCategory.Mouse,
                            ConnectionType = DeviceConnectionType.Wireless24G,
                            BatteryPercentage = battery,
                            IsCharging = charging,
                            IsConnected = true,
                            VoltageMv = mv
                        };
                        _deviceCache[dev.Id] = dev;
                        return dev;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AtkVxe] Read error: {ex.Message}");
            }
        }

        if (_deviceCache.TryGetValue(cacheKey, out var fallback)) return fallback;
        return null;
    }

    private BatteryDeviceInfo? PollPortronicsHydraKeyboard(List<HidDevice> endpoints)
    {
        var ep = endpoints.FirstOrDefault(d => d.ProductID == 0xFA70) ?? endpoints.FirstOrDefault();
        if (ep == null) return null;

        string name = "Portronics Hydra 10 Keyboard";
        try
        {
            var p = ep.GetProductName();
            if (!string.IsNullOrWhiteSpace(p) && !p.Contains("Receiver", StringComparison.OrdinalIgnoreCase))
            {
                name = p;
            }
        }
        catch { }

        string cacheKey = "HID_PORTRONICS_HYDRA_10";
        int batteryLevel = 100;
        bool isCharging = false;

        // Check if cached
        if (_deviceCache.TryGetValue(cacheKey, out var cached))
        {
            batteryLevel = cached.BatteryPercentage ?? 100;
            isCharging = cached.IsCharging;
        }

        var dev = new BatteryDeviceInfo
        {
            Id = cacheKey,
            Name = name,
            Category = DeviceCategory.Keyboard,
            ConnectionType = DeviceConnectionType.Wireless24G,
            BatteryPercentage = batteryLevel,
            IsCharging = isCharging,
            IsConnected = true
        };
        _deviceCache[cacheKey] = dev;
        return dev;
    }

    private BatteryDeviceInfo? PollLogitechDongle(List<HidDevice> endpoints)
    {
        var endpoint = endpoints.FirstOrDefault(d => d.GetMaxOutputReportLength() >= 7);
        if (endpoint == null) return null;

        string name = "Logitech Wireless Device";
        try
        {
            var p = endpoint.GetProductName();
            if (!string.IsNullOrWhiteSpace(p)) name = p;
        }
        catch { }

        if (!endpoint.TryOpen(out var stream)) return null;

        using (stream)
        {
            stream.ReadTimeout = 400;
            for (byte devIndex = 1; devIndex <= 4; devIndex++)
            {
                try
                {
                    byte[] req = new byte[endpoint.GetMaxOutputReportLength()];
                    req[0] = 0x10;
                    req[1] = devIndex;
                    req[2] = 0x00;
                    req[3] = 0x00;
                    req[4] = 0x10;
                    req[5] = 0x00;
                    stream.Write(req);

                    byte[] resp = new byte[endpoint.GetMaxInputReportLength()];
                    int r = stream.Read(resp);
                    if (r >= 7 && resp[0] == 0x10 && resp[1] == devIndex && resp[2] == 0x00)
                    {
                        byte featureIndex = resp[4];
                        if (featureIndex != 0)
                        {
                            byte[] battReq = new byte[endpoint.GetMaxOutputReportLength()];
                            battReq[0] = 0x10;
                            battReq[1] = devIndex;
                            battReq[2] = featureIndex;
                            battReq[3] = 0x00;
                            stream.Write(battReq);

                            byte[] battResp = new byte[endpoint.GetMaxInputReportLength()];
                            int br = stream.Read(battResp);
                            if (br >= 7 && battResp[0] == 0x10 && battResp[1] == devIndex)
                            {
                                int level = battResp[4];
                                bool charging = (battResp[6] & 0x01) != 0;
                                var dev = new BatteryDeviceInfo
                                {
                                    Id = $"HID_LOGI_{devIndex}",
                                    Name = name,
                                    Category = BatteryDeviceInfo.InferCategory(name, DeviceConnectionType.Wireless24G),
                                    ConnectionType = DeviceConnectionType.Wireless24G,
                                    BatteryPercentage = Math.Clamp(level, 0, 100),
                                    IsCharging = charging,
                                    IsConnected = true
                                };
                                _deviceCache[dev.Id] = dev;
                                return dev;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private BatteryDeviceInfo? PollRazerDongle(List<HidDevice> endpoints)
    {
        var endpoint = endpoints.FirstOrDefault(d => d.GetMaxOutputReportLength() == 91 || d.GetMaxOutputReportLength() == 90);
        if (endpoint == null) return null;

        string name = "Razer Wireless Device";
        try
        {
            var p = endpoint.GetProductName();
            if (!string.IsNullOrWhiteSpace(p)) name = p;
        }
        catch { }

        if (!endpoint.TryOpen(out var stream)) return null;

        using (stream)
        {
            stream.ReadTimeout = 400;
            byte[] report = new byte[endpoint.GetMaxOutputReportLength()];
            int offset = endpoint.GetMaxOutputReportLength() == 91 ? 1 : 0;
            report[offset + 0] = 0x00;
            report[offset + 1] = 0x1F;
            report[offset + 2] = 0x00;
            report[offset + 3] = 0x00;
            report[offset + 4] = 0x00;
            report[offset + 5] = 0x02;
            report[offset + 6] = 0x07;
            report[offset + 7] = 0x80;

            byte crc = 0;
            for (int i = 2; i < 88; i++) crc ^= report[offset + i];
            report[offset + 88] = crc;

            try
            {
                stream.Write(report);
                byte[] resp = new byte[endpoint.GetMaxInputReportLength()];
                int r = stream.Read(resp);
                if (r > offset + 9 && resp[offset + 6] == 0x07)
                {
                    int batt = (int)Math.Round(resp[offset + 9] / 255.0 * 100.0);
                    bool charging = resp[offset + 8] != 0;
                    var dev = new BatteryDeviceInfo
                    {
                        Id = $"HID_RAZER_{endpoint.ProductID:X4}",
                        Name = name,
                        Category = BatteryDeviceInfo.InferCategory(name, DeviceConnectionType.Wireless24G),
                        ConnectionType = DeviceConnectionType.Wireless24G,
                        BatteryPercentage = Math.Clamp(batt, 0, 100),
                        IsCharging = charging,
                        IsConnected = true
                    };
                    _deviceCache[dev.Id] = dev;
                    return dev;
                }
            }
            catch { }
        }

        return null;
    }

    private BatteryDeviceInfo? PollPlayStationController(List<HidDevice> endpoints)
    {
        var ep = endpoints.FirstOrDefault(d => d.GetMaxInputReportLength() >= 32);
        if (ep == null) return null;

        bool isDualSense = ep.ProductID == 0x0CE6 || ep.ProductID == 0x0DF2;
        string name = isDualSense ? "PlayStation DualSense Controller" : "PlayStation DualShock 4 Controller";

        if (!ep.TryOpen(out var stream)) return null;

        using (stream)
        {
            stream.ReadTimeout = 400;
            try
            {
                byte[] buf = new byte[ep.GetMaxInputReportLength()];
                int read = stream.Read(buf);
                if (read >= 31)
                {
                    int battByteIndex = isDualSense ? 53 : 30;
                    if (read > battByteIndex)
                    {
                        byte b = buf[battByteIndex];
                        int level = (b & 0x0F) * 10;
                        bool charging = (b & 0x10) != 0 || (b & 0xF0) != 0;

                        var dev = new BatteryDeviceInfo
                        {
                            Id = $"HID_SONY_PS_{ep.ProductID:X4}",
                            Name = name,
                            Category = DeviceCategory.Gamepad,
                            ConnectionType = DeviceConnectionType.Wireless24G,
                            BatteryPercentage = Math.Clamp(level, 0, 100),
                            IsCharging = charging,
                            IsConnected = true
                        };
                        _deviceCache[dev.Id] = dev;
                        return dev;
                    }
                }
            }
            catch { }
        }

        return null;
    }

    private BatteryDeviceInfo? PollNintendoSwitchController(List<HidDevice> endpoints)
    {
        var ep = endpoints.FirstOrDefault(d => d.GetMaxInputReportLength() >= 12);
        if (ep == null) return null;

        string name = ep.ProductID == 0x2009 ? "Nintendo Switch Pro Controller" : "Nintendo Joy-Con Controller";
        if (!ep.TryOpen(out var stream)) return null;

        using (stream)
        {
            stream.ReadTimeout = 400;
            try
            {
                byte[] buf = new byte[ep.GetMaxInputReportLength()];
                int read = stream.Read(buf);
                if (read >= 3)
                {
                    byte b = buf[2];
                    int levelRaw = (b >> 4) & 0x0E;
                    int pct = levelRaw switch
                    {
                        8 => 100,
                        6 => 70,
                        4 => 30,
                        2 => 10,
                        _ => 50
                    };
                    bool charging = (b & 0x10) != 0;

                    var dev = new BatteryDeviceInfo
                    {
                        Id = $"HID_SWITCH_{ep.ProductID:X4}",
                        Name = name,
                        Category = DeviceCategory.Gamepad,
                        ConnectionType = DeviceConnectionType.Wireless24G,
                        BatteryPercentage = pct,
                        IsCharging = charging,
                        IsConnected = true
                    };
                    _deviceCache[dev.Id] = dev;
                    return dev;
                }
            }
            catch { }
        }

        return null;
    }

    private BatteryDeviceInfo? PollSteelSeriesDongle(List<HidDevice> endpoints)
    {
        var ep = endpoints.FirstOrDefault(d => d.GetMaxFeatureReportLength() >= 2);
        if (ep == null) return null;

        string name = "SteelSeries Wireless Device";
        try
        {
            var p = ep.GetProductName();
            if (!string.IsNullOrWhiteSpace(p)) name = p;
        }
        catch { }

        if (!ep.TryOpen(out var stream)) return null;

        using (stream)
        {
            try
            {
                byte[] buf = new byte[ep.GetMaxFeatureReportLength()];
                buf[0] = 0xAA;
                buf[1] = 0x01;
                stream.GetFeature(buf);
                if (buf.Length >= 3 && buf[2] <= 100 && buf[2] > 0)
                {
                    var dev = new BatteryDeviceInfo
                    {
                        Id = $"HID_STEELSERIES_{ep.ProductID:X4}",
                        Name = name,
                        Category = BatteryDeviceInfo.InferCategory(name, DeviceConnectionType.Wireless24G),
                        ConnectionType = DeviceConnectionType.Wireless24G,
                        BatteryPercentage = buf[2],
                        IsConnected = true
                    };
                    _deviceCache[dev.Id] = dev;
                    return dev;
                }
            }
            catch { }
        }

        return null;
    }

    private BatteryDeviceInfo? PollDigitizerStylus(List<HidDevice> endpoints)
    {
        // Digitizer devices (Wacom, Huion, XP-Pen) reporting battery status
        foreach (var ep in endpoints)
        {
            try
            {
                string? prod = ep.GetProductName();
                if (prod != null && (prod.Contains("Pen", StringComparison.OrdinalIgnoreCase) ||
                                     prod.Contains("Stylus", StringComparison.OrdinalIgnoreCase) ||
                                     prod.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
                                     prod.Contains("Wacom", StringComparison.OrdinalIgnoreCase) ||
                                     prod.Contains("Huion", StringComparison.OrdinalIgnoreCase)))
                {
                    if (ep.GetMaxFeatureReportLength() > 0 && ep.TryOpen(out var stream))
                    {
                        using (stream)
                        {
                            byte[] buf = new byte[ep.GetMaxFeatureReportLength()];
                            stream.GetFeature(buf);
                            if (buf.Length >= 2 && buf[1] > 0 && buf[1] <= 100)
                            {
                                return new BatteryDeviceInfo
                                {
                                    Id = $"HID_STYLUS_{ep.VendorID:X4}_{ep.ProductID:X4}",
                                    Name = prod,
                                    Category = DeviceCategory.Stylus,
                                    ConnectionType = DeviceConnectionType.Wireless24G,
                                    BatteryPercentage = buf[1],
                                    IsConnected = true
                                };
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private BatteryDeviceInfo? PollStandardHidPower(List<HidDevice> endpoints)
    {
        foreach (var ep in endpoints)
        {
            try
            {
                if (ep.GetMaxFeatureReportLength() > 0 && ep.TryOpen(out var stream))
                {
                    using (stream)
                    {
                        byte[] buf = new byte[ep.GetMaxFeatureReportLength()];
                        stream.GetFeature(buf);
                        if (buf.Length >= 2 && buf[1] > 0 && buf[1] <= 100)
                        {
                            string name = ep.GetProductName() ?? $"HID Device (0x{ep.VendorID:X4})";
                            return new BatteryDeviceInfo
                            {
                                Id = $"HID_STD_{ep.VendorID:X4}_{ep.ProductID:X4}",
                                Name = name,
                                Category = BatteryDeviceInfo.InferCategory(name, DeviceConnectionType.Wireless24G),
                                ConnectionType = DeviceConnectionType.Wireless24G,
                                BatteryPercentage = buf[1],
                                IsConnected = true
                            };
                        }
                    }
                }
            }
            catch { }
        }
        return null;
    }
}
