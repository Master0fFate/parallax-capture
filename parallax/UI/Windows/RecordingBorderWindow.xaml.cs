using System.Windows;
using System.Windows.Interop;

namespace parallax.UI.Windows
{
    public partial class RecordingBorderWindow : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int GWL_EXSTYLE = -20;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // Extra space above the border for the REC label
        private const int TopPadding = 22;

        public RecordingBorderWindow(int x, int y, int width, int height)
        {
            InitializeComponent();

            // Layout inside the window (top=0 is the window top):
            //
            //   y=0..18  REC label area
            //   y=18     ── transparent gap (4px)
            //   y=22     ── top border line (2px thick)
            //   y=24..29 ── transparent gap (5px, outside capture region)
            //   y=29     ── capture region top (same as `y` in screen coords)
            //
            // Window bounds in screen coords:
            //   Left  = x - 7
            //   Top   = y - 7 - TopPadding  = y - 29
            //   Width = width  + 14
            //   Height = height + 14 + TopPadding = height + 36

            int gap = 7; // 5px transparent + 2px border

            Left   = x - gap;
            Top    = y - gap - TopPadding;
            Width  = width  + (gap * 2);
            Height = height + (gap * 2) + TopPadding;

            // Position REC label inside window bounds at (4, 4)
            RecLabel.Margin = new Thickness(4, 4, 0, 0);

            Loaded += (s, e) => MakeClickThrough();
        }

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }
    }
}
