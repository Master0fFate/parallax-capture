using System.Drawing;

namespace parallax.Core.Models
{
    public class CaptureResult
    {
        public Bitmap? Image { get; set; }
        public string? FilePath { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVideo { get; set; }
        public bool Success { get; set; }
    }
}
