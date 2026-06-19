#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
using System.Diagnostics;
using NAudio.Wave;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

internal sealed class WindowsMicrophoneSpeechRecorder : ISpeechRecorder
{
    private static readonly WaveFormat RecordingFormat = new(16_000, 16, 1);

    public async Task<SpeechRecordingResult> RecordOnceAsync(
        ParallaxSettings settings,
        IPlatformLocations locations,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SpeechRecordingResult(false, null, TimeSpan.Zero, "Microphone recording is only wired on Windows.");
        }

        if (WaveInEvent.DeviceCount <= 0)
        {
            return new SpeechRecordingResult(false, null, TimeSpan.Zero, "No microphone input devices were found.");
        }

        Directory.CreateDirectory(locations.TempDirectory);
        string audioPath = Path.Combine(locations.TempDirectory, $"speech-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.wav");
        int deviceNumber = ResolveDeviceNumber(settings.SpeechMicrophoneDeviceId);
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = RecordingFormat,
                BufferMilliseconds = 50
            };
            await using var writer = new WaveFileWriter(audioPath, RecordingFormat);

            waveIn.DataAvailable += (_, args) =>
            {
                if (args.BytesRecorded > 0)
                {
                    writer.Write(args.Buffer, 0, args.BytesRecorded);
                    writer.Flush();
                }
            };
            waveIn.RecordingStopped += (_, args) => stopped.TrySetResult(args.Exception);
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    waveIn.StopRecording();
                }
                catch (InvalidOperationException)
                {
                    stopped.TrySetResult(null);
                }
            });

            waveIn.StartRecording();
            await stopped.Task.WaitAsync(TimeSpan.FromMinutes(10));
            stopwatch.Stop();
            writer.Flush();
            if (stopped.Task.Result != null)
            {
                return Failure(audioPath, stopwatch.Elapsed, $"Microphone recording failed: {stopped.Task.Result.Message}");
            }

            if (new FileInfo(audioPath).Length <= 44)
            {
                return Failure(audioPath, stopwatch.Elapsed, "Microphone recording did not capture any audio.");
            }

            return new SpeechRecordingResult(true, audioPath, stopwatch.Elapsed, "Microphone recording captured.");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return File.Exists(audioPath) && new FileInfo(audioPath).Length > 44
                ? new SpeechRecordingResult(true, audioPath, stopwatch.Elapsed, "Microphone recording stopped.")
                : Failure(audioPath, stopwatch.Elapsed, "Speech-to-text was stopped before audio was captured.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(audioPath, stopwatch.Elapsed, $"Microphone recording failed: {ex.Message}");
        }
    }

    private static int ResolveDeviceNumber(string configuredDevice)
    {
        if (int.TryParse(configuredDevice, out int parsed) && parsed >= 0 && parsed < WaveInEvent.DeviceCount)
        {
            return parsed;
        }

        if (!string.IsNullOrWhiteSpace(configuredDevice))
        {
            for (int index = 0; index < WaveInEvent.DeviceCount; index++)
            {
                var capabilities = WaveInEvent.GetCapabilities(index);
                if (capabilities.ProductName.Contains(configuredDevice, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        return 0;
    }

    private static SpeechRecordingResult Failure(string audioPath, TimeSpan duration, string message)
    {
        if (File.Exists(audioPath))
        {
            File.Delete(audioPath);
        }

        return new SpeechRecordingResult(false, null, duration, message);
    }
}
#endif
