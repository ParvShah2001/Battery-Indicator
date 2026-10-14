using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BatteryIndicator.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;

namespace BatteryIndicator.Services;

/// <summary>
/// Universal Bluetooth & BLE battery provider.
/// Supports:
///  - Standard BLE GATT Battery Service (0x180F / 0x2A19)
///  - OnePlus / Oppo / Realme / HeyMelody TWS earbuds authenticated protocol (SPP UUID 0000079A)
///  - Audio headset companion battery telemetry
/// </summary>
public class BluetoothBatteryProvider : IDeviceBatteryProvider
{
    public string ProviderName => "Bluetooth Devices";

    // Stable cache across poll intervals so connected devices remain visible and don't flicker
    private static readonly Dictionary<string, (int? Battery, bool Charging, List<SubBatteryInfo>? Sub)> _batteryCache = new();

    public async Task<List<BatteryDeviceInfo>> PollDevicesAsync()
    {
        var result = new List<BatteryDeviceInfo>();

        // 1. Scan paired Bluetooth LE devices with GATT Battery Service
        try
        {
            await PollBluetoothLEDevicesAsync(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BluetoothProvider] BLE scan failed: {ex.Message}");
        }

        // 2. Scan paired Bluetooth Classic devices (Headsets/Earbuds)
        try
        {
            await PollBluetoothClassicDevicesAsync(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BluetoothProvider] Classic scan failed: {ex.Message}");
        }

        return result;
    }

