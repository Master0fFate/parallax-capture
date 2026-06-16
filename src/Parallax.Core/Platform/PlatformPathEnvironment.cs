namespace Parallax.Core.Platform;

public sealed record PlatformPathEnvironment(
    PlatformKind Platform,
    string UserProfile,
    string? RoamingAppData = null,
    string? LocalAppData = null,
    string? TempDirectory = null,
    string? XdgConfigHome = null,
    string? XdgDataHome = null,
    string? XdgStateHome = null,
    string? PicturesDirectory = null,
    string? VideosDirectory = null);
