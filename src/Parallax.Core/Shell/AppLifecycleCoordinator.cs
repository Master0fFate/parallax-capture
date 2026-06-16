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
    private readonly List<IAppLifecycleResource> _resources = [];
    private bool _isRunning;

    public AppLifecycleCoordinator(
        IPlatformBackend platform,
        ITrayService trayService,
        IGlobalHotkeyService hotkeyService)
    {
        _platform = platform;
        _trayService = trayService;
        _hotkeyService = hotkeyService;
    }

    public bool IsRunning => _isRunning;

    public bool ShutdownRequested { get; private set; }

    public IReadOnlyList<string> CleanupLog { get; private set; } = [];

    public TraySurfaceModel StartTrayFirst(ParallaxSettings settings, bool isRecording = false)
    {
        _isRunning = true;
        var hotkeys = HotkeyPlanner.Plan(settings, _hotkeyService.Capability);
        foreach (var hotkey in hotkeys.Where(item => item.ShouldRegister))
        {
            if (!HotkeyParser.TryParse(hotkey.Gesture, out var parsed, out _))
            {
                continue;
            }

            _hotkeyService.Register(hotkey.RegistrationId, parsed.Modifiers, parsed.VirtualKey, hotkey.DisplayText, () => { });
        }

        var state = new ShellRuntimeState(isRecording, _trayService.IsAvailable);
        var surface = TraySurfaceBuilder.Build(_platform.Info, _platform.Capabilities, state, hotkeys);
        _trayService.SetMenu(surface.MenuItems.Select(item => new TrayMenuItem(
            item.Action.ToString(),
            item.Label,
            item.IsEnabled,
            item.IsVisible,
            item.Status)).ToArray());
        _trayService.SetToolTip(surface.Tooltip);
        return surface;
    }

    public void TrackResource(IAppLifecycleResource resource)
    {
        _resources.Add(resource);
    }

    public void Quit()
    {
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
}
