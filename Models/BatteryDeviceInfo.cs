using System;

namespace BatteryIndicator.Models;

public enum DeviceConnectionType
{
    Wireless24G,
    Bluetooth,
    BluetoothLE,
    UsbWired,
    XboxWireless
}

public enum DeviceCategory
{
    Mouse,
    Keyboard,
    Headphones,
    Earbuds,
    Speaker,
    Gamepad,
    Stylus,
    FlightStickOrWheel,
    VrController,
    PhoneOrTablet,
    Laptop,
    Ups,
    Generic
}

public class SubBatteryInfo
{
    public required string Label { get; set; } // "L", "R"
    public int Percentage { get; set; }
    public bool IsCharging { get; set; }
}

public class BatteryDeviceInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public DeviceConnectionType ConnectionType { get; set; }
    public DeviceCategory Category { get; set; }
    public int? BatteryPercentage { get; set; }
    public bool IsCharging { get; set; }
    public bool IsConnected { get; set; }
    public int? VoltageMv { get; set; }
    public int? DischargeRateMw { get; set; } // Power rate in milliwatts
    public int? HealthPercentage { get; set; } // Battery health / degradation %
    public List<SubBatteryInfo>? SubBatteries { get; set; } // For TWS earbuds (L, R)
    public bool IsPinnedToTray { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public string BatteryDisplay
    {
        get
        {
            if (SubBatteries != null && SubBatteries.Count > 0)
            {
                return string.Join(" | ", SubBatteries.Select(s => $"{s.Label}: {s.Percentage}%{(s.IsCharging ? "⚡" : "")}"));
            }
            return BatteryPercentage.HasValue
                ? $"{BatteryPercentage.Value}%" + (IsCharging ? " ⚡" : "")
                : "Unknown";
        }
    }

    public string ConnectionDisplay => ConnectionType switch
    {
        DeviceConnectionType.Wireless24G => "2.4 GHz",
        DeviceConnectionType.Bluetooth => "Bluetooth",
        DeviceConnectionType.BluetoothLE => "Bluetooth",
        DeviceConnectionType.UsbWired => "USB / Wired",
        DeviceConnectionType.XboxWireless => "Xbox Wireless",
        _ => "Wireless"
    };

    public string DefaultGlyph => Category switch
    {
        DeviceCategory.Mouse => "🖱️",
        DeviceCategory.Keyboard => "⌨️",
        DeviceCategory.Headphones => "🎧",
        DeviceCategory.Earbuds => "🎵",
        DeviceCategory.Speaker => "🔊",
        DeviceCategory.Gamepad => "🎮",
        DeviceCategory.Stylus => "🖊️",
        DeviceCategory.FlightStickOrWheel => "🕹️",
        DeviceCategory.VrController => "🥽",
        DeviceCategory.PhoneOrTablet => "📱",
        DeviceCategory.Laptop => "💻",
        DeviceCategory.Ups => "🔋",
        _ => "🔋"
    };

    public static DeviceCategory InferCategory(string name, DeviceConnectionType connectionType)
    {
        string n = name.ToLowerInvariant();

        if (n.Contains("bud") || n.Contains("airpod") || n.Contains("wf-") || n.Contains("in-ear") || n.Contains("nord buds") || n.Contains("freebuds"))
            return DeviceCategory.Earbuds;

        if (n.Contains("headphone") || n.Contains("headset") || n.Contains("wh-") || n.Contains("arctis") || n.Contains("cloud") || n.Contains("kraken") || n.Contains("blackshark"))
            return DeviceCategory.Headphones;

        if (n.Contains("speaker") || n.Contains("soundcore") || n.Contains("boom") || n.Contains("flip") || n.Contains("charge") || n.Contains("pill") || n.Contains("echo"))
            return DeviceCategory.Speaker;

        if (n.Contains("controller") || n.Contains("gamepad") || n.Contains("xbox") || n.Contains("dualsense") || n.Contains("dualshock") || n.Contains("8bitdo") || n.Contains("switch pro") || n.Contains("joy-con"))
            return DeviceCategory.Gamepad;

        if (n.Contains("keyboard") || n.Contains("blackwidow") || n.Contains("deathstalker") || n.Contains("huntsman") || n.Contains("g915") || n.Contains("mx keys") || n.Contains("keychron") || n.Contains("k380"))
            return DeviceCategory.Keyboard;

        if (n.Contains("mouse") || n.Contains("deathadder") || n.Contains("viper") || n.Contains("basilisk") || n.Contains("r1") || n.Contains("g pro") || n.Contains("g502") || n.Contains("g305") || n.Contains("mx master") || n.Contains("vxe"))
            return DeviceCategory.Mouse;

        if (n.Contains("pen") || n.Contains("stylus") || n.Contains("pencil") || n.Contains("wacom") || n.Contains("huion") || n.Contains("surface pen"))
            return DeviceCategory.Stylus;

        if (n.Contains("wheel") || n.Contains("pedal") || n.Contains("flight") || n.Contains("hotas") || n.Contains("joystick") || n.Contains("thrustmaster") || n.Contains("fanatec") || n.Contains("g29") || n.Contains("g923") || n.Contains("g920"))
            return DeviceCategory.FlightStickOrWheel;

        if (n.Contains("quest") || n.Contains("vive") || n.Contains("index") || n.Contains("vr controller") || n.Contains("oculus"))
            return DeviceCategory.VrController;

        if (n.Contains("phone") || n.Contains("galaxy") || (n.Contains("pixel") && !n.Contains("buds")) || n.Contains("iphone") || n.Contains("ipad") || n.Contains("tablet"))
            return DeviceCategory.PhoneOrTablet;

        if (n.Contains("ups") || n.Contains("back-ups") || n.Contains("cyberpower") || n.Contains("apc"))
            return DeviceCategory.Ups;

        if (n.Contains("laptop") || n.Contains("internal battery") || n.Contains("notebook"))
            return DeviceCategory.Laptop;

        return connectionType == DeviceConnectionType.Wireless24G ? DeviceCategory.Mouse : DeviceCategory.Generic;
    }
}
