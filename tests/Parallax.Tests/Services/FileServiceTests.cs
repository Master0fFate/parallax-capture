using System.Drawing;
using System.Drawing.Imaging;
using parallax.Core.Models;
using parallax.Core.Services;
using Parallax.Tests.Fixtures;

namespace Parallax.Tests.Services;

public class FileServiceTests : IClassFixture<TempFileFixture>, IDisposable
{
    private readonly TempFileFixture _fixture;
    private readonly List<string> _createdFiles = new();

    public FileServiceTests(TempFileFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        foreach (var f in _createdFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); }
            catch { /* best-effort */ }
        }
    }

    [Fact]
    public void SaveScreenshot_CreatesPngFile()
    {
        using var bmp = new Bitmap(100, 100);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Red);

        string path = _fixture.FileService.SaveScreenshot(bmp);
        _createdFiles.Add(path);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".png", path);
        Assert.Contains("parallax_", Path.GetFileName(path));
    }

    [Fact]
    public void SaveScreenshot_WithJpegFormat_CreatesJpgFile()
    {
        _fixture.Settings.ImageFormat = "jpg";
        using var bmp = new Bitmap(100, 100);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Blue);

        string path = _fixture.FileService.SaveScreenshot(bmp);
        _createdFiles.Add(path);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".jpg", path);
    }

    [Fact]
    public void SaveScreenshot_WithBmpFormat_CreatesBmpFile()
    {
        _fixture.Settings.ImageFormat = "bmp";
        using var bmp = new Bitmap(50, 50);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Green);

        string path = _fixture.FileService.SaveScreenshot(bmp);
        _createdFiles.Add(path);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".bmp", path);
    }

    [Fact]
    public void GetSaveFolder_CreatesDirectory()
    {
        // Reset SeparateFolders (IClassFixture shares state; other tests may have mutated it)
        _fixture.Settings.SeparateFolders = false;
        string folder = _fixture.FileService.GetSaveFolder();
        Assert.True(Directory.Exists(folder));
        Assert.Equal(_fixture.TempDir, folder);
    }

    [Fact]
    public void GetVideoFilePath_ReturnsPathWithExtension()
    {
        string path = _fixture.FileService.GetVideoFilePath("mp4");
        Assert.EndsWith(".mp4", path);
        Assert.Contains("parallax_", Path.GetFileName(path));
    }

    [Fact]
    public void GetVideoFilePath_WithDifferentExtension()
    {
        string path = _fixture.FileService.GetVideoFilePath("avi");
        Assert.EndsWith(".avi", path);
    }

    [Fact]
    public void GetTempVideoPath_IsInTempDir()
    {
        string path = _fixture.FileService.GetTempVideoPath("mp4");
        Assert.StartsWith(Path.GetTempPath(), path);
        Assert.Contains("parallax", path);
        Assert.EndsWith(".mp4", path);
    }

    [Fact]
    public void SeparateFolders_CreatesImagesSubfolder()
    {
        _fixture.Settings.SeparateFolders = true;
        string folder = _fixture.FileService.GetSaveFolder();
        Assert.EndsWith("images", folder);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void SeparateFolders_VideoPathUsesVideosSubfolder()
    {
        _fixture.Settings.SeparateFolders = true;
        string path = _fixture.FileService.GetVideoFilePath("mp4");
        Assert.Contains("videos", path);
    }
}
