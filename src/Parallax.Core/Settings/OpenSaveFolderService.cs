using Parallax.Core.Platform;

namespace Parallax.Core.Settings;

public sealed class OpenSaveFolderService
{
    private readonly IPlatformLocations _locations;
    private readonly IFolderLauncher _folderLauncher;

    public OpenSaveFolderService(IPlatformLocations locations, IFolderLauncher folderLauncher)
    {
        _locations = locations;
        _folderLauncher = folderLauncher;
    }

    public FolderLaunchResult Open(ParallaxSettings settings)
    {
        var validation = SaveFolderPolicy.ValidateAndCreate(settings, _locations);
        if (!validation.Success)
        {
            return new FolderLaunchResult(false, validation.Message);
        }

        var launch = _folderLauncher.OpenFolder(validation.RootFolder);
        return launch.Success
            ? launch
            : new FolderLaunchResult(false, string.IsNullOrWhiteSpace(launch.Message)
                ? "Could not open the save folder with the platform file manager."
                : launch.Message);
    }
}
