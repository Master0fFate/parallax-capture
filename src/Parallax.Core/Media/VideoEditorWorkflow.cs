using Parallax.Core.Platform;
using Parallax.Core.Recording;
using Parallax.Core.Settings;

namespace Parallax.Core.Media;

public interface IVideoMetadataReader
{
    VideoMetadataResult Read(string videoPath);
}

public sealed class VideoEditorWorkflow
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".avi",
        ".mov",
        ".wmv",
        ".mkv"
    };

    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IFFmpegRunner _ffmpegRunner;
    private readonly IVideoMetadataReader _metadataReader;
    private readonly CollisionSafeVideoFileService _fileService;
    private readonly TimeSpan _ffmpegTimeout;

    public VideoEditorWorkflow(
        IFFmpegLocator ffmpegLocator,
        IFFmpegRunner ffmpegRunner,
        IVideoMetadataReader metadataReader,
        CollisionSafeVideoFileService? fileService = null,
        TimeSpan? ffmpegTimeout = null)
    {
        _ffmpegLocator = ffmpegLocator;
        _ffmpegRunner = ffmpegRunner;
        _metadataReader = metadataReader;
        _fileService = fileService ?? new CollisionSafeVideoFileService();
        _ffmpegTimeout = ffmpegTimeout ?? TimeSpan.FromMinutes(30);
    }

    public VideoEditorState? State { get; private set; }

    public VideoEditorOpenResult Open(
        string videoPath,
        bool isRecordingActive = false,
        bool isAnotherEditorActive = false)
    {
        if (isRecordingActive)
        {
            return VideoEditorOpenResult.Failed(
                VideoEditorOpenFailureKind.AlreadyRecording,
                "Stop the active recording before opening the video editor.");
        }

        if (isAnotherEditorActive)
        {
            return VideoEditorOpenResult.Failed(
                VideoEditorOpenFailureKind.EditorAlreadyOpen,
                "A video editor is already open. Save or close the current edit before opening another video.");
        }

        if (!IsSupportedVideoPath(videoPath))
        {
            return VideoEditorOpenResult.Failed(
                VideoEditorOpenFailureKind.UnsupportedFile,
                "Open a supported video file: MP4, AVI, MOV, WMV, or MKV.");
        }

        if (!File.Exists(videoPath))
        {
            return VideoEditorOpenResult.Failed(VideoEditorOpenFailureKind.UnreadableFile, "Video file could not be read.");
        }

        var metadata = _metadataReader.Read(videoPath);
        if (!metadata.Success || metadata.Metadata is null || metadata.Metadata.Duration <= TimeSpan.Zero)
        {
            return VideoEditorOpenResult.Failed(VideoEditorOpenFailureKind.MetadataUnavailable, metadata.Message);
        }

        var ffmpeg = _ffmpegLocator.Locate();
        string status = ffmpeg.IsAvailable
            ? $"Video loaded. {ffmpeg.Message}"
            : $"Video loaded for preview and save-original. {ffmpeg.Message}";
        State = new VideoEditorState(
            videoPath,
            metadata.Metadata.Duration,
            TimeSpan.Zero,
            IsPlaying: false,
            IsMuted: false,
            TimeSpan.Zero,
            metadata.Metadata.Duration,
            ffmpeg,
            status);

        return VideoEditorOpenResult.Opened(State, status);
    }

    public VideoEditorState TogglePlayback()
    {
        EnsureOpen();
        State = State! with { IsPlaying = !State.IsPlaying };
        return State;
    }

    public VideoEditorState ToggleMute()
    {
        EnsureOpen();
        State = State! with { IsMuted = !State.IsMuted };
        return State;
    }

    public VideoEditorState Seek(TimeSpan position)
    {
        EnsureOpen();
        State = State! with { CurrentTime = Clamp(position, TimeSpan.Zero, State.Duration) };
        return State;
    }

    public TrimValidationResult SetTrim(TimeSpan start, TimeSpan end)
    {
        EnsureOpen();
        var validation = VideoTrimPolicy.Validate(start, end, State!.Duration);
        if (validation.Success && validation.Range is not null)
        {
            State = State with
            {
                TrimStart = validation.Range.Start,
                TrimEnd = validation.Range.End,
                StatusMessage = validation.Message
            };
        }
        else
        {
            State = State with { StatusMessage = validation.Message };
        }

        return validation;
    }

    public VideoSaveResult SaveOriginal(ParallaxSettings settings, IPlatformLocations locations)
    {
        EnsureOpen();
        return _fileService.SaveOriginal(State!.SourcePath, settings, locations);
    }

    public VideoExportResult SaveTrimmed(ParallaxSettings settings, IPlatformLocations locations)
    {
        EnsureOpen();
        var validation = VideoTrimPolicy.Validate(State!.TrimStart, State.TrimEnd, State.Duration);
        if (!validation.Success || validation.Range is null)
        {
            return new VideoExportResult(false, VideoExportKind.TrimmedVideo, null, SourcePreserved(), validation.Message);
        }

        string outputPath = _fileService.CreateExportPath(settings, locations, SaveMediaKind.Video, "mp4");
        return RunFFmpeg(
            VideoExportKind.TrimmedVideo,
            outputPath,
            ffmpegPath => FFmpegCommandBuilder.BuildTrim(ffmpegPath, State.SourcePath, outputPath, validation.Range, _ffmpegTimeout));
    }

    public VideoExportResult SaveCurrentFrame(ParallaxSettings settings, IPlatformLocations locations)
    {
        EnsureOpen();
        string outputPath = _fileService.CreateExportPath(settings, locations, SaveMediaKind.Image, "png");
        return RunFFmpeg(
            VideoExportKind.CurrentFrame,
            outputPath,
            ffmpegPath => FFmpegCommandBuilder.BuildCurrentFrame(ffmpegPath, State!.SourcePath, outputPath, State.CurrentTime, State.Duration, _ffmpegTimeout));
    }

    public VideoExportResult ExportGif(ParallaxSettings settings, IPlatformLocations locations)
    {
        EnsureOpen();
        var validation = VideoTrimPolicy.Validate(State!.TrimStart, State.TrimEnd, State.Duration);
        if (!validation.Success || validation.Range is null)
        {
            return new VideoExportResult(false, VideoExportKind.Gif, null, SourcePreserved(), validation.Message);
        }

        string outputPath = _fileService.CreateExportPath(settings, locations, SaveMediaKind.Gif, "gif");
        return RunFFmpeg(
            VideoExportKind.Gif,
            outputPath,
            ffmpegPath => FFmpegCommandBuilder.BuildGif(ffmpegPath, State.SourcePath, outputPath, validation.Range, _ffmpegTimeout));
    }

    public static bool IsSupportedVideoPath(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    private VideoExportResult RunFFmpeg(
        VideoExportKind kind,
        string outputPath,
        Func<string, FFmpegRunRequest> buildRequest)
    {
        var ffmpeg = _ffmpegLocator.Locate();
        State = State! with { FFmpeg = ffmpeg };
        if (!ffmpeg.IsAvailable || ffmpeg.ExecutablePath is null)
        {
            string message = "FFmpeg is required for trim, frame, and GIF export. Install FFmpeg manually or use the user-initiated installer.";
            State = State with { StatusMessage = message };
            return new VideoExportResult(false, kind, null, SourcePreserved(), message);
        }

        var run = _ffmpegRunner.Run(buildRequest(ffmpeg.ExecutablePath));
        string status = run.Success ? $"Export completed: {run.OutputPath}" : run.Message;
        State = State with { StatusMessage = status };
        return new VideoExportResult(run.Success, kind, run.OutputPath, SourcePreserved(), run.Message);
    }

    private bool SourcePreserved()
    {
        return State is not null && File.Exists(State.SourcePath);
    }

    private void EnsureOpen()
    {
        if (State is null)
        {
            throw new InvalidOperationException("Open a supported video before using video editor actions.");
        }
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
