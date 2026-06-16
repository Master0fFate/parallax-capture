namespace Parallax.Core.Platform;

public static class PlatformPathPolicy
{
    private const string WindowsAppDirectoryName = "parallax";
    private const string LinuxAppDirectoryName = "parallax";
    private const string AppleAppDirectoryName = "Parallax Capture";

    public static PlatformLocations Create(PlatformPathEnvironment environment)
    {
        ValidateRoot(environment.UserProfile, nameof(environment.UserProfile));

        return environment.Platform switch
        {
            PlatformKind.Windows => CreateWindows(environment),
            PlatformKind.MacOS => CreateMacOS(environment),
            PlatformKind.Linux => CreateLinux(environment),
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment.Platform, "Unsupported platform.")
        };
    }

    private static PlatformLocations CreateWindows(PlatformPathEnvironment environment)
    {
        string roaming = RequiredOrFallback(environment.RoamingAppData, Combine(environment.UserProfile, "AppData", "Roaming"));
        string local = RequiredOrFallback(environment.LocalAppData, Combine(environment.UserProfile, "AppData", "Local"));
        string temp = RequiredOrFallback(environment.TempDirectory, Combine(local, "Temp"));
        string pictures = RequiredOrFallback(environment.PicturesDirectory, Combine(environment.UserProfile, "Pictures"));
        string videos = RequiredOrFallback(environment.VideosDirectory, Combine(environment.UserProfile, "Videos"));
        string config = Combine(roaming, WindowsAppDirectoryName);

        return new PlatformLocations(
            PlatformKind.Windows,
            ConfigDirectory: config,
            SettingsFilePath: Combine(config, "settings.json"),
            LogsDirectory: Combine(local, WindowsAppDirectoryName, "logs"),
            ToolsDirectory: Combine(local, WindowsAppDirectoryName, "tools"),
            TempDirectory: Combine(temp.TrimEnd('\\', '/'), WindowsAppDirectoryName),
            ScreenshotsDirectory: Combine(pictures, "parallax_captures"),
            RecordingsDirectory: Combine(videos, "parallax_recordings"));
    }

    private static PlatformLocations CreateMacOS(PlatformPathEnvironment environment)
    {
        string home = environment.UserProfile;
        string temp = RequiredOrFallback(environment.TempDirectory, "/tmp");
        string pictures = RequiredOrFallback(environment.PicturesDirectory, Combine(home, "Pictures"));
        string movies = RequiredOrFallback(environment.VideosDirectory, Combine(home, "Movies"));
        string config = Combine(home, "Library", "Application Support", AppleAppDirectoryName);

        return new PlatformLocations(
            PlatformKind.MacOS,
            ConfigDirectory: config,
            SettingsFilePath: Combine(config, "settings.json"),
            LogsDirectory: Combine(home, "Library", "Logs", AppleAppDirectoryName),
            ToolsDirectory: Combine(config, "tools"),
            TempDirectory: Combine(temp.TrimEnd('\\', '/'), LinuxAppDirectoryName),
            ScreenshotsDirectory: Combine(pictures, AppleAppDirectoryName),
            RecordingsDirectory: Combine(movies, AppleAppDirectoryName));
    }

    private static PlatformLocations CreateLinux(PlatformPathEnvironment environment)
    {
        string home = environment.UserProfile;
        string configRoot = RequiredOrFallback(environment.XdgConfigHome, Combine(home, ".config"));
        string dataRoot = RequiredOrFallback(environment.XdgDataHome, Combine(home, ".local", "share"));
        string stateRoot = RequiredOrFallback(environment.XdgStateHome, Combine(home, ".local", "state"));
        string temp = RequiredOrFallback(environment.TempDirectory, "/tmp");
        string pictures = RequiredOrFallback(environment.PicturesDirectory, Combine(home, "Pictures"));
        string videos = RequiredOrFallback(environment.VideosDirectory, Combine(home, "Videos"));
        string config = Combine(configRoot, LinuxAppDirectoryName);

        return new PlatformLocations(
            PlatformKind.Linux,
            ConfigDirectory: config,
            SettingsFilePath: Combine(config, "settings.json"),
            LogsDirectory: Combine(stateRoot, LinuxAppDirectoryName, "logs"),
            ToolsDirectory: Combine(dataRoot, LinuxAppDirectoryName, "tools"),
            TempDirectory: Combine(temp.TrimEnd('\\', '/'), LinuxAppDirectoryName),
            ScreenshotsDirectory: Combine(pictures, AppleAppDirectoryName),
            RecordingsDirectory: Combine(videos, AppleAppDirectoryName));
    }

    private static string RequiredOrFallback(string? value, string fallback)
    {
        string selected = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        ValidateRoot(selected, nameof(value));
        return selected.TrimEnd('\\', '/');
    }

    private static void ValidateRoot(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Platform path roots must be non-empty.", parameterName);
        }
    }

    private static string Combine(params string[] parts)
    {
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        char separator = parts.Any(part => part.Contains('\\', StringComparison.Ordinal)) ? '\\' : '/';
        var trimmed = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select((part, index) => index == 0 ? part.TrimEnd('\\', '/') : part.Trim('\\', '/'))
            .ToArray();

        return string.Join(separator, trimmed);
    }
}
