using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Tests.Core;

public class PlatformSettingsStoreTests
{
    [Theory]
    [InlineData(PlatformKind.Windows)]
    [InlineData(PlatformKind.MacOS)]
    [InlineData(PlatformKind.Linux)]
    public void SaveThenLoadRoundTripsThroughPlatformSettingsPath(PlatformKind platform)
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-core-settings", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
                platform,
                UserProfile: Path.Combine(root, "user"),
                RoamingAppData: Path.Combine(root, "roaming"),
                LocalAppData: Path.Combine(root, "local"),
                TempDirectory: Path.Combine(root, "temp"),
                XdgConfigHome: Path.Combine(root, "xdg-config"),
                XdgDataHome: Path.Combine(root, "xdg-data"),
                XdgStateHome: Path.Combine(root, "xdg-state"),
                PicturesDirectory: Path.Combine(root, "pictures"),
                VideosDirectory: Path.Combine(root, "videos")));

            var store = new JsonSettingsStore(locations);
            var original = new ParallaxSettings
            {
                SaveFolder = locations.ScreenshotsDirectory,
                ImageFormat = "jpeg",
                CopyToClipboardAfterCapture = false,
                SaveAutomatically = true,
                OpenAnnotationEditorAfterScreenshot = false,
                OpenVideoEditorAfterRecording = false,
                SeparateFolders = true,
                StartWithSystem = true,
                HotkeyScreenshotEnabled = false,
                HotkeyFullscreenEnabled = true,
                HotkeyRegionVideoEnabled = false,
                HotkeyScreenshot = "None",
                HotkeyFullscreen = "Alt+PrintScreen",
                HotkeyRegionVideo = "Disabled"
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.True(File.Exists(locations.SettingsFilePath));
            Assert.Equal(original.SaveFolder, loaded.SaveFolder);
            Assert.Equal(original.ImageFormat, loaded.ImageFormat);
            Assert.Equal(original.CopyToClipboardAfterCapture, loaded.CopyToClipboardAfterCapture);
            Assert.Equal(original.SaveAutomatically, loaded.SaveAutomatically);
            Assert.Equal(original.OpenAnnotationEditorAfterScreenshot, loaded.OpenAnnotationEditorAfterScreenshot);
            Assert.Equal(original.OpenVideoEditorAfterRecording, loaded.OpenVideoEditorAfterRecording);
            Assert.Equal(original.SeparateFolders, loaded.SeparateFolders);
            Assert.Equal(original.StartWithSystem, loaded.StartWithSystem);
            Assert.Equal(original.HotkeyScreenshotEnabled, loaded.HotkeyScreenshotEnabled);
            Assert.Equal(original.HotkeyFullscreenEnabled, loaded.HotkeyFullscreenEnabled);
            Assert.Equal(original.HotkeyRegionVideoEnabled, loaded.HotkeyRegionVideoEnabled);
            Assert.Equal(original.HotkeyScreenshot, loaded.HotkeyScreenshot);
            Assert.Equal(original.HotkeyFullscreen, loaded.HotkeyFullscreen);
            Assert.Equal(original.HotkeyRegionVideo, loaded.HotkeyRegionVideo);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadMissingSettingsReturnsDefaultsWithPlatformSaveFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-core-settings", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
                PlatformKind.Linux,
                UserProfile: Path.Combine(root, "home"),
                TempDirectory: Path.Combine(root, "temp"),
                PicturesDirectory: Path.Combine(root, "pictures"),
                VideosDirectory: Path.Combine(root, "videos")));

            var settings = new JsonSettingsStore(locations).Load();

            Assert.Equal(locations.ScreenshotsDirectory, settings.SaveFolder);
            Assert.Equal("png", settings.ImageFormat);
            Assert.True(settings.CopyToClipboardAfterCapture);
            Assert.True(settings.OpenAnnotationEditorAfterScreenshot);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
