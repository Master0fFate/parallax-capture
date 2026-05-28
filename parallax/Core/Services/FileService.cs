using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using parallax.Core.Models;

namespace parallax.Core.Services
{
    public class FileService
    {
        private readonly AppSettings _settings;

        public FileService(AppSettings settings)
        {
            _settings = settings;
        }

        // Generates a timestamped filename and saves the bitmap to the save folder
        public string SaveScreenshot(Bitmap bitmap)
        {
            string folder = GetImageFolder();
            Directory.CreateDirectory(folder);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"parallax_{timestamp}.{_settings.ImageFormat}";
            string fullPath = Path.Combine(folder, fileName);

            ImageFormat format = _settings.ImageFormat.ToLower() switch
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
            Directory.CreateDirectory(folder);
            return folder;
        }

        // Generates a timestamped path for a video file
        public string GetVideoFilePath(string extension = "mp4")
        {
            string folder = GetVideoFolder();
            Directory.CreateDirectory(folder);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return Path.Combine(folder, $"parallax_{timestamp}.{extension}");
        }

        // Generates a temp path for in-progress recordings
        // Temp files auto-delete when the editor closes without saving
        public string GetTempVideoPath(string extension = "mp4")
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "parallax");
            Directory.CreateDirectory(tempDir);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return Path.Combine(tempDir, $"parallax_rec_{timestamp}.{extension}");
        }

        // Opens the base save folder in Windows Explorer
        public void OpenSaveFolder()
        {
            Directory.CreateDirectory(_settings.SaveFolder);
            System.Diagnostics.Process.Start("explorer.exe", _settings.SaveFolder);
        }

        // ── Subfolder resolution for SeparateFolders setting ──

        private string GetImageFolder()
        {
            return _settings.SeparateFolders
                ? Path.Combine(_settings.SaveFolder, "images")
                : _settings.SaveFolder;
        }

        private string GetVideoFolder()
        {
            return _settings.SeparateFolders
                ? Path.Combine(_settings.SaveFolder, "videos")
                : _settings.SaveFolder;
        }
    }
}
