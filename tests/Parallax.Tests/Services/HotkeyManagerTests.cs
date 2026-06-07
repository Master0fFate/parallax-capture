using parallax.Core.Services;

namespace Parallax.Tests.Services;

public class HotkeyManagerTests
{
    [Theory]
    [InlineData("PrintScreen", "PrintScreen")]
    [InlineData("PrtSc", "PrintScreen")]
    [InlineData("PrtScn", "PrintScreen")]
    [InlineData("Snapshot", "PrintScreen")]
    [InlineData("ctrl + shift + s", "Ctrl+Shift+S")]
    [InlineData("Windows+F9", "Win+F9")]
    [InlineData("Alt+1", "Alt+1")]
    public void TryParse_AcceptsSupportedGesturesAndNormalizesDisplay(string input, string expectedDisplay)
    {
        bool parsed = HotkeyManager.TryParse(input, out var hotkey, out string message);

        Assert.True(parsed, message);
        Assert.False(hotkey.Disabled);
        Assert.Equal(expectedDisplay, hotkey.DisplayText);
        Assert.NotEqual(0u, hotkey.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("None")]
    [InlineData("disabled")]
    public void TryParse_TreatsBlankNoneAndDisabledAsDisabled(string? input)
    {
        bool parsed = HotkeyManager.TryParse(input, out var hotkey, out string message);

        Assert.True(parsed, message);
        Assert.True(hotkey.Disabled);
        Assert.Equal("Disabled", hotkey.DisplayText);
        Assert.Equal("Shortcut is disabled.", message);
    }

    [Theory]
    [InlineData("Ctrl+Ctrl+S")]
    [InlineData("Alt+S+T")]
    [InlineData("Alt+")]
    [InlineData("Ctrl+Mouse1")]
    [InlineData("F25")]
    public void TryParse_RejectsAmbiguousOrUnsupportedGestures(string input)
    {
        bool parsed = HotkeyManager.TryParse(input, out _, out string message);

        Assert.False(parsed);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void FormatForDisplay_ReportsDisabledInvalidAndNormalizedStates()
    {
        Assert.Equal("disabled", HotkeyManager.FormatForDisplay(enabled: false, "Alt+R"));
        Assert.Equal("disabled", HotkeyManager.FormatForDisplay(enabled: true, "None"));
        Assert.Equal("invalid", HotkeyManager.FormatForDisplay(enabled: true, "Alt+Mouse1"));
        Assert.Equal("Ctrl+Alt+F12", HotkeyManager.FormatForDisplay(enabled: true, "Control+Alt+F12"));
    }

    [Fact]
    public void RegisterConfigured_DisabledShortcutDoesNotRequireWindowHandle()
    {
        using var manager = new HotkeyManager();

        bool result = manager.RegisterConfigured(
            HotkeyManager.ID_REGION_SCREENSHOT,
            enabled: false,
            gesture: "PrintScreen",
            callback: () => { },
            out string message);

        Assert.True(result);
        Assert.Equal("Shortcut is disabled.", message);
    }

    [Fact]
    public void RegisterConfigured_EnabledShortcutWithoutWindowHandleFailsClearly()
    {
        using var manager = new HotkeyManager();

        bool result = manager.RegisterConfigured(
            HotkeyManager.ID_REGION_SCREENSHOT,
            enabled: true,
            gesture: "PrintScreen",
            callback: () => { },
            out string message);

        Assert.False(result);
        Assert.Contains("could not be registered", message);
    }
}
