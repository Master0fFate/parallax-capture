using Parallax.App.Avalonia.Speech;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;
using Parallax.Core.Speech;

namespace Parallax.Tests.Speech;

internal static class SpeechTestSupport
{
    public static PlatformLocations TestLocations(string root)
    {
        return new PlatformLocations(
            PlatformKind.Windows,
            ConfigDirectory: Path.Combine(root, "config"),
            SettingsFilePath: Path.Combine(root, "config", "settings.json"),
            LogsDirectory: Path.Combine(root, "logs"),
            ToolsDirectory: Path.Combine(root, "tools"),
            TempDirectory: Path.Combine(root, "temp"),
            ScreenshotsDirectory: Path.Combine(root, "pictures"),
            RecordingsDirectory: Path.Combine(root, "videos"),
            TranscriptionHistoryDirectory: Path.Combine(root, "transcriptions"),
            SpeechModelsDirectory: Path.Combine(root, "models"));
    }

    public static PlatformCapabilitySet CapabilitySet()
    {
        return new PlatformCapabilitySet(
            ScreenCapture: CapabilityResult.Supported("capture"),
            ScreenRecording: CapabilityResult.Supported("recording"),
            GlobalHotkeys: CapabilityResult.Supported("hotkeys"),
            Clipboard: CapabilityResult.Supported("clipboard"),
            StartupRegistration: CapabilityResult.Supported("startup"),
            CaptureExclusion: CapabilityResult.Supported("exclusion"),
            SpeechToText: CapabilityResult.Supported("speech"));
    }
}

internal sealed class FakeSpeechRecorder : ISpeechRecorder
{
    public FakeSpeechRecorder(SpeechRecordingResult nextResult)
    {
        NextResult = nextResult;
    }

    public SpeechRecordingResult NextResult { get; }

    public Task<SpeechRecordingResult> RecordOnceAsync(
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeTranscriber : ISpeechTranscriber
{
    private readonly string _text;

    public FakeTranscriber(string text)
    {
        _text = text;
    }

    public Task<SpeechTranscriptionResult> TranscribeAsync(
        string audioPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SpeechTranscriptionResult(true, _text, "ok"));
    }
}

internal sealed class FakeTextOutput : ITranscribedTextOutput
{
    public string InsertedText { get; private set; } = string.Empty;

    public SpeechPasteMethod PasteMethod { get; private set; }

    public bool AutoSubmit { get; private set; }

    public Task<SpeechTextOutputResult> InsertAsync(
        string text,
        SpeechPasteMethod pasteMethod,
        bool copyToClipboard,
        bool autoSubmit,
        CancellationToken cancellationToken)
    {
        InsertedText = text;
        PasteMethod = pasteMethod;
        AutoSubmit = autoSubmit;
        return Task.FromResult(new SpeechTextOutputResult(true, "inserted"));
    }
}

internal sealed class FakeKeyChordSender : IKeyChordSender
{
    public List<string> Sent { get; } = [];

    public SpeechTextOutputResult SendPaste(SpeechPasteMethod pasteMethod)
    {
        Sent.Add(pasteMethod.ToString());
        return pasteMethod == SpeechPasteMethod.ExternalScript
            ? new SpeechTextOutputResult(false, "not configured")
            : new SpeechTextOutputResult(true, "paste");
    }

    public SpeechTextOutputResult SendAutoSubmit()
    {
        Sent.Add("AutoSubmit");
        return new SpeechTextOutputResult(true, "submit");
    }
}

internal sealed class FakeTextClipboard : ITextClipboard
{
    public string Text { get; private set; } = string.Empty;

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
