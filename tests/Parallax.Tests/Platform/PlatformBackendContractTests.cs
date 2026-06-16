using Parallax.Core.Platform;

namespace Parallax.Tests.Platform;

public class PlatformBackendContractTests
{
    [Theory]
    [MemberData(nameof(FakeBackends))]
    public void FakePlatformBackendsExposePathAndCapabilityContracts(IPlatformBackend backend)
    {
        Assert.NotEmpty(backend.Info.DisplayName);
        Assert.Equal(backend.Info.Kind, backend.Locations.Platform);
        Assert.EndsWith("settings.json", backend.Locations.SettingsFilePath);
        Assert.Contains("parallax", backend.Locations.TempDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(backend.Locations.ScreenshotsDirectory);
        Assert.NotEmpty(backend.Locations.RecordingsDirectory);
        Assert.NotNull(backend.Capabilities.ScreenCapture);
        Assert.NotNull(backend.Capabilities.GlobalHotkeys);
        Assert.NotNull(backend.Capabilities.CaptureExclusion);
    }

    [Fact]
    public void NonWindowsFakeBackendsUseExplicitPermissionOrUnsupportedStates()
    {
        var mac = FakePlatformBackend.CreateMacOS();
        var linux = FakePlatformBackend.CreateLinuxWayland();

        Assert.Equal(CapabilityState.RequiresPermission, mac.Capabilities.ScreenCapture.State);
        Assert.Contains("Screen Recording", mac.Capabilities.ScreenCapture.Message);
        Assert.Equal(CapabilityState.RequiresUserMediation, linux.Capabilities.ScreenCapture.State);
        Assert.Contains("portal", linux.Capabilities.ScreenCapture.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CapabilityState.Unsupported, linux.Capabilities.CaptureExclusion.State);
        Assert.Contains("best-effort", linux.Capabilities.CaptureExclusion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoreAssemblyDoesNotReferenceDesktopOrWin32Frameworks()
    {
        var referenceNames = typeof(PlatformKind).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PresentationFramework", referenceNames);
        Assert.DoesNotContain("PresentationCore", referenceNames);
        Assert.DoesNotContain("WindowsBase", referenceNames);
        Assert.DoesNotContain("System.Windows.Forms", referenceNames);
        Assert.DoesNotContain("Microsoft.Win32.Registry", referenceNames);
    }

    public static TheoryData<IPlatformBackend> FakeBackends() => new()
    {
        FakePlatformBackend.CreateWindows(),
        FakePlatformBackend.CreateMacOS(),
        FakePlatformBackend.CreateLinuxWayland()
    };
}

internal sealed class FakePlatformBackend : IPlatformBackend
{
    private FakePlatformBackend(IPlatformInfo info, IPlatformLocations locations, PlatformCapabilitySet capabilities)
    {
        Info = info;
        Locations = locations;
        Capabilities = capabilities;
    }

    public IPlatformInfo Info { get; }

    public IPlatformLocations Locations { get; }

    public PlatformCapabilitySet Capabilities { get; }

    public static FakePlatformBackend CreateWindows()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Windows,
            UserProfile: @"C:\Users\Fake",
            RoamingAppData: @"C:\Users\Fake\AppData\Roaming",
            LocalAppData: @"C:\Users\Fake\AppData\Local",
            TempDirectory: @"C:\Users\Fake\AppData\Local\Temp",
            PicturesDirectory: @"C:\Users\Fake\Pictures",
            VideosDirectory: @"C:\Users\Fake\Videos"));

        return new FakePlatformBackend(
            new PlatformInfo(PlatformKind.Windows, "Windows fake"),
            locations,
            new PlatformCapabilitySet(
                ScreenCapture: CapabilityResult.Supported("Windows Graphics Capture is available in the fake backend."),
                ScreenRecording: CapabilityResult.Supported("ScreenRecorderLib-compatible recording is available in the fake backend."),
                GlobalHotkeys: CapabilityResult.Supported("Win32 hotkeys are available in the fake backend."),
                Clipboard: CapabilityResult.Supported("Clipboard is available in the fake backend."),
                StartupRegistration: CapabilityResult.Supported("Per-user startup registration is available in the fake backend."),
                CaptureExclusion: CapabilityResult.Supported("Windows capture exclusion is best-effort.")));
    }

    public static FakePlatformBackend CreateMacOS()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.MacOS,
            UserProfile: "/Users/fake",
            TempDirectory: "/tmp",
            PicturesDirectory: "/Users/fake/Pictures",
            VideosDirectory: "/Users/fake/Movies"));

        return new FakePlatformBackend(
            new PlatformInfo(PlatformKind.MacOS, "macOS fake"),
            locations,
            new PlatformCapabilitySet(
                ScreenCapture: CapabilityResult.RequiresPermission("Grant Screen Recording permission in System Settings."),
                ScreenRecording: CapabilityResult.RequiresPermission("Grant Screen Recording permission before recording."),
                GlobalHotkeys: CapabilityResult.RequiresPermission("Accessibility or Input Monitoring may be required for global shortcuts."),
                Clipboard: CapabilityResult.Supported("Clipboard is available in the fake backend."),
                StartupRegistration: CapabilityResult.Supported("LaunchAgent startup registration is available in the fake backend."),
                CaptureExclusion: CapabilityResult.Unsupported("Capture exclusion is best-effort only and is not guaranteed on macOS.")));
    }

    public static FakePlatformBackend CreateLinuxWayland()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Linux,
            UserProfile: "/home/fake",
            TempDirectory: "/tmp",
            XdgConfigHome: "/home/fake/.config",
            XdgDataHome: "/home/fake/.local/share",
            XdgStateHome: "/home/fake/.local/state",
            PicturesDirectory: "/home/fake/Pictures",
            VideosDirectory: "/home/fake/Videos"));

        return new FakePlatformBackend(
            new PlatformInfo(PlatformKind.Linux, "Linux Wayland fake"),
            locations,
            new PlatformCapabilitySet(
                ScreenCapture: CapabilityResult.RequiresUserMediation("Wayland capture uses xdg-desktop-portal and may require a portal picker."),
                ScreenRecording: CapabilityResult.RequiresUserMediation("Wayland recording uses xdg-desktop-portal and PipeWire when available."),
                GlobalHotkeys: CapabilityResult.Unsupported("Global shortcuts may be unavailable on this Wayland desktop."),
                Clipboard: CapabilityResult.Supported("Clipboard is available in the fake backend."),
                StartupRegistration: CapabilityResult.Supported("XDG autostart registration is available in the fake backend."),
                CaptureExclusion: CapabilityResult.Unsupported("Capture exclusion is best-effort only and is not guaranteed on Linux.")));
    }
}
