using System;
using System.Text.Json.Serialization;

namespace BatteryIndicator.Models;

/// <summary>
/// Crowdsourced peripheral profile definition.
/// Enables community members to support new dongles, mice, keyboards, and gamepads
/// via clean JSON files without recompiling the application.
/// </summary>
public class DeviceProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Generic";

    [JsonPropertyName("connectionType")]
    public string ConnectionType { get; set; } = "Wireless24G";

    [JsonPropertyName("vid")]
    public int? Vid { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("nameContains")]
    public string? NameContains { get; set; }

    [JsonPropertyName("devicePathContains")]
    public string? DevicePathContains { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "HidFeatureReport"; // "HidFeatureReport", "HidInputReport", "HidOutputReport"

    [JsonPropertyName("reportId")]
    public byte ReportId { get; set; } = 0x00;

    [JsonPropertyName("reportLength")]
    public int ReportLength { get; set; } = 64;

    [JsonPropertyName("commandHex")]
    public string? CommandHex { get; set; }

    [JsonPropertyName("responseHeaderHex")]
    public string? ResponseHeaderHex { get; set; }

    [JsonPropertyName("batteryByteOffset")]
    public int BatteryByteOffset { get; set; } = 0;

    [JsonPropertyName("chargingByteOffset")]
    public int? ChargingByteOffset { get; set; }

    [JsonPropertyName("chargingBitMask")]
    public int? ChargingBitMask { get; set; }

    [JsonPropertyName("minRawValue")]
    public int MinRawValue { get; set; } = 0;

    [JsonPropertyName("maxRawValue")]
    public int MaxRawValue { get; set; } = 100;

    [JsonPropertyName("customIcon")]
    public string? CustomIcon { get; set; }

    public DeviceCategory ParsedCategory => Category?.ToLowerInvariant() switch
    {
        "mouse" => DeviceCategory.Mouse,
        "keyboard" => DeviceCategory.Keyboard,
        "headphones" => DeviceCategory.Headphones,
        "earbuds" => DeviceCategory.Earbuds,
        "speaker" => DeviceCategory.Speaker,
        "gamepad" => DeviceCategory.Gamepad,
        "stylus" => DeviceCategory.Stylus,
        "flightstick" or "wheel" => DeviceCategory.FlightStickOrWheel,
        "vr" => DeviceCategory.VrController,
        "ups" => DeviceCategory.Ups,
        _ => DeviceCategory.Generic
    };

    public DeviceConnectionType ParsedConnectionType => ConnectionType?.ToLowerInvariant() switch
    {
        "wireless24g" or "2.4g" or "2.4ghz" => DeviceConnectionType.Wireless24G,
        "bluetooth" or "bt" => DeviceConnectionType.Bluetooth,
        "bluetoothle" or "ble" => DeviceConnectionType.BluetoothLE,
        "usbwired" or "usb" or "wired" => DeviceConnectionType.UsbWired,
        "xboxwireless" or "xbox" => DeviceConnectionType.XboxWireless,
        _ => DeviceConnectionType.Wireless24G
    };
}
