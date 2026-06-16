using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;

namespace Parallax.Core.Settings;

public sealed record RuntimeSettingsApplyResult(
    bool Saved,
    SaveFolderValidationResult SaveFolder,
    IReadOnlyList<PlannedHotkey> Hotkeys,
    StartupRegistrationResult Startup,
    ThemePreset Theme);

public sealed class RuntimeSettingsApplier
{
    private readonly IPlatformBackend _platform;
    private readonly JsonSettingsStore _settingsStore;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IStartupService _startupService;
    private readonly ThemeSettingsService _themeService;

    public RuntimeSettingsApplier(
        IPlatformBackend platform,
        JsonSettingsStore settingsStore,
        IGlobalHotkeyService hotkeyService,
        IStartupService startupService,
        ThemeSettingsService themeService)
    {
        _platform = platform;
        _settingsStore = settingsStore;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _themeService = themeService;
    }

    public RuntimeSettingsApplyResult Apply(ParallaxSettings settings, string executablePath)
    {
        var saveFolder = SaveFolderPolicy.ValidateAndCreate(settings, _platform.Locations);
        var theme = _themeService.Preview(settings.ThemeFamily, settings.ThemeMode);
        settings.ThemeFamily = theme.Family;
        settings.ThemeMode = theme.Mode;

        _hotkeyService.UnregisterAll();
        var plannedHotkeys = HotkeyPlanner.Plan(settings, _hotkeyService.Capability);
        foreach (var hotkey in plannedHotkeys.Where(item => item.ShouldRegister))
        {
            if (!HotkeyParser.TryParse(hotkey.Gesture, out var parsed, out _))
            {
                continue;
            }

            var registration = _hotkeyService.Register(
                hotkey.RegistrationId,
                parsed.Modifiers,
                parsed.VirtualKey,
                parsed.DisplayText,
                () => { });

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

        return new RuntimeSettingsApplyResult(saveFolder.Success, saveFolder, plannedHotkeys, startup, theme);
    }
}
