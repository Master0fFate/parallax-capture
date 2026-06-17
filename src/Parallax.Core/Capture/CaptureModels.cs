namespace Parallax.Core.Capture;

public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static RgbaColor Transparent => new(0, 0, 0, 0);

    public static RgbaColor White => new(255, 255, 255);

    public static RgbaColor Black => new(0, 0, 0);

    public static RgbaColor Red => new(255, 0, 0);

    public static RgbaColor Green => new(0, 128, 0);

    public static RgbaColor Blue => new(0, 0, 255);

    public static RgbaColor Yellow => new(255, 255, 0, 128);

    public RgbaColor BlendOver(RgbaColor background)
    {
        if (A == 255)
        {
            return this;
        }

        double alpha = A / 255.0;
        return new RgbaColor(
            (byte)Math.Clamp(Math.Round((R * alpha) + (background.R * (1 - alpha))), 0, 255),
            (byte)Math.Clamp(Math.Round((G * alpha) + (background.G * (1 - alpha))), 0, 255),
            (byte)Math.Clamp(Math.Round((B * alpha) + (background.B * (1 - alpha))), 0, 255),
            255);
    }
}

public readonly record struct CapturePoint(int X, int Y);

public readonly record struct CaptureRectangle(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct LogicalRectangle(double X, double Y, double Width, double Height);

public readonly record struct DpiScale(double X, double Y);

public sealed class CaptureImage
{
    private readonly RgbaColor[] _pixels;

    public CaptureImage(int width, int height, IReadOnlyList<RgbaColor> pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be positive.");
        }

        if (pixels.Count != width * height)
        {
            throw new ArgumentException("Pixel count must match the image dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<RgbaColor> Pixels => _pixels;

    public static CaptureImage CreateSolid(int width, int height, RgbaColor color)
    {
        return new CaptureImage(width, height, Enumerable.Repeat(color, width * height).ToArray());
    }

    public CaptureImage Clone()
    {
        return new CaptureImage(Width, Height, _pixels);
    }

    public RgbaColor GetPixel(int x, int y)
    {
        if (!Contains(x, y))
        {
            return RgbaColor.Transparent;
        }

        return _pixels[(y * Width) + x];
    }

    public void SetPixel(int x, int y, RgbaColor color)
    {
        if (Contains(x, y))
        {
            _pixels[(y * Width) + x] = color;
        }
    }

    public bool Contains(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }
}

public enum CaptureFailureKind
{
    Cancelled,
    PermissionDenied,
    RequiresUserMediation,
    Unsupported,
    Failed
}

public sealed record CaptureResult(
    bool Success,
    CaptureImage? Image,
    CaptureFailureKind? FailureKind,
    string Message)
{
    public static CaptureResult FromImage(CaptureImage image, string message = "Capture completed.")
    {
        return new CaptureResult(true, image, null, message);
    }

    public static CaptureResult PermissionDenied(string message)
    {
        return new CaptureResult(false, null, CaptureFailureKind.PermissionDenied, message);
    }

    public static CaptureResult RequiresUserMediation(string message)
    {
        return new CaptureResult(false, null, CaptureFailureKind.RequiresUserMediation, message);
    }

    public static CaptureResult Unsupported(string message)
    {
        return new CaptureResult(false, null, CaptureFailureKind.Unsupported, message);
    }

    public static CaptureResult Failed(string message)
    {
        return new CaptureResult(false, null, CaptureFailureKind.Failed, message);
    }
}

public sealed record RegionSelectionResult(bool Selected, CaptureRectangle? Bounds, string Message)
{
    public static RegionSelectionResult Cancelled(string message)
    {
        return new RegionSelectionResult(false, null, message);
    }
}

public sealed record ClipboardImageResult(bool Success, string Message);

public static class CaptureGeometryMapper
{
    public static CaptureRectangle MapLogicalToPhysical(LogicalRectangle logical, DpiScale scale)
    {
        if (scale.X <= 0 || scale.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "DPI scale must be positive.");
        }

        int x = (int)Math.Floor(logical.X * scale.X);
        int y = (int)Math.Ceiling(logical.Y * scale.Y);
        int width = (int)Math.Round(logical.Width * scale.X);
        int height = (int)Math.Round(logical.Height * scale.Y);
        return new CaptureRectangle(x, y, width, height);
    }
}
