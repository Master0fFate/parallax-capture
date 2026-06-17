using System.Diagnostics;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Parallax.App.Avalonia.Capture;
using Parallax.App.Avalonia.Settings;
using Parallax.Core.Capture;
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
    private readonly IScreenshotWorkflowRunner _screenshotWorkflow;
    private readonly AppLifecycleCoordinator _coordinator;
    private readonly string _executablePath;
    private readonly ShellFeatureSet _features;
    private ParallaxSettings _settings;
    private Window? _settingsWindow;
    private Window? _statusWindow;

    public AvaloniaShellCommandHandler(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPlatformBackend platform,
        ParallaxSettings settings,
        JsonSettingsStore settingsStore,
        RuntimeSettingsApplier settingsApplier,
        IScreenshotWorkflowRunner screenshotWorkflow,
        AppLifecycleCoordinator coordinator,
        string executablePath,
        ShellFeatureSet? features = null)
    {
        _desktop = desktop;
        _platform = platform;
        _settings = settings;
        _settingsStore = settingsStore;
        _settingsApplier = settingsApplier;
        _screenshotWorkflow = screenshotWorkflow;
        _coordinator = coordinator;
        _executablePath = executablePath;
        _features = features ?? ShellFeatureSet.All;
    }

    public IReadOnlyList<ShellActionId> ExecutedActions => _executedActions;

    private readonly List<ShellActionId> _executedActions = [];

    public void Execute(ShellActionId action)
    {
        if (!_features.Supports(action))
        {
            return;
        }

        _executedActions.Add(action);
        switch (action)
        {
            case ShellActionId.RegionScreenshot:
                RunScreenshot("Capture region", () => _screenshotWorkflow.CaptureRegion(_settings, _platform.Locations));
                break;
            case ShellActionId.FullScreenshot:
                RunScreenshot("Capture full screen", () => _screenshotWorkflow.CaptureFullScreen(_settings, _platform.Locations));
                break;
            case ShellActionId.RecordRegion:
                break;
            case ShellActionId.StopRecording:
                _coordinator.SetRecordingState(false);
                _coordinator.RefreshSurface(_settings);
                break;
            case ShellActionId.OpenVideoEditor:
                break;
            case ShellActionId.OpenImageEditor:
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

    private void RunScreenshot(string title, Func<ScreenshotWorkflowResult> capture)
    {
        var capability = _platform.Capabilities.ScreenCapture;
        if (capability.State != CapabilityState.Supported)
        {
            ShowStatus(title, capability.Message);
            return;
        }

        var result = capture();
        if (result.Cancelled)
        {
            return;
        }

        if (!result.Success || (!result.ClipboardCopied && !result.EditorOpened && string.IsNullOrWhiteSpace(result.SavedPath)))
        {
            ShowStatus(title, FormatScreenshotResult(result));
        }
    }

    private static string FormatScreenshotResult(ScreenshotWorkflowResult result)
    {
        if (!result.Success)
        {
            return result.Message;
        }

        var details = new List<string> { result.Message };
        if (!string.IsNullOrWhiteSpace(result.SavedPath))
        {
            details.Add($"Saved: {result.SavedPath}");
        }

        if (result.ClipboardCopied)
        {
            details.Add("Copied to clipboard.");
        }

        if (result.EditorOpened)
        {
            details.Add("Opened in annotation editor.");
        }

        return string.Join(Environment.NewLine, details);
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
