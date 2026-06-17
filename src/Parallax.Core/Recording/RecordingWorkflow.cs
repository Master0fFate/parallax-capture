using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Recording;

public sealed class RecordingWorkflow
{
    private readonly IRegionSelectionService _regionSelection;
    private readonly IScreenRecordingService _recordingService;
    private readonly ICaptureExclusionService _captureExclusion;
    private readonly IPlatformPermissionService _permissions;
    private readonly CollisionSafeVideoSaver _videoSaver;
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IVideoEditorLauncher _videoEditor;
    private readonly Func<DateTimeOffset> _clock;
    private ActiveRecording? _activeRecording;

    public RecordingWorkflow(
        IRegionSelectionService regionSelection,
        IScreenRecordingService recordingService,
        ICaptureExclusionService captureExclusion,
        IPlatformPermissionService permissions,
        CollisionSafeVideoSaver videoSaver,
        IFFmpegLocator ffmpegLocator,
        IVideoEditorLauncher videoEditor,
        Func<DateTimeOffset>? clock = null)
    {
        _regionSelection = regionSelection;
        _recordingService = recordingService;
        _captureExclusion = captureExclusion;
        _permissions = permissions;
        _videoSaver = videoSaver;
        _ffmpegLocator = ffmpegLocator;
        _videoEditor = videoEditor;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public bool IsRecording => _activeRecording is not null && _recordingService.IsRecording;

    public RecordingStartWorkflowResult StartRegionRecording(ParallaxSettings settings, IPlatformLocations locations)
    {
        if (IsRecording)
        {
            return new RecordingStartWorkflowResult(false, RecordingFailureKind.Failed, null, null, "A recording is already active.");
        }

        var permission = _permissions.GetScreenRecordingPermission();
        if (permission.State != CapabilityState.Supported)
        {
            return new RecordingStartWorkflowResult(
                false,
                MapFailure(permission.State),
                null,
                null,
                permission.Message);
        }

        if (_recordingService.Capability.State == CapabilityState.Unsupported)
        {
            return new RecordingStartWorkflowResult(
                false,
                RecordingFailureKind.Unsupported,
                null,
                null,
                _recordingService.Capability.Message);
        }

        var selection = _regionSelection.SelectRegion();
        if (!selection.Selected || selection.Bounds is null || selection.Bounds.Value.IsEmpty)
        {
            return new RecordingStartWorkflowResult(false, RecordingFailureKind.Cancelled, null, null, selection.Message);
        }

        string tempOutputPath = CreateUniqueTempVideoPath(locations, _clock());
        var start = _recordingService.Start(new RecordingStartRequest(
            selection.Bounds.Value,
            tempOutputPath,
            IncludeSystemAudio: _recordingService.AudioCapability.State != CapabilityState.Unsupported));

        if (!start.Success)
        {
            return new RecordingStartWorkflowResult(false, start.FailureKind, null, tempOutputPath, start.Message);
        }

        var hudExclusion = _captureExclusion.RequestExclusion(CaptureExclusionTarget.RecordingHud);
        var borderExclusion = _captureExclusion.RequestExclusion(CaptureExclusionTarget.RecordingBorder);
        _activeRecording = new ActiveRecording(
            tempOutputPath,
            _clock(),
            hudExclusion,
            borderExclusion);

        var hud = CreateHudState(_clock(), start.Audio);
        string message = start.Audio.IsDegraded
            ? $"{start.Message} Audio is degraded: {start.Audio.Message}"
            : start.Message;
        return new RecordingStartWorkflowResult(true, null, hud, tempOutputPath, message);
    }

    public RecordingHudState GetHudState(DateTimeOffset now)
    {
        if (_activeRecording is null)
        {
            return new RecordingHudState(
                IsVisible: false,
                BorderVisible: false,
                StopEnabled: false,
                "00:00",
                CaptureExclusionResult.Unsupported("No recording HUD is active."),
                CaptureExclusionResult.Unsupported("No recording border is active."),
                "Recording is idle.");
        }

        return CreateHudState(now, _recordingService.AudioCapability);
    }

    public RecordingStopWorkflowResult StopRecording(
        RecordingStopSource source,
        ParallaxSettings settings,
        IPlatformLocations locations)
    {
        if (_activeRecording is null)
        {
            return new RecordingStopWorkflowResult(
                false,
                source,
                null,
                RecordingFailureKind.Failed,
                PartialOutputPreserved: false,
                "No recording is active.");
        }

        var stop = _recordingService.Stop(source);
        var active = _activeRecording;
        _activeRecording = null;

        if (!stop.Success)
        {
            return new RecordingStopWorkflowResult(
                false,
                source,
                null,
                stop.FailureKind,
                stop.PartialOutputPreserved,
                stop.Message);
        }

        var completion = RecordingCompletionPolicy.Complete(
            stop.OutputPath ?? active.TempOutputPath,
            settings,
            locations,
            _videoSaver,
            _ffmpegLocator,
            _videoEditor);

        return new RecordingStopWorkflowResult(true, source, completion, null, PartialOutputPreserved: false, completion.Message);
    }

    private RecordingHudState CreateHudState(DateTimeOffset now, AudioCaptureCapability audio)
    {
        if (_activeRecording is null)
        {
            throw new InvalidOperationException("Cannot create recording HUD state without an active recording.");
        }

        var elapsed = now - _activeRecording.StartedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        string status = audio.IsDegraded
            ? $"Recording. Audio degraded: {audio.Message}"
            : "Recording.";
        return new RecordingHudState(
            IsVisible: true,
            BorderVisible: true,
            StopEnabled: true,
            $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}",
            _activeRecording.HudExclusion,
            _activeRecording.BorderExclusion,
            status);
    }

