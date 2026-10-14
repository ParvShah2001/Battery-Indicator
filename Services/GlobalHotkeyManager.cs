using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BatteryIndicator.Services;

/// <summary>
/// Registers a system-wide global hotkey to summon the Battery Indicator flyout from anywhere
/// (including full-screen games, videos, or other applications).
/// Default shortcut: Win + Alt + B
/// </summary>
public class GlobalHotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HotkeyMessageWindow _window;
    private readonly Action _onHotkeyPressed;
    private const int HotkeyId = 0xBA77;
    private bool _isRegistered;

    public GlobalHotkeyManager(Action onHotkeyPressed)
    {
        _onHotkeyPressed = onHotkeyPressed;
        _window = new HotkeyMessageWindow(OnHotkeyTriggered);
    }

    public bool Register(uint modifiers = 0, uint key = 0)
    {
        Unregister();

        if (modifiers == 0 && key == 0)
        {
            modifiers = SettingsManager.HotkeyModifiers;
            key = SettingsManager.HotkeyKey;
        }

        uint finalMods = modifiers | MOD_NOREPEAT;
        _isRegistered = RegisterHotKey(_window.Handle, HotkeyId, finalMods, key);
        
        // If MOD_NOREPEAT is rejected by older subsystem, try without it
        if (!_isRegistered)
        {
            _isRegistered = RegisterHotKey(_window.Handle, HotkeyId, modifiers, key);
        }

        // If Windows key combinations are intercepted by the OS, attempt Ctrl+Alt fallback
        if (!_isRegistered && (modifiers & MOD_WIN) != 0)
        {
            uint altMods = (modifiers & ~MOD_WIN) | MOD_CONTROL | MOD_ALT;
            _isRegistered = RegisterHotKey(_window.Handle, HotkeyId, altMods, key);
        }

        return _isRegistered;
    }

    public void Unregister()
    {
        if (!_isRegistered) return;
        UnregisterHotKey(_window.Handle, HotkeyId);
        _isRegistered = false;
    }

    private void OnHotkeyTriggered()
    {
        _onHotkeyPressed?.Invoke();
    }

    public void Dispose()
    {
        Unregister();
        _window.Dispose();
    }

    private class HotkeyMessageWindow : Form
    {
        private readonly Action _onTrigger;

        public HotkeyMessageWindow(Action onTrigger)
        {
            _onTrigger = onTrigger;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            // Force HWND creation immediately
            IntPtr h = Handle;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style = 0x8000000; // WS_DISABLED
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                _onTrigger?.Invoke();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
