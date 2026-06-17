using Parallax.Core.Recording;

namespace Parallax.Core.Media;

public static class FFmpegCommandBuilder
{
    public static FFmpegRunRequest BuildTrim(
        string ffmpegPath,
        string sourcePath,
        string outputPath,
        VideoTrimRange range,
        TimeSpan timeout)
    {
        EnsureSafeSourceAndOutput(sourcePath, outputPath);
        return new FFmpegRunRequest(
            ffmpegPath,
            [
                "-n",
                "-ss", VideoTrimPolicy.FormatFFmpegTime(range.Start),
                "-i", sourcePath,
                "-t", VideoTrimPolicy.FormatFFmpegTime(range.Duration),
                "-c:v", "libx264",
                "-preset", "fast",
                "-crf", "23",
                "-c:a", "aac",
                "-b:a", "128k",
                "-movflags", "+faststart",
                outputPath
            ],
            outputPath,
            timeout);
    }

    public static FFmpegRunRequest BuildCurrentFrame(
        string ffmpegPath,
        string sourcePath,
        string outputPath,
        TimeSpan position,
        TimeSpan duration,
        TimeSpan timeout)
    {
        EnsureSafeSourceAndOutput(sourcePath, outputPath);
        TimeSpan clamped = VideoTrimPolicy.ClampFramePosition(position, duration);
        return new FFmpegRunRequest(
            ffmpegPath,
            [
                "-n",
                "-ss", VideoTrimPolicy.FormatFFmpegTime(clamped),
                "-i", sourcePath,
                "-frames:v", "1",
                outputPath
            ],
            outputPath,
            timeout);
    }

    public static FFmpegRunRequest BuildGif(
        string ffmpegPath,
        string sourcePath,
        string outputPath,
        VideoTrimRange range,
        TimeSpan timeout)
    {
        EnsureSafeSourceAndOutput(sourcePath, outputPath);
        return new FFmpegRunRequest(
            ffmpegPath,
            [
                "-n",
                "-i", sourcePath,
                "-ss", VideoTrimPolicy.FormatFFmpegTime(range.Start),
                "-t", VideoTrimPolicy.FormatFFmpegTime(range.Duration),
                "-filter_complex", "[0:v]fps=12,scale=720:-2:flags=lanczos,split[v1][v2];[v1]palettegen=max_colors=128[p];[v2][p]paletteuse=dither=bayer:bayer_scale=5",
                "-loop", "0",
                outputPath
            ],
            outputPath,
            timeout);
    }

    public static void EnsureSafeSourceAndOutput(string sourcePath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        if (string.Equals(
            Path.GetFullPath(sourcePath),
            Path.GetFullPath(outputPath),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("FFmpeg output path must not overwrite the source media.");
        }
    }
}
