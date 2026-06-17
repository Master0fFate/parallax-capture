using Parallax.Core.Recording;

namespace Parallax.Core.Media;

public enum VideoEditorOpenFailureKind
{
    None,
    AlreadyRecording,
    EditorAlreadyOpen,
    UnsupportedFile,
    UnreadableFile,
    MetadataUnavailable
}

public enum VideoExportKind
{
    TrimmedVideo,
    CurrentFrame,
    Gif
}

public sealed record VideoMetadata(TimeSpan Duration);

public sealed record VideoMetadataResult(bool Success, VideoMetadata? Metadata, string Message)
{
    public static VideoMetadataResult Loaded(TimeSpan duration)
    {
        return new VideoMetadataResult(true, new VideoMetadata(duration), "Video metadata loaded.");
    }

    public static VideoMetadataResult Failed(string message)
    {
        return new VideoMetadataResult(false, null, message);
    }
}

public sealed record VideoTrimRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
}

public sealed record TrimValidationResult(
    bool Success,
    VideoTrimRange? Range,
    string Message)
{
    public static TrimValidationResult Valid(VideoTrimRange range)
    {
        return new TrimValidationResult(true, range, "Trim range is valid.");
    }

    public static TrimValidationResult Invalid(string message)
    {
        return new TrimValidationResult(false, null, message);
    }
}

public sealed record VideoEditorState(
    string SourcePath,
    TimeSpan Duration,
    TimeSpan CurrentTime,
    bool IsPlaying,
    bool IsMuted,
    TimeSpan TrimStart,
    TimeSpan TrimEnd,
    FFmpegAvailability FFmpeg,
    string StatusMessage)
{
    public bool CanPreview => Duration > TimeSpan.Zero;

    public bool CanSaveOriginal => !string.IsNullOrWhiteSpace(SourcePath);

    public bool CanUseFFmpeg => FFmpeg.IsAvailable;

    public bool CanExportTrim => CanUseFFmpeg && VideoTrimPolicy.Validate(TrimStart, TrimEnd, Duration).Success;

    public bool CanSaveFrame => CanUseFFmpeg && Duration > TimeSpan.Zero;

    public bool CanExportGif => CanExportTrim;
}

public sealed record VideoEditorOpenResult(
    bool Success,
    VideoEditorOpenFailureKind FailureKind,
    VideoEditorState? State,
    string Message)
{
    public static VideoEditorOpenResult Opened(VideoEditorState state, string message)
    {
        return new VideoEditorOpenResult(true, VideoEditorOpenFailureKind.None, state, message);
    }

    public static VideoEditorOpenResult Failed(VideoEditorOpenFailureKind failureKind, string message)
    {
        return new VideoEditorOpenResult(false, failureKind, null, message);
    }
}

public sealed record VideoExportResult(
    bool Success,
    VideoExportKind Kind,
    string? OutputPath,
    bool SourcePreserved,
    string Message);

public sealed record FFmpegDiscoveryCandidate(
    string Kind,
    string Path);

public sealed record FFmpegDiscoveryResult(
    bool IsAvailable,
    string? ExecutablePath,
    string SelectedKind,
    IReadOnlyList<FFmpegDiscoveryCandidate> Candidates,
    string Message)
{
    public FFmpegAvailability ToAvailability()
    {
        return IsAvailable && ExecutablePath is not null
            ? FFmpegAvailability.Available(ExecutablePath)
            : FFmpegAvailability.Missing(Message);
    }
}

public sealed record FFmpegDiscoveryRequest(
    Platform.PlatformKind Platform,
    string AppDirectory,
    string ToolsDirectory,
    IReadOnlyList<string> PathDirectories);

public sealed record FFmpegProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardError);

public sealed record FFmpegInstallRequest(
    Uri SourceUri,
    string ToolsDirectory,
    Platform.PlatformKind Platform,
    long MaxDownloadBytes);

public sealed record FFmpegInstallResult(
    bool Success,
    IReadOnlyList<string> InstalledBinaries,
    bool TempCleaned,
    string TrustBoundaryMessage,
    string Message);
