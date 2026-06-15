using parallax.UI.Windows;

namespace Parallax.Tests.UI;

public class VideoEditorTimelineTimeTests
{
    [Theory]
    [InlineData(0, "00:00.000")]
    [InlineData(1234, "00:01.234")]
    [InlineData(75456, "01:15.456")]
    [InlineData(3723456, "01:02:03.456")]
    public void FormatTimelineTime_PreservesMilliseconds(int milliseconds, string expected)
    {
        Assert.Equal(expected, VideoEditorWindow.FormatTimelineTime(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData("00:01.234", 1234)]
    [InlineData("01:15.456", 75456)]
    [InlineData("01:02:03.456", 3723456)]
    [InlineData("90:12.345", 5412345)]
    [InlineData("1.25", 1250)]
    public void ParseTimelineTime_AcceptsMillisecondsAndLegacySeconds(string text, int expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), VideoEditorWindow.ParseTimelineTime(text));
    }
}
