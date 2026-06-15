using System.Windows;
using parallax.Core.Models;
using parallax.Core.Services;
using parallax.Tray;

namespace parallax
{
    public partial class App : Application
    {
        // ── All services are instantiated here and live for the app lifetime
        private SettingsService?  _settingsService;
        private AppSettings?      _settings;
        private ScreenshotService? _screenshotService;
        private RecorderService?  _recorderService;
        private ClipboardService? _clipboardService;
        private FileService?      _fileService;
        private TrayIconManager?  _trayManager;
        private HotkeyManager?    _hotkeyManager;

        // ── Hidden background window needed to receive hotkey messages
        // (HotkeyManager needs a real HWND — a hidden Window provides this)
        private Window? _backgroundWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception handlers — prevents silent process death
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show(
                    $"Unhandled exception: {ex?.Message}\n\n{ex?.StackTrace}",
                    "Parallax Capture - Fatal error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"UI thread exception: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "Parallax Capture - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            // 1. Load settings
            _settingsService   = new SettingsService();
            _settings          = _settingsService.Load();
            AppThemeService.Apply(_settings);

            // 2. Instantiate all services
            _screenshotService = new ScreenshotService();
            _recorderService   = new RecorderService();
            _clipboardService  = new ClipboardService();
            _fileService       = new FileService(_settings);

            // 3. Initialize tray icon
            _trayManager = new TrayIconManager(
                _screenshotService,
                _recorderService,
                _clipboardService,
                _fileService,
                _settingsService,
                _settings
            );
            _trayManager.SettingsChanged += OnSettingsChanged;
            _trayManager.Initialize();

            // 4. Create hidden background window for hotkey HWND
            _backgroundWindow = new Window
            {
                Width = 0, Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent
            };
            _backgroundWindow.Show(); // Must show to get HWND

            // 5. Register global hotkeys
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.Initialize(_backgroundWindow);
            var warnings = RegisterConfiguredHotkeys(showWarnings: true);

            // 6. Show welcome balloon — note which hotkeys are actually active
            string balloonMsg = warnings.Count == 0
                ? BuildHotkeyStatusMessage()
                : "Some enabled shortcuts could not be registered. Open Settings > Hotkeys to change them.";
            _trayManager.ShowBalloon("Parallax Capture is running", balloonMsg);
        }

        private void OnSettingsChanged(AppSettings settings)
        {
            _settings = settings;
            AppThemeService.Apply(_settings);
            RegisterConfiguredHotkeys(showWarnings: true);
        }

        private List<string> RegisterConfiguredHotkeys(bool showWarnings)
        {
            var warnings = new List<string>();
            if (_hotkeyManager == null || _trayManager == null || _settings == null)
                return warnings;

            _hotkeyManager.UnregisterAll();

            RegisterHotkey(
                "Capture region",
                HotkeyManager.ID_REGION_SCREENSHOT,
                _settings.HotkeyScreenshotEnabled,
                _settings.HotkeyScreenshot,
                () => _trayManager.TriggerRegionScreenshot(),
                warnings);

            RegisterHotkey(
                "Capture full screen",
                HotkeyManager.ID_FULLSCREEN,
                _settings.HotkeyFullscreenEnabled,
                _settings.HotkeyFullscreen,
                () => _trayManager.TriggerFullScreenshot(),
                warnings);

            RegisterHotkey(
                "Start or stop recording",
                HotkeyManager.ID_REGION_VIDEO,
                _settings.HotkeyRegionVideoEnabled,
                _settings.HotkeyRegionVideo,
                () =>
                {
                    if (_recorderService?.IsRecording == true)
                        _trayManager.StopRecording();
                    else
                        _trayManager.TriggerRegionVideo();
                },
                warnings);

            if (showWarnings && warnings.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    string.Join("\n\n", warnings),
                    "Parallax Capture - Shortcut warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return warnings;
        }

        private void RegisterHotkey(
            string actionName,
            int id,
            bool enabled,
            string? gesture,
            Action callback,
            List<string> warnings)
        {
            if (_hotkeyManager == null)
                return;

            if (!_hotkeyManager.RegisterConfigured(id, enabled, gesture, callback, out string message))
                warnings.Add($"{actionName}: {message}");
        }

        private string BuildHotkeyStatusMessage()
        {
            if (_settings == null)
                return "Parallax Capture shortcuts are ready.";

            return "Shortcuts: " +
                $"capture region {FormatHotkey(_settings.HotkeyScreenshotEnabled, _settings.HotkeyScreenshot)}, " +
                $"capture full screen {FormatHotkey(_settings.HotkeyFullscreenEnabled, _settings.HotkeyFullscreen)}, " +
                $"start or stop recording {FormatHotkey(_settings.HotkeyRegionVideoEnabled, _settings.HotkeyRegionVideo)}.";
        }

        private static string FormatHotkey(bool enabled, string? gesture)
        {
            string display = HotkeyManager.FormatForDisplay(enabled, gesture);
            return display == "disabled" ? "disabled" : display;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _hotkeyManager?.Dispose(); } catch { }
            try { _recorderService?.Dispose(); } catch { }
            try { _trayManager?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}
