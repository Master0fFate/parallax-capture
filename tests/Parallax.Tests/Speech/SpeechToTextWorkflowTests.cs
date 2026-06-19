using Parallax.Core.Settings;
using Parallax.Core.Speech;
using static Parallax.Tests.Speech.SpeechTestSupport;

namespace Parallax.Tests.Speech;

public sealed class SpeechToTextWorkflowTests
{
    [Fact]
    public async Task TranscribeOnceAsync_records_history_applies_dictionary_and_appends_space()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-workflow", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            Directory.CreateDirectory(root);
            var settings = ParallaxSettings.CreateDefaults(Path.Combine(root, "captures"));
            settings.SpeechCustomWords = "ParallaxCapture=parallel axe capture";
            settings.SpeechAppendTrailingSpace = true;
            settings.SpeechMaxHistoryEntries = 3;
            settings.SpeechHistoryRetentionDays = 7;
            var recorder = new FakeSpeechRecorder(new SpeechRecordingResult(
                Success: true,
                AudioPath: Path.Combine(root, "take.wav"),
                Duration: TimeSpan.FromSeconds(2),
                Message: "recorded"));
            File.WriteAllBytes(recorder.NextResult.AudioPath!, [1, 2, 3, 4]);
            var output = new FakeTextOutput();
            var workflow = new SpeechToTextWorkflow(
                recorder,
                new FakeTranscriber("parallel axe capture"),
                output,
                new FileTranscriptionHistoryStore(locations),
                new SilentSpeechFeedbackService());

            var result = await workflow.TranscribeOnceAsync(settings, locations, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("ParallaxCapture ", result.Text);
            Assert.Equal("ParallaxCapture ", output.InsertedText);
            Assert.Equal(SpeechPasteMethod.CtrlV, output.PasteMethod);
            Assert.Contains("ParallaxCapture", File.ReadAllText(Path.Combine(locations.TranscriptionHistoryDirectory, "history.jsonl")));
            Assert.True(File.Exists(Path.Combine(locations.TranscriptionHistoryDirectory, "recordings", "take.wav")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Custom_words_replace_declared_aliases_without_discarding_surrounding_text()
    {
        string text = SpeechTextPostProcessor.Apply(
            "open parallel axe capture after this sentence",
            "ParallaxCapture=parallel axe capture|parallax capture",
            appendTrailingSpace: false);

        Assert.Equal("open ParallaxCapture after this sentence", text);
    }
}