    private static RecordingFailureKind MapFailure(CapabilityState state)
    {
        return state switch
        {
            CapabilityState.RequiresPermission => RecordingFailureKind.PermissionDenied,
            CapabilityState.RequiresUserMediation => RecordingFailureKind.RequiresUserMediation,
            CapabilityState.Unsupported => RecordingFailureKind.Unsupported,
            _ => RecordingFailureKind.Failed
        };
    }

    private static string CreateUniqueTempVideoPath(IPlatformLocations locations, DateTimeOffset now)
    {
        Directory.CreateDirectory(locations.TempDirectory);
        string baseName = $"parallax_recording_{now.ToLocalTime():yyyy-MM-dd_HH-mm-ss}";
        string candidate = Path.Combine(locations.TempDirectory, $"{baseName}.mp4");
        for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
        {
            if (suffix > 999)
            {
                throw new IOException("Could not create a unique temporary recording path.");
            }

            candidate = Path.Combine(locations.TempDirectory, $"{baseName}_{suffix}.mp4");
        }

        return candidate;
    }

    private sealed record ActiveRecording(
        string TempOutputPath,
        DateTimeOffset StartedAt,
        CaptureExclusionResult HudExclusion,
        CaptureExclusionResult BorderExclusion);
}

public sealed class CollisionSafeVideoSaver
{
    private readonly Func<DateTimeOffset> _clock;

    public CollisionSafeVideoSaver()
        : this(() => DateTimeOffset.Now)
    {
    }

    public CollisionSafeVideoSaver(Func<DateTimeOffset> clock)
    {
        _clock = clock;
    }

