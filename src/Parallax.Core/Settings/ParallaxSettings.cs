namespace Parallax.Core.Settings;

using Parallax.Core.Speech;

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

    public bool HotkeyScreenshotEnabled { get; set; } = true;

    public bool HotkeyFullscreenEnabled { get; set; } = true;

    public bool HotkeyRegionVideoEnabled { get; set; } = true;

    public string HotkeyScreenshot { get; set; } = "PrintScreen";

    public string HotkeyRegionVideo { get; set; } = "Alt+R";

    public string HotkeyFullscreen { get; set; } = "Alt+PrintScreen";

    public bool SpeechToTextEnabled { get; set; }

    public string SpeechShortcut { get; set; } = "Ctrl+Shift+D";

    public SpeechShortcutMode SpeechShortcutMode { get; set; } = SpeechShortcutMode.PushToTalk;

    public SpeechProviderKind SpeechProvider { get; set; } = SpeechProviderKind.OpenAiCompatible;

    public string SpeechApiBaseUrl { get; set; } = "https://api.openai.com/v1";

    public string SpeechApiKey { get; set; } = string.Empty;

    public string SpeechModel { get; set; } = "gpt-4o-mini-transcribe";

    public string SpeechLanguage { get; set; } = "auto";

    public string SpeechMicrophoneDeviceId { get; set; } = string.Empty;

    public string SpeechFeedbackOutputDeviceId { get; set; } = string.Empty;

    public bool SpeechFeedbackSoundsEnabled { get; set; } = true;

    public bool SpeechHiddenLauncherOnStart { get; set; } = true;

    public bool SpeechShowTrayIcon { get; set; } = true;

    public int SpeechTrayOrderPosition { get; set; } = 40;

    public SpeechModelUnloadPolicy SpeechModelUnloadPolicy { get; set; } = SpeechModelUnloadPolicy.AfterIdle;

    public int SpeechUnloadModelAfterMinutes { get; set; } = 5;

    public SpeechPasteMethod SpeechPasteMethod { get; set; } = SpeechPasteMethod.CtrlV;

    public bool SpeechCopyToClipboard { get; set; }

    public bool SpeechAutoSubmit { get; set; }

    public string SpeechCustomWords { get; set; } = string.Empty;

    public bool SpeechAppendTrailingSpace { get; set; }

    public int SpeechMaxHistoryEntries { get; set; } = 100;

    public int SpeechHistoryRetentionDays { get; set; } = 30;

    public static ParallaxSettings CreateDefaults(string saveFolder) => new()
    {
        SaveFolder = saveFolder
    };
}
