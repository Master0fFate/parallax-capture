using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Speech;

namespace Parallax.Core.Settings;

public sealed record RuntimeSettingsApplyResult(
    bool Saved,
    SaveFolderValidationResult SaveFolder,
    IReadOnlyList<PlannedHotkey> Hotkeys,
    StartupRegistrationResult Startup);

public sealed class RuntimeSettingsApplier
{
    private readonly IPlatformBackend _platform;
    private readonly JsonSettingsStore _settingsStore;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IStartupService _startupService;
    private readonly Func<HotkeyAction, Action> _hotkeyCallbackFactory;
    private readonly Func<HotkeyAction, Action> _hotkeyReleaseCallbackFactory;
    private readonly Func<HotkeyAction, bool> _supportsHotkey;

    public RuntimeSettingsApplier(
        IPlatformBackend platform,
        JsonSettingsStore settingsStore,
        IGlobalHotkeyService hotkeyService,
        IStartupService startupService,
        Func<HotkeyAction, Action> hotkeyCallbackFactory,
        Func<HotkeyAction, bool>? supportsHotkey = null,
        Func<HotkeyAction, Action>? hotkeyReleaseCallbackFactory = null)
    {
        _platform = platform;
        _settingsStore = settingsStore;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _hotkeyCallbackFactory = hotkeyCallbackFactory;
        _hotkeyReleaseCallbackFactory = hotkeyReleaseCallbackFactory ?? (_ => () => { });
        _supportsHotkey = supportsHotkey ?? (_ => true);
    }

    public RuntimeSettingsApplyResult Apply(ParallaxSettings settings, string executablePath)
    {
        var saveFolder = SaveFolderPolicy.ValidateAndCreate(settings, _platform.Locations);

        _hotkeyService.UnregisterAll();
        var plannedHotkeys = HotkeyPlanner.Plan(settings, _hotkeyService.Capability);
        foreach (var hotkey in plannedHotkeys.Where(item => item.ShouldRegister && _supportsHotkey(item.Action)))
        {
            if (!HotkeyParser.TryParse(hotkey.Gesture, out var parsed, out _))
            {
                continue;
            }

            var registration = Register(settings, hotkey, parsed);

            if (!registration.IsRegistered)
            {
                plannedHotkeys = plannedHotkeys.Select(item => item.Action == hotkey.Action
                    ? item with
                    {
                        State = PlannedHotkeyState.Conflict,
                        Message = registration.Message
                    }
                    : item).ToArray();
            }
        }

        var startup = _startupService.SetEnabled(settings.StartWithSystem, executablePath);
        if (saveFolder.Success)
        {
            _settingsStore.Save(settings);
        }

        return new RuntimeSettingsApplyResult(saveFolder.Success, saveFolder, plannedHotkeys, startup);
    }

    private HotkeyRegistrationResult Register(ParallaxSettings settings, PlannedHotkey hotkey, ParsedHotkey parsed)
    {
        return hotkey.Action == HotkeyAction.SpeechToText && settings.SpeechShortcutMode == SpeechShortcutMode.PushToTalk
            ? _hotkeyService.RegisterHold(
                hotkey.RegistrationId,
                parsed.Modifiers,
                parsed.VirtualKey,
                parsed.DisplayText,
                _hotkeyCallbackFactory(hotkey.Action),
                _hotkeyReleaseCallbackFactory(hotkey.Action))
            : _hotkeyService.Register(
                hotkey.RegistrationId,
                parsed.Modifiers,
                parsed.VirtualKey,
                parsed.DisplayText,
                _hotkeyCallbackFactory(hotkey.Action));
    }
}
