using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using parallax.Core.Models;

namespace parallax.Core.Services
{
    public class ScreenshotService
    {
        // Captures the ENTIRE virtual screen (all monitors combined)
        public Bitmap CaptureFullScreen()
        {
            var bounds = GetFullScreenBounds();
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

            return bitmap;
        }

        // Captures a specific region of the screen by pixel coordinates
        public Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and Height must be positive.");

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

            return bitmap;
        }

        // Returns the full virtual desktop bounds across all monitors
        public Rectangle GetFullScreenBounds()
        {
            int left = SystemInformation.VirtualScreen.Left;
            int top = SystemInformation.VirtualScreen.Top;
            int width = SystemInformation.VirtualScreen.Width;
            int height = SystemInformation.VirtualScreen.Height;
            return new Rectangle(left, top, width, height);
        }

        // Returns the bounds of the primary monitor only
        public Rectangle GetPrimaryScreenBounds()
        {
            return Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }
    }
}
