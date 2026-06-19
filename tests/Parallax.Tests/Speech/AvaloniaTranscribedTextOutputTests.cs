using Parallax.App.Avalonia.Speech;
using Parallax.Core.Speech;

namespace Parallax.Tests.Speech;

public sealed class AvaloniaTranscribedTextOutputTests
{
    [Fact]
    public async Task InsertAsync_copies_direct_clipboard_without_key_chord()
    {
        var clipboard = new FakeTextClipboard();
        var sender = new FakeKeyChordSender();
        var output = new AvaloniaTranscribedTextOutput(clipboard, sender);

        var result = await output.InsertAsync(
            "ready",
            SpeechPasteMethod.DirectClipboard,
            copyToClipboard: true,
            autoSubmit: false,
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("ready", clipboard.Text);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task InsertAsync_none_copies_without_paste_or_submit()
    {
        var clipboard = new FakeTextClipboard();
        var sender = new FakeKeyChordSender();
        var output = new AvaloniaTranscribedTextOutput(clipboard, sender);

        var result = await output.InsertAsync(
            "ready",
            SpeechPasteMethod.None,
            copyToClipboard: false,
            autoSubmit: false,
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("ready", clipboard.Text);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task InsertAsync_external_script_reports_not_configured()
    {
        var clipboard = new FakeTextClipboard();
        var sender = new FakeKeyChordSender();
        var output = new AvaloniaTranscribedTextOutput(clipboard, sender);

        var result = await output.InsertAsync(
            "ready",
            SpeechPasteMethod.ExternalScript,
            copyToClipboard: false,
            autoSubmit: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ready", clipboard.Text);
        Assert.Equal([SpeechPasteMethod.ExternalScript.ToString()], sender.Sent);
    }


    [Fact]
    public async Task InsertAsync_sends_selected_paste_chord_and_auto_submit()
    {
        var clipboard = new FakeTextClipboard();
        var sender = new FakeKeyChordSender();
        var output = new AvaloniaTranscribedTextOutput(clipboard, sender);

        var result = await output.InsertAsync(
            "ready",
            SpeechPasteMethod.CtrlShiftV,
            copyToClipboard: false,
            autoSubmit: true,
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("ready", clipboard.Text);
        Assert.Equal([SpeechPasteMethod.CtrlShiftV.ToString(), "AutoSubmit"], sender.Sent);
    }
}
