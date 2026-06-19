using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Speech;

public enum SpeechProviderKind
{
    OpenAiCompatible,
    LocalWhisper
}

public enum SpeechShortcutMode
{
    PushToTalk,
    Toggle
}

public enum SpeechPasteMethod
{
    None,
    DirectClipboard,
    CtrlV,
    CtrlShiftV,
    ShiftInsert,
    ExternalScript
}

public enum SpeechModelUnloadPolicy
{
    Never,
    Immediately,
    AfterIdle
}

public sealed record SpeechRecordingResult(
    bool Success,
    string? AudioPath,
    TimeSpan Duration,
    string Message);

public sealed record SpeechTranscriptionResult(
    bool Success,
    string Text,
    string Message);

public sealed record SpeechTextOutputResult(bool Success, string Message);

public sealed record SpeechWorkflowResult(
    bool Success,
    string Text,
    string? AudioPath,
    string Message);

public sealed record TranscriptionHistoryEntry(
    DateTimeOffset CreatedAt,
    string Text,
    string AudioPath,
    TimeSpan Duration);

public sealed record SpeechModelManifest(
    string Id,
    string DisplayName,
    string FileName,
    Uri DownloadUri,
    string Sha256,
    long SizeBytes,
    string[] Languages);

public interface ISpeechRecorder
{
    Task<SpeechRecordingResult> RecordOnceAsync(
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken);
}

public interface ISpeechTranscriber
{
    Task<SpeechTranscriptionResult> TranscribeAsync(
        string audioPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken);
}

public interface ITranscribedTextOutput
{
    Task<SpeechTextOutputResult> InsertAsync(
        string text,
        SpeechPasteMethod pasteMethod,
        bool copyToClipboard,
        bool autoSubmit,
        CancellationToken cancellationToken);
}

public interface ITranscriptionHistoryStore
{
    void Append(TranscriptionHistoryEntry entry);

    void Cleanup(int maxEntries, int retentionDays, DateTimeOffset now);
}

public interface ISpeechFeedbackService
{
    void Started(ParallaxSettings settings);

    void Stopped(ParallaxSettings settings);
}

public sealed class SilentSpeechFeedbackService : ISpeechFeedbackService
{
    public void Started(ParallaxSettings settings)
    {
    }

    public void Stopped(ParallaxSettings settings)
    {
    }
}
