using System.Security.Cryptography;
using Parallax.Core.Platform;

namespace Parallax.Core.Speech;

public sealed class SpeechModelCatalog
{
    public static IReadOnlyList<SpeechModelManifest> BuiltInModels { get; } =
    [
        new(
            "whisper-tiny",
            "Whisper tiny",
            "ggml-tiny.bin",
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"),
            "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21",
            77_691_713,
            ["auto"]),
        new(
            "whisper-base",
            "Whisper base",
            "ggml-base.bin",
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin"),
            "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe",
            147_951_465,
            ["auto"])
    ];

    private readonly HttpClient _client;

    public SpeechModelCatalog(HttpClient client)
    {
        _client = client;
    }

    public IReadOnlyList<string> DiscoverLocalModels(IPlatformLocations locations)
    {
        if (!Directory.Exists(locations.SpeechModelsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(locations.SpeechModelsDirectory, "ggml-*.bin", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string> DownloadAsync(
        SpeechModelManifest manifest,
        IPlatformLocations locations,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(locations.SpeechModelsDirectory);
        if (string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            throw new InvalidOperationException($"Downloaded model {manifest.DisplayName} does not have a pinned checksum.");
        }

        string targetPath = Path.Combine(locations.SpeechModelsDirectory, manifest.FileName);
        string partialPath = targetPath + ".partial";

        using var response = await _client.GetAsync(manifest.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = File.Create(partialPath))
        {
            var buffer = new byte[128 * 1024];
            long written = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;
                if (manifest.SizeBytes > 0)
                {
                    progress?.Report((double)written / manifest.SizeBytes);
                }
            }
        }

        if (!await Sha256MatchesAsync(partialPath, manifest.Sha256, cancellationToken))
        {
            File.Delete(partialPath);
            throw new InvalidOperationException($"Downloaded model {manifest.DisplayName} failed checksum verification.");
        }

        File.Move(partialPath, targetPath, overwrite: true);
        return targetPath;
    }

    private static async Task<bool> Sha256MatchesAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        string actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
