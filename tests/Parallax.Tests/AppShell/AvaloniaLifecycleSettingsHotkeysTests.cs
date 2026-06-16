using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;

namespace Parallax.Tests.AppShell;

public sealed class AvaloniaLifecycleSettingsHotkeysTests
{
    [Fact]
    public void TrayFirstStartupBuildsParityMenuWithoutShowingMainWindow()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows);
        var tray = new FakeTrayService(isAvailable: true);
        var hotkeys = new FakeHotkeyService(CapabilityResult.Supported("Hotkeys supported."));
        var coordinator = new AppLifecycleCoordinator(platform, tray, hotkeys);

        var surface = coordinator.StartTrayFirst(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory));

        Assert.True(coordinator.IsRunning);
        Assert.False(surface.MainWindowVisibleAtStartup);
        Assert.Equal("Parallax Capture", tray.ToolTip);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.RegionScreenshot);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.FullScreenshot);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.RecordRegion);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.OpenVideoEditor);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.OpenImageEditor);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.OpenSaveFolder);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.Settings);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.Quit);
        Assert.Equal(3, hotkeys.Registered.Count);
    }

    [Fact]
    public void RecordingStateShowsStopActionAndHidesRecordAction()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows);
        var hotkeys = HotkeyPlanner.Plan(
            ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory),
            CapabilityResult.Supported("Hotkeys supported."));

        var surface = TraySurfaceBuilder.Build(
            platform.Info,
            platform.Capabilities,
            new ShellRuntimeState(IsRecording: true, TrayAvailable: true),
            hotkeys);

        Assert.Equal("Parallax Capture is recording", surface.Tooltip);
        Assert.False(surface.MenuItems.Single(item => item.Action == ShellActionId.RecordRegion).IsVisible);
        Assert.True(surface.MenuItems.Single(item => item.Action == ShellActionId.StopRecording).IsVisible);
        Assert.True(surface.MenuItems.Single(item => item.Action == ShellActionId.StopRecording).IsEnabled);
    }

    [Fact]
    public void UnsupportedTraySessionShowsFallbackControlSurfaceWithSameActions()
    {
        var platform = TestPlatform.Create(PlatformKind.Linux);
        var tray = new FakeTrayService(isAvailable: false);
        var coordinator = new AppLifecycleCoordinator(
            platform,
            tray,
            new FakeHotkeyService(platform.Capabilities.GlobalHotkeys));

        var surface = coordinator.StartTrayFirst(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory));

        Assert.True(surface.MainWindowVisibleAtStartup);
        Assert.False(surface.TrayAvailable);
        Assert.Contains("fallback control surface", surface.FallbackMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(surface.MenuItems, item => item.Action == ShellActionId.OpenSaveFolder);
        Assert.Contains("fallback control window", surface.ActivationHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitQuitDisposesHotkeysTrayAndLifecycleResources()
    {
        var platform = TestPlatform.Create(PlatformKind.Windows);
        var tray = new FakeTrayService(isAvailable: true);
        var hotkeys = new FakeHotkeyService(CapabilityResult.Supported("Hotkeys supported."));
        var coordinator = new AppLifecycleCoordinator(platform, tray, hotkeys);
        coordinator.StartTrayFirst(ParallaxSettings.CreateDefaults(platform.Locations.ScreenshotsDirectory));
        var recording = new FakeLifecycleResource("active recording");
        var export = new FakeLifecycleResource("ffmpeg export");
        coordinator.TrackResource(recording);
        coordinator.TrackResource(export);

        coordinator.Quit();

        Assert.False(coordinator.IsRunning);
        Assert.True(coordinator.ShutdownRequested);
        Assert.True(hotkeys.Unregistered);
        Assert.True(hotkeys.Disposed);
        Assert.True(tray.Disposed);
        Assert.True(recording.Stopped);
        Assert.True(export.Stopped);
        Assert.Contains("active recording", coordinator.CleanupLog);
        Assert.Contains("ffmpeg export", coordinator.CleanupLog);
    }

    [Fact]
    public void HotkeyLabelsExposeDisabledInvalidConflictAndUnsupportedStatesWithoutDisablingMenuActions()
    {
        var settings = new ParallaxSettings
        {
            SaveFolder = @"C:\Captures",
            HotkeyScreenshotEnabled = false,
            HotkeyScreenshot = "PrintScreen",
            HotkeyFullscreenEnabled = true,
            HotkeyFullscreen = "Alt+R",
            HotkeyRegionVideoEnabled = true,
            HotkeyRegionVideo = "Alt+R"
        };

        var planned = HotkeyPlanner.Plan(settings, CapabilityResult.Supported("Hotkeys supported."));

        Assert.Equal(PlannedHotkeyState.Disabled, planned.Single(item => item.Action == HotkeyAction.RegionScreenshot).State);
        Assert.Equal(PlannedHotkeyState.Conflict, planned.Single(item => item.Action == HotkeyAction.RegionRecording).State);

        settings.HotkeyFullscreen = "Ctrl++";
        planned = HotkeyPlanner.Plan(settings, CapabilityResult.Supported("Hotkeys supported."));
        Assert.Equal(PlannedHotkeyState.Invalid, planned.Single(item => item.Action == HotkeyAction.FullscreenScreenshot).State);

        planned = HotkeyPlanner.Plan(settings, CapabilityResult.Unsupported("Global shortcuts are unavailable on this desktop."));
        Assert.All(planned.Where(item => item.Action != HotkeyAction.RegionScreenshot), item => Assert.Equal(PlannedHotkeyState.Unsupported, item.State));

        var platform = TestPlatform.Create(PlatformKind.Linux);
        var surface = TraySurfaceBuilder.Build(
            platform.Info,
            platform.Capabilities,
            new ShellRuntimeState(IsRecording: false, TrayAvailable: true),
            planned);
        Assert.True(surface.MenuItems.Single(item => item.Action == ShellActionId.RegionScreenshot).IsEnabled);
        Assert.True(surface.MenuItems.Single(item => item.Action == ShellActionId.RecordRegion).IsEnabled);
    }

    [Theory]
    [InlineData(PlatformKind.Windows, "HKCU Run", false)]
    [InlineData(PlatformKind.MacOS, "LaunchAgent", false)]
    [InlineData(PlatformKind.Linux, "XDG autostart", false)]
    public void StartupRegistrationPlansUsePerUserNativeMechanisms(PlatformKind platformKind, string mechanism, bool requiresAdmin)
    {
        var platform = TestPlatform.Create(platformKind);

        var plan = StartupRegistrationPolicy.CreatePlan(platformKind, platform.Locations, enable: true, @"C:\Apps\Parallax\Parallax.exe");

        Assert.Equal(mechanism, plan.Mechanism);
        Assert.Equal(requiresAdmin, plan.RequiresAdmin);
        Assert.Contains(platformKind == PlatformKind.Windows ? "Run" : platformKind == PlatformKind.MacOS ? "LaunchAgents" : "autostart", plan.TargetPath);
    }

    [Fact]
    public void SaveFolderValidationCreatesSeparateImageVideoAndGifFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-shell-settings", Guid.NewGuid().ToString("N"));
        try
        {
            var platform = TestPlatform.Create(PlatformKind.Windows, root);
            var settings = ParallaxSettings.CreateDefaults(Path.Combine(root, "captures"));
            settings.SeparateFolders = true;

            var result = SaveFolderPolicy.ValidateAndCreate(settings, platform.Locations);

            Assert.True(result.Success, result.Message);
            Assert.True(Directory.Exists(SaveFolderPolicy.GetFolderFor(settings, platform.Locations, SaveMediaKind.Image)));
            Assert.True(Directory.Exists(SaveFolderPolicy.GetFolderFor(settings, platform.Locations, SaveMediaKind.Video)));
            Assert.True(Directory.Exists(SaveFolderPolicy.GetFolderFor(settings, platform.Locations, SaveMediaKind.Gif)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ThemePreviewPersistsNormalizedSettingsAndRecoversInvalidValues()
    {
        var applied = new FakeThemeApplier();
        var service = new ThemeSettingsService(applied);
        var settings = new ParallaxSettings
        {
            ThemeFamily = "unknown theme",
            ThemeMode = "nonsense"
        };

        service.Persist(settings, settings.ThemeFamily, settings.ThemeMode);

        Assert.Equal(ThemeCatalog.FamilyMaterial, settings.ThemeFamily);
        Assert.Equal(ThemeCatalog.ModeDark, settings.ThemeMode);
        Assert.Equal("Material 3 Dark", applied.Applied.Single().DisplayName);
    }

    [Fact]
    public void OpenSaveFolderCreatesRootAndReportsLaunchFailures()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-open-save-folder", Guid.NewGuid().ToString("N"));
        try
        {
            var platform = TestPlatform.Create(PlatformKind.Windows, root);
            var settings = ParallaxSettings.CreateDefaults(Path.Combine(root, "captures"));
            var launcher = new FakeFolderLauncher(success: true);
            var service = new OpenSaveFolderService(platform.Locations, launcher);

            var result = service.Open(settings);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(settings.SaveFolder));
            Assert.Equal(settings.SaveFolder, launcher.OpenedPath);

            var failingResult = new OpenSaveFolderService(platform.Locations, new FakeFolderLauncher(success: false)).Open(settings);
            Assert.False(failingResult.Success);
            Assert.Contains("file manager", failingResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ApplyingRuntimeSettingsUpdatesHotkeysStartupThemeSaveFolderAndPersistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-runtime-settings", Guid.NewGuid().ToString("N"));
        try
        {
            var platform = TestPlatform.Create(PlatformKind.Windows, root);
            var settings = ParallaxSettings.CreateDefaults(Path.Combine(root, "captures"));
            settings.StartWithSystem = true;
            settings.ThemeFamily = "GitHub";
            settings.ThemeMode = "Light";
            settings.HotkeyFullscreen = "Ctrl+Shift+F";

            var hotkeys = new FakeHotkeyService(CapabilityResult.Supported("Hotkeys supported."));
            var startup = new FakeStartupService(platform.Locations);
            var themes = new FakeThemeApplier();
            var applier = new RuntimeSettingsApplier(
                platform,
                new JsonSettingsStore(platform.Locations),
                hotkeys,
                startup,
                new ThemeSettingsService(themes));

            var result = applier.Apply(settings, @"C:\Apps\Parallax\Parallax.exe");

            Assert.True(result.Saved);
            Assert.True(Directory.Exists(settings.SaveFolder));
            Assert.True(hotkeys.Unregistered);
            Assert.Equal(3, hotkeys.Registered.Count);
            Assert.True(startup.LastResult?.Success);
            Assert.Equal("GitHub Light", themes.Applied.Single().DisplayName);
            Assert.True(File.Exists(platform.Locations.SettingsFilePath));
            Assert.Equal("Ctrl+Shift+F", new JsonSettingsStore(platform.Locations).Load().HotkeyFullscreen);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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

        public static TestPlatform Create(PlatformKind kind, string? root = null)
        {
            root ??= Path.Combine(Path.GetTempPath(), "parallax-test-platform", Guid.NewGuid().ToString("N"));
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

            var hotkeys = kind == PlatformKind.Linux
                ? CapabilityResult.Unsupported("Global shortcuts may be unavailable on this Linux desktop.")
                : CapabilityResult.Supported("Global shortcuts are available.");

            return new TestPlatform(
                new PlatformInfo(kind, $"{kind} test"),
                locations,
                new PlatformCapabilitySet(
                    ScreenCapture: CapabilityResult.Supported("Capture available."),
                    ScreenRecording: CapabilityResult.Supported("Recording available."),
                    GlobalHotkeys: hotkeys,
                    Clipboard: CapabilityResult.Supported("Clipboard available."),
                    StartupRegistration: CapabilityResult.Supported("Startup registration available."),
                    CaptureExclusion: kind == PlatformKind.Windows
                        ? CapabilityResult.Supported("Capture exclusion is best-effort.")
                        : CapabilityResult.Unsupported("Capture exclusion is best-effort only.")));
        }
    }

    private sealed class FakeTrayService : ITrayService
    {
        public FakeTrayService(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public string ToolTip { get; private set; } = string.Empty;

        public bool Disposed { get; private set; }

        public IReadOnlyList<TrayMenuItem> Items { get; private set; } = [];

        public void SetMenu(IReadOnlyList<TrayMenuItem> items)
        {
            Items = items;
        }

        public void SetToolTip(string text)
        {
            ToolTip = text;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeHotkeyService : IGlobalHotkeyService
    {
        public FakeHotkeyService(CapabilityResult capability)
        {
            Capability = capability;
        }

        public CapabilityResult Capability { get; }

        public bool Unregistered { get; private set; }

        public bool Disposed { get; private set; }

        public List<string> Registered { get; } = [];

        public HotkeyRegistrationResult Register(int id, uint modifiers, uint virtualKey, string displayText, Action callback)
        {
            Registered.Add(displayText);
            return new HotkeyRegistrationResult(HotkeyRegistrationResultState.Registered, displayText, $"Registered {displayText}.");
        }

        public void UnregisterAll()
        {
            Unregistered = true;
            Registered.Clear();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeLifecycleResource : IAppLifecycleResource
    {
        public FakeLifecycleResource(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool Stopped { get; private set; }

        public void Stop()
        {
            Stopped = true;
        }
    }

    private sealed class FakeThemeApplier : IThemePreviewApplier
    {
        public List<ThemePreset> Applied { get; } = [];

        public void Apply(ThemePreset preset)
        {
            Applied.Add(preset);
        }
    }

    private sealed class FakeFolderLauncher : IFolderLauncher
    {
        private readonly bool _success;

        public FakeFolderLauncher(bool success)
        {
            _success = success;
        }

        public string? OpenedPath { get; private set; }

        public FolderLaunchResult OpenFolder(string folderPath)
        {
            OpenedPath = folderPath;
            return _success
                ? new FolderLaunchResult(true, "Opened.")
                : new FolderLaunchResult(false, "Could not open with the platform file manager.");
        }
    }

    private sealed class FakeStartupService : IStartupService
    {
        private readonly IPlatformLocations _locations;

        public FakeStartupService(IPlatformLocations locations)
        {
            _locations = locations;
        }

        public StartupRegistrationResult? LastResult { get; private set; }

        public StartupRegistrationPlan CreatePlan(bool enable, string executablePath)
        {
            return StartupRegistrationPolicy.CreatePlan(_locations.Platform, _locations, enable, executablePath);
        }

        public StartupRegistrationResult SetEnabled(bool enable, string executablePath)
        {
            LastResult = new StartupRegistrationResult(true, CreatePlan(enable, executablePath), "Applied.");
            return LastResult;
        }
    }
}
