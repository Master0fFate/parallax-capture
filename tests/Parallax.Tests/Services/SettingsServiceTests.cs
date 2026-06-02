using System.Reflection;
using Newtonsoft.Json;
using parallax.Core.Models;
using parallax.Core.Services;

namespace Parallax.Tests.Services;

/// <summary>
/// Tests SettingsService using the real %APPDATA%/parallax/settings.json path.
/// The service uses a hardcoded path, so these tests operate on the real file.
/// We save/load/restore to avoid corrupting real user settings.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly SettingsService _service = new();
    private readonly string _settingsPath;
    private readonly string? _backupJson;

    public SettingsServiceTests()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "parallax",
            "settings.json"
        );

        // Backup existing settings if present
        if (File.Exists(_settingsPath))
            _backupJson = File.ReadAllText(_settingsPath);
    }

    public void Dispose()
    {
        // Restore original settings
        try
        {
            if (_backupJson != null)
                File.WriteAllText(_settingsPath, _backupJson);
            else if (File.Exists(_settingsPath))
                File.Delete(_settingsPath);
        }
        catch { /* best-effort */ }
    }

    [Fact]
    public void Load_WhenNoFileExists_ReturnsDefaults()
    {
        // Ensure no settings file exists
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);

        var settings = _service.Load();
        Assert.NotNull(settings);
        Assert.Equal("png", settings.ImageFormat);
        Assert.True(settings.CopyToClipboardAfterCapture);
    }

    [Fact]
    public void Save_Then_Load_RoundTripsCorrectly()
    {
        var original = new AppSettings
        {
            SaveFolder = "C:\\test",
            ImageFormat = "jpg",
            CopyToClipboardAfterCapture = false,
            SaveAutomatically = true,
            SeparateFolders = true,
            StartWithWindows = true
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(original.SaveFolder, loaded.SaveFolder);
        Assert.Equal(original.ImageFormat, loaded.ImageFormat);
        Assert.Equal(original.CopyToClipboardAfterCapture, loaded.CopyToClipboardAfterCapture);
        Assert.Equal(original.SaveAutomatically, loaded.SaveAutomatically);
        Assert.Equal(original.SeparateFolders, loaded.SeparateFolders);
        Assert.Equal(original.StartWithWindows, loaded.StartWithWindows);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        // Delete the settings dir
        string settingsDir = Path.GetDirectoryName(_settingsPath)!;
        if (Directory.Exists(settingsDir))
            Directory.Delete(settingsDir, recursive: true);

        var settings = new AppSettings();
        _service.Save(settings);

        Assert.True(Directory.Exists(settingsDir));
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_WithCorruptJson_ReturnsDefaultsWithoutCrash()
    {
        string settingsDir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(settingsDir);
        File.WriteAllText(_settingsPath, "{ invalid json }");

        var settings = _service.Load();
        Assert.NotNull(settings);
        Assert.Equal("png", settings.ImageFormat);
    }

    [Fact]
    public void Load_WithPartialJson_ReturnsDefaultsForMissingFields()
    {
        string settingsDir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(settingsDir);
        File.WriteAllText(_settingsPath, """{"CopyToClipboardAfterCapture": false}""");

        var settings = _service.Load();
        Assert.NotNull(settings);
        Assert.False(settings.CopyToClipboardAfterCapture);
        Assert.Equal("png", settings.ImageFormat); // default for missing field
    }

    [Fact]
    public void Load_WithAllFields_DeserializesCorrectly()
    {
        string settingsDir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(settingsDir);
        var data = new
        {
            SaveFolder = "X:\\custom",
            ImageFormat = "jpeg",
            CopyToClipboardAfterCapture = false,
            SaveAutomatically = true,
            SeparateFolders = true,
            StartWithWindows = true
        };
        File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(data));

        var settings = _service.Load();
        Assert.Equal("jpeg", settings.ImageFormat);
        Assert.False(settings.CopyToClipboardAfterCapture);
        Assert.True(settings.SaveAutomatically);
    }
}
