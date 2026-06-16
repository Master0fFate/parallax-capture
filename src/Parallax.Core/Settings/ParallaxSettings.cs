namespace Parallax.Core.Settings;

public sealed class ParallaxSettings
{
    public string SaveFolder { get; set; } = string.Empty;

    public string ImageFormat { get; set; } = "png";

    public bool CopyToClipboardAfterCapture { get; set; } = true;

    public bool SaveAutomatically { get; set; }

    public bool OpenAnnotationEditorAfterScreenshot { get; set; } = true;

    public bool OpenVideoEditorAfterRecording { get; set; } = true;

    public bool SeparateFolders { get; set; }

    public bool StartWithSystem { get; set; }

    public string ThemeFamily { get; set; } = "Material 3";

    public string ThemeMode { get; set; } = "Dark";

    public bool HotkeyScreenshotEnabled { get; set; } = true;

    public bool HotkeyFullscreenEnabled { get; set; } = true;

    public bool HotkeyRegionVideoEnabled { get; set; } = true;

    public string HotkeyScreenshot { get; set; } = "PrintScreen";

    public string HotkeyRegionVideo { get; set; } = "Alt+R";

    public string HotkeyFullscreen { get; set; } = "Alt+PrintScreen";

    public static ParallaxSettings CreateDefaults(string saveFolder) => new()
    {
        SaveFolder = saveFolder
    };
}
