using System.Runtime.InteropServices;
using NAudio.Wave;
using Parallax.Core.Settings;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

internal sealed class AvaloniaSpeechFeedbackService : ISpeechFeedbackService
{
    public void Started(ParallaxSettings settings)
    {
        PlayAsync(settings, 880, 90);
    }

    public void Stopped(ParallaxSettings settings)
    {
        PlayAsync(settings, 520, 110);
    }

    private static void PlayAsync(ParallaxSettings settings, double frequency, int durationMs)
    {
        if (!settings.SpeechFeedbackSoundsEnabled || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        _ = Task.Run(() => PlayTone(settings, frequency, durationMs));
    }

    private static void PlayTone(ParallaxSettings settings, double frequency, int durationMs)
    {
        try
        {
            byte[] audio = CreateTone(frequency, durationMs);
            using var stream = new MemoryStream(audio);
            using var provider = new RawSourceWaveStream(stream, new WaveFormat(44_100, 16, 1));
            using var output = new WaveOutEvent { DeviceNumber = ResolveOutputDeviceNumber(settings.SpeechFeedbackOutputDeviceId) };
            output.Init(provider);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(10);
            }
        }
        catch
        {
        }
    }

    private static byte[] CreateTone(double frequency, int durationMs)
    {
        const int sampleRate = 44_100;
        int sampleCount = sampleRate * durationMs / 1000;
        var bytes = new byte[sampleCount * 2];
        int fadeSamples = Math.Min(sampleCount / 2, sampleRate / 100);

        for (int i = 0; i < sampleCount; i++)
        {
            double envelope = Envelope(i, sampleCount, fadeSamples);
            short sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * short.MaxValue * 0.18);
            bytes[i * 2] = (byte)(sample & 0xff);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }

        return bytes;
    }

    private static double Envelope(int index, int sampleCount, int fadeSamples)
    {
        if (fadeSamples <= 0)
        {
            return 1;
        }

        if (index < fadeSamples)
        {
            return (double)index / fadeSamples;
        }

        int remaining = sampleCount - index - 1;
        return remaining < fadeSamples ? (double)Math.Max(0, remaining) / fadeSamples : 1;
    }

    private static int ResolveOutputDeviceNumber(string deviceId)
    {
        if (int.TryParse(deviceId, out int parsed) && parsed >= -1)
        {
            return parsed;
        }

        return -1;
    }
}
