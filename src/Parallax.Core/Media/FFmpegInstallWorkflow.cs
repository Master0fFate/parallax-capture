using Parallax.Core.Platform;

namespace Parallax.Core.Media;

public interface IBoundedFileDownloader
{
    void Download(Uri sourceUri, string destinationPath, long maxBytes);
}

public interface IArchiveExtractor
{
    void Extract(string archivePath, string destinationDirectory);
}

public interface IFFmpegInstallFileSystem
{
    string CreateIsolatedTempDirectory(string prefix);

    void CreateDirectory(string path);

    string? FindFile(string rootDirectory, string fileName);

    void CopyFile(string sourcePath, string destinationPath, bool overwrite);

    void DeleteFile(string path);

    void DeleteDirectory(string path);
}

public static class FFmpegInstallPolicy
{
    public static readonly Uri DefaultSourceUri = new("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip");

    public const long MaxDownloadBytes = 250L * 1024L * 1024L;

    public const string TrustBoundaryMessage = "FFmpeg download is user-initiated from a configured trusted source. Parallax does not verify signatures or hashes, so use the built-in download only if you trust that source.";

    public static IReadOnlyList<string> ExpectedBinaries(PlatformKind platform)
    {
        return
        [
            FFmpegDiscoveryPolicy.GetBinaryName(platform),
            FFmpegDiscoveryPolicy.GetCompanionBinaryName(platform, "ffplay"),
            FFmpegDiscoveryPolicy.GetCompanionBinaryName(platform, "ffprobe")
        ];
    }
}

public sealed class FFmpegInstallWorkflow
{
    private readonly IBoundedFileDownloader _downloader;
    private readonly IArchiveExtractor _extractor;
    private readonly IFFmpegInstallFileSystem _fileSystem;

    public FFmpegInstallWorkflow(
        IBoundedFileDownloader downloader,
        IArchiveExtractor extractor,
        IFFmpegInstallFileSystem fileSystem)
    {
        _downloader = downloader;
        _extractor = extractor;
        _fileSystem = fileSystem;
    }

    public FFmpegInstallResult Install(FFmpegInstallRequest request)
    {
        string tempRoot = _fileSystem.CreateIsolatedTempDirectory("ffmpeg");
        string archivePath = Path.Combine(tempRoot, "ffmpeg.zip");
        string extractDir = Path.Combine(tempRoot, "extract");
        FFmpegInstallResult result;
        try
        {
            _fileSystem.CreateDirectory(request.ToolsDirectory);
            _fileSystem.CreateDirectory(extractDir);
            _downloader.Download(request.SourceUri, archivePath, request.MaxDownloadBytes);
            _extractor.Extract(archivePath, extractDir);

            var installed = new List<string>();
            foreach (string binary in FFmpegInstallPolicy.ExpectedBinaries(request.Platform))
            {
                string? sourcePath = _fileSystem.FindFile(extractDir, binary);
                if (sourcePath is null)
                {
                    continue;
                }

                string destination = Path.Combine(request.ToolsDirectory, binary);
                _fileSystem.CopyFile(sourcePath, destination, overwrite: true);
                installed.Add(destination);
            }

            bool foundFfmpeg = installed.Any(path =>
                string.Equals(Path.GetFileName(path), FFmpegDiscoveryPolicy.GetBinaryName(request.Platform), StringComparison.OrdinalIgnoreCase));

            result = new FFmpegInstallResult(
                foundFfmpeg,
                installed,
                TempCleaned: false,
                FFmpegInstallPolicy.TrustBoundaryMessage,
                foundFfmpeg
                    ? "FFmpeg tools were installed to the per-user tools folder."
                    : "FFmpeg install failed because the archive did not contain the expected ffmpeg binary.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            result = new FFmpegInstallResult(false, [], TempCleaned: false, FFmpegInstallPolicy.TrustBoundaryMessage, $"FFmpeg install failed safely: {ex.Message}");
        }

        bool tempCleaned = TryCleanup(archivePath, extractDir, tempRoot);
        return result with { TempCleaned = tempCleaned };
    }

    private bool TryCleanup(string archivePath, string extractDir, string tempRoot)
    {
        try
        {
            _fileSystem.DeleteFile(archivePath);
            _fileSystem.DeleteDirectory(extractDir);
            _fileSystem.DeleteDirectory(tempRoot);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
