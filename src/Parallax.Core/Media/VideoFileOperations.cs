using Parallax.Core.Platform;
using Parallax.Core.Recording;
using Parallax.Core.Settings;

namespace Parallax.Core.Media;

public sealed class CollisionSafeVideoFileService
{
    private readonly Func<DateTimeOffset> _clock;

    public CollisionSafeVideoFileService()
        : this(() => DateTimeOffset.Now)
    {
    }

    public CollisionSafeVideoFileService(Func<DateTimeOffset> clock)
    {
        _clock = clock;
    }

    public VideoSaveResult SaveOriginal(string sourcePath, ParallaxSettings settings, IPlatformLocations locations)
    {
        if (!File.Exists(sourcePath))
        {
            return new VideoSaveResult(false, null, SourcePreserved: false, UsedSourceFallback: false, "Source video was not found.");
        }

        try
        {
            string folder = SaveFolderPolicy.GetFolderFor(settings, locations, SaveMediaKind.Video);
            Directory.CreateDirectory(folder);
            string extension = NormalizeVideoExtension(Path.GetExtension(sourcePath));
            string destination = GetUniquePath(folder, $"parallax_video_{_clock().ToLocalTime():yyyy-MM-dd_HH-mm-ss}", extension);
            FFmpegCommandBuilder.EnsureSafeSourceAndOutput(sourcePath, destination);
            File.Copy(sourcePath, destination, overwrite: false);
            return new VideoSaveResult(true, destination, SourcePreserved: File.Exists(sourcePath), UsedSourceFallback: false, $"Saved original video to {destination}.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new VideoSaveResult(false, null, SourcePreserved: File.Exists(sourcePath), UsedSourceFallback: false, $"Original video could not be saved: {ex.Message}");
        }
    }

    public string CreateExportPath(ParallaxSettings settings, IPlatformLocations locations, SaveMediaKind kind, string extension)
    {
        string folder = SaveFolderPolicy.GetFolderFor(settings, locations, kind);
        Directory.CreateDirectory(folder);
        string prefix = kind == SaveMediaKind.Gif ? "parallax_gif" : kind == SaveMediaKind.Image ? "parallax_frame" : "parallax_trimmed";
        return GetUniquePath(folder, $"{prefix}_{_clock().ToLocalTime():yyyy-MM-dd_HH-mm-ss}", extension.TrimStart('.'));
    }

    private static string NormalizeVideoExtension(string extension)
    {
        string normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        return normalized is "avi" or "mov" or "wmv" or "mkv" ? normalized : "mp4";
    }

    private static string GetUniquePath(string folder, string baseName, string extension)
    {
        string candidate = Path.Combine(folder, $"{baseName}.{extension}");
        for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
        {
            if (suffix > 999)
            {
                throw new IOException("Could not create a unique video output name.");
            }

            candidate = Path.Combine(folder, $"{baseName}_{suffix}.{extension}");
        }

        return candidate;
    }
}
