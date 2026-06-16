namespace Parallax.Core.Hotkeys;

public static class HotkeyParser
{
    public const uint ModNone = 0x0000;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWindows = 0x0008;

    public static bool TryParse(string? gesture, out ParsedHotkey hotkey, out string message)
    {
        hotkey = new ParsedHotkey(ModNone, 0, "Disabled", Disabled: true);

        string input = gesture?.Trim() ?? string.Empty;
        if (input.Length == 0 || IsDisabledGesture(input))
        {
            message = "Shortcut is disabled.";
            return true;
        }

        uint modifiers = ModNone;
        uint virtualKey = 0;
        string? keyDisplay = null;
        string[] parts = input.Split('+');

        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();
            if (part.Length == 0)
            {
                message = "Use shortcut text like PrintScreen, Alt+PrintScreen, Ctrl+Shift+S, or None.";
                return false;
            }

            if (TryReadModifier(part, out uint modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    message = $"\"{part}\" is listed more than once.";
                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            if (keyDisplay != null)
            {
                message = "Use one key with optional Ctrl, Alt, Shift, or Win modifiers.";
                return false;
            }

            if (!TryReadKey(part, out virtualKey, out keyDisplay))
            {
                message = $"\"{part}\" is not a supported key. Use PrintScreen, a letter, a digit, or F1-F24.";
                return false;
            }
        }

        if (keyDisplay == null)
        {
            message = "Add a key after the modifiers, or use None to disable the shortcut.";
            return false;
        }

        hotkey = new ParsedHotkey(modifiers, virtualKey, BuildDisplayText(modifiers, keyDisplay), Disabled: false);
        message = "Shortcut is valid.";
        return true;
    }

    public static string FormatForDisplay(bool enabled, string? gesture)
    {
        if (!enabled)
        {
            return "disabled";
        }

        return TryParse(gesture, out var parsed, out _)
            ? parsed.Disabled ? "disabled" : parsed.DisplayText
            : "invalid";
    }

    private static bool IsDisabledGesture(string input)
    {
        return string.Equals(input, "None", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input, "Disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadModifier(string input, out uint modifier)
    {
        string normalized = NormalizeToken(input);
        modifier = normalized switch
        {
            "CTRL" or "CONTROL" => ModControl,
            "ALT" => ModAlt,
            "SHIFT" => ModShift,
            "WIN" or "WINDOWS" => ModWindows,
            _ => 0
        };

        return modifier != 0;
    }

    private static bool TryReadKey(string input, out uint virtualKey, out string display)
    {
        string normalized = NormalizeToken(input);

        if (normalized is "PRINTSCREEN" or "PRTSC" or "PRTSCN" or "SNAPSHOT")
        {
            virtualKey = 0x2C;
            display = "PrintScreen";
            return true;
        }

        if (normalized.Length == 1 && normalized[0] is >= 'A' and <= 'Z')
        {
            virtualKey = normalized[0];
            display = normalized;
            return true;
        }

        if (normalized.Length == 1 && normalized[0] is >= '0' and <= '9')
        {
            virtualKey = normalized[0];
            display = normalized;
            return true;
        }

        if (normalized.Length is >= 2 and <= 3
            && normalized[0] == 'F'
            && int.TryParse(normalized[1..], out int functionKey)
            && functionKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            display = $"F{functionKey}";
            return true;
        }

        virtualKey = 0;
        display = string.Empty;
        return false;
    }

    private static string BuildDisplayText(uint modifiers, string keyDisplay)
    {
        var parts = new List<string>();
        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModWindows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(keyDisplay);
        return string.Join("+", parts);
    }

    private static string NormalizeToken(string input)
    {
        return input.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
