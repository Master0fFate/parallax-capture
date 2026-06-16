using Parallax.Core.Platform;

namespace Parallax.Platform.Linux;

public sealed class LinuxPlatformBackend : IPlatformBackend
{
    public LinuxPlatformBackend(IPlatformLocations locations)
    {
        Locations = locations;
    }

    public IPlatformInfo Info { get; } = new PlatformInfo(PlatformKind.Linux, "Linux");

    public IPlatformLocations Locations { get; }

    public PlatformCapabilitySet Capabilities { get; } = new(
        ScreenCapture: CapabilityResult.RequiresUserMediation("Linux Wayland capture may require xdg-desktop-portal picker mediation."),
        ScreenRecording: CapabilityResult.RequiresUserMediation("Linux Wayland recording uses xdg-desktop-portal and PipeWire when available."),
        GlobalHotkeys: CapabilityResult.Unsupported("Global shortcuts may be unavailable on some Linux desktop sessions."),
        Clipboard: CapabilityResult.Supported("Linux clipboard integration is supported when a desktop session clipboard is available."),
        StartupRegistration: CapabilityResult.Supported("Per-user XDG autostart registration is supported."),
        CaptureExclusion: CapabilityResult.Unsupported("Capture exclusion is best-effort only and is not guaranteed on Linux."));

    public static LinuxPlatformBackend CreateForUserHome(
        string userHome,
        string? tempDirectory = null,
        string? xdgConfigHome = null,
        string? xdgDataHome = null,
        string? xdgStateHome = null)
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Linux,
            UserProfile: userHome,
            TempDirectory: tempDirectory ?? Path.GetTempPath(),
            XdgConfigHome: xdgConfigHome,
            XdgDataHome: xdgDataHome,
            XdgStateHome: xdgStateHome,
            PicturesDirectory: Path.Combine(userHome, "Pictures"),
            VideosDirectory: Path.Combine(userHome, "Videos")));

        return new LinuxPlatformBackend(locations);
    }
}
