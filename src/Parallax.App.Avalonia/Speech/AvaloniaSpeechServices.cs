using Avalonia.Controls;
using Parallax.Core.Platform;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

public static class AvaloniaSpeechServices
{
    public static SpeechToTextWorkflow CreateWorkflow(IPlatformBackend platform, Window? owner = null)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        return new SpeechToTextWorkflow(
            CreateRecorder(platform),
            CreateTranscriber(httpClient, platform),
            new AvaloniaTranscribedTextOutput(
                owner?.Clipboard == null ? null : new AvaloniaTextClipboard(owner.Clipboard),
                PlatformKeyChordSender.Create(platform.Info.Kind)),
            new FileTranscriptionHistoryStore(platform.Locations),
            new AvaloniaSpeechFeedbackService());
    }

    private static ISpeechRecorder CreateRecorder(IPlatformBackend platform)
    {
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
        if (platform.Info.Kind == PlatformKind.Windows)
        {
            return new WindowsMicrophoneSpeechRecorder();
        }
#endif

        return new UnsupportedSpeechRecorder(platform.Capabilities.SpeechToText);
    }

    private static ISpeechTranscriber CreateTranscriber(HttpClient httpClient, IPlatformBackend platform)
    {
        return platform.Capabilities.SpeechToText.State == CapabilityState.Supported
            ? new SpeechProviderRouterTranscriber(
                new OpenAiCompatibleSpeechTranscriber(httpClient),
                new LocalWhisperCliTranscriber())
            : new UnsupportedSpeechTranscriber(platform.Capabilities.SpeechToText);
    }
}
