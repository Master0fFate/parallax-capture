using System.Net.Http.Headers;
using System.Text.Json;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Speech;

public sealed class OpenAiCompatibleSpeechTranscriber : ISpeechTranscriber
{
    private readonly HttpClient _client;

    public OpenAiCompatibleSpeechTranscriber(HttpClient client)
    {
        _client = client;
    }

    public async Task<SpeechTranscriptionResult> TranscribeAsync(
        string audioPath,
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(audioPath))
            {
                return new SpeechTranscriptionResult(false, string.Empty, "Audio recording was not found.");
            }

            if (string.IsNullOrWhiteSpace(settings.SpeechApiBaseUrl))
            {
                return new SpeechTranscriptionResult(false, string.Empty, "Speech-to-text API base URL is empty.");
            }

            if (!TryCreateTranscriptionUri(settings.SpeechApiBaseUrl, out var transcriptionUri))
            {
                return new SpeechTranscriptionResult(false, string.Empty, "Speech-to-text API base URL is invalid.");
            }

            using var content = new MultipartFormDataContent();
            var audio = new StreamContent(File.OpenRead(audioPath));
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audio, "file", Path.GetFileName(audioPath));
            content.Add(new StringContent(settings.SpeechModel), "model");
            content.Add(new StringContent("json"), "response_format");
            if (!string.Equals(settings.SpeechLanguage, "auto", StringComparison.OrdinalIgnoreCase))
            {
                content.Add(new StringContent(settings.SpeechLanguage), "language");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, transcriptionUri);
            request.Content = content;
            if (!string.IsNullOrWhiteSpace(settings.SpeechApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SpeechApiKey);
            }

            using var response = await _client.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SpeechTranscriptionResult(false, string.Empty, $"Speech-to-text API failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("text", out var textElement))
            {
                return new SpeechTranscriptionResult(false, string.Empty, "Speech-to-text API response did not include text.");
            }

            string text = textElement.GetString() ?? string.Empty;
            return new SpeechTranscriptionResult(true, text.Trim(), "Transcription completed.");
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or IOException
                                   or JsonException
                                   or TaskCanceledException
                                   or InvalidOperationException
                                   or UnauthorizedAccessException)
        {
            return new SpeechTranscriptionResult(false, string.Empty, $"Speech-to-text API could not transcribe audio: {ex.Message}");
        }
    }

    private static bool TryCreateTranscriptionUri(string baseUrl, out Uri transcriptionUri)
    {
        string root = baseUrl.Trim().TrimEnd('/');
        string target = root.EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase)
            ? root
            : root + "/audio/transcriptions";
        return Uri.TryCreate(target, UriKind.Absolute, out transcriptionUri!)
            && (transcriptionUri.Scheme == Uri.UriSchemeHttp || transcriptionUri.Scheme == Uri.UriSchemeHttps);
    }
}
