using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Parallax.Core.Capture;
using Parallax.Core.Platform;

namespace Parallax.App.Avalonia.Capture;

public sealed class AvaloniaRegionSelectionService : IRegionSelectionService
{
    public RegionSelectionResult SelectRegion()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(SelectRegion);
        }

        using var cancellation = new CancellationTokenSource();
        RegionSelectionResult result = RegionSelectionResult.Cancelled("Region selection was cancelled.");
        var window = new RegionSelectionWindow();
        window.Completed += selection =>
        {
            result = selection;
            cancellation.Cancel();
        };
        window.Show();
        Dispatcher.UIThread.MainLoop(cancellation.Token);
        return result;
    }

    private sealed class RegionSelectionWindow : Window
    {
        private readonly Canvas _canvas = new();
        private readonly Border _selection = new()
        {
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new global::Avalonia.Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(24, 59, 130, 246)),
            IsVisible = false
        };

        private readonly TextBlock _sizeLabel = new()
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(210, 17, 19, 24)),
            Padding = new global::Avalonia.Thickness(6, 3),
            IsVisible = false
        };

        private Point _start;
        private bool _selecting;

        public RegionSelectionWindow()
        {
            Title = "Select capture region";
            WindowState = WindowState.FullScreen;
            Topmost = true;
            WindowDecorations = WindowDecorations.None;
            CanResize = false;
            Background = new SolidColorBrush(Color.FromArgb(92, 0, 0, 0));
            Cursor = new Cursor(StandardCursorType.Cross);
            Content = _canvas;
            _canvas.Children.Add(new TextBlock
            {
                Text = "Drag to select a capture region. Press Esc to cancel.",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(210, 17, 19, 24)),
                Padding = new global::Avalonia.Thickness(10, 6),
                Margin = new global::Avalonia.Thickness(18)
            });
            _canvas.Children.Add(_selection);
            _canvas.Children.Add(_sizeLabel);
            KeyDown += OnKeyDown;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }

        public event Action<RegionSelectionResult>? Completed;

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Complete(RegionSelectionResult.Cancelled("Region selection was cancelled."));
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            _start = point.Position;
            _selecting = true;
            _selection.IsVisible = true;
            _sizeLabel.IsVisible = true;
            e.Pointer.Capture(this);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_selecting)
            {
                return;
            }

            UpdateSelection(e.GetPosition(this));
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_selecting)
            {
                return;
            }

            _selecting = false;
            e.Pointer.Capture(null);
            var rect = GetRect(_start, e.GetPosition(this));
            if (rect.Width < 10 || rect.Height < 10)
            {
                Complete(RegionSelectionResult.Cancelled("Selected region was too small."));
                return;
            }

            var screen = Screens.ScreenFromWindow(this);
            double scaling = screen?.Scaling ?? 1;
            var screenBounds = screen?.Bounds ?? new PixelRect(
                0,
                0,
                (int)Math.Round(ClientSize.Width * scaling),
                (int)Math.Round(ClientSize.Height * scaling));
            var bounds = new CaptureRectangle(
                screenBounds.X + (int)Math.Round(rect.X * scaling),
                screenBounds.Y + (int)Math.Round(rect.Y * scaling),
                Math.Max(1, (int)Math.Round(rect.Width * scaling)),
                Math.Max(1, (int)Math.Round(rect.Height * scaling)));
            Complete(new RegionSelectionResult(true, bounds, "Region selected."));
        }

        private void UpdateSelection(Point end)
        {
            var rect = GetRect(_start, end);
            Canvas.SetLeft(_selection, rect.X);
            Canvas.SetTop(_selection, rect.Y);
            _selection.Width = rect.Width;
            _selection.Height = rect.Height;
            _sizeLabel.Text = $"{(int)rect.Width} x {(int)rect.Height}";
            Canvas.SetLeft(_sizeLabel, rect.X);
            Canvas.SetTop(_sizeLabel, Math.Max(0, rect.Y - 28));
        }

        private void Complete(RegionSelectionResult result)
        {
            Completed?.Invoke(result);
            Close();
        }

        private static Rect GetRect(Point a, Point b)
        {
            double x = Math.Min(a.X, b.X);
            double y = Math.Min(a.Y, b.Y);
            return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }
    }
}
