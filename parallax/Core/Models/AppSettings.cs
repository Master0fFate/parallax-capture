namespace parallax.Core.Models
{
    public class AppSettings
    {
        public string SaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\parallax_captures";
        public string ImageFormat { get; set; } = "png";
        public bool CopyToClipboardAfterCapture { get; set; } = true;
        public bool ShowToolbarAfterCapture { get; set; } = true;
        public bool SaveAutomatically { get; set; } = false;
        public bool SeparateFolders { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public int OverlayOpacity { get; set; } = 120;
        public string HotkeyScreenshot { get; set; } = "PrintScreen";
        public string HotkeyRegionVideo { get; set; } = "Alt+R";
        public string HotkeyFullscreen { get; set; } = "Alt+PrintScreen";
    }
}
