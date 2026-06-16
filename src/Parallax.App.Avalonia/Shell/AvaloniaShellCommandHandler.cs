using System.Diagnostics;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Parallax.App.Avalonia.Settings;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;

namespace Parallax.App.Avalonia.Shell;

public sealed class AvaloniaShellCommandHandler
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly IPlatformBackend _platform;
    private readonly JsonSettingsStore _settingsStore;
    private readonly RuntimeSettingsApplier _settingsApplier;
    private readonly ThemeSettingsService _themeSettings;
    private readonly AppLifecycleCoordinator _coordinator;
    private readonly string _executablePath;
    private ParallaxSettings _settings;
    private Window? _settingsWindow;
    private Window? _statusWindow;

    public AvaloniaShellCommandHandler(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPlatformBackend platform,
        ParallaxSettings settings,
        JsonSettingsStore settingsStore,
        RuntimeSettingsApplier settingsApplier,
        ThemeSettingsService themeSettings,
        AppLifecycleCoordinator coordinator,
        string executablePath)
    {
        _desktop = desktop;
        _platform = platform;
        _settings = settings;
        _settingsStore = settingsStore;
        _settingsApplier = settingsApplier;
        _themeSettings = themeSettings;
        _coordinator = coordinator;
        _executablePath = executablePath;
    }

    public IReadOnlyList<ShellActionId> ExecutedActions => _executedActions;

    private readonly List<ShellActionId> _executedActions = [];

    public void Execute(ShellActionId action)
    {
        _executedActions.Add(action);
        switch (action)
        {
            case ShellActionId.RegionScreenshot:
                ShowStatus("Capture region", "The region screenshot command is wired. Capture implementation is provided by the capture milestone.");
                break;
            case ShellActionId.FullScreenshot:
                ShowStatus("Capture full screen", "The full-screen screenshot command is wired. Capture implementation is provided by the capture milestone.");
                break;
            case ShellActionId.RecordRegion:
                ShowStatus("Record region", "The region recording command is wired. Recording implementation is provided by the recording milestone.");
                break;
            case ShellActionId.StopRecording:
                _coordinator.SetRecordingState(false);
                _coordinator.RefreshSurface(_settings);
                ShowStatus("Stop recording", "Stop recording command handled.");
                break;
            case ShellActionId.OpenVideoEditor:
                _coordinator.SetVideoEditorActive(true);
                _coordinator.RefreshSurface(_settings);
                ShowStatus("Video editor", "The video editor command is wired. Media editing implementation is provided by the media milestone.");
                break;
            case ShellActionId.OpenImageEditor:
                ShowStatus("Image editor", "The image editor command is wired. Annotation implementation is provided by the capture/edit milestone.");
                break;
            case ShellActionId.OpenSaveFolder:
                OpenSaveFolder();
                break;
            case ShellActionId.Settings:
                OpenSettings();
                break;
            case ShellActionId.Quit:
                _desktop.Shutdown();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported shell action.");
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _settings,
            _platform,
            _settingsApplier,
            _themeSettings,
            _executablePath,
            result =>
            {
                _settings = _settingsStore.Load();
                _coordinator.RefreshSurface(_settings, result.Hotkeys);
            });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void OpenSaveFolder()
    {
        var service = new OpenSaveFolderService(_platform.Locations, new PlatformFolderLauncher());
        var result = service.Open(_settings);
        ShowStatus(
            result.Success ? "Save folder opened" : "Save folder issue",
            result.Message);
    }

    private void ShowStatus(string title, string message)
    {
        if (_statusWindow != null)
        {
            _statusWindow.Close();
        }

        _statusWindow = new Window
        {
            Title = $"Parallax Capture - {title}",
            Width = 420,
            Height = 180,
            Content = new StackPanel
            {
                Margin = new global::Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                        MinWidth = 80,
                        MinHeight = 32
                    }
                }
            }
        };

        if (_statusWindow.Content is StackPanel panel && panel.Children.LastOrDefault() is Button ok)
        {
            ok.Click += (_, _) => _statusWindow?.Close();
        }

        _statusWindow.Closed += (_, _) =>
        {
            _statusWindow = null;
            _coordinator.SetVideoEditorActive(false);
            _coordinator.RefreshSurface(_settings);
        };
        _statusWindow.Show();
    }

    private sealed class PlatformFolderLauncher : IFolderLauncher
    {
        public FolderLaunchResult OpenFolder(string folderPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return new FolderLaunchResult(true, $"Opened {folderPath}.");
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
            {
                return new FolderLaunchResult(false, $"Could not open the platform file manager: {ex.Message}");
            }
        }
    }
}
