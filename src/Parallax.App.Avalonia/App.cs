using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Parallax.App.Avalonia.Shell;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_LINUX
using Parallax.Platform.Linux;
#endif
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_MACOS
using Parallax.Platform.Mac;
#endif
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
using Parallax.Platform.Windows;
#endif

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
            var features = AvaloniaRuntimeServices.CreateShellFeatureSet(platform);
            var hotkeys = AvaloniaRuntimeServices.CreateHotkeyService(platform);
            var startup = AvaloniaRuntimeServices.CreateStartupService(platform);
            var screenshots = AvaloniaRuntimeServices.CreateScreenshotWorkflowRunner(platform, settings);
            var tray = new AvaloniaTrayService();
            AvaloniaShellCommandHandler? commandHandler = null;
            _coordinator = new AppLifecycleCoordinator(platform, tray, hotkeys, action => commandHandler?.Execute(action), features);
            var runtimeSettings = new RuntimeSettingsApplier(
                platform,
                store,
                hotkeys,
                startup,
                _coordinator.CreateHotkeyCallback,
                _coordinator.SupportsHotkey,
                _coordinator.CreateHotkeyReleaseCallback);
            commandHandler = new AvaloniaShellCommandHandler(
                desktop,
                platform,
                settings,
                store,
                runtimeSettings,
                screenshots,
                _coordinator,
                GetExecutablePath(),
                features);
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
#if PARALLAX_TARGET_WINDOWS
        return WindowsPlatformBackend.CreateCurrentUser();
#elif PARALLAX_TARGET_MACOS
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return MacPlatformBackend.CreateForUserHome(home);
#elif PARALLAX_TARGET_LINUX
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return LinuxPlatformBackend.CreateForUserHome(
            home,
            xdgConfigHome: Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            xdgDataHome: Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            xdgStateHome: Environment.GetEnvironmentVariable("XDG_STATE_HOME"));
#else
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
#endif
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Environment.GetCommandLineArgs().FirstOrDefault()
            ?? AppContext.BaseDirectory;
    }
}
