using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Speech;
using static Parallax.Tests.Speech.SpeechTestSupport;

namespace Parallax.Tests.Speech;

public sealed class LocalWhisperCliTranscriberTests
{
    [Fact]
    public async Task TranscribeAsync_runs_whisper_cli_with_model_audio_output_and_language()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-local-whisper", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            Directory.CreateDirectory(locations.SpeechModelsDirectory);
            Directory.CreateDirectory(locations.TempDirectory);
            string audioPath = Path.Combine(root, "sample.wav");
            string modelPath = Path.Combine(locations.SpeechModelsDirectory, "ggml-tiny.bin");
            File.WriteAllBytes(audioPath, [1, 2, 3]);
            File.WriteAllBytes(modelPath, [4, 5, 6]);
            var executor = new CapturingWhisperExecutor("hello from local whisper");
            var transcriber = new LocalWhisperCliTranscriber(new StaticWhisperLocator("C:\\tools\\whisper-cli.exe"), executor);
            var settings = ParallaxSettings.CreateDefaults(root);
            settings.SpeechProvider = SpeechProviderKind.LocalWhisper;
            settings.SpeechModel = "whisper-tiny";
            settings.SpeechLanguage = "en";

            var result = await transcriber.TranscribeAsync(audioPath, settings, locations, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("hello from local whisper", result.Text);
            Assert.NotNull(executor.Request);
            Assert.Equal("C:\\tools\\whisper-cli.exe", executor.Request.ExecutablePath);
            Assert.Contains("-m", executor.Request.Arguments);
            Assert.Contains(modelPath, executor.Request.Arguments);
            Assert.Contains("-f", executor.Request.Arguments);
            Assert.Contains(audioPath, executor.Request.Arguments);
            Assert.Contains("-otxt", executor.Request.Arguments);
            Assert.Contains("-of", executor.Request.Arguments);
            Assert.Contains("-l", executor.Request.Arguments);
            Assert.Contains("en", executor.Request.Arguments);
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
    public async Task TranscribeAsync_reports_missing_model_and_missing_cli()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-local-whisper-missing", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(locations.SpeechModelsDirectory);
            string audioPath = Path.Combine(root, "sample.wav");
            File.WriteAllBytes(audioPath, [1, 2, 3]);
            var settings = ParallaxSettings.CreateDefaults(root);
            settings.SpeechProvider = SpeechProviderKind.LocalWhisper;
            settings.SpeechModel = "whisper-tiny";
            var transcriber = new LocalWhisperCliTranscriber(new StaticWhisperLocator(null), new CapturingWhisperExecutor("unused"));

            var missingModel = await transcriber.TranscribeAsync(audioPath, settings, locations, CancellationToken.None);

            File.WriteAllBytes(Path.Combine(locations.SpeechModelsDirectory, "ggml-tiny.bin"), [4, 5, 6]);
            var missingCli = await transcriber.TranscribeAsync(audioPath, settings, locations, CancellationToken.None);

            Assert.False(missingModel.Success);
            Assert.Contains("model", missingModel.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(missingCli.Success);
            Assert.Contains("Whisper CLI", missingCli.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task Provider_router_uses_selected_transcriber()
    {
        var openAi = new RecordingTranscriber("api");
        var local = new RecordingTranscriber("local");
        var router = new SpeechProviderRouterTranscriber(openAi, local);
        string root = Path.Combine(Path.GetTempPath(), "parallax-router", Guid.NewGuid().ToString("N"));
        var settings = ParallaxSettings.CreateDefaults(root);
        var locations = TestLocations(root);

        settings.SpeechProvider = SpeechProviderKind.OpenAiCompatible;
        var apiResult = await router.TranscribeAsync("audio.wav", settings, locations, CancellationToken.None);
        settings.SpeechProvider = SpeechProviderKind.LocalWhisper;
        var localResult = await router.TranscribeAsync("audio.wav", settings, locations, CancellationToken.None);

        Assert.Equal("api", apiResult.Text);
        Assert.Equal("local", localResult.Text);
        Assert.Equal(1, openAi.Calls);
        Assert.Equal(1, local.Calls);
    }

    private sealed class StaticWhisperLocator : IWhisperCliExecutableLocator
    {
        private readonly string? _executablePath;

        public StaticWhisperLocator(string? executablePath)
        {
            _executablePath = executablePath;
        }

        public string? FindExecutable(IPlatformLocations locations)
        {
            return _executablePath;
        }
    }

    private sealed class CapturingWhisperExecutor : IWhisperCliProcessExecutor
    {
        private readonly string _text;

        public CapturingWhisperExecutor(string text)
        {
            _text = text;
        }

        public WhisperCliRunRequest? Request { get; private set; }

        public Task<WhisperCliProcessResult> ExecuteAsync(
            WhisperCliRunRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            File.WriteAllText(request.OutputTextPath, _text);
            return Task.FromResult(new WhisperCliProcessResult(0, TimedOut: false, string.Empty, string.Empty));
        }
    }

    private sealed class RecordingTranscriber : ISpeechTranscriber
    {
        private readonly string _text;

        public RecordingTranscriber(string text)
        {
            _text = text;
        }

        public int Calls { get; private set; }

        public Task<SpeechTranscriptionResult> TranscribeAsync(
            string audioPath,
            ParallaxSettings settings,
            IPlatformLocations locations,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new SpeechTranscriptionResult(true, _text, "ok"));
        }
    }
}
