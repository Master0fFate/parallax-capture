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
                    "parallax - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"UI thread exception: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "parallax - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            // 1. Load settings
            _settingsService   = new SettingsService();
            _settings          = _settingsService.Load();

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

            bool printScreenOk = _hotkeyManager.RegisterPrintScreen(() =>
                _trayManager.TriggerRegionScreenshot()
            );

            bool altPrintScreenOk = _hotkeyManager.RegisterAltPrintScreen(() =>
                _trayManager.TriggerFullScreenshot()
            );

            bool altROk = _hotkeyManager.RegisterAltR(() =>
            {
                if (_recorderService.IsRecording)
                    _trayManager.StopRecording();
                else
                    _trayManager.TriggerRegionVideo();
            });

            // KAM #10 — check hotkey registration and notify user if any failed
            if (!printScreenOk)
                System.Windows.MessageBox.Show(
                    "Hotkey Print Screen could not be registered. It may be in use by another application.\n\n" +
                    "Region screenshot via Print Screen will not work until you close the conflicting application.",
                    "parallax - Hotkey Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

            if (!altPrintScreenOk)
                System.Windows.MessageBox.Show(
                    "Hotkey Alt+Print Screen could not be registered. It may be in use by another application.\n\n" +
                    "Full screenshot via Alt+Print Screen will not work.",
                    "parallax - Hotkey Warning",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

            if (!altROk)
                System.Windows.MessageBox.Show(
                    "Hotkey Alt+R could not be registered. It may be in use by another application.\n\n" +
                    "Region video recording via Alt+R will not work.",
                    "parallax - Hotkey Warning",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

            // 6. Show welcome balloon — note which hotkeys are actually active
            string balloonMsg = printScreenOk
                ? "Press Print Screen to capture a region."
                : "Some hotkeys could not be registered — check the warning message.";
            _trayManager.ShowBalloon("parallax is running", balloonMsg);
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
