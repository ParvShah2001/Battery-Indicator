# 🔋 Battery Indicator for Windows 11

<p align="center">
  <img src="Resources/Icons/app.png" alt="Battery Indicator Logo" width="128" height="128" />
</p>

<p align="center">
  <strong>Native Windows 11 taskbar battery monitor for 2.4GHz Wireless Dongles, Bluetooth devices, Android phones, and Gamepads.</strong>
</p>

<p align="center">
  <a href="https://github.com"><img src="https://img.shields.io/badge/Platform-Windows%2011%20%7C%20Windows%2010-0078D4?logo=windows&logoColor=white" alt="Platform" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 9.0" /></a>
  <a href="https://github.com"><img src="https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white" alt="C#" /></a>
  <a href="https://github.com"><img src="https://img.shields.io/badge/License-MIT-green" alt="License" /></a>
  <a href="https://github.com"><img src="https://img.shields.io/badge/Release-v1.0.0-blue" alt="Release" /></a>
  <a href="https://github.com"><img src="https://img.shields.io/badge/Architecture-x64-orange" alt="Architecture" /></a>
</p>

<p align="center">
  <img src="social-preview.png" alt="Battery Indicator Windows 11 Showcase" width="100%" />
</p>

---

## 📌 Why Battery Indicator?

Windows has a major blind spot: **it cannot show the battery percentage of 2.4GHz wireless USB dongle mice, keyboards, or Android smartphones in the taskbar system tray**. Manufacturers force you to keep heavy, resource-hungry proprietary bloatware running (Razer Synapse, Logitech G HUB, etc.) just to see if your mouse or keyboard is about to die.

**Battery Indicator** solves this with a lightweight, native, bloat-free Windows 11 application that queries battery telemetry directly from hardware protocols and displays them right beside your Windows clock.

---

## ✨ Key Features

- **🔘 Multi-Device Taskbar Pinning (`+` Pin Button)**
  - Click the **`+`** button on any device card to pin a dedicated, independent circular battery gauge to your system tray.
  - Features an active color-coded circular progress ring (**Emerald Green** $>30\%$, **Amber Orange** $16\text{--}30\%$, **Crimson Red** $\le 15\%$) with the device's icon in the center and a live charging bolt badge.
  - Pin as many devices as you want simultaneously! Each pinned tray icon has its own quick context menu.
- **⚡ 2.4GHz USB Wireless Dongle Telemetry**
  - **ATK / VXE / Compx:** Reverse-engineered 17-byte HID vendor report protocol (`0x07` command frame).
  - **Logitech:** HID++ 2.0 / 1.0 unified battery feature reports across Lightspeed & Unifying receivers.
  - **Razer:** Hyperspeed 2.4GHz battery status query.
  - **Portronics & Areson:** Composite 2.4GHz wireless mechanical keyboards (e.g. Portronics Hydra 10).
  - **Corsair & SteelSeries:** Slipstream and custom wireless dongle support.
- **📱 Android Smartphone Telemetry (Pixel, Galaxy, OnePlus, etc.)**
  - Integrates seamlessly with Windows Phone Link / Link to Windows live companion telemetry cache, showing your phone's live battery status (e.g. **Google Pixel 7**, Samsung Galaxy).
- **🎧 Proprietary Bluetooth TWS & Headset Support**
  - **OnePlus / Oppo / Realme:** Authenticated SPP RFCOMM handshake (Service UUID `0000079A`) for Nord Buds 3r, Buds Pro 2, Enco, and HeyMelody earbuds.
  - **Sony & Pixel Buds:** Companion Bluetooth telemetry for WH-1000XM4/XM5, WF-1000XM4/XM5, and Pixel Buds.
  - **Apple AirPods & Beats:** Live battery reporting on Windows.
  - **Standard Bluetooth LE:** Universal GATT Battery Service (`0x180F` / `0x2A19`).
- **🎮 Wireless Gamepads & Controllers**
  - Native integration with Xbox Wireless Controllers (Series X|S, One, Elite via `Windows.Gaming.Input`), PlayStation DualSense (PS5) & DualShock 4 (PS4), Nintendo Switch Pro, and 8BitDo wireless gamepads.
- **⚠️ Smart Low Battery Alerts (Configurable)**
  - Native Windows notifications when any connected device drops to or below a configurable threshold (**default: 20%**, customizable from 5% to 50% via dark-mode modal).
  - Smart cooldown safeguard: triggers once per discharge cycle to prevent spam.
- **🎨 Windows 11 Fluent Dark UI**
  - Zero scrollbars and zero horizontal scrolling.
  - Dynamic bottom chevron indicator (`˅`) appearing dynamically when more devices are below view.
  - Smooth mouse-wheel scrolling and click-to-scroll navigation.
  - Razor-sharp ClearType typography with zero jitter on hover.
