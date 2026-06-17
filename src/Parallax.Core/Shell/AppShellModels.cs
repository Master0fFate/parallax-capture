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

public sealed record ShellFeatureSet(
    bool RegionScreenshot = true,
    bool FullScreenshot = true,
    bool RegionRecording = true,
    bool VideoEditor = true,
    bool ImageEditor = true)
{
    public static ShellFeatureSet All { get; } = new();

    public bool Supports(ShellActionId action)
    {
        return action switch
        {
            ShellActionId.RegionScreenshot => RegionScreenshot,
            ShellActionId.FullScreenshot => FullScreenshot,
            ShellActionId.RecordRegion or ShellActionId.StopRecording => RegionRecording,
            ShellActionId.OpenVideoEditor => VideoEditor,
            ShellActionId.OpenImageEditor => ImageEditor,
            _ => true
        };
    }
}

public sealed record ShellRuntimeState(
    bool IsRecording,
    bool TrayAvailable,
    bool HasActiveVideoEditor = false,
    ShellFeatureSet? Features = null);

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
