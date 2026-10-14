# Privacy Policy for Battery Indicator for Windows

**Last updated:** October 2026

Battery Indicator for Windows ("the Application") is an open-source Windows application developed by Parv Shah. This Privacy Policy outlines how the Application handles user information and data.

---

### 1. Zero Data Collection
Battery Indicator for Windows is designed with a strict **privacy-first** approach:
- **No Personal Information:** The Application does not collect, record, process, or transmit any personally identifiable information (PII).
- **No Analytics or Telemetry:** There are no integrated tracking SDKs, telemetry services, crash analytics, or advertising platforms.
- **No Cloud Services or Accounts:** The Application does not require account creation, sign-ins, or registration.

### 2. Device Data and Local Telemetry
- All hardware detection (such as querying connected 2.4GHz USB wireless dongles, Bluetooth devices, Android phone battery status via Windows Phone Link, and gamepads) is conducted **strictly on your local machine**.
- Battery percentages, device names, and hardware statuses are kept in local memory for display in the application window and system tray.
- Device information never leaves your device and is never uploaded or transmitted over the internet.

### 3. Network Usage
- Battery Indicator for Windows does not transmit or receive data over the internet. All hardware querying is done locally via native Windows APIs (USB HID, Bluetooth GATT/RFCOMM, XInput, Windows.Gaming.Input).

### 4. Configuration Storage
- User preferences (such as your chosen low battery threshold percentage, startup preference, and pinned device IDs) are stored locally on your device in a local configuration file (`settings.json`).
- This data remains completely under your control and can be deleted at any time by uninstalling the application or deleting the configuration file.

### 5. Third-Party Services
- The Application does not integrate with any third-party APIs or remote servers.

### 6. Contact and Inquiries
If you have any questions or concerns regarding this Privacy Policy, please open an issue on the official GitHub repository:
- **GitHub Issues:** [https://github.com/ParvShah2001/Battery-Indicator-for-Windows/issues](https://github.com/ParvShah2001/Battery-Indicator-for-Windows/issues)
- **Repository:** [https://github.com/ParvShah2001/Battery-Indicator-for-Windows](https://github.com/ParvShah2001/Battery-Indicator-for-Windows)
