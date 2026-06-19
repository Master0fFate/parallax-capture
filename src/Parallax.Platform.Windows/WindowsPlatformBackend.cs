using Parallax.Core.Platform;

namespace Parallax.Platform.Windows;

public sealed class WindowsPlatformBackend : IPlatformBackend
{
    public WindowsPlatformBackend(IPlatformLocations locations)
    {
        Locations = locations;
    }

    public IPlatformInfo Info { get; } = new PlatformInfo(PlatformKind.Windows, "Windows");

    public IPlatformLocations Locations { get; }

    public PlatformCapabilitySet Capabilities { get; } = new(
        ScreenCapture: CapabilityResult.Supported("Windows capture is provided by the Windows backend."),
        ScreenRecording: CapabilityResult.Supported("Windows region recording is provided by the Windows backend."),
        GlobalHotkeys: CapabilityResult.Supported("Win32 global hotkeys are supported on Windows."),
        Clipboard: CapabilityResult.Supported("Windows clipboard integration is supported."),
        StartupRegistration: CapabilityResult.Supported("Per-user startup registration is supported on Windows."),
        CaptureExclusion: CapabilityResult.Supported("Windows capture exclusion uses best-effort display affinity where available."),
        SpeechToText: CapabilityResult.Supported("Windows speech-to-text can record microphone audio and use OpenAI-compatible API or local Whisper CLI transcription."));

    public static WindowsPlatformBackend CreateCurrentUser()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Windows,
            UserProfile: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            RoamingAppData: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LocalAppData: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            TempDirectory: Path.GetTempPath(),
            PicturesDirectory: Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            VideosDirectory: Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)));

        return new WindowsPlatformBackend(locations);
    }
}
