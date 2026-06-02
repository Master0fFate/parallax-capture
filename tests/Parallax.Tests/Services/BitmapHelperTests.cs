using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using parallax.Core.Helpers;
using Parallax.Tests.Fixtures;

namespace Parallax.Tests.Services;

public class BitmapHelperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _files = new();

    public BitmapHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "parallax_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var f in _files)
        {
            try { if (File.Exists(f)) File.Delete(f); }
            catch { /* best-effort */ }
        }
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private Bitmap CreateTestBitmap(int w = 50, int h = 50)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Magenta);
        return bmp;
    }

    [Fact]
    public void ToBitmapImage_ConvertsCorrectly()
    {
        using var bmp = CreateTestBitmap();
        var result = BitmapHelper.ToBitmapImage(bmp);

        Assert.NotNull(result);
        Assert.Equal(50, result.PixelWidth);
        Assert.Equal(50, result.PixelHeight);
        Assert.True(result.IsFrozen); // cross-thread safe
    }

    [Fact]
    public void ToBitmapImage_With100x200_MatchesDimensions()
    {
        using var bmp = CreateTestBitmap(100, 200);
        var result = BitmapHelper.ToBitmapImage(bmp);

        Assert.Equal(100, result.PixelWidth);
        Assert.Equal(200, result.PixelHeight);
    }

    [Fact]
    public void SaveBitmapSource_SavesPng()
    {
        using var bmp = CreateTestBitmap();
        var source = BitmapHelper.ToBitmapImage(bmp);
        string path = Path.Combine(_tempDir, "test.png");
        _files.Add(path);

        BitmapHelper.SaveBitmapSource(source, path, "png");
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void SaveBitmapSource_SavesJpeg()
    {
        using var bmp = CreateTestBitmap();
        var source = BitmapHelper.ToBitmapImage(bmp);
        string path = Path.Combine(_tempDir, "test.jpg");
        _files.Add(path);

        BitmapHelper.SaveBitmapSource(source, path, "jpg");
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void SaveBitmapSource_SavesBmp()
    {
        using var bmp = CreateTestBitmap();
        var source = BitmapHelper.ToBitmapImage(bmp);
        string path = Path.Combine(_tempDir, "test.bmp");
        _files.Add(path);

        BitmapHelper.SaveBitmapSource(source, path, "bmp");
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void SaveBitmapSource_WithJpegAlias_SavesJpeg()
    {
        using var bmp = CreateTestBitmap();
        var source = BitmapHelper.ToBitmapImage(bmp);
        string path = Path.Combine(_tempDir, "test.jpeg");
        _files.Add(path);

        BitmapHelper.SaveBitmapSource(source, path, "jpeg");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveBitmapSource_DefaultFormat_IsPng()
    {
        using var bmp = CreateTestBitmap();
        var source = BitmapHelper.ToBitmapImage(bmp);
        string path = Path.Combine(_tempDir, "test_default.png");
        _files.Add(path);

        BitmapHelper.SaveBitmapSource(source, path); // no format arg
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CropBitmap_ReturnsCroppedCopy()
    {
        using var bmp = CreateTestBitmap(100, 100);
        using var cropped = BitmapHelper.CropBitmap(bmp, new Rectangle(10, 10, 30, 40));

        Assert.NotNull(cropped);
        Assert.Equal(30, cropped.Width);
        Assert.Equal(40, cropped.Height);
    }

    [Fact]
    public void CropBitmap_WithFullSize_ReturnsSameSize()
    {
        using var bmp = CreateTestBitmap(50, 50);
        using var cropped = BitmapHelper.CropBitmap(bmp, new Rectangle(0, 0, 50, 50));

        Assert.Equal(50, cropped.Width);
        Assert.Equal(50, cropped.Height);
    }

    [Fact]
    public void CropBitmap_WithZeroArea_Throws()
    {
        using var bmp = CreateTestBitmap(50, 50);
        Assert.Throws<ArgumentException>(() =>
            BitmapHelper.CropBitmap(bmp, new Rectangle(0, 0, 0, 10)));
        Assert.Throws<ArgumentException>(() =>
            BitmapHelper.CropBitmap(bmp, new Rectangle(0, 0, 10, 0)));
    }
}
