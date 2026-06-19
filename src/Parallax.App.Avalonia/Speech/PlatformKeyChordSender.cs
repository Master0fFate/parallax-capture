using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Parallax.Core.Platform;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Speech;

public interface IKeyChordSender
{
    SpeechTextOutputResult SendPaste(SpeechPasteMethod pasteMethod);

    SpeechTextOutputResult SendAutoSubmit();
}

public sealed class PlatformKeyChordSender : IKeyChordSender
{
    private readonly PlatformKind _platform;

    private PlatformKeyChordSender(PlatformKind platform)
    {
        _platform = platform;
    }

    public static IKeyChordSender Create(PlatformKind platform)
    {
        return new PlatformKeyChordSender(platform);
    }

    public SpeechTextOutputResult SendPaste(SpeechPasteMethod pasteMethod)
    {
        if (pasteMethod == SpeechPasteMethod.DirectClipboard)
        {
            return new SpeechTextOutputResult(true, "Copied transcription to clipboard.");
        }

        KeyChord? chord = pasteMethod switch
        {
            SpeechPasteMethod.None => null,
            SpeechPasteMethod.CtrlV => KeyChord.ControlV,
            SpeechPasteMethod.CtrlShiftV => KeyChord.ControlShiftV,
            SpeechPasteMethod.ShiftInsert => KeyChord.ShiftInsert,
            SpeechPasteMethod.ExternalScript => null,
            _ => throw new ArgumentOutOfRangeException(nameof(pasteMethod), pasteMethod, "Unsupported paste method.")
        };
        return chord == null
            ? new SpeechTextOutputResult(false, $"{pasteMethod} paste automation is not configured.")
            : Send(chord.Value, "Inserted transcription.");
    }

    public SpeechTextOutputResult SendAutoSubmit()
    {
        return Send(_platform == PlatformKind.MacOS ? KeyChord.CommandEnter : KeyChord.SuperEnter, "Submitted transcription.");
    }

    private SpeechTextOutputResult Send(KeyChord chord, string successMessage)
    {
        if (_platform == PlatformKind.Windows)
        {
            return WindowsKeyChordSender.Send(chord, successMessage);
        }

        if (_platform == PlatformKind.MacOS)
        {
            return RunProcess("osascript", MacScript(chord), successMessage);
        }

        return _platform == PlatformKind.Linux
            ? SendLinux(chord, successMessage)
            : new SpeechTextOutputResult(false, $"Paste automation is unavailable for {_platform}.");
    }

    private static SpeechTextOutputResult SendLinux(KeyChord chord, string successMessage)
    {
        string xdotoolChord = chord switch
        {
            KeyChord.ControlV => "ctrl+v",
            KeyChord.ControlShiftV => "ctrl+shift+v",
            KeyChord.ShiftInsert => "shift+Insert",
            KeyChord.SuperEnter => "Super+Return",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(xdotoolChord))
        {
            var result = RunProcess("xdotool", $"key {xdotoolChord}", successMessage);
            if (result.Success)
            {
                return result;
            }
        }

        string wtypeChord = chord switch
        {
            KeyChord.ControlV => "-M ctrl -k v -m ctrl",
            KeyChord.ControlShiftV => "-M ctrl -M shift -k v -m shift -m ctrl",
            KeyChord.ShiftInsert => "-M shift -k Insert -m shift",
            KeyChord.SuperEnter => "-M logo -k Return -m logo",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(wtypeChord)
            ? new SpeechTextOutputResult(false, "Paste automation is unavailable for this Linux desktop.")
            : RunProcess("wtype", wtypeChord, successMessage);
    }

    private static string MacScript(KeyChord chord)
    {
        return chord switch
        {
            KeyChord.ControlV => "-e 'tell application \"System Events\" to keystroke \"v\" using control down'",
            KeyChord.ControlShiftV => "-e 'tell application \"System Events\" to keystroke \"v\" using {control down, shift down}'",
            KeyChord.ShiftInsert => "-e 'tell application \"System Events\" to key code 114 using shift down'",
            KeyChord.CommandEnter => "-e 'tell application \"System Events\" to key code 36 using command down'",
            _ => string.Empty
        };
    }

    private static SpeechTextOutputResult RunProcess(string fileName, string arguments, string successMessage)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            });
            if (process == null)
            {
                return new SpeechTextOutputResult(false, $"{fileName} could not be started.");
            }

            process.WaitForExit(2000);
            if (process.ExitCode == 0)
            {
                return new SpeechTextOutputResult(true, successMessage);
            }

            string error = process.StandardError.ReadToEnd();
            return new SpeechTextOutputResult(false, string.IsNullOrWhiteSpace(error) ? $"{fileName} exited with code {process.ExitCode}." : error.Trim());
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new SpeechTextOutputResult(false, $"{fileName} paste automation is unavailable: {ex.Message}");
        }
    }
}

public enum KeyChord
{
    ControlV,
    ControlShiftV,
    ShiftInsert,
    SuperEnter,
    CommandEnter
}

internal static class WindowsKeyChordSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkLwin = 0x5B;
    private const ushort VkV = 0x56;
    private const ushort VkInsert = 0x2D;
    private const ushort VkReturn = 0x0D;

    public static SpeechTextOutputResult Send(KeyChord chord, string successMessage)
    {
        ushort[] keys = chord switch
        {
            KeyChord.ControlV => [VkControl, VkV],
            KeyChord.ControlShiftV => [VkControl, VkShift, VkV],
            KeyChord.ShiftInsert => [VkShift, VkInsert],
            KeyChord.SuperEnter => [VkLwin, VkReturn],
            _ => []
        };
        if (keys.Length == 0)
        {
            return new SpeechTextOutputResult(false, "Unsupported Windows key chord.");
        }

        var inputs = new List<Input>();
        foreach (ushort key in keys)
        {
            inputs.Add(KeyDown(key));
        }

        for (int index = keys.Length - 1; index >= 0; index--)
        {
            inputs.Add(KeyUp(keys[index]));
        }

        uint sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        return sent == inputs.Count
            ? new SpeechTextOutputResult(true, successMessage)
            : new SpeechTextOutputResult(false, $"Could not send paste key chord: {Marshal.GetLastWin32Error()}");
    }

    private static Input KeyDown(ushort key)
    {
        return new Input(InputKeyboard, new InputUnion(new KeyboardInput(key, 0, 0, UIntPtr.Zero)));
    }

    private static Input KeyUp(ushort key)
    {
        return new Input(InputKeyboard, new InputUnion(new KeyboardInput(key, 0, KeyEventKeyUp, UIntPtr.Zero)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Input(uint type, InputUnion union)
    {
        public uint Type { get; } = type;

        public InputUnion Union { get; } = union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct InputUnion
    {
        public InputUnion(KeyboardInput keyboard)
        {
            Keyboard = keyboard;
        }

        [FieldOffset(0)]
        public readonly KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardInput(ushort virtualKey, ushort scanCode, uint flags, UIntPtr extraInfo)
    {
        public ushort VirtualKey { get; } = virtualKey;

        public ushort ScanCode { get; } = scanCode;

        public uint Flags { get; } = flags;

        public uint Time { get; } = 0;

        public UIntPtr ExtraInfo { get; } = extraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int size);
}
