namespace parallax.Core.Models
{
    public class AppSettings
    {
        public string SaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\parallax_captures";
        public string ImageFormat { get; set; } = "png";
        public bool CopyToClipboardAfterCapture { get; set; } = true;
        [Obsolete("Reserved for future use — annotation window always opens. Will be wired when skip-annotation mode is implemented.")]
        public bool ShowToolbarAfterCapture { get; set; } = true;
        public bool SaveAutomatically { get; set; } = false;
        public bool SeparateFolders { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        [Obsolete("Reserved for future use — overlay opacity is fixed in XAML. Will be wired when per-user opacity setting is implemented.")]
        public int OverlayOpacity { get; set; } = 120;
        [Obsolete("Reserved for future use — hotkeys are hard-coded in App.xaml.cs. Will be wired when custom hotkey UI is implemented.")]
        public string HotkeyScreenshot { get; set; } = "PrintScreen";
        [Obsolete("Reserved for future use — hotkeys are hard-coded in App.xaml.cs.")]
        public string HotkeyRegionVideo { get; set; } = "Alt+R";
        [Obsolete("Reserved for future use — hotkeys are hard-coded in App.xaml.cs.")]
        public string HotkeyFullscreen { get; set; } = "Alt+PrintScreen";
    }
}
