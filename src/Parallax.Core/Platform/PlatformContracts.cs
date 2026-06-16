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
}

public interface IGlobalHotkeyService
{
}

public interface IScreenshotService
{
}

public interface IRegionSelectionService
{
}

public interface IScreenRecordingService
{
}

public interface IClipboardService
{
}

public interface IStartupService
{
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
