namespace Parallax.Core.Platform;

public static class StartupRegistrationPolicy
{
    public const string AppId = "parallax-capture";
    public const string DisplayName = "Parallax Capture";

    public static StartupRegistrationPlan CreatePlan(
        PlatformKind platform,
        IPlatformLocations locations,
        bool enable,
        string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required for startup registration.", nameof(executablePath));
        }

        return platform switch
        {
            PlatformKind.Windows => new StartupRegistrationPlan(
                platform,
                enable,
                "HKCU Run",
                @"Software\Microsoft\Windows\CurrentVersion\Run\ParallaxCapture",
                RequiresAdmin: false,
                enable
                    ? $"Set the current-user Run entry to \"{executablePath}\"."
                    : "Remove the current-user Run entry."),
            PlatformKind.MacOS => new StartupRegistrationPlan(
                platform,
                enable,
                "LaunchAgent",
                Combine(GetUserHome(locations.ConfigDirectory, "Library"), "Library", "LaunchAgents", "io.parallax.capture.plist"),
                RequiresAdmin: false,
                enable
                    ? $"Write a per-user LaunchAgent that launches \"{executablePath}\"."
                    : "Remove the per-user LaunchAgent plist."),
            PlatformKind.Linux => new StartupRegistrationPlan(
                platform,
                enable,
                "XDG autostart",
                Combine(ParentOf(locations.ConfigDirectory), "autostart", $"{AppId}.desktop"),
                RequiresAdmin: false,
                enable
                    ? $"Write a per-user XDG autostart desktop entry for \"{executablePath}\"."
                    : "Remove the per-user XDG autostart desktop entry."),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
        };
    }

    private static string GetUserHome(string configDirectory, string marker)
    {
        int markerIndex = configDirectory.IndexOf($"{Path.DirectorySeparatorChar}{marker}", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            markerIndex = configDirectory.IndexOf($"/{marker}", StringComparison.Ordinal);
        }

        return markerIndex > 0 ? configDirectory[..markerIndex] : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string ParentOf(string path)
    {
        string? parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(parent) ? path : parent;
    }

    private static string Combine(params string[] parts)
    {
        char separator = parts.Any(part => part.Contains('\\', StringComparison.Ordinal)) ? '\\' : '/';
        return string.Join(separator, parts.Select((part, index) => index == 0 ? part.TrimEnd('\\', '/') : part.Trim('\\', '/')));
    }
}