    private async Task PollBluetoothLEDevicesAsync(List<BatteryDeviceInfo> result)
    {
        string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(selector);

        foreach (var devInfo in devices)
        {
            try
            {
                var ble = await BluetoothLEDevice.FromIdAsync(devInfo.Id);
                if (ble == null || ble.ConnectionStatus != BluetoothConnectionStatus.Connected) continue;

                var services = await ble.GetGattServicesForUuidAsync(GattServiceUuids.Battery);
                if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
                {
                    var levels = new List<int>();
                    foreach (var s in services.Services)
                    {
                        var chars = await s.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
                        if (chars.Status == GattCommunicationStatus.Success)
                        {
                            foreach (var c in chars.Characteristics)
                            {
                                var res = await c.ReadValueAsync();
                                if (res.Status == GattCommunicationStatus.Success)
                                {
                                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(res.Value);
                                    byte level = reader.ReadByte();
                                    levels.Add(Math.Clamp((int)level, 0, 100));
                                }
                            }
                        }
                    }

                    if (levels.Count > 0)
                    {
                        string name = string.IsNullOrWhiteSpace(ble.Name) ? devInfo.Name : ble.Name;
                        var category = BatteryDeviceInfo.InferCategory(name, DeviceConnectionType.BluetoothLE);
                        List<SubBatteryInfo>? subBatt = null;

                        if (levels.Count >= 2 && category == DeviceCategory.Earbuds)
                        {
                            subBatt = new List<SubBatteryInfo>
                            {
                                new SubBatteryInfo { Label = "L", Percentage = levels[0], IsCharging = false },
                                new SubBatteryInfo { Label = "R", Percentage = levels[1], IsCharging = false }
                            };
                        }

                        int avg = (int)Math.Round(levels.Average());
                        result.Add(new BatteryDeviceInfo
                        {
                            Id = $"BLE_{ble.DeviceId}",
                            Name = name,
                            Category = category,
                            ConnectionType = DeviceConnectionType.Bluetooth,
                            BatteryPercentage = avg,
                            SubBatteries = subBatt,
                            IsConnected = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BLE] Failed to read {devInfo.Name}: {ex.Message}");
            }
        }
    }

    private async Task PollBluetoothClassicDevicesAsync(List<BatteryDeviceInfo> result)
    {
        string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(selector);

        foreach (var devInfo in devices)
        {
            try
            {
                var bt = await BluetoothDevice.FromIdAsync(devInfo.Id);
                if (bt == null || bt.ConnectionStatus != BluetoothConnectionStatus.Connected) continue;

                string devName = string.IsNullOrWhiteSpace(bt.Name) ? devInfo.Name : bt.Name;
                string deviceKey = bt.DeviceId;

                int? batteryLevel = null;
                bool isCharging = false;
                List<SubBatteryInfo>? subBatteries = null;

                var rfcommResult = await bt.GetRfcommServicesAsync(BluetoothCacheMode.Cached);

                // Priority 1: Check OnePlus / Oppo HeyMelody 0000079A channel
                foreach (var s in rfcommResult.Services)
                {
                    string uuid = s.ServiceId.AsString().ToUpperInvariant();
                    if (uuid.Contains("0000079A"))
                    {
                        var (batt, chg, subs) = await QueryOnePlusEarbudsAsync(s);
                        if (batt.HasValue)
                        {
                            batteryLevel = batt.Value;
                            isCharging = chg;
                            subBatteries = subs;
                            _batteryCache[deviceKey] = (batteryLevel.Value, isCharging, subBatteries);
                            break;
                        }
                    }
                }

                // Priority 2: Use last-known valid cache if poll cycle is idle
                if (!batteryLevel.HasValue && _batteryCache.TryGetValue(deviceKey, out var cached))
                {
                    batteryLevel = cached.Battery;
                    isCharging = cached.Charging;
                    subBatteries = cached.Sub;
                }

                var category = BatteryDeviceInfo.InferCategory(devName, DeviceConnectionType.Bluetooth);
                // Skip phones without battery telemetry (handled accurately by PhoneLinkBatteryProvider)
                if (category == DeviceCategory.PhoneOrTablet && !batteryLevel.HasValue)
                {
                    continue;
                }

                result.Add(new BatteryDeviceInfo
                {
                    Id = $"BT_{bt.DeviceId}",
                    Name = devName,
                    Category = category,
                    ConnectionType = DeviceConnectionType.Bluetooth,
                    BatteryPercentage = batteryLevel,
                    IsCharging = isCharging,
                    SubBatteries = subBatteries,
                    IsConnected = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BT Classic] Failed to poll {devInfo.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Executes the reverse-engineered OPO HeyMelody handshake & query on SPP UUID 0000079A:
    ///  1. Read spontaneous HELLO packet
    ///  2. Send ACK with received sequence number
    ///  3. Read second device packet & send ACK
    ///  4. Send REGISTER token (B5 50 A0 69)
    ///  5. Query Category 0x06 / Subcommand 0x01
    ///  6. Parse exact earbud battery bytes [0x01, Left%, 0x02, Right%, 0x03, Case%]
    /// </summary>
    private async Task<(int? Battery, bool Charging, List<SubBatteryInfo>? Subs)> QueryOnePlusEarbudsAsync(RfcommDeviceService service)
    {
        try
        {
            using var socket = new StreamSocket();
            using var cts = new System.Threading.CancellationTokenSource(2500);

            await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask(cts.Token);
            using var inStream = socket.InputStream.AsStreamForRead();
            using var outStream = socket.OutputStream.AsStreamForWrite();

            // 1. Initial earbud packet
            byte[] init = new byte[14];
            int r0 = await inStream.ReadAsync(init, 0, 14, cts.Token);
            if (r0 < 7 || init[0] != 0xAA) return (null, false, null);

            byte seq = init[6];

            // 2. Send ACK
            byte[] ack1 = new byte[] { 0xAA, 0x05, 0x00, 0x00, 0x00, 0x01, seq, 0x00, 0x00 };
            await outStream.WriteAsync(ack1, 0, ack1.Length, cts.Token);
            await outStream.FlushAsync(cts.Token);

            // 3. Second earbud packet
            byte[] p2 = new byte[18];
            int r1 = await inStream.ReadAsync(p2, 0, 18, cts.Token);
            if (r1 < 7) return (null, false, null);

            byte seq2 = p2[6];
            byte[] ack2 = new byte[] { 0xAA, 0x05, 0x00, 0x00, 0x00, 0x01, seq2, 0x00, 0x00 };
            await outStream.WriteAsync(ack2, 0, ack2.Length, cts.Token);
            await outStream.FlushAsync(cts.Token);

            // 4. Register token
            byte[] reg = new byte[] { 0xAA, 0x08, 0x00, 0x00, 0x00, 0x02, (byte)(seq2 + 1), 0x00, 0xB5, 0x50, 0xA0, 0x69 };
            await outStream.WriteAsync(reg, 0, reg.Length, cts.Token);
            await outStream.FlushAsync(cts.Token);

            byte[] regResp = new byte[256];
            int regRead = await inStream.ReadAsync(regResp, 0, regResp.Length, cts.Token);

            // 5. Query Battery (Category 0x06, Sub 0x01)
            byte[] qBatt = new byte[] { 0xAA, 0x04, 0x00, 0x00, 0x06, 0x01, (byte)(seq2 + 2), 0x00 };
            await outStream.WriteAsync(qBatt, 0, qBatt.Length, cts.Token);
            await outStream.FlushAsync(cts.Token);

            byte[] bResp = new byte[256];
            int br = await inStream.ReadAsync(bResp, 0, bResp.Length, cts.Token);

            // Response pattern: AA 0F 00 00 06 81 [Seq] 08 00 00 [Count] 01 [Left%] 02 [Right%] 03 [Case%]
            if (br >= 15 && bResp[0] == 0xAA && bResp[4] == 0x06)
            {
                int left = -1;
                int right = -1;

                for (int i = 8; i <= br - 2; i++)
                {
                    if (bResp[i] == 0x01 && bResp[i + 1] <= 100) left = bResp[i + 1];
                    if (bResp[i] == 0x02 && bResp[i + 1] <= 100) right = bResp[i + 1];
                }

                var subs = new List<SubBatteryInfo>();
                if (left >= 0) subs.Add(new SubBatteryInfo { Label = "L", Percentage = left, IsCharging = false });
                if (right >= 0) subs.Add(new SubBatteryInfo { Label = "R", Percentage = right, IsCharging = false });

                int sum = 0;
                int count = 0;
                if (left >= 0) { sum += left; count++; }
                if (right >= 0) { sum += right; count++; }

                if (count > 0)
                {
                    int avg = sum / count;
                    return (avg, false, subs.Count > 0 ? subs : null);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OnePlus] Telemetry query error: {ex.Message}");
        }

        return (null, false, null);
    }
}
