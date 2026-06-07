using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace parallax.UI.Windows
{
    public partial class RecordingControlsWindow : Window
    {
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const double HudMargin = 10.0;
        private const double DefaultHudWidth = 318.0;
        private const double DefaultHudHeight = 58.0;

        private readonly Action _stopRecording;
        private readonly DispatcherTimer _elapsedTimer;
        private readonly DateTimeOffset _startedAt = DateTimeOffset.Now;

        public bool IsCaptureExcluded { get; private set; }

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        public RecordingControlsWindow(int x, int y, int width, int height, Action stopRecording)
        {
            _stopRecording = stopRecording ?? throw new ArgumentNullException(nameof(stopRecording));

            InitializeComponent();
            PositionNearCaptureRegion(x, y, width, height);

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _elapsedTimer.Tick += OnElapsedTimerTick;

            SourceInitialized += (s, e) => ApplyCaptureExclusion();
            Loaded += (s, e) =>
            {
                UpdateElapsedTime();
                _elapsedTimer.Start();
            };
            Closed += (s, e) =>
            {
                _elapsedTimer.Stop();
                _elapsedTimer.Tick -= OnElapsedTimerTick;
            };
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _stopRecording();
        }

        private void OnElapsedTimerTick(object? sender, EventArgs e)
        {
            UpdateElapsedTime();
        }

        private void UpdateElapsedTime()
        {
            var elapsed = DateTimeOffset.Now - _startedAt;
            ElapsedText.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        private void ApplyCaptureExclusion()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            try
            {
                IsCaptureExcluded = SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            }
            catch
            {
                IsCaptureExcluded = false;
            }
        }

        private void PositionNearCaptureRegion(int x, int y, int width, int height)
        {
            var captureRegion = PhysicalToDipRect(x, y, width, height);
            var workArea = GetWorkingAreaDipRect(x, y, width, height);
            double hudWidth = Width > 0 && !double.IsNaN(Width) ? Width : DefaultHudWidth;
            double hudHeight = Height > 0 && !double.IsNaN(Height) ? Height : DefaultHudHeight;

            double left = Clamp(captureRegion.Left, workArea.Left, workArea.Right - hudWidth);
            double aboveTop = captureRegion.Top - hudHeight - HudMargin;
            if (aboveTop >= workArea.Top)
            {
                Left = left;
                Top = aboveTop;
                return;
            }

            double belowTop = captureRegion.Bottom + HudMargin;
            if (belowTop + hudHeight <= workArea.Bottom)
            {
                Left = left;
                Top = belowTop;
                return;
            }

            Left = Clamp(captureRegion.Left + HudMargin, workArea.Left, workArea.Right - hudWidth);
            Top = Clamp(captureRegion.Top + HudMargin, workArea.Top, workArea.Bottom - hudHeight);
        }

        private static Rect GetWorkingAreaDipRect(int x, int y, int width, int height)
        {
            try
            {
                var screen = System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(x, y, width, height));
                var workingArea = screen.WorkingArea;
                return PhysicalToDipRect(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height);
            }
            catch
            {
                return new Rect(
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight);
            }
        }

        private static Rect PhysicalToDipRect(int x, int y, int width, int height)
        {
            var physicalVirtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
            double virtualWidth = SystemParameters.VirtualScreenWidth;
            double virtualHeight = SystemParameters.VirtualScreenHeight;

            if (physicalVirtualScreen.Width <= 0 || physicalVirtualScreen.Height <= 0 || virtualWidth <= 0 || virtualHeight <= 0)
                return new Rect(x, y, width, height);

            double scaleX = physicalVirtualScreen.Width / virtualWidth;
            double scaleY = physicalVirtualScreen.Height / virtualHeight;
            if (scaleX <= 0 || scaleY <= 0 || double.IsNaN(scaleX) || double.IsNaN(scaleY))
                return new Rect(x, y, width, height);

            return new Rect(
                SystemParameters.VirtualScreenLeft + ((x - physicalVirtualScreen.Left) / scaleX),
                SystemParameters.VirtualScreenTop + ((y - physicalVirtualScreen.Top) / scaleY),
                width / scaleX,
                height / scaleY);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min) return min;
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
