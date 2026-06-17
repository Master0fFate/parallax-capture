using Parallax.Core.Platform;
using Parallax.Core.Recording;

namespace Parallax.Core.Media;

public interface IFFmpegDiscoveryFileSystem
{
    bool FileExists(string path);
}

public sealed class PhysicalFFmpegDiscoveryFileSystem : IFFmpegDiscoveryFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }
}

public static class FFmpegDiscoveryPolicy
{
    public static FFmpegDiscoveryResult Discover(
        FFmpegDiscoveryRequest request,
        IFFmpegDiscoveryFileSystem fileSystem)
    {
        string binaryName = GetBinaryName(request.Platform);
        var candidates = BuildCandidates(request, binaryName);
        foreach (var candidate in candidates)
        {
            if (fileSystem.FileExists(candidate.Path))
            {
                return new FFmpegDiscoveryResult(
                    true,
                    candidate.Path,
                    candidate.Kind,
                    candidates,
                    $"FFmpeg is available from {candidate.Kind} at {candidate.Path}.");
            }
        }

        return new FFmpegDiscoveryResult(
            false,
            null,
            "missing",
            candidates,
            "FFmpeg was not found. Install FFmpeg manually or use the user-initiated installer to enable trim, frame, and GIF export.");
    }

    public static string GetBinaryName(PlatformKind platform)
    {
        return platform == PlatformKind.Windows ? "ffmpeg.exe" : "ffmpeg";
    }

    public static string GetCompanionBinaryName(PlatformKind platform, string tool)
    {
        string suffix = platform == PlatformKind.Windows ? ".exe" : string.Empty;
        return $"{tool}{suffix}";
    }

    private static IReadOnlyList<FFmpegDiscoveryCandidate> BuildCandidates(
        FFmpegDiscoveryRequest request,
        string binaryName)
    {
        var candidates = new List<FFmpegDiscoveryCandidate>
        {
            new("bundled", Path.Combine(request.AppDirectory, "ffmpeg", binaryName)),
            new("bundled", Path.Combine(request.AppDirectory, "tools", binaryName)),
            new("app-local", Path.Combine(request.ToolsDirectory, binaryName))
        };

        candidates.AddRange(request.PathDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new FFmpegDiscoveryCandidate("PATH", Path.Combine(path, binaryName))));

        return candidates;
    }
}

public sealed class PlatformFFmpegLocator : IFFmpegLocator
{
    private readonly IPlatformLocations _locations;
    private readonly IFFmpegDiscoveryFileSystem _fileSystem;
    private readonly string _appDirectory;
    private readonly Func<string?> _pathProvider;

    public PlatformFFmpegLocator(
        IPlatformLocations locations,
        string appDirectory,
        IFFmpegDiscoveryFileSystem? fileSystem = null,
        Func<string?>? pathProvider = null)
    {
        _locations = locations;
        _appDirectory = appDirectory;
        _fileSystem = fileSystem ?? new PhysicalFFmpegDiscoveryFileSystem();
        _pathProvider = pathProvider ?? (() => Environment.GetEnvironmentVariable("PATH"));
    }

    public FFmpegAvailability Locate()
    {
        var pathDirectories = (_pathProvider() ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = FFmpegDiscoveryPolicy.Discover(
            new FFmpegDiscoveryRequest(
                _locations.Platform,
                _appDirectory,
                _locations.ToolsDirectory,
                pathDirectories),
            _fileSystem);

        return result.ToAvailability();
    }
}
