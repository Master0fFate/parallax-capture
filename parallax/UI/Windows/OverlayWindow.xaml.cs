using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using parallax.Core.Models;

namespace parallax.UI.Windows
{
    public partial class OverlayWindow : Window
    {
        // ── Public result — filled in when user finishes selection
        public System.Drawing.Rectangle SelectedRegion { get; private set; }
        public bool SelectionConfirmed { get; private set; } = false;

        // ── Internal state
        private System.Windows.Point _startPoint;
        private System.Windows.Point _endPoint;
        private bool _isSelecting = false;

        // ── Full screen dimensions (set on load)
        private double _screenWidth;
        private double _screenHeight;

        public OverlayWindow()
        {
            InitializeComponent();
            Loaded += OverlayWindow_Loaded;
            KeyDown += OverlayWindow_KeyDown;
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Use WPF SystemParameters (device-independent pixels) for window sizing.
            // SystemInformation.VirtualScreen returns physical pixels, which don't
            // match WPF's coordinate space on HiDPI displays — causing selection offset.
            Left = System.Windows.SystemParameters.VirtualScreenLeft;
            Top = System.Windows.SystemParameters.VirtualScreenTop;
            Width = System.Windows.SystemParameters.VirtualScreenWidth;
            Height = System.Windows.SystemParameters.VirtualScreenHeight;

            _screenWidth = Width;
            _screenHeight = Height;

            // Draw the initial full-screen dim (no hole yet)
            DrawDimOverlay(new Rect(0, 0, 0, 0));
        }

        // ── ESC key cancels the overlay
        private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectionConfirmed = false;
                Close();
            }
        }

        // ────────────────────────────────────────────
        // MOUSE EVENTS
        // ────────────────────────────────────────────

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(MainCanvas);
                _isSelecting = true;

                // Hide instruction label once user starts dragging
                InstructionBorder.Visibility = Visibility.Collapsed;

                // Show selection rect and size label
                SelectionRect.Visibility = Visibility.Visible;
                SizeLabel.Visibility = Visibility.Visible;

                MainCanvas.CaptureMouse();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MainCanvas);

            // Show crosshairs on first mouse move (prevents (0,0) artifact)
            CrosshairH.Visibility = Visibility.Visible;
            CrosshairV.Visibility = Visibility.Visible;

            // ── Update crosshair position
            CrosshairH.X1 = 0;
            CrosshairH.X2 = _screenWidth;
            CrosshairH.Y1 = pos.Y;
            CrosshairH.Y2 = pos.Y;

            CrosshairV.X1 = pos.X;
            CrosshairV.X2 = pos.X;
            CrosshairV.Y1 = 0;
            CrosshairV.Y2 = _screenHeight;

            if (_isSelecting)
            {
                _endPoint = pos;
                var selectionRect = GetNormalizedRect(_startPoint, _endPoint);

                // Update the marching ants selection rectangle
                Canvas.SetLeft(SelectionRect, selectionRect.X);
                Canvas.SetTop(SelectionRect, selectionRect.Y);
                SelectionRect.Width = selectionRect.Width;
                SelectionRect.Height = selectionRect.Height;

                // Update dim path with hole
                DrawDimOverlay(selectionRect);

                // Update size label
                int w = (int)selectionRect.Width;
                int h = (int)selectionRect.Height;
                SizeLabelText.Text = $"{w} x {h}";

                // Position the size label just above the selection
                double labelX = selectionRect.X;
                double labelY = selectionRect.Y - 28;
                if (labelY < 0) labelY = selectionRect.Y + 4;

                Canvas.SetLeft(SizeLabel, labelX);
                Canvas.SetTop(SizeLabel, labelY);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            _endPoint = e.GetPosition(MainCanvas);
            _isSelecting = false;
            MainCanvas.ReleaseMouseCapture();

            var rect = GetNormalizedRect(_startPoint, _endPoint);

            // Require a minimum selection size of 10x10 pixels
            if (rect.Width < 10 || rect.Height < 10)
            {
                SelectionConfirmed = false;
                Close();
                return;
            }

            // Convert from canvas-relative DIPs to physical screen pixels.
            // PointToScreen handles DPI scaling correctly — avoids the DIP/physical-pixel
            // mismatch that caused selections to be offset on HiDPI displays.
            // Wrapped in try-catch: PointToScreen can fail on unusual multi-monitor
            // or DPI configurations, which would otherwise leave the overlay stuck open.
            try
            {
                var topLeft = MainCanvas.PointToScreen(new System.Windows.Point(rect.X, rect.Y));
                var bottomRight = MainCanvas.PointToScreen(new System.Windows.Point(
                    rect.X + rect.Width, rect.Y + rect.Height));

                SelectedRegion = new System.Drawing.Rectangle(
                    (int)topLeft.X,
                    (int)topLeft.Y,
                    (int)(bottomRight.X - topLeft.X),
                    (int)(bottomRight.Y - topLeft.Y)
                );

                SelectionConfirmed = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not convert selection to screen coordinates: {ex.Message}",
                    "Parallax Capture",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                SelectionConfirmed = false;
            }

            Close();
        }

        // ────────────────────────────────────────────
        // DRAWING HELPERS
        // ────────────────────────────────────────────

        // Draws the dark overlay with a transparent hole cut out for the selected area
        private void DrawDimOverlay(Rect selection)
        {
            // Full screen geometry
            var fullScreen = new RectangleGeometry(new Rect(0, 0, _screenWidth, _screenHeight));

            // Selection hole geometry
            var hole = new RectangleGeometry(selection);

            // Combine: Exclude punches the hole out of the full screen rect
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullScreen, hole);

            DimPath.Data = combined;
        }

        // Normalizes a rectangle from any two corner points (handles reverse drag)
        private static Rect GetNormalizedRect(System.Windows.Point p1, System.Windows.Point p2)
        {
            double x = Math.Min(p1.X, p2.X);
            double y = Math.Min(p1.Y, p2.Y);
            double w = Math.Abs(p1.X - p2.X);
            double h = Math.Abs(p1.Y - p2.Y);
            return new Rect(x, y, w, h);
        }
    }
}
