namespace parallax.Core.Models
{
    public class AppSettings
    {
        public string SaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\parallax_captures";
        public string ImageFormat { get; set; } = "png";
        public bool CopyToClipboardAfterCapture { get; set; } = true;
        [Obsolete("Reserved for future use. Annotation window always opens. Will be wired when skip-annotation mode is implemented.")]
        public bool ShowToolbarAfterCapture { get; set; } = true;
        public bool SaveAutomatically { get; set; } = false;
        public bool SeparateFolders { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        [Obsolete("Reserved for future use. Overlay opacity is fixed in XAML. Will be wired when per-user opacity setting is implemented.")]
        public int OverlayOpacity { get; set; } = 120;
        public bool HotkeyScreenshotEnabled { get; set; } = true;
        public bool HotkeyFullscreenEnabled { get; set; } = true;
        public bool HotkeyRegionVideoEnabled { get; set; } = true;
        public string HotkeyScreenshot { get; set; } = "PrintScreen";
        public string HotkeyRegionVideo { get; set; } = "Alt+R";
        public string HotkeyFullscreen { get; set; } = "Alt+PrintScreen";
    }
}
