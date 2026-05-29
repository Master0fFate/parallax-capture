using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using parallax.Core.Enums;
using parallax.Core.Helpers;
using parallax.Core.Models;
using parallax.Core.Services;

namespace parallax.UI.Windows
{
    public partial class AnnotationWindow : Window
    {
        // ── Dependencies
        private readonly ClipboardService _clipboardService;
        private readonly FileService _fileService;

        // ── Current screenshot
        private Bitmap _sourceBitmap;

        // ── Annotation state
        private AnnotationTool _currentTool = AnnotationTool.Pen;
        private System.Windows.Media.Color _currentColor = Colors.Red;
        private double _currentThickness = 2.0;

        // ── Drawing state
        private bool _isDrawing = false;
        private System.Windows.Point _drawStart;
        private UIElement? _currentShape;
        private List<System.Windows.Point> _penPoints = new();

        // ── Undo stack — stores snapshots of all annotation children
        private readonly Stack<List<UIElement>> _undoStack = new();

        // ── Text editing
        private TextBox? _activeTextBox;

        // ── Status feedback
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new()
        {
            Interval = TimeSpan.FromSeconds(3)
        };

        // ── Zoom
        private double _zoomLevel = 1.0;
        private static readonly double[] ZoomSteps = { 0.25, 0.33, 0.5, 0.67, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

        public AnnotationWindow(Bitmap screenshot, ClipboardService clipboardService, FileService fileService)
        {
            InitializeComponent();

            if (screenshot == null)
                throw new ArgumentNullException(nameof(screenshot), "Screenshot bitmap cannot be null");

            _sourceBitmap = screenshot;
            _clipboardService = clipboardService;
            _fileService = fileService;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;

            Loaded += AnnotationWindow_Loaded;
        }

        private void AnnotationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Convert GDI+ Bitmap to WPF BitmapImage for display
                var bitmapImage = BitmapHelper.ToBitmapImage(_sourceBitmap);
                ScreenshotImage.Source = bitmapImage;

                // Size all content layers to the screenshot
                ContentGrid.Width  = _sourceBitmap.Width;
                ContentGrid.Height = _sourceBitmap.Height;
                ScreenshotImage.Width  = _sourceBitmap.Width;
                ScreenshotImage.Height = _sourceBitmap.Height;
                AnnotationCanvas.Width  = _sourceBitmap.Width;
                AnnotationCanvas.Height = _sourceBitmap.Height;

                // Build color swatches
                BuildColorSwatches();

                // Force window to front
                Activate();
            }
            catch (Exception ex)
            {
                // If the bitmap conversion fails, show the window anyway with an error message
                // so the user knows what happened (instead of a ghost window)
                ScreenshotImage.Source = null;
                var errorText = new System.Windows.Controls.TextBlock
                {
                    Text = $"Failed to load screenshot:\n{ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    FontSize = 14,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };
                AnnotationCanvas.Children.Add(errorText);
                AnnotationCanvas.Width = 400;
                AnnotationCanvas.Height = 200;
                Width = 420;
                Height = 330;
                Activate();
            }
        }

        // Creates the row of color picker buttons in the toolbar
        private void BuildColorSwatches()
        {
            var colors = new[]
            {
                Colors.Red, Colors.OrangeRed, Colors.Yellow,
                Colors.LimeGreen, Colors.DodgerBlue, Colors.MediumPurple,
                Colors.White, Colors.Black
            };

            foreach (var color in colors)
            {
                var btn = new Button
                {
                    Width = 20, Height = 20,
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(1, 0, 1, 0),
                    Tag = color
                };
                btn.Click += ColorSwatch_Click;
                ColorSwatches.Items.Add(btn);
            }
        }

