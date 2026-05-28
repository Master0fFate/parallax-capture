using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace parallax.Core.Services
{
    public class HotkeyManager : IDisposable
    {
        // Win32 API imports for registering global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier key constants
        private const uint MOD_NONE  = 0x0000;
        private const uint MOD_ALT   = 0x0001;
        private const uint MOD_CTRL  = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN   = 0x0008;

        // Windows message constant for hotkeys
        private const int WM_HOTKEY = 0x0312;

        // Hotkey IDs — each unique hotkey needs a unique ID integer
        public const int ID_REGION_SCREENSHOT = 9001;
        public const int ID_FULLSCREEN        = 9002;
        public const int ID_REGION_VIDEO      = 9003;

        private IntPtr _windowHandle;
        private HwndSource? _source;

        // Dictionary of registered hotkey ID => Action callback
        private readonly Dictionary<int, Action> _callbacks = new();

        // Call this once the main window is loaded to get the HWND
        public void Initialize(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);
        }

        // Register a hotkey: id = unique int, modifiers = alt/ctrl/etc, vk = virtual key code
        public bool Register(int id, uint modifiers, uint virtualKey, Action callback)
        {
            bool result = RegisterHotKey(_windowHandle, id, modifiers, virtualKey);
            if (result)
                _callbacks[id] = callback;
            return result;
        }

        // Shortcut method: Register PrintScreen with no modifiers
        public void RegisterPrintScreen(Action callback)
        {
            Register(ID_REGION_SCREENSHOT, MOD_NONE, 0x2C, callback); // 0x2C = VK_SNAPSHOT
        }

        // Shortcut method: Register Alt+PrintScreen
        public void RegisterAltPrintScreen(Action callback)
        {
            Register(ID_FULLSCREEN, MOD_ALT, 0x2C, callback);
        }

        // Shortcut method: Register Alt+R for region video
        public void RegisterAltR(Action callback)
        {
            Register(ID_REGION_VIDEO, MOD_ALT, 0x52, callback); // 0x52 = R
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
            foreach (var id in _callbacks.Keys)
                UnregisterHotKey(_windowHandle, id);

            _source?.RemoveHook(HwndHook);
            _callbacks.Clear();
        }
    }
}
