namespace Parallax.Core.Shell;

public enum ShellActionId
{
    RegionScreenshot,
    FullScreenshot,
    RecordRegion,
    StopRecording,
    OpenVideoEditor,
    OpenImageEditor,
    OpenSaveFolder,
    Settings,
    Quit
}

public sealed record ShellRuntimeState(
    bool IsRecording,
    bool TrayAvailable,
    bool HasActiveVideoEditor = false);

public sealed record TrayMenuEntry(
    ShellActionId Action,
    string Label,
    bool IsEnabled,
    bool IsVisible,
    string? Status = null);

public sealed record TraySurfaceModel(
    bool TrayAvailable,
    string Tooltip,
    string ActivationHint,
    string? FallbackMessage,
    IReadOnlyList<TrayMenuEntry> MenuItems)
{
    public bool MainWindowVisibleAtStartup => !TrayAvailable;
}