        // ────────────────────────────────────────────
        // TOOLBAR EVENTS
        // ────────────────────────────────────────────

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string toolName)
            {
                _currentTool = Enum.Parse<AnnotationTool>(toolName);

                // Finalize any active text box
                FinalizeTextBox();
            }
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is System.Windows.Media.Color c)
                _currentColor = c;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Guard against InitializeComponent() order — this event can fire
            // during XAML parsing before ThicknessValue/ThicknessSlider are wired.
            if (ThicknessValue == null || ThicknessSlider == null) return;

            _currentThickness = e.NewValue;
            int val = (int)Math.Round(e.NewValue);
            ThicknessValue.Text = val.ToString();
            ThicknessSlider.ToolTip = $"Stroke Thickness: {val}";
        }

        // Opens the Windows Forms ColorDialog for full color selection
        private void MoreColors_Click(object sender, RoutedEventArgs e)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog();
            colorDialog.Color = System.Drawing.Color.FromArgb(
                _currentColor.A, _currentColor.R, _currentColor.G, _currentColor.B);
            colorDialog.FullOpen = true;

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _currentColor = System.Windows.Media.Color.FromArgb(
                    colorDialog.Color.A,
                    colorDialog.Color.R,
                    colorDialog.Color.G,
                    colorDialog.Color.B);
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (AnnotationCanvas.Children.Count > 0)
                AnnotationCanvas.Children.RemoveAt(AnnotationCanvas.Children.Count - 1);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            AnnotationCanvas.Children.Clear();
        }

        // ── Zoom ──

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < ZoomSteps.Length; i++)
            {
                if (ZoomSteps[i] > _zoomLevel + 0.001)
                {
                    ApplyZoom(ZoomSteps[i]);
                    return;
                }
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            for (int i = ZoomSteps.Length - 1; i >= 0; i--)
            {
                if (ZoomSteps[i] < _zoomLevel - 0.001)
                {
                    ApplyZoom(ZoomSteps[i]);
                    return;
                }
            }
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoom(1.0);
        }

        private void ApplyZoom(double level)
        {
            _zoomLevel = level;
            double w = _sourceBitmap.Width  * level;
            double h = _sourceBitmap.Height * level;

            // Grid must have explicit size so ScrollViewer knows content extent
            ContentGrid.Width  = w;
            ContentGrid.Height = h;
            ScreenshotImage.Width  = w;
            ScreenshotImage.Height = h;
            AnnotationCanvas.Width  = w;
            AnnotationCanvas.Height = h;

            TxtZoomLevel.Text = $"{(int)(level * 100)}%";
        }

        // ────────────────────────────────────────────
        // CANVAS DRAWING EVENTS
        // ────────────────────────────────────────────

        private void AnnotationCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            FinalizeTextBox();
            // Canvas is sized at imageW * zoom, so divide to get image coordinates
            var raw = e.GetPosition(AnnotationCanvas);
            _drawStart = new System.Windows.Point(raw.X / _zoomLevel, raw.Y / _zoomLevel);
            _isDrawing = true;
            _penPoints.Clear();

            AnnotationCanvas.CaptureMouse();

            var brush = new SolidColorBrush(_currentColor);

            switch (_currentTool)
            {
                case AnnotationTool.Pen:
                    _penPoints.Add(_drawStart);
                    var polyline = new Polyline
                    {
                        Stroke = brush,
                        StrokeThickness = _currentThickness,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    polyline.Points.Add(_drawStart);
                    _currentShape = polyline;
                    AnnotationCanvas.Children.Add(polyline);
                    break;

                case AnnotationTool.Highlighter:
                    _penPoints.Add(_drawStart);
                    var highlight = new Polyline
                    {
                        Stroke = new SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(100, _currentColor.R, _currentColor.G, _currentColor.B)),
                        StrokeThickness = _currentThickness * 4,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Flat,
                        StrokeEndLineCap = PenLineCap.Flat
                    };
                    highlight.Points.Add(_drawStart);
                    _currentShape = highlight;
                    AnnotationCanvas.Children.Add(highlight);
                    break;

                case AnnotationTool.Arrow:
                    var arrowLine = new Line
                    {
                        Stroke = brush,
                        StrokeThickness = _currentThickness,
                        X1 = _drawStart.X, Y1 = _drawStart.Y,
                        X2 = _drawStart.X, Y2 = _drawStart.Y
                    };
                    _currentShape = arrowLine;
                    AnnotationCanvas.Children.Add(arrowLine);
                    break;

                case AnnotationTool.Rectangle:
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = brush,
                        StrokeThickness = _currentThickness,
                        Fill = System.Windows.Media.Brushes.Transparent
                    };
                    Canvas.SetLeft(rect, _drawStart.X);
                    Canvas.SetTop(rect, _drawStart.Y);
                    _currentShape = rect;
                    AnnotationCanvas.Children.Add(rect);
                    break;

                case AnnotationTool.Ellipse:
                    var ellipse = new Ellipse
                    {
                        Stroke = brush,
                        StrokeThickness = _currentThickness,
                        Fill = System.Windows.Media.Brushes.Transparent
                    };
                    Canvas.SetLeft(ellipse, _drawStart.X);
                    Canvas.SetTop(ellipse, _drawStart.Y);
                    _currentShape = ellipse;
                    AnnotationCanvas.Children.Add(ellipse);
                    break;

                case AnnotationTool.Blur:
                {
                    // Create a rectangle with VisualBrush sampling the image behind it + blur effect
                    var blurRect = new System.Windows.Shapes.Rectangle
                    {
                        StrokeThickness = 0,
                        Fill = new VisualBrush
                        {
                            Visual = ScreenshotImage,
                            ViewboxUnits = BrushMappingMode.Absolute,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Stretch = Stretch.None,
                            AlignmentX = AlignmentX.Left,
                            AlignmentY = AlignmentY.Top
                        },
                        Effect = new System.Windows.Media.Effects.BlurEffect
                        {
                            Radius = 12,
                            KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                        },
                        Opacity = 0.9
                    };
                    Canvas.SetLeft(blurRect, _drawStart.X);
                    Canvas.SetTop(blurRect, _drawStart.Y);
                    _currentShape = blurRect;
                    AnnotationCanvas.Children.Add(blurRect);
                    break;
                }

                case AnnotationTool.Text:
                    var tb = new TextBox
                    {
                        Background = System.Windows.Media.Brushes.Transparent,
                        Foreground = brush,
                        BorderThickness = new Thickness(1),
                        BorderBrush = brush,
                        FontSize = Math.Max(_currentThickness * 2, 10),
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        MinWidth = Math.Max(_currentThickness * 8, 60),
                        MinHeight = Math.Max(_currentThickness * 2, 24),
                        AcceptsReturn = true,
                        Cursor = Cursors.IBeam,
                        CaretBrush = brush
                    };
                    Canvas.SetLeft(tb, _drawStart.X);
                    Canvas.SetTop(tb, _drawStart.Y);
                    _activeTextBox = tb;
                    AnnotationCanvas.Children.Add(tb);
                    tb.Focus();
                    _isDrawing = false;
                    AnnotationCanvas.ReleaseMouseCapture();
                    break;
            }
        }

        private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentShape == null) return;

            // Canvas is sized at imageW * zoom — divide to get true image coordinates
            var raw = e.GetPosition(AnnotationCanvas);
            var pos = new System.Windows.Point(raw.X / _zoomLevel, raw.Y / _zoomLevel);

            // Clamp to image bounds — prevents drawing bleeding into toolbar/titlebar
            pos.X = Math.Max(0, Math.Min(pos.X, _sourceBitmap.Width));
            pos.Y = Math.Max(0, Math.Min(pos.Y, _sourceBitmap.Height));

            switch (_currentTool)
            {
                case AnnotationTool.Pen:
                case AnnotationTool.Highlighter:
                    if (_currentShape is Polyline poly)
                        poly.Points.Add(pos);
                    break;

                case AnnotationTool.Arrow:
                    if (_currentShape is Line line)
                    {
                        line.X2 = pos.X;
                        line.Y2 = pos.Y;
                    }
                    break;

                case AnnotationTool.Rectangle:
                    if (_currentShape is System.Windows.Shapes.Rectangle rect)
                    {
                        double x = Math.Min(pos.X, _drawStart.X);
                        double y = Math.Min(pos.Y, _drawStart.Y);
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        rect.Width  = Math.Abs(pos.X - _drawStart.X);
                        rect.Height = Math.Abs(pos.Y - _drawStart.Y);
                    }
                    break;

                case AnnotationTool.Ellipse:
                    if (_currentShape is Ellipse el)
                    {
                        double x = Math.Min(pos.X, _drawStart.X);
                        double y = Math.Min(pos.Y, _drawStart.Y);
                        Canvas.SetLeft(el, x);
                        Canvas.SetTop(el, y);
                        el.Width  = Math.Abs(pos.X - _drawStart.X);
                        el.Height = Math.Abs(pos.Y - _drawStart.Y);
                    }
                    break;

                case AnnotationTool.Blur:
                    if (_currentShape is System.Windows.Shapes.Rectangle bl)
                    {
                        double x = Math.Min(pos.X, _drawStart.X);
                        double y = Math.Min(pos.Y, _drawStart.Y);
                        Canvas.SetLeft(bl, x);
                        Canvas.SetTop(bl, y);
                        bl.Width  = Math.Abs(pos.X - _drawStart.X);
                        bl.Height = Math.Abs(pos.Y - _drawStart.Y);
                        // Update the VisualBrush viewbox to track the image behind
                        if (bl.Fill is VisualBrush vb)
                        {
                            vb.Viewbox = new Rect(x, y, bl.Width, bl.Height);
                            vb.Viewport = new Rect(0, 0, bl.Width, bl.Height);
                        }
                    }
                    break;
            }
        }

        private void AnnotationCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;

            var endPos = e.GetPosition(AnnotationCanvas);

            // For arrows: draw arrowhead at the end
            if (_currentTool == AnnotationTool.Arrow && _currentShape is Line arrowLine)
            {
                DrawArrowhead(arrowLine.X1, arrowLine.Y1, endPos.X, endPos.Y);
            }

            // For blur: finalize the VisualBrush viewbox to match the final rectangle position
            if (_currentTool == AnnotationTool.Blur && _currentShape is System.Windows.Shapes.Rectangle blurRect)
            {
                double x = Canvas.GetLeft(blurRect);
                double y = Canvas.GetTop(blurRect);
                if (blurRect.Fill is VisualBrush vb)
                {
                    vb.Viewbox = new Rect(x, y, blurRect.Width, blurRect.Height);
                    vb.Viewport = new Rect(0, 0, blurRect.Width, blurRect.Height);
                }
            }

            _isDrawing = false;
            _currentShape = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }

        // ────────────────────────────────────────────
        // ARROWHEAD DRAWING
        // ────────────────────────────────────────────

        private void DrawArrowhead(double x1, double y1, double x2, double y2)
        {
            // Calculate angle of the line
            double angle = Math.Atan2(y2 - y1, x2 - x1);
            double arrowLength = 14.0;
            double arrowAngle  = 25.0 * (Math.PI / 180.0);

            var brush = new SolidColorBrush(_currentColor);

            // Left wing of arrowhead
            var leftX = x2 - arrowLength * Math.Cos(angle - arrowAngle);
            var leftY = y2 - arrowLength * Math.Sin(angle - arrowAngle);

            // Right wing of arrowhead
            var rightX = x2 - arrowLength * Math.Cos(angle + arrowAngle);
            var rightY = y2 - arrowLength * Math.Sin(angle + arrowAngle);

            // Draw left wing line
            var leftLine = new Line
            {
                Stroke = brush,
                StrokeThickness = _currentThickness,
                X1 = x2, Y1 = y2,
                X2 = leftX, Y2 = leftY,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            // Draw right wing line
            var rightLine = new Line
            {
                Stroke = brush,
                StrokeThickness = _currentThickness,
                X1 = x2, Y1 = y2,
                X2 = rightX, Y2 = rightY,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            AnnotationCanvas.Children.Add(leftLine);
            AnnotationCanvas.Children.Add(rightLine);
        }

        // ────────────────────────────────────────────
        // TEXT BOX MANAGEMENT
        // ────────────────────────────────────────────

        // Finalizes any active text box (removes border, makes it non-editable looking)
        private void FinalizeTextBox()
        {
            if (_activeTextBox != null)
            {
                _activeTextBox.BorderThickness = new Thickness(0);
                _activeTextBox.IsReadOnly = true;
                _activeTextBox = null;
            }
        }

        // ────────────────────────────────────────────
        // RENDER FINAL IMAGE (screenshot + annotations)
        // ────────────────────────────────────────────

        private BitmapSource RenderFinalImage()
        {
            // Temporarily reset canvas to original image size for rendering.
            // Annotations are at image coordinates (mouse / zoom), so rendering
            // at 1:1 produces the correct output at the original resolution.
            double prevW = AnnotationCanvas.Width;
            double prevH = AnnotationCanvas.Height;
            AnnotationCanvas.Width  = _sourceBitmap.Width;
            AnnotationCanvas.Height = _sourceBitmap.Height;

            // Create a render target the size of the image
            var renderTarget = new RenderTargetBitmap(
                _sourceBitmap.Width,
                _sourceBitmap.Height,
                96, 96,
                PixelFormats.Pbgra32
            );

            // First render the screenshot image
            var imageVisual = new DrawingVisual();
            using (var ctx = imageVisual.RenderOpen())
            {
                var bitmapImage = BitmapHelper.ToBitmapImage(_sourceBitmap);
                ctx.DrawImage(bitmapImage, new Rect(0, 0, _sourceBitmap.Width, _sourceBitmap.Height));
            }
            renderTarget.Render(imageVisual);

            // Then render the annotation canvas on top (at original size)
            renderTarget.Render(AnnotationCanvas);

            // Restore zoomed size
            AnnotationCanvas.Width  = prevW;
            AnnotationCanvas.Height = prevH;

            return renderTarget;
        }

        // ────────────────────────────────────────────
        // ACTION BUTTONS
        // ────────────────────────────────────────────

        private void BtnCopyClipboard_Click(object sender, RoutedEventArgs e)
        {
            FinalizeTextBox();
            var rendered = RenderFinalImage();
            System.Windows.Clipboard.SetImage(rendered);
            ShowStatus("Copied to clipboard");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            FinalizeTextBox();
            var rendered = RenderFinalImage();
            string actualPath = System.IO.Path.Combine(
                _fileService.GetSaveFolder(),
                $"parallax_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            BitmapHelper.SaveBitmapSource(rendered, actualPath, "png");
            ShowStatus($"Saved \u2014 {System.IO.Path.GetFileName(actualPath)}");
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            FinalizeTextBox();
            var dialog = new SaveFileDialog
            {
                Title = "Save Screenshot",
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap|*.bmp",
                DefaultExt = "png",
                FileName = $"parallax_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var rendered = RenderFinalImage();
                string ext = System.IO.Path.GetExtension(dialog.FileName).TrimStart('.').ToLower();
                BitmapHelper.SaveBitmapSource(rendered, dialog.FileName, ext);
                ShowStatus($"Saved \u2014 {System.IO.Path.GetFileName(dialog.FileName)}");
            }
        }

        // Shows a green status message in the bottom bar that auto-fades after 3 seconds
        private void ShowStatus(string message)
        {
            TxtStatus.Text = message;
            TxtStatus.Opacity = 1;

            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTick;
            _statusTimer.Tick += OnStatusTick;
            _statusTimer.Start();
        }

        private void OnStatusTick(object? sender, EventArgs e)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTick;
            TxtStatus.Opacity = 0;
        }

        // ────────────────────────────────────────────
        // KEYBOARD SHORTCUTS
        // ────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Z:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        BtnUndo_Click(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // Makes the annotation window draggable by its title bar
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
