using Parallax.Core.Platform;

namespace Parallax.Core.Settings;

public enum SaveMediaKind
{
    Image,
    Video,
    Gif
}

public sealed record SaveFolderValidationResult(bool Success, string RootFolder, string Message);

public static class SaveFolderPolicy
{
    public static SaveFolderValidationResult ValidateAndCreate(
        ParallaxSettings settings,
        IPlatformLocations locations,
        bool createDirectories = true)
    {
        string root = ResolveSaveRoot(settings, locations);
        if (!IsSafeAbsolutePath(root, locations.Platform))
        {
            return new SaveFolderValidationResult(false, root, "The configured save folder must be a safe absolute path.");
        }

        if (createDirectories)
        {
            try
            {
                Directory.CreateDirectory(root);
                if (settings.SeparateFolders)
                {
                    Directory.CreateDirectory(GetFolderFor(settings, locations, SaveMediaKind.Image));
                    Directory.CreateDirectory(GetFolderFor(settings, locations, SaveMediaKind.Video));
                    Directory.CreateDirectory(GetFolderFor(settings, locations, SaveMediaKind.Gif));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
            {
                return new SaveFolderValidationResult(false, root, "Save folder could not be created. Choose a folder you can write to.");
            }
        }

        return new SaveFolderValidationResult(true, root, "Save folder is ready.");
    }

    public static string GetFolderFor(ParallaxSettings settings, IPlatformLocations locations, SaveMediaKind kind)
    {
        string root = ResolveSaveRoot(settings, locations);
        if (!settings.SeparateFolders)
        {
            return root;
        }

        string subfolder = kind switch
        {
            SaveMediaKind.Video => "videos",
            SaveMediaKind.Gif => "gifs",
            _ => "images"
        };
        return Path.Combine(root, subfolder);
    }

    public static string ResolveSaveRoot(ParallaxSettings settings, IPlatformLocations locations)
    {
        string configured = settings.SaveFolder?.Trim() ?? string.Empty;
        string selected = string.IsNullOrWhiteSpace(configured)
            ? locations.ScreenshotsDirectory
            : Environment.ExpandEnvironmentVariables(configured);

        return selected.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsSafeAbsolutePath(string folder, PlatformKind platform)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        if (folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        return platform switch
        {
            PlatformKind.Windows => Path.IsPathFullyQualified(folder) && !folder.EndsWith(":", StringComparison.Ordinal),
            PlatformKind.MacOS or PlatformKind.Linux => folder.StartsWith("/", StringComparison.Ordinal),
            _ => false
        };
    }
}
