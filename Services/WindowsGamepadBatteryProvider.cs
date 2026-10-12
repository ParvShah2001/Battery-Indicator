using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BatteryIndicator.Models;
using Windows.Gaming.Input;
using Windows.System.Power;

namespace BatteryIndicator.Services;

/// <summary>
/// Telemetry provider for Xbox, 8BitDo, and XInput-compatible wireless gamepads.
/// Uses Windows.Gaming.Input and XInputGetBatteryInformation native APIs.
/// </summary>
public class WindowsGamepadBatteryProvider : IDeviceBatteryProvider
{
    public string ProviderName => "Gamepads & Controllers";

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    private const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;
    private const byte BATTERY_TYPE_DISCONNECTED = 0x00;
    private const byte BATTERY_TYPE_WIRED = 0x01;
    private const byte BATTERY_TYPE_ALKALINE = 0x02;
    private const byte BATTERY_TYPE_NIMH = 0x03;
    private const byte BATTERY_TYPE_UNKNOWN = 0xFF;

    private const byte BATTERY_LEVEL_EMPTY = 0x00;
    private const byte BATTERY_LEVEL_LOW = 0x01;
    private const byte BATTERY_LEVEL_MEDIUM = 0x02;
    private const byte BATTERY_LEVEL_FULL = 0x03;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern int XInputGetBatteryInformation14(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern int XInputGetBatteryInformation91(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    public Task<List<BatteryDeviceInfo>> PollDevicesAsync()
    {
        var result = new List<BatteryDeviceInfo>();
        var seenIds = new HashSet<string>();

        // 1. Try Windows.Gaming.Input.Gamepad
        try
        {
            var gamepads = Gamepad.Gamepads;
            for (int i = 0; i < gamepads.Count; i++)
            {
                var gp = gamepads[i];
                var report = gp.TryGetBatteryReport();
                if (report != null && report.Status != BatteryStatus.NotPresent)
                {
                    int? pct = null;
                    if (report.RemainingCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.Value > 0)
                    {
                        pct = (int)Math.Round((double)report.RemainingCapacityInMilliwattHours.Value / report.FullChargeCapacityInMilliwattHours.Value * 100.0);
                    }
                    else if (report.Status == BatteryStatus.Charging || report.Status == BatteryStatus.Discharging)
                    {
                        // Some controllers report percentage via charge level
                        pct = 80;
                    }

                    string id = $"WGI_Gamepad_{i + 1}";
                    seenIds.Add(id);
                    result.Add(new BatteryDeviceInfo
                    {
                        Id = id,
                        Name = $"Xbox Wireless Controller #{i + 1}",
                        Category = DeviceCategory.Gamepad,
                        ConnectionType = DeviceConnectionType.XboxWireless,
                        BatteryPercentage = pct,
                        IsCharging = report.Status == BatteryStatus.Charging,
                        IsConnected = true
                    });
                }
            }
        }
        catch { }

        // 2. Fallback: Query XInput slots 0 to 3
        if (result.Count == 0)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (TryGetXInputBattery(slot, out var info))
                {
                    if (info.BatteryType != BATTERY_TYPE_DISCONNECTED)
                    {
                        int pct = info.BatteryLevel switch
                        {
                            BATTERY_LEVEL_FULL => 100,
                            BATTERY_LEVEL_MEDIUM => 65,
                            BATTERY_LEVEL_LOW => 25,
                            _ => 10
                        };

                        bool isWired = info.BatteryType == BATTERY_TYPE_WIRED;
                        string id = $"XINPUT_Gamepad_{slot + 1}";

                        result.Add(new BatteryDeviceInfo
                        {
                            Id = id,
                            Name = $"Wireless Gamepad (Slot {slot + 1})",
                            Category = DeviceCategory.Gamepad,
                            ConnectionType = isWired ? DeviceConnectionType.UsbWired : DeviceConnectionType.XboxWireless,
                            BatteryPercentage = isWired ? 100 : pct,
                            IsCharging = isWired,
                            IsConnected = true
                        });
                    }
                }
            }
        }

        return Task.FromResult(result);
    }

    private static bool TryGetXInputBattery(int slot, out XINPUT_BATTERY_INFORMATION info)
    {
        info = default;
        try
        {
            int res = XInputGetBatteryInformation14(slot, BATTERY_DEVTYPE_GAMEPAD, out info);
            return res == 0;
        }
        catch
        {
            try
            {
                int res = XInputGetBatteryInformation91(slot, BATTERY_DEVTYPE_GAMEPAD, out info);
                return res == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
