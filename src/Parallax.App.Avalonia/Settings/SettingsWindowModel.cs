using Parallax.Core.Settings;
using Parallax.Core.Speech;

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
        SpeechToTextEnabled = settings.SpeechToTextEnabled;
        SpeechShortcut = settings.SpeechShortcut;
        SpeechShortcutMode = settings.SpeechShortcutMode;
        SpeechProvider = settings.SpeechProvider;
        SpeechApiBaseUrl = settings.SpeechApiBaseUrl;
        SpeechApiKey = settings.SpeechApiKey;
        SpeechModel = settings.SpeechModel;
        SpeechLanguage = settings.SpeechLanguage;
        SpeechMicrophoneDeviceId = settings.SpeechMicrophoneDeviceId;
        SpeechFeedbackOutputDeviceId = settings.SpeechFeedbackOutputDeviceId;
        SpeechFeedbackSoundsEnabled = settings.SpeechFeedbackSoundsEnabled;
        SpeechHiddenLauncherOnStart = settings.SpeechHiddenLauncherOnStart;
        SpeechShowTrayIcon = settings.SpeechShowTrayIcon;
        SpeechTrayOrderPosition = settings.SpeechTrayOrderPosition;
        SpeechModelUnloadPolicy = settings.SpeechModelUnloadPolicy;
        SpeechUnloadModelAfterMinutes = settings.SpeechUnloadModelAfterMinutes;
        SpeechPasteMethod = settings.SpeechPasteMethod;
        SpeechCopyToClipboard = settings.SpeechCopyToClipboard;
        SpeechAutoSubmit = settings.SpeechAutoSubmit;
        SpeechCustomWords = settings.SpeechCustomWords;
        SpeechAppendTrailingSpace = settings.SpeechAppendTrailingSpace;
        SpeechMaxHistoryEntries = settings.SpeechMaxHistoryEntries;
        SpeechHistoryRetentionDays = settings.SpeechHistoryRetentionDays;
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

    public bool SpeechToTextEnabled { get; set; }

    public string SpeechShortcut { get; set; }

    public SpeechShortcutMode SpeechShortcutMode { get; set; }

    public SpeechProviderKind SpeechProvider { get; set; }

    public string SpeechApiBaseUrl { get; set; }

    public string SpeechApiKey { get; set; }

    public string SpeechModel { get; set; }

    public string SpeechLanguage { get; set; }

    public string SpeechMicrophoneDeviceId { get; set; }

    public string SpeechFeedbackOutputDeviceId { get; set; }

    public bool SpeechFeedbackSoundsEnabled { get; set; }

    public bool SpeechHiddenLauncherOnStart { get; set; }

    public bool SpeechShowTrayIcon { get; set; }

    public int SpeechTrayOrderPosition { get; set; }

    public SpeechModelUnloadPolicy SpeechModelUnloadPolicy { get; set; }

    public int SpeechUnloadModelAfterMinutes { get; set; }

    public SpeechPasteMethod SpeechPasteMethod { get; set; }

    public bool SpeechCopyToClipboard { get; set; }

    public bool SpeechAutoSubmit { get; set; }

    public string SpeechCustomWords { get; set; }

    public bool SpeechAppendTrailingSpace { get; set; }

    public int SpeechMaxHistoryEntries { get; set; }

    public int SpeechHistoryRetentionDays { get; set; }

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
        target.SpeechToTextEnabled = SpeechToTextEnabled;
        target.SpeechShortcut = SpeechShortcut.Trim();
        target.SpeechShortcutMode = SpeechShortcutMode;
        target.SpeechProvider = SpeechProvider;
        target.SpeechApiBaseUrl = SpeechApiBaseUrl.Trim();
        target.SpeechApiKey = SpeechApiKey.Trim();
        target.SpeechModel = SpeechModel.Trim();
        target.SpeechLanguage = NormalizeAuto(SpeechLanguage);
        target.SpeechMicrophoneDeviceId = SpeechMicrophoneDeviceId.Trim();
        target.SpeechFeedbackOutputDeviceId = SpeechFeedbackOutputDeviceId.Trim();
        target.SpeechFeedbackSoundsEnabled = SpeechFeedbackSoundsEnabled;
        target.SpeechHiddenLauncherOnStart = SpeechHiddenLauncherOnStart;
        target.SpeechShowTrayIcon = SpeechShowTrayIcon;
        target.SpeechTrayOrderPosition = Math.Max(0, SpeechTrayOrderPosition);
        target.SpeechModelUnloadPolicy = SpeechModelUnloadPolicy;
        target.SpeechUnloadModelAfterMinutes = Math.Max(0, SpeechUnloadModelAfterMinutes);
        target.SpeechPasteMethod = SpeechPasteMethod;
        target.SpeechCopyToClipboard = SpeechCopyToClipboard;
        target.SpeechAutoSubmit = SpeechAutoSubmit;
        target.SpeechCustomWords = SpeechCustomWords.Trim();
        target.SpeechAppendTrailingSpace = SpeechAppendTrailingSpace;
        target.SpeechMaxHistoryEntries = Math.Max(1, SpeechMaxHistoryEntries);
        target.SpeechHistoryRetentionDays = Math.Max(0, SpeechHistoryRetentionDays);
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

    private static string NormalizeAuto(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? "auto" : normalized;
    }
}
