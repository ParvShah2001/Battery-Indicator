================================================================================
BATTERY INDICATOR - CUSTOM ICON & LOGO GUIDE
================================================================================

You can customize all device icons and the application logo by placing your own
image files in this folder:

Location: D:\github\Battery-Indicator\Resources\Icons\

--------------------------------------------------------------------------------
1. APPLICATION LOGO:
--------------------------------------------------------------------------------
- Filename: app.ico (or app.png)
- Format:   .ico (Windows Icon container with 16x16 up to 256x256) or .png
- Location: Put app.ico in the project root: D:\github\Battery-Indicator\app.ico
            and also in: D:\github\Battery-Indicator\Resources\Icons\app.ico
- Usage:    Shows as the main app icon, the Windows Explorer executable icon, 
            and the default taskbar notification tray icon.

--------------------------------------------------------------------------------
2. DEVICE CATEGORY ICONS:
--------------------------------------------------------------------------------
- Format:       PNG (.png) with transparent background
                (or .ico, .jpg)
- Recommended:  64x64, 128x128, or 256x256 pixels
- Category Filenames:
    • mouse.png         - Wireless Mice (ATK, Logitech, Razer, etc.)
    • keyboard.png      - Wireless Keyboards (G915, BlackWidow, Keychron, etc.)
    • headphones.png    - Over-Ear Headphones & Headsets (Sony WH-1000XM5, Arctis, etc.)
    • earbuds.png       - TWS In-Ear Buds (OnePlus Buds, Pixel Buds, AirPods, etc.)
    • gamepad.png       - Gamepads & Controllers (Xbox, DualSense PS5, Switch Pro, 8BitDo)
    • stylus.png        - Drawing Tablet Pens & Styluses (Wacom, Surface Pen, Huion)
    • speaker.png       - Bluetooth Portable Speakers (JBL, Bose, Soundcore)
    • wheel.png         - Racing Wheels, Pedals, and Flight Sticks / HOTAS
    • vr.png            - VR Headsets and VR Controllers (Meta Quest, Vive, Index)
    • phone.png         - Smartphones and Tablets (Pixel, Galaxy, iPhone, iPad)

--------------------------------------------------------------------------------
3. DEVICE-SPECIFIC ICONS (OPTIONAL):
--------------------------------------------------------------------------------
If you want an exact custom icon for a specific model (e.g. your ATK VXE R1 mouse),
name the PNG using the device name (lowercased with spaces replaced by underscores):
    • atk_vxe_r1_mouse.png   or   atk_vxe_r1.png
    • oneplus_nord_buds_3r.png
    • sony_wh_1000xm5.png
    • xbox_wireless_controller.png
    • playstation_dualsense_controller.png

--------------------------------------------------------------------------------
4. HOW IT WORKS:
--------------------------------------------------------------------------------
- If a custom file is found here, Battery Indicator displays your custom artwork
  inside the device card tab AND inside the system tray circular ring!
- If no file is placed for a category, it automatically renders the built-in
  high-quality Windows 11 Fluent vector glyphs (e.g. 🖱️, 🎧, 🎮, ⌨️, 🖊️, 🔊).
================================================================================
