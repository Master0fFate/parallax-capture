using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

internal sealed class UnsupportedSpeechRecorder : ISpeechRecorder
{
    private readonly CapabilityResult _capability;

    public UnsupportedSpeechRecorder(CapabilityResult capability)
    {
        _capability = capability;
    }

    public Task<SpeechRecordingResult> RecordOnceAsync(
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SpeechRecordingResult(
            false,
            null,
            TimeSpan.Zero,
            $"Speech recording needs a platform microphone recorder. {_capability.Message}"));
    }
}

internal sealed class UnsupportedSpeechTranscriber : ISpeechTranscriber
{
    private readonly CapabilityResult _capability;

    public UnsupportedSpeechTranscriber(CapabilityResult capability)
    {
        _capability = capability;
    }

    public Task<SpeechTranscriptionResult> TranscribeAsync(
        string audioPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SpeechTranscriptionResult(
            false,
            string.Empty,
            $"Speech transcription is unavailable. {_capability.Message}"));
    }
}
