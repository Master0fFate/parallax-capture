namespace Parallax.Core.Media;

public static class VideoTrimPolicy
{
    private static readonly TimeSpan MinimumTrimDuration = TimeSpan.FromMilliseconds(1);

    public static TrimValidationResult Validate(TimeSpan start, TimeSpan end, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return TrimValidationResult.Invalid("Video duration must be greater than zero before trimming.");
        }

        TimeSpan clampedStart = Clamp(start, TimeSpan.Zero, duration);
        TimeSpan clampedEnd = Clamp(end, TimeSpan.Zero, duration);

        if (clampedEnd <= clampedStart)
        {
            return TrimValidationResult.Invalid("Trim end must be after trim start.");
        }

        if (clampedEnd - clampedStart < MinimumTrimDuration)
        {
            return TrimValidationResult.Invalid("Trim range must be greater than zero.");
        }

        return TrimValidationResult.Valid(new VideoTrimRange(clampedStart, clampedEnd));
    }

    public static TimeSpan ClampFramePosition(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero || duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (position >= duration)
        {
            return duration <= TimeSpan.FromMilliseconds(10)
                ? TimeSpan.Zero
                : duration - TimeSpan.FromMilliseconds(10);
        }

        return position;
    }

    public static string FormatFFmpegTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}.{value.Milliseconds:D3}";
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (max < min)
        {
            max = min;
        }

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
