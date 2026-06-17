using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Settings;

public sealed class SettingsWindowModel
{
    public SettingsWindowModel(ParallaxSettings settings)
    {
        SaveFolder = settings.SaveFolder;
        ImageFormat = settings.ImageFormat;
        CopyToClipboardAfterCapture = settings.CopyToClipboardAfterCapture;
        SaveAutomatically = settings.SaveAutomatically;
        OpenAnnotationEditorAfterScreenshot = settings.OpenAnnotationEditorAfterScreenshot;
        OpenVideoEditorAfterRecording = settings.OpenVideoEditorAfterRecording;
        SeparateFolders = settings.SeparateFolders;
        StartWithSystem = settings.StartWithSystem;
        HotkeyScreenshotEnabled = settings.HotkeyScreenshotEnabled;
        HotkeyFullscreenEnabled = settings.HotkeyFullscreenEnabled;
        HotkeyRegionVideoEnabled = settings.HotkeyRegionVideoEnabled;
        HotkeyScreenshot = settings.HotkeyScreenshot;
        HotkeyFullscreen = settings.HotkeyFullscreen;
        HotkeyRegionVideo = settings.HotkeyRegionVideo;
    }

    public string SaveFolder { get; set; }

    public string ImageFormat { get; set; }

    public bool CopyToClipboardAfterCapture { get; set; }

    public bool SaveAutomatically { get; set; }

    public bool OpenAnnotationEditorAfterScreenshot { get; set; }

    public bool OpenVideoEditorAfterRecording { get; set; }

    public bool SeparateFolders { get; set; }

    public bool StartWithSystem { get; set; }

    public bool HotkeyScreenshotEnabled { get; set; }

    public bool HotkeyFullscreenEnabled { get; set; }

    public bool HotkeyRegionVideoEnabled { get; set; }

    public string HotkeyScreenshot { get; set; }

    public string HotkeyFullscreen { get; set; }

    public string HotkeyRegionVideo { get; set; }

    public RuntimeSettingsApplyResult Save(
        ParallaxSettings target,
        RuntimeSettingsApplier applier,
        string executablePath)
    {
        ApplyTo(target);
        return applier.Apply(target, executablePath);
    }

    public void ApplyTo(ParallaxSettings target)
    {
        target.SaveFolder = SaveFolder.Trim();
        target.ImageFormat = NormalizeImageFormat(ImageFormat);
        target.CopyToClipboardAfterCapture = CopyToClipboardAfterCapture;
        target.SaveAutomatically = SaveAutomatically;
        target.OpenAnnotationEditorAfterScreenshot = OpenAnnotationEditorAfterScreenshot;
        target.OpenVideoEditorAfterRecording = OpenVideoEditorAfterRecording;
        target.SeparateFolders = SeparateFolders;
        target.StartWithSystem = StartWithSystem;
        target.HotkeyScreenshotEnabled = HotkeyScreenshotEnabled;
        target.HotkeyFullscreenEnabled = HotkeyFullscreenEnabled;
        target.HotkeyRegionVideoEnabled = HotkeyRegionVideoEnabled;
        target.HotkeyScreenshot = HotkeyScreenshot.Trim();
        target.HotkeyFullscreen = HotkeyFullscreen.Trim();
        target.HotkeyRegionVideo = HotkeyRegionVideo.Trim();
    }

    private static string NormalizeImageFormat(string? imageFormat)
    {
        string normalized = imageFormat?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return normalized is "jpg" or "jpeg"
            ? "jpeg"
            : normalized is "bmp"
                ? "bmp"
                : "png";
    }
}
