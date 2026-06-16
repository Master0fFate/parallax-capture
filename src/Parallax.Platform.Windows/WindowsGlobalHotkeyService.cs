using System.ComponentModel;
using System.Runtime.InteropServices;
using Parallax.Core.Platform;

namespace Parallax.Platform.Windows;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly Dictionary<int, Action> _callbacks = [];
    private readonly Dictionary<int, string> _displayTexts = [];
    private readonly string _className = $"ParallaxCaptureHotkeys_{Guid.NewGuid():N}";
    private readonly WndProc _wndProc;
    private readonly IntPtr _instance;
    private IntPtr _windowHandle;
    private bool _registeredClass;
    private bool _disposed;

    private WindowsGlobalHotkeyService()
    {
        _wndProc = WindowProcedure;
        _instance = GetModuleHandle(null);
        InitializeMessageWindow();
    }

    public CapabilityResult Capability { get; private set; } =
        CapabilityResult.Supported("Win32 global hotkeys are supported on Windows.");

    public static IGlobalHotkeyService CreateForCurrentThread()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new UnsupportedGlobalHotkeyService(
                CapabilityResult.Unsupported("Win32 global hotkeys are only available on Windows."));
        }

        try
        {
            return new WindowsGlobalHotkeyService();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new UnsupportedGlobalHotkeyService(
                CapabilityResult.Unsupported($"Win32 global hotkeys could not be initialized: {ex.Message}"));
        }
    }

    public HotkeyRegistrationResult Register(
        int id,
        uint modifiers,
        uint virtualKey,
        string displayText,
        Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_windowHandle == IntPtr.Zero)
        {
            return new HotkeyRegistrationResult(
                HotkeyRegistrationResultState.Unsupported,
                displayText,
                Capability.Message);
        }

        if (_callbacks.ContainsKey(id))
        {
            UnregisterHotKey(_windowHandle, id);
            _callbacks.Remove(id);
            _displayTexts.Remove(id);
        }

        if (!RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
        {
            int error = Marshal.GetLastWin32Error();
            return new HotkeyRegistrationResult(
                HotkeyRegistrationResultState.Conflict,
                displayText,
                $"\"{displayText}\" could not be registered by Windows. Choose another shortcut or turn this one off. Win32 error {error}.");
        }

        _callbacks[id] = callback;
        _displayTexts[id] = displayText;
        return new HotkeyRegistrationResult(
            HotkeyRegistrationResultState.Registered,
            displayText,
            $"Registered {displayText}.");
    }

    public void UnregisterAll()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            foreach (int id in _callbacks.Keys.ToArray())
            {
                UnregisterHotKey(_windowHandle, id);
            }
        }

        _callbacks.Clear();
        _displayTexts.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        if (_windowHandle != IntPtr.Zero)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        if (_registeredClass)
        {
            UnregisterClass(_className, _instance);
            _registeredClass = false;
        }

        _disposed = true;
    }

    private void InitializeMessageWindow()
    {
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = _wndProc,
            Instance = _instance,
            ClassName = _className
        };

        ushort atom = RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassEx failed.");
        }

        _registeredClass = true;
        _windowHandle = CreateWindowEx(
            0,
            _className,
            "Parallax Capture Hotkeys",
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            _instance,
            IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed.");
        }
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                callback.Invoke();
                return IntPtr.Zero;
            }
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WndProc WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr IconSmall;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int exStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    private sealed class UnsupportedGlobalHotkeyService : IGlobalHotkeyService
    {
        public UnsupportedGlobalHotkeyService(CapabilityResult capability)
        {
            Capability = capability;
        }

        public CapabilityResult Capability { get; }

        public HotkeyRegistrationResult Register(int id, uint modifiers, uint virtualKey, string displayText, Action callback)
        {
            return new HotkeyRegistrationResult(HotkeyRegistrationResultState.Unsupported, displayText, Capability.Message);
        }

        public void UnregisterAll()
        {
        }

        public void Dispose()
        {
        }
    }
}
