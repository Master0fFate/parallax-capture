using Avalonia.Input.Platform;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

public sealed class AvaloniaTranscribedTextOutput : ITranscribedTextOutput
{
    private readonly ITextClipboard? _clipboard;
    private readonly IKeyChordSender _keyChordSender;

    public AvaloniaTranscribedTextOutput(ITextClipboard? clipboard, IKeyChordSender keyChordSender)
    {
        _clipboard = clipboard;
        _keyChordSender = keyChordSender;
    }

    public async Task<SpeechTextOutputResult> InsertAsync(
        string text,
        SpeechPasteMethod pasteMethod,
        bool copyToClipboard,
        bool autoSubmit,
        CancellationToken cancellationToken)
    {
        if (_clipboard == null)
        {
            return new SpeechTextOutputResult(false, "Clipboard is not available in this desktop session.");
        }

        await _clipboard.SetTextAsync(text);
        if (pasteMethod is SpeechPasteMethod.None or SpeechPasteMethod.DirectClipboard || copyToClipboard)
        {
            return autoSubmit
                ? _keyChordSender.SendAutoSubmit()
                : new SpeechTextOutputResult(true, "Copied transcription to clipboard.");
        }

        var paste = _keyChordSender.SendPaste(pasteMethod);
        return !paste.Success || !autoSubmit ? paste : _keyChordSender.SendAutoSubmit();
    }
}

public interface ITextClipboard
{
    Task SetTextAsync(string text);
}

public sealed class AvaloniaTextClipboard : ITextClipboard
{
    private readonly IClipboard _clipboard;

    public AvaloniaTextClipboard(IClipboard clipboard)
    {
        _clipboard = clipboard;
    }

    public Task SetTextAsync(string text)
    {
        return _clipboard.SetTextAsync(text);
    }
}
