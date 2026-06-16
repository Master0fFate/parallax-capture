using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace parallax.Core.Services
{
    public class HotkeyManager : IDisposable
    {
        public sealed record ParsedHotkey(uint Modifiers, uint VirtualKey, string DisplayText, bool Disabled);

        // Win32 API imports for registering global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier key constants
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Windows message constant for hotkeys
        private const int WM_HOTKEY = 0x0312;

        // Hotkey IDs — each unique hotkey needs a unique ID integer
        public const int ID_REGION_SCREENSHOT = 9001;
        public const int ID_FULLSCREEN = 9002;
        public const int ID_REGION_VIDEO = 9003;

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private bool _disposed;

        // Dictionary of registered hotkey ID => Action callback
        private readonly Dictionary<int, Action> _callbacks = new();

        // Call this once the main window is loaded to get the HWND
        public void Initialize(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);
        }

        // Register a hotkey: id = unique int, modifiers = alt/ctrl/etc, vk = virtual key code
        public bool Register(int id, uint modifiers, uint virtualKey, Action callback)
        {
            if (_windowHandle == IntPtr.Zero)
                return false;

            if (_callbacks.ContainsKey(id))
            {
                UnregisterHotKey(_windowHandle, id);
                _callbacks.Remove(id);
            }

            bool result = RegisterHotKey(_windowHandle, id, modifiers, virtualKey);
            if (result)
                _callbacks[id] = callback;
            return result;
        }

        public bool RegisterConfigured(int id, bool enabled, string? gesture, Action callback, out string message)
        {
            if (!enabled)
            {
                message = "Shortcut is disabled.";
                return true;
            }

            if (!TryParse(gesture, out var parsed, out message))
                return false;

            if (parsed.Disabled)
            {
                message = "Shortcut is disabled.";
                return true;
            }

            if (!Register(id, parsed.Modifiers, parsed.VirtualKey, callback))
            {
                message = $"\"{parsed.DisplayText}\" could not be registered. Choose another shortcut or turn this one off.";
                return false;
            }

            message = $"Registered {parsed.DisplayText}.";
            return true;
        }

        public static bool TryParse(string? gesture, out ParsedHotkey hotkey, out string message)
        {
            hotkey = new ParsedHotkey(MOD_NONE, 0, "Disabled", true);

            string input = gesture?.Trim() ?? string.Empty;
            if (input.Length == 0 || IsDisabledGesture(input))
            {
                message = "Shortcut is disabled.";
                return true;
            }

            uint modifiers = MOD_NONE;
            uint virtualKey = 0;
            string? keyDisplay = null;
            string[] parts = input.Split('+');

            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0)
                {
                    message = "Use shortcut text like PrintScreen, Alt+PrintScreen, Ctrl+Shift+S, or None.";
                    return false;
                }

                if (TryReadModifier(part, out uint modifier))
                {
                    if ((modifiers & modifier) != 0)
                    {
                        message = $"\"{part}\" is listed more than once.";
                        return false;
                    }

                    modifiers |= modifier;
                    continue;
                }

                if (keyDisplay != null)
                {
                    message = "Use one key with optional Ctrl, Alt, Shift, or Win modifiers.";
                    return false;
                }

                if (!TryReadKey(part, out virtualKey, out keyDisplay))
                {
                    message = $"\"{part}\" is not a supported key. Use PrintScreen, a letter, a digit, or F1-F24.";
                    return false;
                }
            }

            if (keyDisplay == null)
            {
                message = "Add a key after the modifiers, or use None to disable the shortcut.";
                return false;
            }

            hotkey = new ParsedHotkey(modifiers, virtualKey, BuildDisplayText(modifiers, keyDisplay), false);
            message = "Shortcut is valid.";
            return true;
        }

        public static string FormatForDisplay(bool enabled, string? gesture)
        {
            if (!enabled)
                return "disabled";

            return TryParse(gesture, out var parsed, out _)
                ? parsed.Disabled ? "disabled" : parsed.DisplayText
                : "invalid";
        }

        // Shortcut method: Register PrintScreen with no modifiers. Returns true if successful.
        public bool RegisterPrintScreen(Action callback)
        {
            return Register(ID_REGION_SCREENSHOT, MOD_NONE, 0x2C, callback); // 0x2C = VK_SNAPSHOT
        }

        // Shortcut method: Register Alt+PrintScreen. Returns true if successful.
        public bool RegisterAltPrintScreen(Action callback)
        {
            return Register(ID_FULLSCREEN, MOD_ALT, 0x2C, callback);
        }

        // Shortcut method: Register Alt+R for region video. Returns true if successful.
        public bool RegisterAltR(Action callback)
        {
            return Register(ID_REGION_VIDEO, MOD_ALT, 0x52, callback); // 0x52 = R
        }

        public void UnregisterAll()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                foreach (var id in _callbacks.Keys.ToList())
                    UnregisterHotKey(_windowHandle, id);
            }

            _callbacks.Clear();
        }

        // Windows message pump hook — fires when any registered hotkey is pressed
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_callbacks.TryGetValue(id, out var callback))
                {
                    callback.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            UnregisterAll();
            _source?.RemoveHook(HwndHook);
            _source = null;
            _windowHandle = IntPtr.Zero;
            _disposed = true;
        }

        private static bool IsDisabledGesture(string input)
        {
            return string.Equals(input, "None", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "Disabled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadModifier(string input, out uint modifier)
        {
            string normalized = NormalizeToken(input);
            modifier = normalized switch
            {
                "CTRL" or "CONTROL" => MOD_CTRL,
                "ALT" => MOD_ALT,
                "SHIFT" => MOD_SHIFT,
                "WIN" or "WINDOWS" => MOD_WIN,
                _ => 0
            };

            return modifier != 0;
        }

        private static bool TryReadKey(string input, out uint virtualKey, out string display)
        {
            string normalized = NormalizeToken(input);

            if (normalized is "PRINTSCREEN" or "PRTSC" or "PRTSCN" or "SNAPSHOT")
            {
                virtualKey = 0x2C;
                display = "PrintScreen";
                return true;
            }

            if (normalized.Length == 1 && normalized[0] is >= 'A' and <= 'Z')
            {
                virtualKey = normalized[0];
                display = normalized;
                return true;
            }

            if (normalized.Length == 1 && normalized[0] is >= '0' and <= '9')
            {
                virtualKey = normalized[0];
                display = normalized;
                return true;
            }

            if (normalized.Length >= 2 && normalized[0] == 'F' && int.TryParse(normalized[1..], out int functionNumber))
            {
                if (functionNumber is >= 1 and <= 24)
                {
                    virtualKey = (uint)(0x70 + functionNumber - 1);
                    display = $"F{functionNumber}";
                    return true;
                }
            }

            virtualKey = 0;
            display = string.Empty;
            return false;
        }

        private static string BuildDisplayText(uint modifiers, string keyDisplay)
        {
            var parts = new List<string>();
            if ((modifiers & MOD_CTRL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(keyDisplay);
            return string.Join("+", parts);
        }

        private static string NormalizeToken(string input)
        {
            return input.Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .ToUpperInvariant();
        }
    }
}
