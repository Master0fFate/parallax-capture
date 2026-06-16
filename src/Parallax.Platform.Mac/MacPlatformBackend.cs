using Parallax.Core.Platform;

namespace Parallax.Platform.Mac;

public sealed class MacPlatformBackend : IPlatformBackend
{
    public MacPlatformBackend(IPlatformLocations locations)
    {
        Locations = locations;
    }

    public IPlatformInfo Info { get; } = new PlatformInfo(PlatformKind.MacOS, "macOS");

    public IPlatformLocations Locations { get; }

    public PlatformCapabilitySet Capabilities { get; } = new(
        ScreenCapture: CapabilityResult.RequiresPermission("macOS capture requires Screen Recording permission."),
        ScreenRecording: CapabilityResult.RequiresPermission("macOS recording requires Screen Recording permission."),
        GlobalHotkeys: CapabilityResult.RequiresPermission("Global shortcuts may require Accessibility or Input Monitoring permission."),
        Clipboard: CapabilityResult.Supported("macOS clipboard integration is supported."),
        StartupRegistration: CapabilityResult.Supported("Per-user LaunchAgent startup registration is supported."),
        CaptureExclusion: CapabilityResult.Unsupported("Capture exclusion is best-effort only and is not guaranteed on macOS."));

    public static MacPlatformBackend CreateForUserHome(string userHome, string? tempDirectory = null)
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.MacOS,
            UserProfile: userHome,
            TempDirectory: tempDirectory ?? Path.GetTempPath(),
            PicturesDirectory: Path.Combine(userHome, "Pictures"),
            VideosDirectory: Path.Combine(userHome, "Movies")));

        return new MacPlatformBackend(locations);
    }
}
