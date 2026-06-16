using Parallax.Core.Platform;

namespace Parallax.Tests.Platform;

public class PlatformPathPolicyTests
{
    [Fact]
    public void WindowsPathsResolveToSafePerUserLocations()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Windows,
            UserProfile: @"C:\Users\Ada",
            RoamingAppData: @"C:\Users\Ada\AppData\Roaming",
            LocalAppData: @"C:\Users\Ada\AppData\Local",
            TempDirectory: @"C:\Users\Ada\AppData\Local\Temp",
            PicturesDirectory: @"C:\Users\Ada\Pictures",
            VideosDirectory: @"C:\Users\Ada\Videos"));

        Assert.Equal(PlatformKind.Windows, locations.Platform);
        Assert.Equal(@"C:\Users\Ada\AppData\Roaming\parallax", locations.ConfigDirectory);
        Assert.Equal(@"C:\Users\Ada\AppData\Roaming\parallax\settings.json", locations.SettingsFilePath);
        Assert.Equal(@"C:\Users\Ada\AppData\Local\parallax\logs", locations.LogsDirectory);
        Assert.Equal(@"C:\Users\Ada\AppData\Local\parallax\tools", locations.ToolsDirectory);
        Assert.Equal(@"C:\Users\Ada\AppData\Local\Temp\parallax", locations.TempDirectory);
        Assert.Equal(@"C:\Users\Ada\Pictures\parallax_captures", locations.ScreenshotsDirectory);
        Assert.Equal(@"C:\Users\Ada\Videos\parallax_recordings", locations.RecordingsDirectory);
    }

    [Fact]
    public void MacPathsResolveToUserLibraryAndMediaFolders()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.MacOS,
            UserProfile: "/Users/ada",
            TempDirectory: "/var/folders/demo/T/",
            PicturesDirectory: "/Users/ada/Pictures",
            VideosDirectory: "/Users/ada/Movies"));

        Assert.Equal(PlatformKind.MacOS, locations.Platform);
        Assert.Equal("/Users/ada/Library/Application Support/Parallax Capture", locations.ConfigDirectory);
        Assert.Equal("/Users/ada/Library/Application Support/Parallax Capture/settings.json", locations.SettingsFilePath);
        Assert.Equal("/Users/ada/Library/Logs/Parallax Capture", locations.LogsDirectory);
        Assert.Equal("/Users/ada/Library/Application Support/Parallax Capture/tools", locations.ToolsDirectory);
        Assert.Equal("/var/folders/demo/T/parallax", locations.TempDirectory);
        Assert.Equal("/Users/ada/Pictures/Parallax Capture", locations.ScreenshotsDirectory);
        Assert.Equal("/Users/ada/Movies/Parallax Capture", locations.RecordingsDirectory);
    }

    [Fact]
    public void LinuxPathsHonorXdgOverrides()
    {
        var locations = PlatformPathPolicy.Create(new PlatformPathEnvironment(
            PlatformKind.Linux,
            UserProfile: "/home/ada",
            TempDirectory: "/tmp",
            XdgConfigHome: "/home/ada/.config-custom",
            XdgDataHome: "/home/ada/.local/share-custom",
            XdgStateHome: "/home/ada/.local/state-custom",
            PicturesDirectory: "/home/ada/Images",
            VideosDirectory: "/home/ada/Movies"));

        Assert.Equal(PlatformKind.Linux, locations.Platform);
        Assert.Equal("/home/ada/.config-custom/parallax", locations.ConfigDirectory);
        Assert.Equal("/home/ada/.config-custom/parallax/settings.json", locations.SettingsFilePath);
        Assert.Equal("/home/ada/.local/state-custom/parallax/logs", locations.LogsDirectory);
        Assert.Equal("/home/ada/.local/share-custom/parallax/tools", locations.ToolsDirectory);
        Assert.Equal("/tmp/parallax", locations.TempDirectory);
        Assert.Equal("/home/ada/Images/Parallax Capture", locations.ScreenshotsDirectory);
        Assert.Equal("/home/ada/Movies/Parallax Capture", locations.RecordingsDirectory);
    }
}
