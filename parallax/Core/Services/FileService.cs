using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using parallax.Core.Models;

namespace parallax.Core.Services
{
    public class FileService
    {
        private AppSettings _settings;

        public FileService(AppSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
        }

        // Generates a timestamped filename and saves the bitmap to the save folder
        public string SaveScreenshot(Bitmap bitmap)
        {
            string folder = GetImageFolder();
            EnsureDirectory(folder, "Image save folder");

            string extension = NormalizeExtension(_settings.ImageFormat, "png");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fullPath = GetUniquePath(folder, $"parallax_{timestamp}", extension);

            ImageFormat format = extension.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => ImageFormat.Jpeg,
                "bmp" => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            bitmap.Save(fullPath, format);
            return fullPath;
        }

        // Returns the configured save folder path (creates it if needed)
        public string GetSaveFolder()
        {
            string folder = GetImageFolder();
            EnsureDirectory(folder, "Save folder");
            return folder;
        }

        // Generates a timestamped path for an image file without writing it.
        public string GetImageFilePath(string extension = "png")
        {
            string folder = GetImageFolder();
            EnsureDirectory(folder, "Image save folder");

            extension = NormalizeExtension(extension, "png");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return GetUniquePath(folder, $"parallax_{timestamp}", extension);
        }

        // Generates a timestamped path for a video file
        public string GetVideoFilePath(string extension = "mp4")
        {
            string folder = GetVideoFolder();
            EnsureDirectory(folder, "Video save folder");
            extension = NormalizeExtension(extension, "mp4");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return GetUniquePath(folder, $"parallax_{timestamp}", extension);
        }

        // Generates a temp path for in-progress recordings
        // Temp files auto-delete when the editor closes without saving
        public string GetTempVideoPath(string extension = "mp4")
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "parallax");
            EnsureDirectory(tempDir, "Temporary recording folder");
            extension = NormalizeExtension(extension, "mp4");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return GetUniquePath(tempDir, $"parallax_rec_{timestamp}", extension);
        }

        // Opens the base save folder in Windows Explorer
        public void OpenSaveFolder()
        {
            string folder = ResolveSaveRoot();
            EnsureDirectory(folder, "Save folder");

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string explorerPath = Path.Combine(windowsDirectory, "explorer.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(explorerPath) ? explorerPath : "explorer.exe",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(folder);

            try
            {
                if (Process.Start(startInfo) == null)
                {
                    throw new InvalidOperationException("File Explorer did not start.");
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                throw new InvalidOperationException("Could not open the save folder in File Explorer.", ex);
            }
        }

        // ── Subfolder resolution for SeparateFolders setting ──

        private string GetImageFolder()
        {
            string saveRoot = ResolveSaveRoot();
            return _settings.SeparateFolders
                ? Path.Combine(saveRoot, "images")
                : saveRoot;
        }

        private string GetVideoFolder()
        {
            string saveRoot = ResolveSaveRoot();
            return _settings.SeparateFolders
                ? Path.Combine(saveRoot, "videos")
                : saveRoot;
        }

        private string ResolveSaveRoot()
        {
            string folder = string.IsNullOrWhiteSpace(_settings.SaveFolder)
                ? GetDefaultSaveFolder()
                : Environment.ExpandEnvironmentVariables(_settings.SaveFolder.Trim());

            try
            {
                if (!Path.IsPathFullyQualified(folder))
                {
                    throw new InvalidOperationException("The configured save folder must be a full folder path.");
                }

                return Path.GetFullPath(folder);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException("The configured save folder is not a valid path.", ex);
            }
        }

        private static string GetDefaultSaveFolder()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string root = string.IsNullOrWhiteSpace(pictures)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : pictures;
            return Path.Combine(root, "parallax_captures");
        }

        private static void EnsureDirectory(string folder, string label)
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"{label} could not be created. Choose a folder you can write to.", ex);
            }
        }

        private static string NormalizeExtension(string extension, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(extension)
                ? fallback
                : extension.Trim().TrimStart('.');

            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = fallback;
            }

            if (normalized.StartsWith(".", StringComparison.Ordinal)
                || normalized.EndsWith(".", StringComparison.Ordinal)
                || normalized.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("File extension must only contain letters, numbers, and single dots.");
            }

            foreach (char c in normalized)
            {
                bool allowed = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '.';
                if (!allowed)
                {
                    throw new InvalidOperationException("File extension must only contain letters, numbers, and single dots.");
                }
            }

            return normalized;
        }

        private static string GetUniquePath(string folder, string baseName, string extension)
        {
            string candidate = Path.Combine(folder, $"{baseName}.{extension}");
            for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
            {
                if (suffix > 999)
                {
                    throw new IOException("Could not create a unique file name for this capture.");
                }

                candidate = Path.Combine(folder, $"{baseName}_{suffix}.{extension}");
            }

            return candidate;
        }
    }
}
