using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;

namespace Parallax.Core.Shell;

public static class TraySurfaceBuilder
{
    public static TraySurfaceModel Build(
        IPlatformInfo platform,
        PlatformCapabilitySet capabilities,
        ShellRuntimeState state,
        IReadOnlyList<PlannedHotkey> hotkeys)
    {
        var hotkeyByAction = hotkeys.ToDictionary(item => item.Action);
        string tooltip = state.IsRecording
            ? "Parallax Capture is recording"
            : "Parallax Capture";

        string activationHint = platform.Kind switch
        {
            PlatformKind.MacOS => "Use the menu bar status item to open capture actions, settings, and quit.",
            PlatformKind.Linux => state.TrayAvailable
                ? "Use the desktop status item or fallback control window to open capture actions, settings, and quit."
                : "Tray/status item support is unavailable in this session. Use the fallback control window.",
            _ => "Left-click or right-click the tray icon to open capture actions, settings, and quit."
        };

        string? fallbackMessage = state.TrayAvailable
            ? null
            : "Tray/status APIs are unavailable. The fallback control surface exposes the same capture, editor, settings, folder, and quit actions.";

        var features = state.Features ?? ShellFeatureSet.All;
        var items = new List<TrayMenuEntry>
        {
            BuildAction(ShellActionId.RegionScreenshot, "Capture region", HotkeyAction.RegionScreenshot, capabilities.ScreenCapture, hotkeyByAction, isVisible: features.RegionScreenshot),
            BuildAction(ShellActionId.FullScreenshot, "Capture full screen", HotkeyAction.FullscreenScreenshot, capabilities.ScreenCapture, hotkeyByAction, isVisible: features.FullScreenshot),
            BuildAction(
                ShellActionId.RecordRegion,
                "Record region",
                HotkeyAction.RegionRecording,
                capabilities.ScreenRecording,
                hotkeyByAction,
                isVisible: features.RegionRecording && !state.IsRecording,
                forceDisabled: state.HasActiveVideoEditor,
                disabledStatus: "Close the active video editor before starting a recording."),
            new(
                ShellActionId.StopRecording,
                "Stop recording",
                IsEnabled: features.RegionRecording && state.IsRecording,
                IsVisible: features.RegionRecording && state.IsRecording,
                Status: state.IsRecording ? "Recording is active." : "Recording is not active."),
            new(ShellActionId.OpenVideoEditor, "Open video editor", IsEnabled: features.VideoEditor && !state.IsRecording && !state.HasActiveVideoEditor, IsVisible: features.VideoEditor),
            new(ShellActionId.OpenImageEditor, "Open image editor", IsEnabled: features.ImageEditor && !state.IsRecording, IsVisible: features.ImageEditor),
            new(ShellActionId.OpenSaveFolder, "Open save folder", IsEnabled: true, IsVisible: true),
            new(ShellActionId.Settings, "Settings", IsEnabled: true, IsVisible: true),
            new(ShellActionId.Quit, "Quit Parallax Capture", IsEnabled: true, IsVisible: true)
        };

        return new TraySurfaceModel(state.TrayAvailable, tooltip, activationHint, fallbackMessage, items);
    }

    private static TrayMenuEntry BuildAction(
        ShellActionId action,
        string label,
        HotkeyAction hotkeyAction,
        CapabilityResult capability,
        IReadOnlyDictionary<HotkeyAction, PlannedHotkey> hotkeys,
        bool isVisible = true,
        bool forceDisabled = false,
        string? disabledStatus = null)
    {
        hotkeys.TryGetValue(hotkeyAction, out var hotkey);
        string formattedLabel = hotkey == null
            ? label
            : $"{label} ({FormatHotkeyLabel(hotkey)})";

        bool enabled = !forceDisabled
            && capability.State is CapabilityState.Supported or CapabilityState.RequiresPermission or CapabilityState.RequiresUserMediation;
        return new TrayMenuEntry(action, formattedLabel, enabled, isVisible, enabled ? hotkey?.Message : disabledStatus ?? capability.Message);
    }

    private static string FormatHotkeyLabel(PlannedHotkey hotkey)
    {
        return hotkey.State switch
        {
            PlannedHotkeyState.Registered => hotkey.DisplayText,
            PlannedHotkeyState.Disabled => "shortcut disabled",
            PlannedHotkeyState.Invalid => "shortcut invalid",
            PlannedHotkeyState.Conflict => $"shortcut conflict: {hotkey.DisplayText}",
            PlannedHotkeyState.Unsupported => hotkey.DisplayText,
            _ => hotkey.DisplayText
        };
    }
}
