using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using parallax.Core.Helpers;

namespace parallax.Core.Services
{
    public class ClipboardService
    {
        // Copies a System.Drawing.Bitmap to the Windows clipboard
        public void CopyBitmapToClipboard(Bitmap bitmap)
        {
            var bitmapImage = BitmapHelper.ToBitmapImage(bitmap);
            Clipboard.SetImage(bitmapImage);
        }

        // Copies a file path string to the clipboard
        public void CopyTextToClipboard(string text)
        {
            Clipboard.SetText(text);
        }

        // Copies a file to clipboard as a file drop list (paste into File Explorer works)
        public void CopyFileToClipboard(string filePath)
        {
            var collection = new System.Collections.Specialized.StringCollection { filePath };
            Clipboard.SetFileDropList(collection);
        }
    }
}