    public VideoSaveResult Save(string sourcePath, ParallaxSettings settings, IPlatformLocations locations)
    {
        if (!File.Exists(sourcePath))
        {
            return new VideoSaveResult(false, null, SourcePreserved: false, UsedSourceFallback: false, "Recording output was not found.");
        }

        try
        {
            string folder = SaveFolderPolicy.GetFolderFor(settings, locations, SaveMediaKind.Video);
            Directory.CreateDirectory(folder);
            string destination = GetUniquePath(folder, $"parallax_recording_{_clock().ToLocalTime():yyyy-MM-dd_HH-mm-ss}", "mp4");
            File.Copy(sourcePath, destination, overwrite: false);
            return new VideoSaveResult(true, destination, SourcePreserved: File.Exists(sourcePath), UsedSourceFallback: false, $"Saved recording to {destination}.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return new VideoSaveResult(true, sourcePath, SourcePreserved: File.Exists(sourcePath), UsedSourceFallback: true, $"Recording stayed in its temporary location because it could not be copied to the save folder: {ex.Message}");
        }
    }

    private static string GetUniquePath(string folder, string baseName, string extension)
    {
        string candidate = Path.Combine(folder, $"{baseName}.{extension}");
        for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
        {
            if (suffix > 999)
            {
                throw new IOException("Could not create a unique recording file name.");
            }

            candidate = Path.Combine(folder, $"{baseName}_{suffix}.{extension}");
        }

        return candidate;
    }
}

public static class RecordingCompletionPolicy
{
    public static RecordingCompletionResult Complete(
        string outputPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CollisionSafeVideoSaver videoSaver,
        IFFmpegLocator ffmpegLocator,
        IVideoEditorLauncher videoEditor)
    {
        var save = videoSaver.Save(outputPath, settings, locations);
        if (!save.Success || save.SavedPath is null)
        {
            return new RecordingCompletionResult(false, null, false, save.SourcePreserved, save.UsedSourceFallback, save.Message);
        }

        bool editorOpened = false;
        string message = save.Message;
        if (settings.OpenVideoEditorAfterRecording)
        {
            var ffmpeg = ffmpegLocator.Locate();
            if (!ffmpeg.IsAvailable)
            {
                message = $"{message} Video editor was not opened because FFmpeg is unavailable: {ffmpeg.Message}";
            }
            else
            {
                var launch = videoEditor.Open(save.SavedPath);
                editorOpened = launch.Success;
                message = launch.Success
                    ? $"{message} {launch.Message}"
                    : $"{message} Video editor could not be opened: {launch.Message}";
            }
        }

        return new RecordingCompletionResult(true, save.SavedPath, editorOpened, save.SourcePreserved, save.UsedSourceFallback, message);
    }
}

public static class RecordingTopologyPolicy
{
    public static RecordingTopology Describe(IPlatformBackend platform)
    {
        var capability = platform.Capabilities.ScreenRecording;
        return platform.Info.Kind switch
        {
            PlatformKind.Windows when capability.State == CapabilityState.Supported => new RecordingTopology(
                platform.Info.Kind,
                RecordingTopologyKind.DirectRegionCapture,
                RequiresPermission: false,
                RequiresPicker: false,
                "Windows records the selected region directly through the Windows recording backend."),
            PlatformKind.MacOS => new RecordingTopology(
                platform.Info.Kind,
                RecordingTopologyKind.PermissionRequired,
                RequiresPermission: true,
                RequiresPicker: false,
                $"{capability.Message} Grant Screen Recording permission and retry from Parallax Capture."),
            PlatformKind.Linux when capability.State == CapabilityState.RequiresUserMediation => new RecordingTopology(
                platform.Info.Kind,
                RecordingTopologyKind.PortalPickerRequired,
                RequiresPermission: false,
                RequiresPicker: true,
                $"{capability.Message} The portal picker may choose a screen, window, or region depending on the desktop."),
            PlatformKind.Linux when capability.State == CapabilityState.Supported => new RecordingTopology(
                platform.Info.Kind,
                RecordingTopologyKind.X11Fallback,
                RequiresPermission: false,
                RequiresPicker: false,
                "Linux X11 recording can use direct region capture when the backend exposes coordinates."),
            _ => new RecordingTopology(
                platform.Info.Kind,
                RecordingTopologyKind.Unsupported,
                RequiresPermission: false,
                RequiresPicker: false,
                capability.Message)
        };
    }
}
