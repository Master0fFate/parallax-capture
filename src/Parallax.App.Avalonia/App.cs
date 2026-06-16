using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Parallax.App.Avalonia.Shell;
using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;
using Parallax.Platform.Linux;
using Parallax.Platform.Mac;
using Parallax.Platform.Windows;

namespace Parallax.App.Avalonia;

public sealed class App : Application
{
    private AppLifecycleCoordinator? _coordinator;

    public override void Initialize()
    {
        Styles.Add(new global::Avalonia.Themes.Fluent.FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _coordinator?.Quit();

            IPlatformBackend platform = CreatePlatformBackend();
            var store = new JsonSettingsStore(platform.Locations);
            var settings = store.Load();

            var tray = new AvaloniaTrayService();
            var hotkeys = new UnsupportedHotkeyService(platform.Capabilities.GlobalHotkeys);
            _coordinator = new AppLifecycleCoordinator(platform, tray, hotkeys);
            var surface = _coordinator.StartTrayFirst(settings);

            if (surface.MainWindowVisibleAtStartup)
            {
                desktop.MainWindow = new FallbackControlWindow(surface);
                desktop.MainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IPlatformBackend CreatePlatformBackend()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsPlatformBackend.CreateCurrentUser();
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return MacPlatformBackend.CreateForUserHome(home);
        }

        return LinuxPlatformBackend.CreateForUserHome(
            home,
            xdgConfigHome: Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            xdgDataHome: Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            xdgStateHome: Environment.GetEnvironmentVariable("XDG_STATE_HOME"));
    }

    private sealed class UnsupportedHotkeyService : IGlobalHotkeyService
    {
        public UnsupportedHotkeyService(CapabilityResult capability)
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
