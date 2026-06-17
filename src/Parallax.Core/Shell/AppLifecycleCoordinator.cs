using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Shell;

public interface IAppLifecycleResource
{
    string Name { get; }

    void Stop();
}

public sealed class AppLifecycleCoordinator
{
    private readonly IPlatformBackend _platform;
    private readonly ITrayService _trayService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly Action<ShellActionId>? _executeAction;
    private readonly ShellFeatureSet _features;
    private readonly List<IAppLifecycleResource> _resources = [];
    private bool _isRunning;
    private bool _isRecording;
    private bool _hasActiveVideoEditor;

    public AppLifecycleCoordinator(
        IPlatformBackend platform,
        ITrayService trayService,
        IGlobalHotkeyService hotkeyService,
        Action<ShellActionId>? executeAction = null,
        ShellFeatureSet? features = null)
    {
        _platform = platform;
        _trayService = trayService;
        _hotkeyService = hotkeyService;
        _executeAction = executeAction;
        _features = features ?? ShellFeatureSet.All;
    }

    public bool IsRunning => _isRunning;

    public bool ShutdownRequested { get; private set; }

    public IReadOnlyList<string> CleanupLog { get; private set; } = [];

    public TraySurfaceModel StartTrayFirst(ParallaxSettings settings, bool isRecording = false)
    {
        _isRunning = true;
        _isRecording = isRecording;
        var hotkeys = HotkeyPlanner.Plan(settings, _hotkeyService.Capability);
        foreach (var hotkey in hotkeys.Where(item => item.ShouldRegister && SupportsHotkey(item.Action)))
        {
            if (!HotkeyParser.TryParse(hotkey.Gesture, out var parsed, out _))
            {
                continue;
            }

            _hotkeyService.Register(
                hotkey.RegistrationId,
                parsed.Modifiers,
                parsed.VirtualKey,
                hotkey.DisplayText,
                CreateHotkeyCallback(hotkey.Action));
        }

        return RefreshSurface(settings, hotkeys);
    }

    public Action CreateHotkeyCallback(HotkeyAction action)
    {
        return () =>
        {
            var shellAction = action == HotkeyAction.RegionRecording && _isRecording
                ? ShellActionId.StopRecording
                : MapHotkeyAction(action);
            if (_features.Supports(shellAction))
            {
                Execute(shellAction);
            }
        };
    }

    public bool SupportsHotkey(HotkeyAction action)
    {
        return _features.Supports(MapHotkeyAction(action));
    }

    public TraySurfaceModel RefreshSurface(
        ParallaxSettings settings,
        IReadOnlyList<PlannedHotkey>? plannedHotkeys = null)
    {
        var hotkeys = plannedHotkeys ?? HotkeyPlanner.Plan(settings, _hotkeyService.Capability);
        var state = new ShellRuntimeState(_isRecording, _trayService.IsAvailable, _hasActiveVideoEditor, _features);
        var surface = TraySurfaceBuilder.Build(_platform.Info, _platform.Capabilities, state, hotkeys);
        _trayService.SetMenu(surface.MenuItems.Select(item => new TrayMenuItem(
            item.Action.ToString(),
            item.Label,
            item.IsEnabled,
            item.IsVisible,
            item.Status,
            () => Execute(item.Action))).ToArray());
        _trayService.SetToolTip(surface.Tooltip);
        return surface;
    }

    public void SetRecordingState(bool isRecording)
    {
        _isRecording = isRecording;
    }

    public void SetVideoEditorActive(bool isActive)
    {
        _hasActiveVideoEditor = isActive;
    }

    public void TrackResource(IAppLifecycleResource resource)
    {
        _resources.Add(resource);
    }

    public void Quit()
    {
        if (ShutdownRequested && !_isRunning)
        {
            return;
        }

        var cleanup = new List<string>();
        ShutdownRequested = true;

        try
        {
            _hotkeyService.UnregisterAll();
            cleanup.Add("hotkeys");
        }
        catch (Exception ex)
        {
            cleanup.Add($"hotkeys failed: {ex.Message}");
        }

        foreach (var resource in _resources.AsEnumerable().Reverse())
        {
            try
            {
                resource.Stop();
                cleanup.Add(resource.Name);
            }
            catch (Exception ex)
            {
                cleanup.Add($"{resource.Name} failed: {ex.Message}");
            }
        }

        try
        {
            _trayService.Dispose();
            cleanup.Add("tray");
        }
        catch (Exception ex)
        {
            cleanup.Add($"tray failed: {ex.Message}");
        }

        try
        {
            _hotkeyService.Dispose();
            cleanup.Add("hotkey service");
        }
        catch (Exception ex)
        {
            cleanup.Add($"hotkey service failed: {ex.Message}");
        }

        _isRunning = false;
        CleanupLog = cleanup;
    }

    private void Execute(ShellActionId action)
    {
        if (!_features.Supports(action))
        {
            return;
        }

        if (action == ShellActionId.Quit)
        {
            Quit();
            _executeAction?.Invoke(action);
            return;
        }

        _executeAction?.Invoke(action);
    }

    private static ShellActionId MapHotkeyAction(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.RegionScreenshot => ShellActionId.RegionScreenshot,
            HotkeyAction.FullscreenScreenshot => ShellActionId.FullScreenshot,
            HotkeyAction.RegionRecording => ShellActionId.RecordRegion,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported hotkey action.")
        };
    }
}
