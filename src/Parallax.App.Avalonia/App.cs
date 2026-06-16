using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Parallax.App.Avalonia.Shell;
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
            var themeSettings = AvaloniaRuntimeServices.CreateThemeSettingsService(this);
            themeSettings.Preview(settings.ThemeFamily, settings.ThemeMode);
            var hotkeys = AvaloniaRuntimeServices.CreateHotkeyService(platform);
            var startup = AvaloniaRuntimeServices.CreateStartupService(platform);
            var runtimeSettings = new RuntimeSettingsApplier(
                platform,
                store,
                hotkeys,
                startup,
                themeSettings);

            var tray = new AvaloniaTrayService();
            AvaloniaShellCommandHandler? commandHandler = null;
            _coordinator = new AppLifecycleCoordinator(platform, tray, hotkeys, action => commandHandler?.Execute(action));
            commandHandler = new AvaloniaShellCommandHandler(
                desktop,
                platform,
                settings,
                store,
                runtimeSettings,
                themeSettings,
                _coordinator,
                GetExecutablePath());
            var surface = _coordinator.StartTrayFirst(settings);

            if (surface.MainWindowVisibleAtStartup)
            {
                desktop.MainWindow = new FallbackControlWindow(surface, action => commandHandler.Execute(action));
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

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Environment.GetCommandLineArgs().FirstOrDefault()
            ?? AppContext.BaseDirectory;
    }
}
