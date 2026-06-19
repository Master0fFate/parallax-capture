using Parallax.Core.Platform;

namespace Parallax.Core.Speech;

public sealed class WhisperCliExecutableLocator : IWhisperCliExecutableLocator
{
    public string? FindExecutable(IPlatformLocations locations)
    {
        foreach (string candidate in CandidatePaths(locations))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidatePaths(IPlatformLocations locations)
    {
        foreach (string name in ExecutableNames(locations.Platform))
        {
            if (!string.IsNullOrWhiteSpace(locations.ToolsDirectory))
            {
                yield return Path.Combine(locations.ToolsDirectory, name);
            }

            yield return Path.Combine(AppContext.BaseDirectory, name);
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        foreach (string name in ExecutableNames(locations.Platform))
        {
            yield return Path.Combine(directory, name);
        }
    }

    private static string[] ExecutableNames(PlatformKind platform)
    {
        return platform == PlatformKind.Windows
            ? ["whisper-cli.exe", "main.exe", "whisper.exe"]
            : ["whisper-cli", "main", "whisper"];
    }
}
