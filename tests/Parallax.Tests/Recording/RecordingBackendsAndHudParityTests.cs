using Parallax.Core.Capture;
using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Recording;
using Parallax.Core.Settings;
using Parallax.Core.Shell;

namespace Parallax.Tests.Recording;

public sealed class RecordingBackendsAndHudParityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parallax-recording-tests", Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now = new(2026, 6, 17, 12, 34, 56, DateTimeOffset.Now.Offset);

    [Fact]
    public void WindowsRegionRecordingStartShowsHudBorderElapsedAndRequestsCaptureExclusion()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Supported("Default system output audio will be attempted."));
        var exclusion = new FakeCaptureExclusionService(platform.Capabilities.CaptureExclusion);
        var workflow = CreateWorkflow(platform, recording, exclusion: exclusion);

        var started = workflow.StartRegionRecording(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory), platform.Locations);
        var hud = workflow.GetHudState(_now.AddSeconds(65));

        Assert.True(started.Success, started.Message);
        Assert.True(recording.IsRecording);
        Assert.True(started.Hud?.IsVisible);
        Assert.True(started.Hud?.BorderVisible);
        Assert.True(started.Hud?.StopEnabled);
        Assert.Equal("01:05", hud.ElapsedLabel);
        Assert.Contains(CaptureExclusionTarget.RecordingHud, exclusion.Requests);
        Assert.Contains(CaptureExclusionTarget.RecordingBorder, exclusion.Requests);
        Assert.True(started.Hud?.HudExclusion.Requested);
        Assert.True(started.Hud?.BorderExclusion.Requested);
    }

    [Theory]
    [InlineData(RecordingStopSource.Tray)]
    [InlineData(RecordingStopSource.Hotkey)]
    public void FallbackStopPathsFinalizeCollisionSafeSaveAndAutoOpenWhenFfmpegIsAvailable(RecordingStopSource source)
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, Path.Combine(_root, source.ToString()));
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        settings.OpenVideoEditorAfterRecording = true;
        Directory.CreateDirectory(settings.SaveFolder);
        File.WriteAllText(Path.Combine(settings.SaveFolder, "parallax_recording_2026-06-17_12-34-56.mp4"), "existing");
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Supported("Audio supported."));
        var editor = new FakeVideoEditorLauncher();
        var workflow = CreateWorkflow(platform, recording, editor: editor);

        var started = workflow.StartRegionRecording(settings, platform.Locations);
        var stopped = workflow.StopRecording(source, settings, platform.Locations);

        Assert.True(started.Success, started.Message);
        Assert.True(stopped.Success, stopped.Message);
        Assert.Contains(source, recording.StopSources);
        Assert.NotNull(stopped.Completion?.SavedPath);
        Assert.EndsWith("parallax_recording_2026-06-17_12-34-56_1.mp4", stopped.Completion!.SavedPath);
        Assert.True(File.Exists(stopped.Completion.SavedPath));
        Assert.True(File.Exists(started.TempOutputPath));
        Assert.True(stopped.Completion.SourcePreserved);
        Assert.True(stopped.Completion.VideoEditorOpened);
        Assert.Equal(stopped.Completion.SavedPath, editor.OpenedPath);
    }

    [Fact]
    public void FailedRecordingPreservesPartialOutputAndDoesNotPresentSuccess()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Supported("Audio supported."))
        {
            FailStop = true
        };
        var editor = new FakeVideoEditorLauncher();
        var workflow = CreateWorkflow(platform, recording, editor: editor);

        var started = workflow.StartRegionRecording(settings, platform.Locations);
        var stopped = workflow.StopRecording(RecordingStopSource.Hud, settings, platform.Locations);

        Assert.True(started.Success, started.Message);
        Assert.False(stopped.Success);
        Assert.Equal(RecordingFailureKind.Failed, stopped.FailureKind);
        Assert.True(stopped.PartialOutputPreserved);
        Assert.Null(stopped.Completion);
        Assert.False(editor.Opened);
        Assert.True(File.Exists(started.TempOutputPath));
    }

    [Fact]
    public void CompletionFailureSurfacesErrorAndDoesNotPresentFinalRecordingAsSuccess()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        settings.OpenVideoEditorAfterRecording = true;
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Supported("Audio supported."))
        {
            DeleteOutputBeforeSuccessfulStopReturns = true
        };
        var editor = new FakeVideoEditorLauncher();
        var workflow = CreateWorkflow(platform, recording, editor: editor);

        var started = workflow.StartRegionRecording(settings, platform.Locations);
        var stopped = workflow.StopRecording(RecordingStopSource.Tray, settings, platform.Locations);

        Assert.True(started.Success, started.Message);
        Assert.False(stopped.Success);
        Assert.Equal(RecordingFailureKind.Failed, stopped.FailureKind);
        Assert.False(stopped.PartialOutputPreserved);
        Assert.NotNull(stopped.Completion);
        Assert.False(stopped.Completion!.Success);
        Assert.Null(stopped.Completion.SavedPath);
        Assert.False(stopped.Completion.SourcePreserved);
        Assert.False(stopped.Completion.UsedSourceFallback);
        Assert.False(editor.Opened);
        Assert.Contains("finalization failed", stopped.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recording output was not found", stopped.Message);
        Assert.False(File.Exists(started.TempOutputPath));
    }

    [Fact]
    public void UnsupportedAudioStartsWithExplicitDegradedState()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Unsupported("System audio capture is unavailable in this session."));
        var workflow = CreateWorkflow(platform, recording);

        var started = workflow.StartRegionRecording(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory), platform.Locations);

        Assert.True(started.Success, started.Message);
        Assert.False(recording.LastStartRequest?.IncludeSystemAudio);
        Assert.Contains("Audio is degraded", started.Message);
        Assert.Contains("audio degraded", started.Hud?.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingFfmpegPreventsAutoOpenButKeepsCollisionSafeRecording()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        settings.OpenVideoEditorAfterRecording = true;
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("Windows recording supported."),
            AudioCaptureCapability.Supported("Audio supported."));
        var editor = new FakeVideoEditorLauncher();
        var workflow = CreateWorkflow(
            platform,
            recording,
            ffmpeg: new FakeFFmpegLocator(FFmpegAvailability.Missing("Install FFmpeg to trim or open recordings.")),
            editor: editor);

        var started = workflow.StartRegionRecording(settings, platform.Locations);
        var stopped = workflow.StopRecording(RecordingStopSource.Tray, settings, platform.Locations);

        Assert.True(started.Success, started.Message);
        Assert.True(stopped.Success, stopped.Message);
        Assert.True(File.Exists(stopped.Completion?.SavedPath));
        Assert.False(stopped.Completion?.VideoEditorOpened);
        Assert.False(editor.Opened);
        Assert.Contains("FFmpeg is unavailable", stopped.Message);
    }

    [Fact]
    public void PermissionDeniedCanRetryAfterPermissionStateChangesWithoutRestart()
    {
        var platform = TestPlatform.Create(PlatformKind.MacOS, _root);
        var permissions = new SequencePermissionService(
            CapabilityResult.RequiresPermission("Grant Screen Recording permission in System Settings."),
            CapabilityResult.Supported("Permission granted."));
        var recording = new FakeRecordingService(
            CapabilityResult.Supported("macOS recording supported after permission."),
            AudioCaptureCapability.RequiresPermission("System audio may need additional OS permission."));
        var workflow = CreateWorkflow(platform, recording, permissions: permissions);

        var denied = workflow.StartRegionRecording(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory), platform.Locations);
        permissions.RefreshScreenRecordingPermission();
        var granted = workflow.StartRegionRecording(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory), platform.Locations);

        Assert.False(denied.Success);
        Assert.Equal(RecordingFailureKind.PermissionDenied, denied.FailureKind);
        Assert.True(granted.Success, granted.Message);
        Assert.True(recording.IsRecording);
    }

    [Fact]
    public void PlatformRecordingTopologyCoversMacLinuxPickerX11AndBestEffortExclusionLimits()
    {
        var windows = TestPlatform.Create(PlatformKind.Windows, _root);
        var mac = TestPlatform.Create(PlatformKind.MacOS, _root);
        var linuxWayland = TestPlatform.Create(PlatformKind.Linux, _root, CapabilityResult.RequiresUserMediation("Wayland uses xdg-desktop-portal and PipeWire."));
        var linuxX11 = TestPlatform.Create(PlatformKind.Linux, _root, CapabilityResult.Supported("X11 direct capture is available."));

        Assert.Equal(RecordingTopologyKind.DirectRegionCapture, RecordingTopologyPolicy.Describe(windows).Kind);
        var macTopology = RecordingTopologyPolicy.Describe(mac);
        Assert.Equal(RecordingTopologyKind.PermissionRequired, macTopology.Kind);
        Assert.True(macTopology.RequiresPermission);
        Assert.Contains("Screen Recording", macTopology.Message);
        var waylandTopology = RecordingTopologyPolicy.Describe(linuxWayland);
        Assert.Equal(RecordingTopologyKind.PortalPickerRequired, waylandTopology.Kind);
        Assert.True(waylandTopology.RequiresPicker);
        Assert.Contains("portal picker", waylandTopology.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RecordingTopologyKind.X11Fallback, RecordingTopologyPolicy.Describe(linuxX11).Kind);
        Assert.Equal(CapabilityState.Unsupported, mac.Capabilities.CaptureExclusion.State);
        Assert.Contains("best-effort", mac.Capabilities.CaptureExclusion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CapabilityState.Unsupported, linuxWayland.Capabilities.CaptureExclusion.State);
    }

    [Fact]
    public void RecordingHotkeyTogglesToStopWhenRecordingIsActive()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var executed = new List<ShellActionId>();
        var coordinator = new AppLifecycleCoordinator(
            platform,
            new FakeTrayService(),
            new FakeHotkeyService(CapabilityResult.Supported("Hotkeys supported.")),
            executed.Add);
        coordinator.StartTrayFirst(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory));
        coordinator.SetRecordingState(true);

        coordinator.CreateHotkeyCallback(HotkeyAction.RegionRecording).Invoke();

        Assert.Contains(ShellActionId.StopRecording, executed);
        Assert.DoesNotContain(ShellActionId.RecordRegion, executed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private RecordingWorkflow CreateWorkflow(
        IPlatformBackend platform,
        FakeRecordingService recording,
        FakeCaptureExclusionService? exclusion = null,
        IPlatformPermissionService? permissions = null,
        IFFmpegLocator? ffmpeg = null,
        FakeVideoEditorLauncher? editor = null)
    {
        return new RecordingWorkflow(
            new FakeRegionSelectionService(new CaptureRectangle(10, 20, 320, 180)),
            recording,
            exclusion ?? new FakeCaptureExclusionService(platform.Capabilities.CaptureExclusion),
            permissions ?? new SequencePermissionService(CapabilityResult.Supported("Permission granted.")),
            new CollisionSafeVideoSaver(() => _now),
            ffmpeg ?? new FakeFFmpegLocator(FFmpegAvailability.Available(@"C:\Tools\ffmpeg.exe")),
            editor ?? new FakeVideoEditorLauncher(),
            () => _now);
    }

    private sealed class TestPlatform : IPlatformBackend
    {
        private TestPlatform(IPlatformInfo info, IPlatformLocations locations, PlatformCapabilitySet capabilities)
        {
            Info = info;
            Locations = locations;
            Capabilities = capabilities;
        }

        public IPlatformInfo Info { get; }

        public IPlatformLocations Locations { get; }

        public PlatformCapabilitySet Capabilities { get; }

        public static TestPlatform Create(PlatformKind kind, string root, CapabilityResult? recordingOverride = null)
        {
            var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
                kind,
                UserProfile: Path.Combine(root, "user"),
                RoamingAppData: Path.Combine(root, "roaming"),
                LocalAppData: Path.Combine(root, "local"),
                TempDirectory: Path.Combine(root, "temp"),
                XdgConfigHome: Path.Combine(root, "xdg-config"),
                XdgDataHome: Path.Combine(root, "xdg-data"),
                XdgStateHome: Path.Combine(root, "xdg-state"),
                PicturesDirectory: Path.Combine(root, "pictures"),
                VideosDirectory: Path.Combine(root, "videos")));

            CapabilityResult recording = recordingOverride ?? kind switch
            {
                PlatformKind.Windows => CapabilityResult.Supported("Windows region recording is available."),
                PlatformKind.MacOS => CapabilityResult.RequiresPermission("macOS recording requires Screen Recording permission."),
                _ => CapabilityResult.RequiresUserMediation("Linux Wayland recording uses xdg-desktop-portal and PipeWire.")
            };

            return new TestPlatform(
                new PlatformInfo(kind, $"{kind} test"),
                locations,
                new PlatformCapabilitySet(
                    ScreenCapture: CapabilityResult.Supported("Capture supported."),
                    ScreenRecording: recording,
                    GlobalHotkeys: kind == PlatformKind.Linux
                        ? CapabilityResult.Unsupported("Global shortcuts may be unavailable.")
                        : CapabilityResult.Supported("Global shortcuts available."),
                    Clipboard: CapabilityResult.Supported("Clipboard supported."),
                    StartupRegistration: CapabilityResult.Supported("Startup supported."),
                    CaptureExclusion: kind == PlatformKind.Windows
                        ? CapabilityResult.Supported("Windows display affinity capture exclusion is best-effort.")
                        : CapabilityResult.Unsupported("Capture exclusion is best-effort only and not guaranteed on this platform.")));
        }
    }

    private sealed class FakeRegionSelectionService : IRegionSelectionService
    {
        private readonly CaptureRectangle _region;

        public FakeRegionSelectionService(CaptureRectangle region)
        {
            _region = region;
        }

        public RegionSelectionResult SelectRegion()
        {
            return new RegionSelectionResult(true, _region, "Region selected.");
        }
    }

    private sealed class FakeRecordingService : IScreenRecordingService
    {
        public FakeRecordingService(CapabilityResult capability, AudioCaptureCapability audioCapability)
        {
            Capability = capability;
            AudioCapability = audioCapability;
        }

        public CapabilityResult Capability { get; }

        public AudioCaptureCapability AudioCapability { get; }

        public bool IsRecording { get; private set; }

        public bool FailStop { get; init; }

        public bool DeleteOutputBeforeSuccessfulStopReturns { get; init; }

        public RecordingStartRequest? LastStartRequest { get; private set; }

        public List<RecordingStopSource> StopSources { get; } = [];

        public RecordingStartResult Start(RecordingStartRequest request)
        {
            LastStartRequest = request;
            IsRecording = true;
            return RecordingStartResult.Started("fake-session", request.OutputPath, AudioCapability, "Recording started.");
        }

        public RecordingStopResult Stop(RecordingStopSource source)
        {
            StopSources.Add(source);
            IsRecording = false;
            if (LastStartRequest is null)
            {
                return RecordingStopResult.Failed(RecordingFailureKind.Failed, null, false, "Recording never started.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LastStartRequest.OutputPath)!);
            File.WriteAllBytes(LastStartRequest.OutputPath, [0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50]);

            if (FailStop)
            {
                return RecordingStopResult.Failed(RecordingFailureKind.Failed, LastStartRequest.OutputPath, true, "Native recorder failed while finalizing.");
            }

            if (DeleteOutputBeforeSuccessfulStopReturns)
            {
                File.Delete(LastStartRequest.OutputPath);
            }

            return RecordingStopResult.Stopped(LastStartRequest.OutputPath, TimeSpan.FromSeconds(2), "Recording stopped.");
        }
    }

    private sealed class FakeCaptureExclusionService : ICaptureExclusionService
    {
        private readonly CapabilityResult _capability;

        public FakeCaptureExclusionService(CapabilityResult capability)
        {
            _capability = capability;
        }

        public List<CaptureExclusionTarget> Requests { get; } = [];

        public CaptureExclusionResult RequestExclusion(CaptureExclusionTarget target)
        {
            Requests.Add(target);
            return _capability.State == CapabilityState.Supported
                ? CaptureExclusionResult.Supported(_capability.Message)
                : CaptureExclusionResult.Unsupported(_capability.Message);
        }
    }

    private sealed class SequencePermissionService : IPlatformPermissionService
    {
        private readonly Queue<CapabilityResult> _states;
        private CapabilityResult _current;

        public SequencePermissionService(params CapabilityResult[] states)
        {
            _states = new Queue<CapabilityResult>(states.Skip(1));
            _current = states.First();
        }

        public CapabilityResult GetScreenRecordingPermission()
        {
            return _current;
        }

        public CapabilityResult RefreshScreenRecordingPermission()
        {
            if (_states.Count > 0)
            {
                _current = _states.Dequeue();
            }

            return _current;
        }
    }

    private sealed class FakeFFmpegLocator : IFFmpegLocator
    {
        private readonly FFmpegAvailability _availability;

        public FakeFFmpegLocator(FFmpegAvailability availability)
        {
            _availability = availability;
        }

        public FFmpegAvailability Locate()
        {
            return _availability;
        }
    }

    private sealed class FakeVideoEditorLauncher : IVideoEditorLauncher
    {
        public bool Opened { get; private set; }

        public string? OpenedPath { get; private set; }

        public VideoEditorLaunchResult Open(string videoPath)
        {
            Opened = true;
            OpenedPath = videoPath;
            return VideoEditorLaunchResult.Opened($"Opened {videoPath}.");
        }
    }

    private sealed class FakeTrayService : ITrayService
    {
        public bool IsAvailable => true;

        public void SetMenu(IReadOnlyList<TrayMenuItem> items)
        {
        }

        public void SetToolTip(string text)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeHotkeyService : IGlobalHotkeyService
    {
        public FakeHotkeyService(CapabilityResult capability)
        {
            Capability = capability;
        }

        public CapabilityResult Capability { get; }

        public HotkeyRegistrationResult Register(int id, uint modifiers, uint virtualKey, string displayText, Action callback)
        {
            return new HotkeyRegistrationResult(HotkeyRegistrationResultState.Registered, displayText, "Registered.");
        }

        public HotkeyRegistrationResult RegisterHold(int id, uint modifiers, uint virtualKey, string displayText, Action started, Action stopped)
        {
            return new HotkeyRegistrationResult(HotkeyRegistrationResultState.Registered, displayText, "Registered hold.");
        }

        public void UnregisterAll()
        {
        }

        public void Dispose()
        {
        }
    }
}
