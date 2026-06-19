using System.Text.Json;
using Parallax.Core.Speech;
using static Parallax.Tests.Speech.SpeechTestSupport;

namespace Parallax.Tests.Speech;

public sealed class FileTranscriptionHistoryStoreTests
{
    [Fact]
    public void Cleanup_applies_max_entries_and_retention_without_deleting_when_retention_is_never()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-history", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            Directory.CreateDirectory(root);
            var store = new FileTranscriptionHistoryStore(locations);
            var oldPath = Path.Combine(root, "old.wav");
            var recentPath = Path.Combine(root, "recent.wav");
            File.WriteAllBytes(oldPath, [1]);
            File.WriteAllBytes(recentPath, [2]);
            store.Append(new TranscriptionHistoryEntry(DateTimeOffset.UtcNow.AddDays(-9), "old", oldPath, TimeSpan.FromSeconds(1)));
            store.Append(new TranscriptionHistoryEntry(DateTimeOffset.UtcNow, "recent", recentPath, TimeSpan.FromSeconds(1)));

            store.Cleanup(maxEntries: 1, retentionDays: 7, DateTimeOffset.UtcNow);

            Assert.False(File.Exists(Path.Combine(locations.TranscriptionHistoryDirectory, "recordings", "old.wav")));
            Assert.True(File.Exists(Path.Combine(locations.TranscriptionHistoryDirectory, "recordings", "recent.wav")));

            store.Cleanup(maxEntries: 10, retentionDays: 0, DateTimeOffset.UtcNow.AddDays(365));

            Assert.True(File.Exists(Path.Combine(locations.TranscriptionHistoryDirectory, "recordings", "recent.wav")));
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
    public void Cleanup_does_not_delete_audio_paths_outside_managed_recordings_directory()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-speech-history-safe", Guid.NewGuid().ToString("N"));
        try
        {
            var locations = TestLocations(root);
            Directory.CreateDirectory(locations.TranscriptionHistoryDirectory);
            string outsidePath = Path.Combine(root, "outside.wav");
            File.WriteAllBytes(outsidePath, [7, 7, 7]);
            var hostileEntry = new TranscriptionHistoryEntry(
                DateTimeOffset.UtcNow.AddDays(-30),
                "outside",
                outsidePath,
                TimeSpan.FromSeconds(1));
            File.WriteAllText(
                Path.Combine(locations.TranscriptionHistoryDirectory, "history.jsonl"),
                JsonSerializer.Serialize(hostileEntry, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + Environment.NewLine);
            var store = new FileTranscriptionHistoryStore(locations);

            store.Cleanup(maxEntries: 0, retentionDays: 1, DateTimeOffset.UtcNow);

            Assert.True(File.Exists(outsidePath));
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
