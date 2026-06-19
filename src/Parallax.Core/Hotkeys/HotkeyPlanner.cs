using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.Core.Hotkeys;

public static class HotkeyPlanner
{
    public const int RegionScreenshotId = 9001;
    public const int FullscreenScreenshotId = 9002;
    public const int RegionRecordingId = 9003;
    public const int SpeechToTextId = 9004;

    public static IReadOnlyList<HotkeyBinding> FromSettings(ParallaxSettings settings)
    {
        return
        [
            new HotkeyBinding(
                HotkeyAction.RegionScreenshot,
                "Capture region",
                settings.HotkeyScreenshotEnabled,
                settings.HotkeyScreenshot,
                RegionScreenshotId),
            new HotkeyBinding(
                HotkeyAction.FullscreenScreenshot,
                "Capture full screen",
                settings.HotkeyFullscreenEnabled,
                settings.HotkeyFullscreen,
                FullscreenScreenshotId),
            new HotkeyBinding(
                HotkeyAction.RegionRecording,
                "Start or stop recording",
                settings.HotkeyRegionVideoEnabled,
                settings.HotkeyRegionVideo,
                RegionRecordingId),
            new HotkeyBinding(
                HotkeyAction.SpeechToText,
                "Transcribe speech",
                settings.SpeechToTextEnabled,
                settings.SpeechShortcut,
                SpeechToTextId)
        ];
    }

    public static IReadOnlyList<PlannedHotkey> Plan(
        ParallaxSettings settings,
        CapabilityResult globalHotkeyCapability,
        IReadOnlySet<HotkeyAction>? conflictingActions = null)
    {
        return Plan(FromSettings(settings), globalHotkeyCapability, conflictingActions);
    }

    public static IReadOnlyList<PlannedHotkey> Plan(
        IReadOnlyList<HotkeyBinding> bindings,
        CapabilityResult globalHotkeyCapability,
        IReadOnlySet<HotkeyAction>? conflictingActions = null)
    {
        conflictingActions ??= new HashSet<HotkeyAction>();
        var planned = new List<PlannedHotkey>();
        var used = new Dictionary<(uint Modifiers, uint VirtualKey), string>();

        foreach (var binding in bindings)
        {
            string gesture = binding.Gesture?.Trim() ?? string.Empty;
            if (!binding.Enabled)
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    "disabled",
                    PlannedHotkeyState.Disabled,
                    "Shortcut is disabled. Tray and fallback menu actions remain available."));
                continue;
            }

            if (globalHotkeyCapability.State == CapabilityState.Unsupported)
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    "unsupported",
                    PlannedHotkeyState.Unsupported,
                    globalHotkeyCapability.Message));
                continue;
            }

            if (globalHotkeyCapability.State == CapabilityState.RequiresPermission)
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    "permission required",
                    PlannedHotkeyState.Unsupported,
                    globalHotkeyCapability.Message));
                continue;
            }

            if (!HotkeyParser.TryParse(gesture, out var parsed, out string parseMessage))
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    "invalid",
                    PlannedHotkeyState.Invalid,
                    parseMessage));
                continue;
            }

            if (parsed.Disabled)
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    "disabled",
                    PlannedHotkeyState.Disabled,
                    "Shortcut is disabled. Tray and fallback menu actions remain available."));
                continue;
            }

            if (conflictingActions.Contains(binding.Action))
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    parsed.DisplayText,
                    PlannedHotkeyState.Conflict,
                    $"\"{parsed.DisplayText}\" could not be registered. Choose another shortcut or turn this one off."));
                continue;
            }

            var key = (parsed.Modifiers, parsed.VirtualKey);
            if (used.TryGetValue(key, out string? existingName))
            {
                planned.Add(new PlannedHotkey(
                    binding.Action,
                    binding.Name,
                    binding.RegistrationId,
                    gesture,
                    parsed.DisplayText,
                    PlannedHotkeyState.Conflict,
                    $"{parsed.DisplayText} is already assigned to {existingName}."));
                continue;
            }

            used[key] = binding.Name;
            planned.Add(new PlannedHotkey(
                binding.Action,
                binding.Name,
                binding.RegistrationId,
                gesture,
                parsed.DisplayText,
                PlannedHotkeyState.Registered,
                $"Registered {parsed.DisplayText}."));
        }

        return planned;
    }
}
