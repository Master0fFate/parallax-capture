using Avalonia;
using Parallax.App.Avalonia.Capture;
using Parallax.Core.Capture;
using Parallax.Core.Platform;
using Parallax.Core.Settings;
using Parallax.Core.Shell;
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
using Parallax.Platform.Windows;
#endif

namespace Parallax.App.Avalonia;

public static class AvaloniaRuntimeServices
{
    public static ShellFeatureSet CreateShellFeatureSet(IPlatformBackend platform)
    {
        return platform.Info.Kind == PlatformKind.Windows
            ? new ShellFeatureSet(
                RegionScreenshot: true,
                FullScreenshot: true,
                RegionRecording: false,
                SpeechToText: true,
                VideoEditor: false,
                ImageEditor: false)
            : new ShellFeatureSet(
                RegionScreenshot: false,
                FullScreenshot: false,
                RegionRecording: false,
                SpeechToText: true,
                VideoEditor: false,
                ImageEditor: false);
    }

    public static IGlobalHotkeyService CreateHotkeyService(IPlatformBackend platform)
    {
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
        if (platform.Info.Kind == PlatformKind.Windows)
        {
            return WindowsGlobalHotkeyService.CreateForCurrentThread();
        }
#endif

        return new UnsupportedHotkeyService(platform.Capabilities.GlobalHotkeys);
    }

    public static IStartupService CreateStartupService(IPlatformBackend platform)
    {
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
        if (platform.Info.Kind == PlatformKind.Windows)
        {
            return new WindowsStartupService(platform.Locations);
        }
#endif

        return new PlannedStartupService(platform.Locations);
    }

    public static IScreenshotWorkflowRunner CreateScreenshotWorkflowRunner(IPlatformBackend platform, ParallaxSettings settings)
    {
#if PARALLAX_MULTI_TARGET || PARALLAX_TARGET_WINDOWS
        if (platform.Info.Kind == PlatformKind.Windows)
        {
            var clipboard = new WindowsClipboardService();
            var saver = new CollisionSafeImageSaver();
            return new AvaloniaScreenshotWorkflowRunner(new ScreenshotWorkflow(
                new WindowsScreenshotService(),
                new AvaloniaRegionSelectionService(),
                clipboard,
                saver,
                new AvaloniaAnnotationEditorLauncher(clipboard, saver, platform.Locations, settings)));
        }
#endif

        return new UnsupportedScreenshotWorkflowRunner(platform.Capabilities.ScreenCapture);
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

        public HotkeyRegistrationResult RegisterHold(int id, uint modifiers, uint virtualKey, string displayText, Action started, Action stopped)
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

    private sealed class UnsupportedScreenshotWorkflowRunner : IScreenshotWorkflowRunner
    {
        private readonly CapabilityResult _capability;

        public UnsupportedScreenshotWorkflowRunner(CapabilityResult capability)
        {
            _capability = capability;
        }

        public ScreenshotWorkflowResult CaptureRegion(ParallaxSettings settings, IPlatformLocations locations)
        {
            return Unsupported("Region screenshot");
        }

        public ScreenshotWorkflowResult CaptureFullScreen(ParallaxSettings settings, IPlatformLocations locations)
        {
            return Unsupported("Full-screen screenshot");
        }

        private ScreenshotWorkflowResult Unsupported(string action)
        {
            var failure = _capability.State == CapabilityState.RequiresPermission
                ? CaptureFailureKind.PermissionDenied
                : _capability.State == CapabilityState.RequiresUserMediation
                    ? CaptureFailureKind.RequiresUserMediation
                    : CaptureFailureKind.Unsupported;
            return new ScreenshotWorkflowResult(false, false, failure, null, null, false, false, $"{action} is unavailable. {_capability.Message}");
        }
    }
}
