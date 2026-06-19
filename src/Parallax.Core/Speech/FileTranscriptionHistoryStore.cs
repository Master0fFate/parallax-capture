using System.Text.Json;
using Parallax.Core.Platform;

namespace Parallax.Core.Speech;

public sealed class FileTranscriptionHistoryStore : ITranscriptionHistoryStore
{
    private readonly IPlatformLocations _locations;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public FileTranscriptionHistoryStore(IPlatformLocations locations)
    {
        _locations = locations;
    }

    private string HistoryPath => Path.Combine(_locations.TranscriptionHistoryDirectory, "history.jsonl");

    private string RecordingsDirectory => Path.Combine(_locations.TranscriptionHistoryDirectory, "recordings");

    public void Append(TranscriptionHistoryEntry entry)
    {
        Directory.CreateDirectory(_locations.TranscriptionHistoryDirectory);
        Directory.CreateDirectory(RecordingsDirectory);
        string audioPath = CopyRecording(entry.AudioPath);
        var stored = entry with { AudioPath = audioPath };
        File.AppendAllText(HistoryPath, JsonSerializer.Serialize(stored, _jsonOptions) + Environment.NewLine);
    }

    public void Cleanup(int maxEntries, int retentionDays, DateTimeOffset now)
    {
        var entries = ReadEntries();
        DateTimeOffset? cutoff = retentionDays <= 0 ? null : now.AddDays(-retentionDays);
        var kept = entries
            .Where(entry => cutoff == null || entry.CreatedAt >= cutoff.Value)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(Math.Max(0, maxEntries))
            .OrderBy(entry => entry.CreatedAt)
            .ToArray();
        var keptPaths = kept
            .Select(entry => CanonicalPathOrNull(entry.AudioPath))
            .Where(path => path != null)
            .Select(path => path!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            string? audioPath = CanonicalPathOrNull(entry.AudioPath);
            if (audioPath != null
                && !keptPaths.Contains(audioPath)
                && IsManagedRecordingPath(audioPath)
                && File.Exists(audioPath))
            {
                File.Delete(audioPath);
            }
        }

        Directory.CreateDirectory(_locations.TranscriptionHistoryDirectory);
        File.WriteAllLines(HistoryPath, kept.Select(entry => JsonSerializer.Serialize(entry, _jsonOptions)));
    }

    private IReadOnlyList<TranscriptionHistoryEntry> ReadEntries()
    {
        if (!File.Exists(HistoryPath))
        {
            return [];
        }

        var entries = new List<TranscriptionHistoryEntry>();
        foreach (string line in File.ReadLines(HistoryPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<TranscriptionHistoryEntry>(line, _jsonOptions);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return entries;
    }

    private bool IsManagedRecordingPath(string path)
    {
        string root = Path.GetFullPath(RecordingsDirectory);
        string separatorRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(separatorRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CanonicalPathOrNull(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private string CopyRecording(string sourcePath)
    {
        Directory.CreateDirectory(RecordingsDirectory);
        string fileName = Path.GetFileName(sourcePath);
        string targetPath = Path.Combine(RecordingsDirectory, fileName);
        if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        return targetPath;
    }
}
