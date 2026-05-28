using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace parallax.Core.Helpers
{
    public static class BitmapHelper
    {
        // Converts System.Drawing.Bitmap to WPF BitmapImage for display in Image controls
        public static BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // Required for cross-thread use
            return bitmapImage;
        }

        // Saves a BitmapSource (from WPF canvas render) to disk
        public static void SaveBitmapSource(BitmapSource source, string filePath, string format = "png")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var fileStream = new FileStream(filePath, FileMode.Create);

            BitmapEncoder encoder = format.ToLower() switch
            {
                "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                "bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(fileStream);
        }

        // Crops a Bitmap to the given rectangle
        public static Bitmap CropBitmap(Bitmap source, Rectangle cropArea)
        {
            return source.Clone(cropArea, source.PixelFormat);
        }
    }
}
