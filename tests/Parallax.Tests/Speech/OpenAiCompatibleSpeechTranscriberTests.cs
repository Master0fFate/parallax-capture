using System.Net;
using System.Net.Http;
using Parallax.Core.Settings;
using Parallax.Core.Speech;
using static Parallax.Tests.Speech.SpeechTestSupport;

namespace Parallax.Tests.Speech;

public sealed class OpenAiCompatibleSpeechTranscriberTests
{
    [Fact]
    public async Task TranscribeAsync_posts_multipart_audio_model_and_language()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-api", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string audioPath = Path.Combine(root, "sample.wav");
            File.WriteAllBytes(audioPath, [1, 2, 3]);
            var handler = new CapturingHandler();
            var transcriber = new OpenAiCompatibleSpeechTranscriber(new HttpClient(handler));
            var settings = ParallaxSettings.CreateDefaults(root);
            settings.SpeechApiBaseUrl = "https://api.groq.com/openai/v1";
            settings.SpeechApiKey = "gsk_test";
            settings.SpeechModel = "whisper-large-v3-turbo";
            settings.SpeechLanguage = "en";

            var result = await transcriber.TranscribeAsync(audioPath, settings, TestLocations(root), CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("hello parallax", result.Text);
            Assert.Equal("https://api.groq.com/openai/v1/audio/transcriptions", handler.RequestUri);
            Assert.Equal("Bearer", handler.AuthorizationScheme);
            Assert.Equal("gsk_test", handler.AuthorizationParameter);
            Assert.Contains("name=model", handler.MultipartBody);
            Assert.Contains("whisper-large-v3-turbo", handler.MultipartBody);
            Assert.Contains("name=language", handler.MultipartBody);
            Assert.Contains("en", handler.MultipartBody);
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
    public async Task TranscribeAsync_returns_failures_for_invalid_url_network_error_and_malformed_json()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-api-failures", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string audioPath = Path.Combine(root, "sample.wav");
            File.WriteAllBytes(audioPath, [1, 2, 3]);
            var locations = TestLocations(root);
            var settings = ParallaxSettings.CreateDefaults(root);
            settings.SpeechModel = "whisper-large-v3-turbo";

            settings.SpeechApiBaseUrl = "not a url";
            var invalidUrl = await new OpenAiCompatibleSpeechTranscriber(new HttpClient(new CapturingHandler()))
                .TranscribeAsync(audioPath, settings, locations, CancellationToken.None);

            settings.SpeechApiBaseUrl = "https://api.example.test/v1";
            var network = await new OpenAiCompatibleSpeechTranscriber(new HttpClient(new ThrowingHandler()))
                .TranscribeAsync(audioPath, settings, locations, CancellationToken.None);
            var nonSuccess = await new OpenAiCompatibleSpeechTranscriber(new HttpClient(new StaticHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}")))
                .TranscribeAsync(audioPath, settings, locations, CancellationToken.None);
            var malformed = await new OpenAiCompatibleSpeechTranscriber(new HttpClient(new StaticHandler(HttpStatusCode.OK, "not-json")))
                .TranscribeAsync(audioPath, settings, locations, CancellationToken.None);

            Assert.False(invalidUrl.Success);
            Assert.Contains("invalid", invalidUrl.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(network.Success);
            Assert.False(nonSuccess.Success);
            Assert.Contains("400", nonSuccess.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(malformed.Success);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
