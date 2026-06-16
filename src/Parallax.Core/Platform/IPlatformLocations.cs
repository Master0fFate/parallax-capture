namespace Parallax.Core.Platform;

public interface IPlatformLocations
{
    PlatformKind Platform { get; }

    string ConfigDirectory { get; }

    string SettingsFilePath { get; }

    string LogsDirectory { get; }

    string ToolsDirectory { get; }

    string TempDirectory { get; }

    string ScreenshotsDirectory { get; }

    string RecordingsDirectory { get; }
}

public sealed record PlatformLocations(
    PlatformKind Platform,
    string ConfigDirectory,
    string SettingsFilePath,
    string LogsDirectory,
    string ToolsDirectory,
    string TempDirectory,
    string ScreenshotsDirectory,
    string RecordingsDirectory) : IPlatformLocations;
