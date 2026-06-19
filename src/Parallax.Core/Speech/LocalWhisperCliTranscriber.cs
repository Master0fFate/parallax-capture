using System.Text;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Speech;

public sealed class LocalWhisperCliTranscriber : ISpeechTranscriber
{
    private const int MaxLogChars = 4000;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly IWhisperCliExecutableLocator _executableLocator;
    private readonly IWhisperCliProcessExecutor _processExecutor;

    public LocalWhisperCliTranscriber(
        IWhisperCliExecutableLocator? executableLocator = null,
        IWhisperCliProcessExecutor? processExecutor = null)
    {
        _executableLocator = executableLocator ?? new WhisperCliExecutableLocator();
        _processExecutor = processExecutor ?? new ProcessWhisperCliExecutor();
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

            string? modelPath = ResolveModelPath(settings, locations);
            if (modelPath == null)
            {
                return new SpeechTranscriptionResult(
                    false,
                    string.Empty,
                    $"Local Whisper model was not found. Download or copy a ggml-*.bin model into {locations.SpeechModelsDirectory}.");
            }

            string? executablePath = _executableLocator.FindExecutable(locations);
            if (executablePath == null)
            {
                return new SpeechTranscriptionResult(
                    false,
                    string.Empty,
                    $"Whisper CLI was not found. Install whisper.cpp's whisper-cli/main executable in {locations.ToolsDirectory} or on PATH.");
            }

            Directory.CreateDirectory(locations.TempDirectory);
            string outputPrefix = Path.Combine(locations.TempDirectory, $"parallax-whisper-{Guid.NewGuid():N}");
            string outputTextPath = outputPrefix + ".txt";
            var arguments = BuildArguments(modelPath, audioPath, outputPrefix, settings.SpeechLanguage);
            var result = await _processExecutor.ExecuteAsync(
                new WhisperCliRunRequest(executablePath, arguments, outputTextPath, DefaultTimeout),
                cancellationToken);

            if (result.TimedOut)
            {
                DeleteOutput(outputTextPath);
                return new SpeechTranscriptionResult(false, string.Empty, "Local Whisper transcription timed out.");
            }

            if (result.ExitCode != 0)
            {
                DeleteOutput(outputTextPath);
                return new SpeechTranscriptionResult(
                    false,
                    string.Empty,
                    $"Local Whisper failed with exit code {result.ExitCode}. {FormatLog(result.StandardError)}");
            }

            string text = await ReadTranscriptionAsync(outputTextPath, result.StandardOutput, cancellationToken);
            DeleteOutput(outputTextPath);
            return string.IsNullOrWhiteSpace(text)
                ? new SpeechTranscriptionResult(false, string.Empty, "Local Whisper completed without transcribed text.")
                : new SpeechTranscriptionResult(true, text.Trim(), "Local Whisper transcription completed.");
        }
        catch (Exception ex) when (ex is IOException
                                   or InvalidOperationException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or System.ComponentModel.Win32Exception)
        {
            return new SpeechTranscriptionResult(false, string.Empty, $"Local Whisper could not transcribe audio: {ex.Message}");
        }
    }

    private static string? ResolveModelPath(ParallaxSettings settings, IPlatformLocations locations)
    {
        string requested = settings.SpeechModel.Trim();
        if (!string.IsNullOrWhiteSpace(requested) && File.Exists(requested))
        {
            return Path.GetFullPath(requested);
        }

        Directory.CreateDirectory(locations.SpeechModelsDirectory);
        foreach (string candidate in CandidateModelPaths(requested, locations))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory
            .EnumerateFiles(locations.SpeechModelsDirectory, "ggml-*.bin", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IEnumerable<string> CandidateModelPaths(string requested, IPlatformLocations locations)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            yield return Path.Combine(locations.SpeechModelsDirectory, requested);
            if (!Path.HasExtension(requested))
            {
                yield return Path.Combine(locations.SpeechModelsDirectory, requested + ".bin");
            }
        }

        foreach (var manifest in SpeechModelCatalog.BuiltInModels)
        {
            if (MatchesManifest(requested, manifest))
            {
                yield return Path.Combine(locations.SpeechModelsDirectory, manifest.FileName);
            }
        }
    }

    private static bool MatchesManifest(string requested, SpeechModelManifest manifest)
    {
        return string.IsNullOrWhiteSpace(requested)
            || string.Equals(requested, manifest.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(requested, manifest.DisplayName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(requested, manifest.FileName, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildArguments(
        string modelPath,
        string audioPath,
        string outputPrefix,
        string language)
    {
        var arguments = new List<string>
        {
            "-m",
            modelPath,
            "-f",
            audioPath,
            "-otxt",
            "-of",
            outputPrefix
        };

        if (!string.IsNullOrWhiteSpace(language)
            && !string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("-l");
            arguments.Add(language.Trim());
        }

        return arguments;
    }

    private static async Task<string> ReadTranscriptionAsync(
        string outputTextPath,
        string standardOutput,
        CancellationToken cancellationToken)
    {
        if (File.Exists(outputTextPath))
        {
            return await File.ReadAllTextAsync(outputTextPath, cancellationToken);
        }

        return standardOutput.Trim();
    }

    private static string FormatLog(string log)
    {
        var builder = new StringBuilder(log.Length);
        foreach (char c in log)
        {
            builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
        }

        string normalized = builder.ToString().Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Whisper CLI did not provide details.";
        }

        return normalized.Length > MaxLogChars
            ? $"Whisper CLI reported: {normalized[..MaxLogChars]}..."
            : $"Whisper CLI reported: {normalized}";
    }

    private static void DeleteOutput(string outputTextPath)
    {
        try
        {
            if (File.Exists(outputTextPath))
            {
                File.Delete(outputTextPath);
            }
        }
        catch
        {
        }
    }
}
