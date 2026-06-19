using Parallax.Core.Platform;

namespace Parallax.Core.Speech;

public sealed class SpeechToTextWorkflow
{
    private readonly ISpeechRecorder _recorder;
    private readonly ISpeechTranscriber _transcriber;
    private readonly ITranscribedTextOutput _output;
    private readonly ITranscriptionHistoryStore _history;
    private readonly ISpeechFeedbackService _feedback;

    public SpeechToTextWorkflow(
        ISpeechRecorder recorder,
        ISpeechTranscriber transcriber,
        ITranscribedTextOutput output,
        ITranscriptionHistoryStore history,
        ISpeechFeedbackService feedback)
    {
        _recorder = recorder;
        _transcriber = transcriber;
        _output = output;
        _history = history;
        _feedback = feedback;
    }

    public async Task<SpeechWorkflowResult> TranscribeOnceAsync(
        Settings.ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        _feedback.Started(settings);
        SpeechRecordingResult recording;
        try
        {
            recording = await _recorder.RecordOnceAsync(settings, locations, cancellationToken);
        }
        finally
        {
            _feedback.Stopped(settings);
        }

        if (!recording.Success || string.IsNullOrWhiteSpace(recording.AudioPath))
        {
            return new SpeechWorkflowResult(false, string.Empty, null, recording.Message);
        }

        var transcription = await _transcriber.TranscribeAsync(
            recording.AudioPath,
            settings,
            locations,
            cancellationToken);
        if (!transcription.Success)
        {
            return new SpeechWorkflowResult(false, string.Empty, recording.AudioPath, transcription.Message);
        }

        string text = SpeechTextPostProcessor.Apply(
            transcription.Text,
            settings.SpeechCustomWords,
            settings.SpeechAppendTrailingSpace);
        var output = await _output.InsertAsync(
            text,
            settings.SpeechPasteMethod,
            settings.SpeechCopyToClipboard,
            settings.SpeechAutoSubmit,
            cancellationToken);
        if (!output.Success)
        {
            return new SpeechWorkflowResult(false, text, recording.AudioPath, output.Message);
        }

        _history.Append(new TranscriptionHistoryEntry(
            DateTimeOffset.UtcNow,
            text,
            recording.AudioPath,
            recording.Duration));
        _history.Cleanup(settings.SpeechMaxHistoryEntries, settings.SpeechHistoryRetentionDays, DateTimeOffset.UtcNow);
        return new SpeechWorkflowResult(true, text, recording.AudioPath, "Transcription inserted.");
    }
}
