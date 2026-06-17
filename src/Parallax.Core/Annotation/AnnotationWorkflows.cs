using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Annotation;

public interface IImageFileReader
{
    CaptureImage Read(string path);
}

public sealed record OpenExistingImageResult(bool Success, CaptureImage? Image, string Message);

public sealed class OpenExistingImageWorkflow
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp"
    };

    private readonly IImageFileReader _reader;

    public OpenExistingImageWorkflow(IImageFileReader reader)
    {
        _reader = reader;
    }

    public OpenExistingImageResult Open(string path)
    {
        string extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            return new OpenExistingImageResult(false, null, "Unsupported image format. Open a PNG, JPEG, or BMP file.");
        }

        try
        {
            return new OpenExistingImageResult(true, _reader.Read(path), "Image opened.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new OpenExistingImageResult(false, null, $"Image could not be opened: {ex.Message}");
        }
    }
}

public static class AnnotationExportWorkflow
{
    public static ImageSaveResult Save(
        AnnotationDocument document,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CollisionSafeImageSaver saver,
        string? sourcePath)
    {
        var result = saver.Save(document.Render(), settings, locations);
        if (!result.Success || sourcePath is null || result.FilePath is null)
        {
            return result;
        }

        if (Path.GetFullPath(result.FilePath).Equals(Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
        {
            return ImageSaveResult.Failed(result.Format, "Annotation export refused to overwrite the source image.");
        }

        return result;
    }

    public static ClipboardImageResult Copy(AnnotationDocument document, IClipboardService clipboard)
    {
        return clipboard.CopyImage(document.Render());
    }
}
