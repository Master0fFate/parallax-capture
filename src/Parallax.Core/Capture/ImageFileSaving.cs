using System.IO.Compression;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Capture;

public sealed record ImageFileFormat(string Id, string Extension, string DisplayName)
{
    public static ImageFileFormat Png { get; } = new("png", "png", "PNG");

    public static ImageFileFormat Jpeg { get; } = new("jpeg", "jpg", "JPEG");

    public static ImageFileFormat Bmp { get; } = new("bmp", "bmp", "BMP");
}

public sealed record ImageSaveResult(
    bool Success,
    string? FilePath,
    ImageFileFormat Format,
    string Message)
{
    public static ImageSaveResult Failed(ImageFileFormat format, string message)
    {
        return new ImageSaveResult(false, null, format, message);
    }
}

public sealed class CollisionSafeImageSaver
{
    private readonly Func<DateTimeOffset> _clock;

    public CollisionSafeImageSaver()
        : this(() => DateTimeOffset.Now)
    {
    }

    public CollisionSafeImageSaver(Func<DateTimeOffset> clock)
    {
        _clock = clock;
    }

    public ImageSaveResult Save(CaptureImage image, ParallaxSettings settings, IPlatformLocations locations)
    {
        var format = ImageFormatPolicy.Normalize(settings.ImageFormat);
        string folder = SaveFolderPolicy.GetFolderFor(settings, locations, SaveMediaKind.Image);

        try
        {
            Directory.CreateDirectory(folder);
            string timestamp = _clock().ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
            string path = GetUniquePath(folder, $"parallax_{timestamp}", format.Extension);
            File.WriteAllBytes(path, SimpleImageEncoder.Encode(image, format));
            return new ImageSaveResult(true, path, format, $"Saved screenshot to {path}.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return ImageSaveResult.Failed(format, $"Image could not be saved: {ex.Message}");
        }
    }

    private static string GetUniquePath(string folder, string baseName, string extension)
    {
        string candidate = Path.Combine(folder, $"{baseName}.{extension}");
        for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
        {
            if (suffix > 999)
            {
                throw new IOException("Could not create a unique image file name.");
            }

            candidate = Path.Combine(folder, $"{baseName}_{suffix}.{extension}");
        }

        return candidate;
    }
}

public static class ImageFormatPolicy
{
    public static ImageFileFormat Normalize(string? configuredFormat)
    {
        string normalized = string.IsNullOrWhiteSpace(configuredFormat)
            ? "png"
            : configuredFormat.Trim().TrimStart('.').ToLowerInvariant();

        return normalized switch
        {
            "jpg" or "jpeg" => ImageFileFormat.Jpeg,
            "bmp" => ImageFileFormat.Bmp,
            _ => ImageFileFormat.Png
        };
    }
}

public static class SimpleImageEncoder
{
    public static byte[] Encode(CaptureImage image, ImageFileFormat format)
    {
        if (format == ImageFileFormat.Bmp)
        {
            return EncodeBmp(image);
        }

        if (format == ImageFileFormat.Jpeg)
        {
            return EncodeJpegPlaceholder(image);
        }

        return EncodePng(image);
    }

    private static byte[] EncodeBmp(CaptureImage image)
    {
        int rowStride = ((image.Width * 3) + 3) / 4 * 4;
        int pixelBytes = rowStride * image.Height;
        int fileSize = 54 + pixelBytes;
        using var stream = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(image.Width);
        writer.Write(image.Height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        byte[] padding = new byte[rowStride - (image.Width * 3)];
        for (int y = image.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);
                writer.Write(color.B);
                writer.Write(color.G);
                writer.Write(color.R);
            }

            writer.Write(padding);
        }

        return stream.ToArray();
    }

    private static byte[] EncodeJpegPlaceholder(CaptureImage image)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8, 0xFF, 0xE0]);
        stream.Write("JFIF"u8);
        stream.Write(BitConverter.GetBytes(image.Width));
        stream.Write(BitConverter.GetBytes(image.Height));
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    private static byte[] EncodePng(CaptureImage image)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WritePngChunk(stream, "IHDR"u8, CreateIhdr(image));
        WritePngChunk(stream, "IDAT"u8, CreatePngPixelData(image));
        WritePngChunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    private static byte[] CreateIhdr(CaptureImage image)
    {
        byte[] ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, image.Width);
        WriteBigEndian(ihdr, 4, image.Height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        return ihdr;
    }

    private static byte[] CreatePngPixelData(CaptureImage image)
    {
        using var raw = new MemoryStream();
        for (int y = 0; y < image.Height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);
                raw.WriteByte(color.R);
                raw.WriteByte(color.G);
                raw.WriteByte(color.B);
                raw.WriteByte(color.A);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        return compressed.ToArray();
    }

    private static void WritePngChunk(Stream stream, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        using var crcInput = new MemoryStream();
        crcInput.Write(type);
        crcInput.Write(data);
        uint crc = Crc32(crcInput.ToArray());
        Span<byte> crcBytes = stackalloc byte[4];
        WriteBigEndian(crcBytes, 0, unchecked((int)crc));
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(Span<byte> target, int offset, int value)
    {
        target[offset] = (byte)((value >> 24) & 0xFF);
        target[offset + 1] = (byte)((value >> 16) & 0xFF);
        target[offset + 2] = (byte)((value >> 8) & 0xFF);
        target[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1
                    ? (crc >> 1) ^ 0xEDB88320
                    : crc >> 1;
            }
        }

        return ~crc;
    }
}
