using parallax.Core.Models;

namespace Parallax.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void DefaultSaveFolder_IsMyPicturesParallaxCaptures()
    {
        var s = new AppSettings();
        Assert.Contains("parallax_captures", s.SaveFolder);
    }

    [Fact]
    public void DefaultImageFormat_IsPng()
    {
        var s = new AppSettings();
        Assert.Equal("png", s.ImageFormat);
    }

    [Fact]
    public void CopyToClipboardAfterCapture_DefaultsTrue()
    {
        var s = new AppSettings();
        Assert.True(s.CopyToClipboardAfterCapture);
    }

    [Fact]
    public void SaveAutomatically_DefaultsFalse()
    {
        var s = new AppSettings();
        Assert.False(s.SaveAutomatically);
    }

    [Fact]
    public void SeparateFolders_DefaultsFalse()
    {
        var s = new AppSettings();
        Assert.False(s.SeparateFolders);
    }

    [Fact]
    public void StartWithWindows_DefaultsFalse()
    {
        var s = new AppSettings();
        Assert.False(s.StartWithWindows);
    }

    [Fact]
    public void EditorAutoOpenFlags_DefaultTrue()
    {
        var s = new AppSettings();
        Assert.True(s.OpenAnnotationEditorAfterScreenshot);
        Assert.True(s.OpenVideoEditorAfterRecording);
    }

    // KAM #2 — dead fields: verify they still exist (backward compat) but are marked [Obsolete]
    [Fact]
    public void ShowToolbarAfterCapture_IsMarkedObsolete()
    {
        var s = new AppSettings();
        var prop = typeof(AppSettings).GetProperty(nameof(AppSettings.ShowToolbarAfterCapture));
        Assert.NotNull(prop);
        var obsolete = prop!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
        Assert.NotEmpty(obsolete);
    }

    [Fact]
    public void OverlayOpacity_IsMarkedObsolete()
    {
        var s = new AppSettings();
        var prop = typeof(AppSettings).GetProperty(nameof(AppSettings.OverlayOpacity));
        Assert.NotNull(prop);
        var obsolete = prop!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
        Assert.NotEmpty(obsolete);
    }

    [Fact]
    public void HotkeyFields_DefaultToEnabledConfiguredShortcuts()
    {
        var s = new AppSettings();

        Assert.True(s.HotkeyScreenshotEnabled);
        Assert.True(s.HotkeyFullscreenEnabled);
        Assert.True(s.HotkeyRegionVideoEnabled);
        Assert.Equal("PrintScreen", s.HotkeyScreenshot);
        Assert.Equal("Alt+PrintScreen", s.HotkeyFullscreen);
        Assert.Equal("Alt+R", s.HotkeyRegionVideo);
    }

    [Fact]
    public void HotkeyFields_AreNoLongerObsolete()
    {
        foreach (var name in new[] { "HotkeyScreenshot", "HotkeyFullscreen", "HotkeyRegionVideo" })
        {
            var prop = typeof(AppSettings).GetProperty(name);
            Assert.NotNull(prop);
            var obsolete = prop!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
            Assert.Empty(obsolete);
        }
    }
}
