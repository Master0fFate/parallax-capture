namespace Parallax.Core.Hotkeys;

public enum HotkeyAction
{
    RegionScreenshot,
    FullscreenScreenshot,
    RegionRecording
}

public sealed record HotkeyBinding(HotkeyAction Action, string Name, bool Enabled, string? Gesture, int RegistrationId);

public sealed record ParsedHotkey(uint Modifiers, uint VirtualKey, string DisplayText, bool Disabled);

public enum PlannedHotkeyState
{
    Registered,
    Disabled,
    Invalid,
    Conflict,
    Unsupported
}

public sealed record PlannedHotkey(
    HotkeyAction Action,
    string Name,
    int RegistrationId,
    string Gesture,
    string DisplayText,
    PlannedHotkeyState State,
    string Message)
{
    public bool ShouldRegister => State == PlannedHotkeyState.Registered;
}
