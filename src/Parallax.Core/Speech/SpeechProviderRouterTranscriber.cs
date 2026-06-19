using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Speech;

public sealed class SpeechProviderRouterTranscriber : ISpeechTranscriber
{
    private readonly ISpeechTranscriber _openAiCompatible;
    private readonly ISpeechTranscriber _localWhisper;

    public SpeechProviderRouterTranscriber(
        ISpeechTranscriber openAiCompatible,
        ISpeechTranscriber localWhisper)
    {
        _openAiCompatible = openAiCompatible;
        _localWhisper = localWhisper;
    }

    public Task<SpeechTranscriptionResult> TranscribeAsync(
        string audioPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        return settings.SpeechProvider == SpeechProviderKind.LocalWhisper
            ? _localWhisper.TranscribeAsync(audioPath, settings, locations, cancellationToken)
            : _openAiCompatible.TranscribeAsync(audioPath, settings, locations, cancellationToken);
    }
}
