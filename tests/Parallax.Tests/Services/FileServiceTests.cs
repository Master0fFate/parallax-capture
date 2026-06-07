using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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
    public void GetVideoFilePath_WithUnsafeExtension_ThrowsClearError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _fixture.FileService.GetVideoFilePath(@"..\evil"));
        Assert.Contains("File extension", ex.Message);
    }

    [Fact]
    public void GetImageFilePath_WithMultipartExtension_ReturnsControlledPath()
    {
        string path = _fixture.FileService.GetImageFilePath("frame.png");
        Assert.EndsWith(".frame.png", path);
        Assert.DoesNotContain("..", Path.GetFileName(path));
    }

    [Fact]
    public void GeneratedPaths_AvoidExistingFileCollisions()
    {
        string existing = Path.Combine(_fixture.TempDir, "parallax_fixed.png");
        File.WriteAllText(existing, "already here");
        _createdFiles.Add(existing);

        string path = InvokeGetUniquePath(_fixture.TempDir, "parallax_fixed", "png");

        Assert.EndsWith("parallax_fixed_1.png", path);
    }

    [Fact]
    public void GeneratedPaths_AvoidExistingDirectoryCollisions()
    {
        string existingDirectory = Path.Combine(_fixture.TempDir, "parallax_fixed.mp4");
        Directory.CreateDirectory(existingDirectory);

        string path = InvokeGetUniquePath(_fixture.TempDir, "parallax_fixed", "mp4");

        Assert.EndsWith("parallax_fixed_1.mp4", path);
    }

    [Fact]
    public void GetSaveFolder_WithRelativePath_ThrowsClearError()
    {
        var service = new FileService(new AppSettings { SaveFolder = "relative-folder" });
        var ex = Assert.Throws<InvalidOperationException>(() => service.GetSaveFolder());
        Assert.Contains("full folder path", ex.Message);
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

    private static string InvokeGetUniquePath(string folder, string baseName, string extension)
    {
        var method = typeof(FileService).GetMethod("GetUniquePath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        object? result = method!.Invoke(null, [folder, baseName, extension]);
        return Assert.IsType<string>(result);
    }
}
