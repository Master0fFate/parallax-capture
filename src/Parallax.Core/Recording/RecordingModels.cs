using Parallax.Core.Capture;
using Parallax.Core.Platform;

namespace Parallax.Core.Recording;

public enum RecordingFailureKind
{
    Cancelled,
    PermissionDenied,
    RequiresUserMediation,
    Unsupported,
    Failed
}

public enum RecordingStopSource
{
    Hud,
    Tray,
    Hotkey,
    Shutdown
}

public enum CaptureExclusionTarget
{
    RecordingHud,
    RecordingBorder
}

public enum RecordingTopologyKind
{
    DirectRegionCapture,
    PermissionRequired,
    PortalPickerRequired,
    X11Fallback,
    Unsupported
}

public sealed record AudioCaptureCapability(CapabilityState State, string Message)
{
    public bool IsDegraded => State != CapabilityState.Supported;

    public static AudioCaptureCapability Supported(string message) => new(CapabilityState.Supported, message);

    public static AudioCaptureCapability RequiresPermission(string message) => new(CapabilityState.RequiresPermission, message);

    public static AudioCaptureCapability RequiresUserMediation(string message) => new(CapabilityState.RequiresUserMediation, message);

    public static AudioCaptureCapability Unsupported(string message) => new(CapabilityState.Unsupported, message);
}

public sealed record RecordingStartRequest(
    CaptureRectangle Region,
    string OutputPath,
    bool IncludeSystemAudio);

public sealed record RecordingStartResult(
    bool Success,
    string? SessionId,
    string? OutputPath,
    RecordingFailureKind? FailureKind,
    AudioCaptureCapability Audio,
    string Message)
{
    public static RecordingStartResult Started(
        string sessionId,
        string outputPath,
        AudioCaptureCapability audio,
        string message)
    {
        return new RecordingStartResult(true, sessionId, outputPath, null, audio, message);
    }

    public static RecordingStartResult Failed(
        RecordingFailureKind kind,
        AudioCaptureCapability audio,
        string message)
    {
        return new RecordingStartResult(false, null, null, kind, audio, message);
    }
}

public sealed record RecordingStopResult(
    bool Success,
    string? OutputPath,
    TimeSpan Duration,
    RecordingFailureKind? FailureKind,
    bool PartialOutputPreserved,
    string Message)
{
    public static RecordingStopResult Stopped(string outputPath, TimeSpan duration, string message)
    {
        return new RecordingStopResult(true, outputPath, duration, null, PartialOutputPreserved: false, message);
    }

    public static RecordingStopResult Failed(
        RecordingFailureKind kind,
        string? partialOutputPath,
        bool partialOutputPreserved,
        string message)
    {
        return new RecordingStopResult(false, partialOutputPath, TimeSpan.Zero, kind, partialOutputPreserved, message);
    }
}

public sealed record CaptureExclusionResult(
    bool Requested,
    CapabilityState State,
    string Message)
{
    public static CaptureExclusionResult Supported(string message)
    {
        return new CaptureExclusionResult(true, CapabilityState.Supported, message);
    }

    public static CaptureExclusionResult Unsupported(string message)
    {
        return new CaptureExclusionResult(false, CapabilityState.Unsupported, message);
    }
}

public sealed record RecordingHudState(
    bool IsVisible,
    bool BorderVisible,
    bool StopEnabled,
    string ElapsedLabel,
    CaptureExclusionResult HudExclusion,
    CaptureExclusionResult BorderExclusion,
    string StatusMessage);

public sealed record FFmpegAvailability(
    bool IsAvailable,
    string? ExecutablePath,
    string Message)
{
    public static FFmpegAvailability Available(string executablePath)
    {
        return new FFmpegAvailability(true, executablePath, $"FFmpeg is available at {executablePath}.");
    }

    public static FFmpegAvailability Missing(string message)
    {
        return new FFmpegAvailability(false, null, message);
    }
}

public sealed record FFmpegRunRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string OutputPath,
    TimeSpan Timeout);

public sealed record FFmpegRunResult(
    bool Success,
    string Message,
    string? OutputPath);

public sealed record VideoSaveResult(
    bool Success,
    string? SavedPath,
    bool SourcePreserved,
    bool UsedSourceFallback,
    string Message);

public sealed record VideoEditorLaunchResult(bool Success, string Message)
{
    public static VideoEditorLaunchResult Opened(string message) => new(true, message);

    public static VideoEditorLaunchResult Failed(string message) => new(false, message);
}

public interface IVideoEditorLauncher
{
    VideoEditorLaunchResult Open(string videoPath);
}

public sealed record RecordingCompletionResult(
    bool Success,
    string? SavedPath,
    bool VideoEditorOpened,
    bool SourcePreserved,
    bool UsedSourceFallback,
    string Message);

public sealed record RecordingStartWorkflowResult(
    bool Success,
    RecordingFailureKind? FailureKind,
    RecordingHudState? Hud,
    string? TempOutputPath,
    string Message);

public sealed record RecordingStopWorkflowResult(
    bool Success,
    RecordingStopSource Source,
    RecordingCompletionResult? Completion,
    RecordingFailureKind? FailureKind,
    bool PartialOutputPreserved,
    string Message);

public sealed record RecordingTopology(
    PlatformKind Platform,
    RecordingTopologyKind Kind,
    bool RequiresPermission,
    bool RequiresPicker,
    string Message);