- **🖼️ 100% Customizable Icons**
  - Drop your own `.png` or `.ico` files in `Resources\Icons\` and the app automatically auto-crops whitespace, aspect-fits, and renders crisp white silhouettes on the dark circular gauges.

---

## 🖥️ User Interface Preview

```
┌──────────────────────────────────────────────┐
│  🔋 Battery Indicator              [Refresh]  │
├──────────────────────────────────────────────┤
│  [🖱️] ATK VXE R1                      85% [+] │
│      2.4 GHz                                 │
├──────────────────────────────────────────────┤
│  [📱] Pixel 7                         46% [✓] │
│      Bluetooth                               │
├──────────────────────────────────────────────┤
│  [🎵] OnePlus Nord Buds 3r            90% [+] │
│      Bluetooth                               │
├──────────────────────────────────────────────┤
│  [⌨️] Portronics Hydra 10             100% [+] │
│      2.4 GHz                                 │
├──────────────────────────────────────────────┤
│                     ˅                        │
├──────────────────────────────────────────────┤
│  Last updated - 03:45 PM         Devices - 4 │
└──────────────────────────────────────────────┘
```

---

## 📊 Supported Hardware & Protocols

| Category | Brands & Devices | Protocol / Mechanism | Connection |
| :--- | :--- | :--- | :--- |
| **Wireless Mice** | ATK / VXE (R1, Dragonfly, F1), Logitech (G Pro, G502, Superlight), Razer (DeathAdder, Viper, Basilisk) | Compx 17-byte HID / Logitech HID++ 2.0 / Razer Feature Reports | 2.4 GHz Dongle |
| **Wireless Keyboards** | Portronics Hydra 10, Keychron, Logitech G915 / MX Keys, Razer BlackWidow, Areson dongles | Areson HID Feature Report / Logitech HID++ / GATT | 2.4 GHz / Bluetooth |
| **TWS Earbuds** | OnePlus Nord Buds 3r, OnePlus Buds Pro 2, Oppo Enco, Realme Buds | HeyMelody Authenticated SPP RFCOMM (UUID `0000079A`) | Bluetooth |
| **Headphones** | Sony WH-1000XM4/XM5, Apple AirPods Max, Bose QC, SteelSeries Arctis | Bluetooth LE GATT Battery Service (`0x180F`) / Handsfree Telemetry | Bluetooth / 2.4 GHz |
| **Smartphones** | Google Pixel (Pixel 7, 8, 9), Samsung Galaxy, OnePlus | Windows Phone Link Start Menu Companion Telemetry | Phone Link / Bluetooth |
| **Gamepads** | Xbox Wireless (Series X\|S, One, Elite), PS5 DualSense, PS4 DualShock, 8BitDo, Switch Pro | Windows.Gaming.Input / HID Input Report 0x11 / DirectInput | Xbox Wireless / BT / Dongle |
| **Speakers** | JBL, Soundcore, Bose, UE Boom, Marshall | BLE GATT Battery Service (`0x180F`) | Bluetooth |
| **Stylus / Tablet** | Microsoft Surface Pen, Wacom, Huion, Apple Pencil (via companion) | Digitizer HID Battery Report / BLE GATT | Bluetooth / RF |

---

## 🚀 Getting Started

### Option 1: Download Pre-built Release (Recommended)
1. Download the latest release from the [Releases](https://github.com) section.
2. Extract the folder and double-click **`Battery Indicator.exe`**.
3. The app will immediately appear in your Windows system tray.

### Option 2: Run at Windows Startup
1. Press `Win + R`, type `shell:startup`, and press Enter.
2. Right-click **`Battery Indicator.exe`** $\rightarrow$ **Create shortcut**.
3. Drag the shortcut into your Startup folder.

---

## 🛠️ Building from Source

### Prerequisites:
- **Windows 10 (version 19041+) or Windows 11 (22H2 / 23H2 / 24H2)**
- **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)**

### Build & Run:
```powershell
# Clone the repository
git clone https://github.com/yourusername/Battery-Indicator.git
cd Battery-Indicator

# Build and run
dotnet run
```

### Publish Portable Single-Folder Distribution:
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
The production binaries will be ready in the `publish\` directory.

---

## 🎨 Custom Icons Guide

Place custom `.png` or `.ico` files in [`Resources\Icons\`](file:///d:/github/Battery-Indicator/Resources/Icons) to customize the visual appearance:

| Filename | Purpose | Recommended Resolution |
| :--- | :--- | :--- |
| `app.png` or `app.ico` | Application Logo & Master System Tray Icon | 500x500 PNG / Multi-res ICO |
| `mouse.png` | All wireless mice | 512x512 Transparent PNG |
| `keyboard.png` | All wireless keyboards | 512x512 Transparent PNG |
| `earbuds.png` | In-ear TWS earbuds | 512x512 Transparent PNG |
| `headphones.png` | Over-ear headphones & headsets | 512x512 Transparent PNG |
| `gamepad.png` | Controllers & gamepads | 512x512 Transparent PNG |
| `phone.png` | Smartphones & tablets | 512x512 Transparent PNG |
| `speaker.png` | Bluetooth speakers | 512x512 Transparent PNG |
| `stylus.png` | Drawing pens & styluses | 512x512 Transparent PNG |
| `wheel.png` | Racing wheels & flight sticks | 512x512 Transparent PNG |
| `vr.png` | VR controllers & headsets | 512x512 Transparent PNG |

*Tip: You can also name icons after specific device names (e.g. `atk_vxe_r1.png`, `pixel_7.png`) for per-device customization!*

---

## ⚙️ Configuration (`settings.json`)

Settings are stored automatically in `settings.json`:
```json
{
  "LowBatteryThreshold": 20,
  "LowBatteryAlertsEnabled": true
}
```
You can configure thresholds directly from the system tray:
- Right-click tray icon $\rightarrow$ **⚠️ Low Battery Alert (<X%)** $\rightarrow$ Choose preset or enter custom percentage.

---

## 🤝 Contributing

Contributions are warmly welcome! If you have a wireless dongle device or Bluetooth protocol not yet supported:
1. Fork the repo and create a new feature branch (`git checkout -b feature/NewDeviceSupport`).
2. Add the vendor ID / protocol parsing in [`Services/`](Services/).
3. Submit a Pull Request.

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for more information.
