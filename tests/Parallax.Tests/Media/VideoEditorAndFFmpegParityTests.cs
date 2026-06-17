using Parallax.Core.Media;
using Parallax.Core.Platform;
using Parallax.Core.Recording;
using Parallax.Core.Settings;
using Parallax.Core.Shell;

namespace Parallax.Tests.Media;

public sealed class VideoEditorAndFFmpegParityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parallax-video-tests", Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now = new(2026, 6, 17, 12, 34, 56, DateTimeOffset.Now.Offset);

    [Fact]
    public void VideoEditorOpensSupportedMediaAndMaintainsPlaybackMuteAndTimeState()
    {
        string source = CreateVideo("recording.mp4");
        var workflow = CreateWorkflow(FFmpegAvailability.Available(@"C:\Tools\ffmpeg.exe"), TimeSpan.FromSeconds(42));

        var opened = workflow.Open(source);
        var playing = workflow.TogglePlayback();
        var muted = workflow.ToggleMute();
        var seeked = workflow.Seek(TimeSpan.FromSeconds(15));

        Assert.True(opened.Success, opened.Message);
        Assert.Equal(TimeSpan.FromSeconds(42), opened.State?.Duration);
        Assert.True(playing.IsPlaying);
        Assert.True(muted.IsMuted);
        Assert.Equal(TimeSpan.FromSeconds(15), seeked.CurrentTime);
        Assert.True(opened.State?.CanSaveOriginal);
        Assert.True(opened.State?.CanExportTrim);
    }

    [Fact]
    public void TrimValidationClampsToDurationAndRejectsInvalidOrdering()
    {
        var valid = VideoTrimPolicy.Validate(TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(99), TimeSpan.FromSeconds(30));
        var zero = VideoTrimPolicy.Validate(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        var reversed = VideoTrimPolicy.Validate(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        Assert.True(valid.Success, valid.Message);
        Assert.Equal(TimeSpan.Zero, valid.Range?.Start);
        Assert.Equal(TimeSpan.FromSeconds(30), valid.Range?.End);
        Assert.False(zero.Success);
        Assert.Contains("after trim start", zero.Message);
        Assert.False(reversed.Success);
        Assert.Contains("after trim start", reversed.Message);
    }

    [Fact]
    public void SaveOriginalCopiesToCollisionSafeDestinationAndPreservesSource()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        string source = CreateVideo("source.mp4");
        Directory.CreateDirectory(settings.SaveFolder);
        File.WriteAllText(Path.Combine(settings.SaveFolder, "parallax_video_2026-06-17_12-34-56.mp4"), "existing");
        var workflow = CreateWorkflow(FFmpegAvailability.Missing("missing"), TimeSpan.FromSeconds(5));
        Assert.True(workflow.Open(source).Success);

        var saved = workflow.SaveOriginal(settings, platform.Locations);

        Assert.True(saved.Success, saved.Message);
        Assert.EndsWith("parallax_video_2026-06-17_12-34-56_1.mp4", saved.SavedPath);
        Assert.True(File.Exists(saved.SavedPath));
        Assert.True(File.Exists(source));
        Assert.True(saved.SourcePreserved);
    }

    [Theory]
    [InlineData(PlatformKind.Windows, "ffmpeg.exe")]
    [InlineData(PlatformKind.MacOS, "ffmpeg")]
    [InlineData(PlatformKind.Linux, "ffmpeg")]
    public void FFmpegDiscoveryChecksBundledAppLocalAndPathByPlatform(PlatformKind platformKind, string binaryName)
    {
        var fs = new FakeDiscoveryFileSystem();
        string app = Path.Combine(_root, platformKind.ToString(), "app");
        string tools = Path.Combine(_root, platformKind.ToString(), "tools");
        string pathDir = Path.Combine(_root, platformKind.ToString(), "path");
        string expected = Path.Combine(tools, binaryName);
        fs.Existing.Add(expected);

        var result = FFmpegDiscoveryPolicy.Discover(
            new FFmpegDiscoveryRequest(platformKind, app, tools, [pathDir]),
            fs);

        Assert.True(result.IsAvailable, result.Message);
        Assert.Equal(expected, result.ExecutablePath);
        Assert.Equal("app-local", result.SelectedKind);
        Assert.Contains(result.Candidates, candidate => candidate.Kind == "bundled");
        Assert.Contains(result.Candidates, candidate => candidate.Kind == "PATH");
    }

    [Fact]
    public void MissingFFmpegBlocksExportActionsButKeepsPreviewAndSaveOriginalAvailable()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        string source = CreateVideo("missing-ffmpeg.mp4");
        var workflow = CreateWorkflow(FFmpegAvailability.Missing("Install FFmpeg to enable media exports."), TimeSpan.FromSeconds(10));

        var opened = workflow.Open(source);
        var export = workflow.SaveCurrentFrame(ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory), platform.Locations);

        Assert.True(opened.Success, opened.Message);
        Assert.True(opened.State?.CanPreview);
        Assert.True(opened.State?.CanSaveOriginal);
        Assert.False(opened.State?.CanSaveFrame);
        Assert.False(export.Success);
        Assert.True(export.SourcePreserved);
        Assert.True(File.Exists(source));
        Assert.Contains("FFmpeg is required", export.Message);
    }

    [Fact]
    public void FFmpegCommandsUseArgumentListsGeneratedOutputsAndNoSourceOverwrite()
    {
        string source = Path.Combine(_root, "input file.mp4");
        string output = Path.Combine(_root, "output file.mp4");
        var range = new VideoTrimRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));

        var request = FFmpegCommandBuilder.BuildTrim("ffmpeg", source, output, range, TimeSpan.FromMinutes(1));

        Assert.Equal("ffmpeg", request.ExecutablePath);
        Assert.Contains("-n", request.Arguments);
        Assert.Contains(source, request.Arguments);
        Assert.Equal(output, request.OutputPath);
        Assert.Equal(output, request.Arguments[^1]);
        Assert.Throws<InvalidOperationException>(() => FFmpegCommandBuilder.BuildTrim("ffmpeg", source, source, range, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void SafeRunnerBoundsLogsRemovesPartialOutputAndPreservesSourceOnFailure()
    {
        string source = CreateVideo("input.mp4");
        string output = Path.Combine(_root, "partial.mp4");
        var fs = new FakeOutputFileSystem();
        var runner = new SafeFFmpegRunner(
            new FakeProcessExecutor(
                new FFmpegProcessResult(1, TimedOut: false, new string('x', SafeFFmpegRunner.MaxLogChars + 100)),
                () => fs.Files[output] = 12),
            fs);

        var result = runner.Run(new FFmpegRunRequest("ffmpeg", ["-n", "-i", source, output], output, TimeSpan.FromMinutes(1)));

        Assert.False(result.Success);
        Assert.False(fs.Files.ContainsKey(output));
        Assert.True(File.Exists(source));
        Assert.True(result.Message.Length < SafeFFmpegRunner.MaxLogChars + 120);
    }

    [Fact]
    public void SaveCurrentFrameUsesClampedTimestampAndPngOutput()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        string source = CreateVideo("frame.mp4");
        var runner = new CapturingRunner();
        var workflow = CreateWorkflow(FFmpegAvailability.Available(@"C:\Tools\ffmpeg.exe"), TimeSpan.FromSeconds(10), runner);
        Assert.True(workflow.Open(source).Success);
        workflow.Seek(TimeSpan.FromSeconds(10));

        var result = workflow.SaveCurrentFrame(ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory), platform.Locations);

        Assert.True(result.Success, result.Message);
        Assert.Equal(VideoExportKind.CurrentFrame, result.Kind);
        Assert.EndsWith(".png", result.OutputPath);
        Assert.NotNull(runner.LastRequest);
        Assert.Contains("00:00:09.990", runner.LastRequest.Arguments);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void GifExportUsesSelectedTrimRangeAndGeneratedGifPath()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        string source = CreateVideo("gif.mp4");
        var runner = new CapturingRunner();
        var workflow = CreateWorkflow(FFmpegAvailability.Available(@"C:\Tools\ffmpeg.exe"), TimeSpan.FromSeconds(12), runner);
        Assert.True(workflow.Open(source).Success);
        Assert.True(workflow.SetTrim(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)).Success);

        var result = workflow.ExportGif(ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory), platform.Locations);

        Assert.True(result.Success, result.Message);
        Assert.Equal(VideoExportKind.Gif, result.Kind);
        Assert.EndsWith(".gif", result.OutputPath);
        Assert.NotNull(runner.LastRequest);
        Assert.Contains("-filter_complex", runner.LastRequest.Arguments);
        Assert.Contains("00:00:02.000", runner.LastRequest.Arguments);
        Assert.Contains("00:00:03.000", runner.LastRequest.Arguments);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void OpenExistingVideoRejectsUnsupportedUnreadableAndConflictingFlows()
    {
        var workflow = CreateWorkflow(FFmpegAvailability.Available("ffmpeg"), TimeSpan.FromSeconds(10));
        string source = CreateVideo("supported.mp4");
        string unsupported = Path.Combine(_root, "notes.txt");
        File.WriteAllText(unsupported, "not video");

        Assert.True(workflow.Open(source).Success);
        Assert.Equal(VideoEditorOpenFailureKind.UnsupportedFile, workflow.Open(unsupported).FailureKind);
        Assert.Equal(VideoEditorOpenFailureKind.UnreadableFile, workflow.Open(Path.Combine(_root, "missing.mp4")).FailureKind);
        Assert.Equal(VideoEditorOpenFailureKind.AlreadyRecording, workflow.Open(source, isRecordingActive: true).FailureKind);
        Assert.Equal(VideoEditorOpenFailureKind.EditorAlreadyOpen, workflow.Open(source, isAnotherEditorActive: true).FailureKind);
    }

    [Fact]
    public void ShellSurfaceDisablesConflictingRecordingAndEditorFlows()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var hotkeys = Array.Empty<Parallax.Core.Hotkeys.PlannedHotkey>();

        var editorActive = TraySurfaceBuilder.Build(platform.Info, platform.Capabilities, new ShellRuntimeState(false, true, HasActiveVideoEditor: true), hotkeys);
        var recordingActive = TraySurfaceBuilder.Build(platform.Info, platform.Capabilities, new ShellRuntimeState(true, true), hotkeys);

        Assert.False(editorActive.MenuItems.Single(item => item.Action == ShellActionId.RecordRegion).IsEnabled);
        Assert.Contains("video editor", editorActive.MenuItems.Single(item => item.Action == ShellActionId.RecordRegion).Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(recordingActive.MenuItems.Single(item => item.Action == ShellActionId.OpenVideoEditor).IsEnabled);
    }

    [Fact]
    public void FFmpegInstallWorkflowUsesTrustedSourceLimitExpectedBinariesAndCleansTemp()
    {
        var fs = new FakeInstallFileSystem(_root);
        var downloader = new FakeDownloader();
        var extractor = new FakeExtractor(fs, PlatformKind.Windows);
        var workflow = new FFmpegInstallWorkflow(downloader, extractor, fs);

        var result = workflow.Install(new FFmpegInstallRequest(
            FFmpegInstallPolicy.DefaultSourceUri,
            Path.Combine(_root, "tools"),
            PlatformKind.Windows,
            FFmpegInstallPolicy.MaxDownloadBytes));

        Assert.True(result.Success, result.Message);
        Assert.True(result.TempCleaned);
        Assert.Equal(FFmpegInstallPolicy.DefaultSourceUri, downloader.SourceUri);
        Assert.Equal(FFmpegInstallPolicy.MaxDownloadBytes, downloader.MaxBytes);
        Assert.Contains(result.InstalledBinaries, path => path.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("does not verify signatures or hashes", result.TrustBoundaryMessage);
        Assert.True(fs.CopiedExpectedOnly);
        Assert.Contains(fs.DeletedDirectories, path => path.Contains("ffmpeg_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecordingToVideoEditorFlowPreservesSourceAndOpensSavedTrimExport()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows, _root);
        var settings = ParallaxSettings.CreateDefaults(platform.Locations.RecordingsDirectory);
        settings.OpenVideoEditorAfterRecording = true;
        string recording = CreateVideo("recording-output.mp4");
        var editor = new FakeVideoEditorLauncher();

        var completion = RecordingCompletionPolicy.Complete(
            recording,
            settings,
            platform.Locations,
            new CollisionSafeVideoSaver(() => _now),
            new FakeFFmpegLocator(FFmpegAvailability.Available(@"C:\Tools\ffmpeg.exe")),
            editor);

        Assert.True(completion.Success, completion.Message);
        Assert.True(completion.VideoEditorOpened);
        Assert.True(completion.SourcePreserved);
        Assert.True(File.Exists(recording));
        Assert.True(File.Exists(completion.SavedPath));
        Assert.Equal(completion.SavedPath, editor.OpenedPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private VideoEditorWorkflow CreateWorkflow(FFmpegAvailability availability, TimeSpan duration, IFFmpegRunner? runner = null)
    {
        return new VideoEditorWorkflow(
            new FakeFFmpegLocator(availability),
            runner ?? new CapturingRunner(),
            new FakeMetadataReader(duration),
            new CollisionSafeVideoFileService(() => _now),
            TimeSpan.FromMinutes(1));
    }

    private string CreateVideo(string fileName)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, fileName);
        File.WriteAllBytes(path, [0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50]);
        return path;
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

        public static TestPlatform Create(PlatformKind kind, string root)
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

            return new TestPlatform(
                new PlatformInfo(kind, $"{kind} test"),
                locations,
                new PlatformCapabilitySet(
                    CapabilityResult.Supported("capture"),
                    CapabilityResult.Supported("recording"),
                    CapabilityResult.Supported("hotkeys"),
                    CapabilityResult.Supported("clipboard"),
                    CapabilityResult.Supported("startup"),
                    CapabilityResult.Supported("best-effort exclusion")));
        }
    }

    private sealed class FakeMetadataReader : IVideoMetadataReader
    {
        private readonly TimeSpan _duration;

        public FakeMetadataReader(TimeSpan duration)
        {
            _duration = duration;
        }

        public VideoMetadataResult Read(string videoPath)
        {
            return VideoMetadataResult.Loaded(_duration);
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

    private sealed class CapturingRunner : IFFmpegRunner
    {
        public FFmpegRunRequest? LastRequest { get; private set; }

        public FFmpegRunResult Run(FFmpegRunRequest request)
        {
            LastRequest = request;
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
            File.WriteAllBytes(request.OutputPath, [1]);
            return new FFmpegRunResult(true, "ok", request.OutputPath);
        }
    }

    private sealed class FakeDiscoveryFileSystem : IFFmpegDiscoveryFileSystem
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path)
        {
            return Existing.Contains(path);
        }
    }

    private sealed class FakeProcessExecutor : IFFmpegProcessExecutor
    {
        private readonly FFmpegProcessResult _result;
        private readonly Action? _beforeResult;

        public FakeProcessExecutor(FFmpegProcessResult result, Action? beforeResult = null)
        {
            _result = result;
            _beforeResult = beforeResult;
        }

        public FFmpegProcessResult Execute(FFmpegRunRequest request)
        {
            _beforeResult?.Invoke();
            return _result;
        }
    }

    private sealed class FakeOutputFileSystem : IFFmpegOutputFileSystem
    {
        public Dictionary<string, long> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path)
        {
            return Files.ContainsKey(path);
        }

        public long GetFileLength(string path)
        {
            return Files[path];
        }

        public void DeleteFile(string path)
        {
            Files.Remove(path);
        }
    }

    private sealed class FakeDownloader : IBoundedFileDownloader
    {
        public Uri? SourceUri { get; private set; }

        public long MaxBytes { get; private set; }

        public void Download(Uri sourceUri, string destinationPath, long maxBytes)
        {
            SourceUri = sourceUri;
            MaxBytes = maxBytes;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllText(destinationPath, "zip");
        }
    }

    private sealed class FakeExtractor : IArchiveExtractor
    {
        private readonly FakeInstallFileSystem _fileSystem;
        private readonly PlatformKind _platform;

        public FakeExtractor(FakeInstallFileSystem fileSystem, PlatformKind platform)
        {
            _fileSystem = fileSystem;
            _platform = platform;
        }

        public void Extract(string archivePath, string destinationDirectory)
        {
            foreach (string binary in FFmpegInstallPolicy.ExpectedBinaries(_platform))
            {
                _fileSystem.AvailableExtractedFiles[binary] = Path.Combine(destinationDirectory, "bin", binary);
            }
        }
    }

    private sealed class FakeInstallFileSystem : IFFmpegInstallFileSystem
    {
        private readonly string _root;
        private readonly HashSet<string> _expected = new(StringComparer.OrdinalIgnoreCase)
        {
            "ffmpeg.exe",
            "ffplay.exe",
            "ffprobe.exe"
        };

        public FakeInstallFileSystem(string root)
        {
            _root = root;
        }

        public Dictionary<string, string> AvailableExtractedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Copied { get; } = [];

        public List<string> DeletedDirectories { get; } = [];

        public bool CopiedExpectedOnly => Copied.All(path => _expected.Contains(Path.GetFileName(path)));

        public string CreateIsolatedTempDirectory(string prefix)
        {
            string path = Path.Combine(_root, $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public string? FindFile(string rootDirectory, string fileName)
        {
            return AvailableExtractedFiles.TryGetValue(fileName, out string? path) ? path : null;
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            Copied.Add(destinationPath);
        }

        public void DeleteFile(string path)
        {
        }

        public void DeleteDirectory(string path)
        {
            DeletedDirectories.Add(path);
        }
    }

    private sealed class FakeVideoEditorLauncher : IVideoEditorLauncher
    {
        public string? OpenedPath { get; private set; }

        public VideoEditorLaunchResult Open(string videoPath)
        {
            OpenedPath = videoPath;
            return VideoEditorLaunchResult.Opened($"Opened {videoPath}.");
        }
    }
}
