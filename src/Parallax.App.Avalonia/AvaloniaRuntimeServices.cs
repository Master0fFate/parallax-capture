using Avalonia;
using Parallax.App.Avalonia.Settings;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Platform.Windows;

namespace Parallax.App.Avalonia;

public static class AvaloniaRuntimeServices
{
    public static IGlobalHotkeyService CreateHotkeyService(IPlatformBackend platform)
    {
        return platform.Info.Kind == PlatformKind.Windows
            ? WindowsGlobalHotkeyService.CreateForCurrentThread()
            : new UnsupportedHotkeyService(platform.Capabilities.GlobalHotkeys);
    }

    public static IStartupService CreateStartupService(IPlatformBackend platform)
    {
        return platform.Info.Kind == PlatformKind.Windows
            ? new WindowsStartupService(platform.Locations)
            : new PlannedStartupService(platform.Locations);
    }

    public static ThemeSettingsService CreateThemeSettingsService(Application application)
    {
        return new ThemeSettingsService(new AvaloniaThemePreviewApplier(application));
    }

    private sealed class UnsupportedHotkeyService : IGlobalHotkeyService
    {
        public UnsupportedHotkeyService(CapabilityResult capability)
        {
            Capability = capability.State == CapabilityState.Supported
                ? CapabilityResult.Unsupported("Global shortcuts are not implemented for this platform session.")
                : capability;
        }

        public CapabilityResult Capability { get; }

        public HotkeyRegistrationResult Register(int id, uint modifiers, uint virtualKey, string displayText, Action callback)
        {
            return new HotkeyRegistrationResult(HotkeyRegistrationResultState.Unsupported, displayText, Capability.Message);
        }

        public void UnregisterAll()
        {
        }

        public void Dispose()
        {
        }
    }
}
