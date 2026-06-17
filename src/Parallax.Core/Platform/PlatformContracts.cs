using Parallax.Core.Capture;

namespace Parallax.Core.Platform;

public interface IPlatformInfo
{
    PlatformKind Kind { get; }

    string DisplayName { get; }
}

public sealed record PlatformInfo(PlatformKind Kind, string DisplayName) : IPlatformInfo;

public interface IPlatformBackend
{
    IPlatformInfo Info { get; }

    IPlatformLocations Locations { get; }

    PlatformCapabilitySet Capabilities { get; }
}

public enum CapabilityState
{
    Supported,
    RequiresPermission,
    RequiresUserMediation,
    Unsupported
}

public sealed record CapabilityResult(CapabilityState State, string Message)
{
    public static CapabilityResult Supported(string message) => new(CapabilityState.Supported, message);

    public static CapabilityResult RequiresPermission(string message) => new(CapabilityState.RequiresPermission, message);

    public static CapabilityResult RequiresUserMediation(string message) => new(CapabilityState.RequiresUserMediation, message);

    public static CapabilityResult Unsupported(string message) => new(CapabilityState.Unsupported, message);
}

public sealed record PlatformCapabilitySet(
    CapabilityResult ScreenCapture,
    CapabilityResult ScreenRecording,
    CapabilityResult GlobalHotkeys,
    CapabilityResult Clipboard,
    CapabilityResult StartupRegistration,
    CapabilityResult CaptureExclusion);

public interface ITrayService
{
    bool IsAvailable { get; }

    void SetMenu(IReadOnlyList<TrayMenuItem> items);

    void SetToolTip(string text);

    void Dispose();
}

public interface IGlobalHotkeyService : IDisposable
{
    CapabilityResult Capability { get; }

    HotkeyRegistrationResult Register(int id, uint modifiers, uint virtualKey, string displayText, Action callback);

    void UnregisterAll();
}

public interface IScreenshotService
{
    CaptureResult CaptureRegion(CaptureRectangle region);

    CaptureResult CaptureFullScreen();
}

public interface IRegionSelectionService
{
    RegionSelectionResult SelectRegion();
}

public interface IScreenRecordingService
{
}

public interface IClipboardService
{
    ClipboardImageResult CopyImage(CaptureImage image);
}

public interface IStartupService
{
    StartupRegistrationPlan CreatePlan(bool enable, string executablePath);

    StartupRegistrationResult SetEnabled(bool enable, string executablePath);
}

public interface ICaptureExclusionService
{
}

public interface IPlatformPermissionService
{
}

public interface IFFmpegLocator
{
}

public interface IFFmpegRunner
{
}

public interface IVideoPreviewService
{
}

public interface IPackagingEnvironment
{
}

public sealed record TrayMenuItem(
    string Id,
    string Label,
    bool IsEnabled,
    bool IsVisible,
    string? Status = null,
    Action? Activate = null)
{
    public void Invoke()
    {
        if (IsEnabled && IsVisible)
        {
            Activate?.Invoke();
        }
    }
}

public enum HotkeyRegistrationResultState
{
    Registered,
    Disabled,
    Invalid,
    Conflict,
    Unsupported
}

public sealed record HotkeyRegistrationResult(
    HotkeyRegistrationResultState State,
    string DisplayText,
    string Message)
{
    public bool IsRegistered => State == HotkeyRegistrationResultState.Registered;
}

public sealed record StartupRegistrationPlan(
    PlatformKind Platform,
    bool Enable,
    string Mechanism,
    string TargetPath,
    bool RequiresAdmin,
    string Description);

public sealed record StartupRegistrationResult(
    bool Success,
    StartupRegistrationPlan Plan,
    string Message);

public interface IFolderLauncher
{
    FolderLaunchResult OpenFolder(string folderPath);
}

public sealed record FolderLaunchResult(bool Success, string Message);
