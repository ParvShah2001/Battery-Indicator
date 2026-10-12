# Battery Indicator

A native Windows 11 application that displays live battery levels for devices connected through **2.4GHz Wireless USB Dongles**, **Bluetooth**, and **Xbox Wireless** right in your Windows taskbar system tray (notification area).

---

## Features

- **Multi-Device System Tray Pinning (`+` Button):**
  - Click the **`+`** button on any device card to pin a dedicated, independent icon to the Windows system tray.
  - Pinned tray icons feature a **circular battery ring gauge** around the perimeter (color-coded in Cyan/Mint `#00F298`, Amber `#FFA726`, or Coral `#FF5252`) with the **device icon in the center** and a live charging bolt badge.
  - Pin as many devices as you want simultaneously! Each pinned device has its own quick context menu to unpin or view stats.
- **Customizable App Logo & Device Icons:**
  - Place your custom `.png` (transparent background) or `.ico` files in `Resources\Icons\` and the app automatically uses them both in the flyout cards and inside the system tray circular gauge.
  - Full fallback to crisp Windows 11 vector glyphs when no custom file is provided.
- **Universal Multi-Category & Multi-Brand Device Telemetry:**
  - **Wireless Mice & Keyboards:** ATK / VXE (Compx 17-byte HID), Logitech Lightspeed & Unifying (HID++ 2.0), Razer 2.4G & Hyperspeed, Corsair Slipstream, SteelSeries.
  - **TWS Earbuds & Headphones:** OnePlus Nord Buds 3r & HeyMelody authenticated SPP RFCOMM protocol, Sony WH-1000XM4/XM5 & WF-1000XM4/XM5, Pixel Buds, Apple AirPods on Windows, and standard Bluetooth LE GATT Battery Service (`0x180F`).
  - **Gamepads & Controllers:** Xbox Wireless Controllers (Series X|S, One, Elite via `Windows.Gaming.Input`), XInput gamepads (8BitDo, PowerA, Razer Wolverine), PlayStation DualSense (PS5) & DualShock 4 (PS4 via HID reports), Nintendo Switch Pro & Joy-Cons.
  - **Drawing Tablet Styluses:** Wacom, Huion, XP-Pen digitizers, and Microsoft Surface Pen.
  - **Bluetooth Speakers:** Portable speakers (JBL, Bose, Soundcore, Marshall, UE Boom via GATT battery).
  - **Flight Sticks & Racing Wheels:** Thrustmaster, Logitech G29/G923, Fanatec via HID reports.
  - **VR Controllers:** Meta Quest, Valve Index, HTC Vive controllers.
  - **Smartphones & Tablets:** Android & iOS Bluetooth battery reporting.
- **Windows 11 Native Fluent UI:**
  - Filleted tab-style cards acting as responsive battery meters with zero white corner artifacts.
  - Native Windows 11 Desktop Window Manager rounded corners and dark theme.
  - Live footer showing exact last update time and active connected count.

---

## Custom Icon Guide

To use your own custom logo and device icons, place them in:
`Resources\Icons\` (or `publish\Resources\Icons\`)

| Type | Filename | Format | Description |
| :--- | :--- | :--- | :--- |
| **App Logo** | `app.ico` | `.ico` (16 to 256px) | Main application & taskbar icon |
| **Mouse** | `mouse.png` | `.png` (transparent) | Wireless mice |
| **Keyboard** | `keyboard.png` | `.png` (transparent) | Wireless keyboards |
| **Earbuds** | `earbuds.png` | `.png` (transparent) | In-ear TWS earbuds |
| **Headphones** | `headphones.png` | `.png` (transparent) | Over-ear headphones & headsets |
| **Gamepad** | `gamepad.png` | `.png` (transparent) | Xbox, PS5, Switch, 8BitDo |
| **Stylus** | `stylus.png` | `.png` (transparent) | Drawing pens & styluses |
| **Speaker** | `speaker.png` | `.png` (transparent) | Bluetooth speakers |
| **Wheel / Stick** | `wheel.png` | `.png` (transparent) | Racing wheels & flight sticks |
| **VR** | `vr.png` | `.png` (transparent) | VR headsets & controllers |
| **Phone** | `phone.png` | `.png` (transparent) | Smartphones & tablets |

You can also name images after specific devices (e.g. `atk_vxe_r1.png`, `oneplus_nord_buds_3r.png`).

---

## Building and Running

### Requirements:
- **Windows 10 (version 19041+) or Windows 11**
- **.NET 9.0 SDK** (Installed locally in `%USERPROFILE%\.dotnet`)

### To Run:
```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
dotnet run
```

### To Publish Standalone Release:
```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
The executable is generated at `publish\Battery Indicator.exe`.
