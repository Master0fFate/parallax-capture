using System.Reflection;
using parallax.Core.Models;
using parallax.Core.Services;

namespace Parallax.Tests.Fixtures;

/// <summary>
/// Creates isolated temp directories and clean AppSettings for each test.
/// Disposes temp files after the test completes.
/// </summary>
public class TempFileFixture : IDisposable
{
    public string TempDir { get; }
    public AppSettings Settings { get; }
    public FileService FileService { get; }

    public TempFileFixture()
    {
        TempDir = Path.Combine(Path.GetTempPath(), "parallax_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);

        Settings = new AppSettings
        {
            SaveFolder = TempDir,
            ImageFormat = "png",
            CopyToClipboardAfterCapture = false,
            SaveAutomatically = false,
            SeparateFolders = false,
            StartWithWindows = false
        };

        FileService = new FileService(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(TempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
