using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        private readonly string _imageFormat;

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
        private RichTextBox? _activeTextBox;
        private bool _isUpdatingTextToolbar;
        private bool _isMovingText;
        private RichTextBox? _movingTextBox;
        private System.Windows.Point _textMoveStart;
        private System.Windows.Point _textMoveOrigin;

        // ── Status feedback
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new()
        {
            Interval = TimeSpan.FromSeconds(3)
        };

        // ── Zoom
        private double _zoomLevel = 1.0;
        private static readonly double[] ZoomSteps = { 0.25, 0.33, 0.5, 0.67, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

        public AnnotationWindow(Bitmap screenshot, ClipboardService clipboardService, FileService fileService, string imageFormat = "png")
        {
            InitializeComponent();

            if (screenshot == null)
                throw new ArgumentNullException(nameof(screenshot), "Screenshot bitmap cannot be null");

            _sourceBitmap = screenshot;
            _clipboardService = clipboardService;
            _fileService = fileService;
            _imageFormat = imageFormat;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;

            Loaded += AnnotationWindow_Loaded;

            // Clean up bitmap when window closes (KAM #3)
            Closed += (s, e) =>
            {
                _statusTimer.Stop();
                _statusTimer.Tick -= OnStatusTick;
                _sourceBitmap?.Dispose();
                _sourceBitmap = null!;
            };
        }

        private void AnnotationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Convert GDI+ Bitmap to WPF BitmapImage for display
                var bitmapImage = BitmapHelper.ToBitmapImage(_sourceBitmap);
                ScreenshotImage.Source = bitmapImage;

                // Size canvas to match the screenshot (stays at natural size;
                // zoom is applied via LayoutTransform on the parent ContentGrid)
                AnnotationCanvas.Width  = _sourceBitmap.Width;
                AnnotationCanvas.Height = _sourceBitmap.Height;
                TextAdornerCanvas.Width = _sourceBitmap.Width;
                TextAdornerCanvas.Height = _sourceBitmap.Height;

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
                    Foreground = TryFindResource("ProductDangerBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Red,
                    FontSize = 14,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };
                AnnotationCanvas.Children.Add(errorText);
                AnnotationCanvas.Width = 400;
                AnnotationCanvas.Height = 200;
                TextAdornerCanvas.Width = 400;
                TextAdornerCanvas.Height = 200;
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
                    Width = 24, Height = 24,
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2, 0, 2, 0),
                    Tag = color,
                    ToolTip = "Set annotation color",
                    Style = (Style)FindResource("ColorSwatchStyle")
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
            {
                _currentColor = c;
                UpdateTextColorPreview();
                ApplyTextSelectionValue(TextElement.ForegroundProperty, new SolidColorBrush(_currentColor));
            }
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Guard against InitializeComponent() order — this event can fire
            // during XAML parsing before ThicknessValue/ThicknessSlider are wired.
            if (ThicknessValue == null || ThicknessSlider == null) return;

            _currentThickness = e.NewValue;
            int val = (int)Math.Round(e.NewValue);
            ThicknessValue.Text = val.ToString();
            ThicknessSlider.ToolTip = $"Stroke size: {val}";

            if (!_isUpdatingTextToolbar && _activeTextBox != null)
            {
                ApplyTextSelectionValue(TextElement.FontSizeProperty, GetTextFontSize(e.NewValue));
                UpdateTextAdorners();
            }
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
                UpdateTextColorPreview();
                ApplyTextSelectionValue(TextElement.ForegroundProperty, new SolidColorBrush(_currentColor));
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            HideTextAdorners();
            _activeTextBox = null;
            if (AnnotationCanvas.Children.Count > 0)
                AnnotationCanvas.Children.RemoveAt(AnnotationCanvas.Children.Count - 1);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            HideTextAdorners();
            _activeTextBox = null;
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
            ZoomTransform.ScaleX = level;
            ZoomTransform.ScaleY = level;
            TxtZoomLevel.Text = $"{(int)(level * 100)}%";
        }

        // ────────────────────────────────────────────
        // CANVAS DRAWING EVENTS
        // ────────────────────────────────────────────

        private void AnnotationCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (IsTextInteractionSource(e.OriginalSource)) return;

            FinalizeTextBox();
            // GetPosition(AnnotationCanvas) already traverses the visual tree including
            // the parent ContentGrid's LayoutTransform, returning coordinates in the
            // Canvas's own logical coordinate space (0..Width, 0..Height). No zoom
            // correction needed — Canvas.SetLeft/Top and geometry expect this space.
            _drawStart = e.GetPosition(AnnotationCanvas);
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
                    // Snapshot the current pixel stack before placing the blur rectangle.
                    // This blurs screenshot pixels and any existing annotations without
                    // recursively sampling the blur rectangle itself.
                    var blurSnapshot = RenderFinalImage();
                    var blurRect = new System.Windows.Shapes.Rectangle
                    {
                        StrokeThickness = 0,
                        Fill = new ImageBrush(blurSnapshot)
                        {
                            ViewboxUnits = BrushMappingMode.Absolute,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Stretch = Stretch.Fill,
                            AlignmentX = AlignmentX.Left,
                            AlignmentY = AlignmentY.Top
                        },
                        Effect = new System.Windows.Media.Effects.BlurEffect
                        {
                            Radius = 12,
                            KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                        },
                        Opacity = 1.0
                    };
                    Canvas.SetLeft(blurRect, _drawStart.X);
                    Canvas.SetTop(blurRect, _drawStart.Y);
                    _currentShape = blurRect;
                    AnnotationCanvas.Children.Add(blurRect);
                    break;
                }

                case AnnotationTool.Text:
                    var tb = CreateRichTextAnnotationBox(brush);
                    Canvas.SetLeft(tb, _drawStart.X);
                    Canvas.SetTop(tb, _drawStart.Y);
                    AnnotationCanvas.Children.Add(tb);
                    SetActiveTextBox(tb, focus: true);
                    _isDrawing = false;
                    AnnotationCanvas.ReleaseMouseCapture();
                    break;
            }
        }

        private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentShape == null) return;

            // GetPosition(AnnotationCanvas) returns logical canvas-space coords —
            // the parent LayoutTransform is already accounted for by WPF.
            var pos = e.GetPosition(AnnotationCanvas);

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
                        // Update the ImageBrush crop to track the snapshotted pixels behind the blur.
                        if (bl.Fill is ImageBrush vb)
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

            // For arrows: draw arrowhead at the end position (already in canvas coords)
            if (_currentTool == AnnotationTool.Arrow && _currentShape is Line arrowLine)
            {
                // Use the line's final X2/Y2 (already Pushed+Clamped in MouseMove) to ensure
                // the arrowhead exactly matches the visual endpoint regardless of zoom.
                DrawArrowhead(arrowLine.X1, arrowLine.Y1, arrowLine.X2, arrowLine.Y2);
            }

            // For blur: finalize the ImageBrush viewbox to match the final rectangle position
            if (_currentTool == AnnotationTool.Blur && _currentShape is System.Windows.Shapes.Rectangle blurRect)
            {
                double x = Canvas.GetLeft(blurRect);
                double y = Canvas.GetTop(blurRect);
                if (blurRect.Fill is ImageBrush vb)
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

        private RichTextBox CreateRichTextAnnotationBox(SolidColorBrush brush)
        {
            double fontSize = GetTextFontSize(_currentThickness);
            string fontFamilyName = GetSelectedFontFamilyName();
            var fontFamily = new System.Windows.Media.FontFamily(fontFamilyName);

            var paragraph = new Paragraph(new Run())
            {
                Margin = new Thickness(0),
                LineHeight = fontSize * 1.25
            };

            var document = new FlowDocument(paragraph)
            {
                PagePadding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = fontFamily,
                FontSize = fontSize,
                Foreground = brush
            };

            var textBox = new RichTextBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = brush,
                BorderThickness = new Thickness(1),
                BorderBrush = brush,
                FontSize = fontSize,
                FontFamily = fontFamily,
                MinWidth = Math.Max(_currentThickness * 16, 120),
                Width = Math.Max(_currentThickness * 24, 180),
                MinHeight = Math.Max(_currentThickness * 2.5, 28),
                Padding = new Thickness(4),
                Cursor = Cursors.IBeam,
                CaretBrush = brush,
                AcceptsTab = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Document = document
            };

            textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            textBox.GotKeyboardFocus += TextBox_GotKeyboardFocus;
            textBox.SelectionChanged += TextBox_SelectionChanged;
            textBox.TextChanged += TextBox_TextChanged;

            return textBox;
        }

        private static double GetTextFontSize(double thickness) => Math.Max(thickness * 2, 10);

        private string GetSelectedFontFamilyName()
        {
            if (CmbTextFontFamily?.SelectedItem is ComboBoxItem item && item.Content is string selected)
                return selected;

            return "Segoe UI";
        }

        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not RichTextBox textBox) return;

            if (_activeTextBox != textBox)
            {
                SetActiveTextBox(textBox, focus: true);
                e.Handled = true;
                return;
            }

            SetActiveTextBox(textBox, focus: false);
        }

        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is RichTextBox textBox)
                SetActiveTextBox(textBox, focus: false);
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender == _activeTextBox)
            {
                UpdateTextToolbarFromSelection();
                UpdateTextAdorners();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender == _activeTextBox)
                UpdateTextAdorners();
        }

        private void SetActiveTextBox(RichTextBox textBox, bool focus)
        {
            if (_activeTextBox != null && _activeTextBox != textBox)
                DeactivateTextBox(_activeTextBox);

            _activeTextBox = textBox;
            textBox.IsReadOnly = false;
            textBox.BorderThickness = new Thickness(1);
            textBox.BorderBrush = new SolidColorBrush(_currentColor);
            textBox.Focusable = true;

            FloatingTextToolbar.Visibility = Visibility.Visible;
            TextMoveHandle.Visibility = Visibility.Visible;

            if (focus && !textBox.IsKeyboardFocusWithin)
                textBox.Focus();

            UpdateTextColorPreview();
            UpdateTextToolbarFromSelection();
            UpdateTextAdorners();
        }

        private static void DeactivateTextBox(RichTextBox textBox)
        {
            textBox.Selection.Select(textBox.Document.ContentEnd, textBox.Document.ContentEnd);
            textBox.BorderThickness = new Thickness(0);
            textBox.IsReadOnly = true;
        }

        // Finalizes any active text box (removes editing chrome and hides adorners).
        private void FinalizeTextBox()
        {
            if (_activeTextBox != null)
            {
                if (IsRichTextBoxEmpty(_activeTextBox))
                {
                    AnnotationCanvas.Children.Remove(_activeTextBox);
                }
                else
                {
                    DeactivateTextBox(_activeTextBox);
                }

                _activeTextBox = null;
            }

            HideTextAdorners();
        }

        private static bool IsRichTextBoxEmpty(RichTextBox textBox)
        {
            string text = new TextRange(textBox.Document.ContentStart, textBox.Document.ContentEnd).Text;
            return string.IsNullOrWhiteSpace(text);
        }

        private void HideTextAdorners()
        {
            FloatingTextToolbar.Visibility = Visibility.Collapsed;
            TextMoveHandle.Visibility = Visibility.Collapsed;
            _isMovingText = false;
            _movingTextBox = null;
        }

        private void UpdateTextColorPreview()
        {
            if (TextColorPreview != null)
                TextColorPreview.Fill = new SolidColorBrush(_currentColor);
        }

        private void UpdateTextToolbarFromSelection()
        {
            if (_activeTextBox == null || _isUpdatingTextToolbar) return;

            try
            {
                _isUpdatingTextToolbar = true;
                var range = GetTextFormattingRange();

                var familyValue = range.GetPropertyValue(TextElement.FontFamilyProperty);
                if (familyValue is System.Windows.Media.FontFamily family)
                    SelectFontFamily(family.Source);

                var sizeValue = range.GetPropertyValue(TextElement.FontSizeProperty);
                if (sizeValue is double fontSize && ThicknessSlider != null)
                {
                    double thickness = Math.Max(ThicknessSlider.Minimum, Math.Min(ThicknessSlider.Maximum, fontSize / 2));
                    if (Math.Abs(ThicknessSlider.Value - thickness) > 0.01)
                        ThicknessSlider.Value = thickness;
                }
            }
            finally
            {
                _isUpdatingTextToolbar = false;
            }
        }

        private void SelectFontFamily(string familyName)
        {
            foreach (var item in CmbTextFontFamily.Items)
            {
                if (item is ComboBoxItem comboItem
                    && comboItem.Content is string itemFamily
                    && string.Equals(itemFamily, familyName, StringComparison.OrdinalIgnoreCase))
                {
                    CmbTextFontFamily.SelectedItem = comboItem;
                    return;
                }
            }
        }

        private TextRange GetTextFormattingRange()
        {
            if (_activeTextBox == null)
                throw new InvalidOperationException("No active text annotation is selected.");

            if (!_activeTextBox.Selection.IsEmpty)
                return _activeTextBox.Selection;

            return new TextRange(_activeTextBox.Document.ContentStart, _activeTextBox.Document.ContentEnd);
        }

        private void ApplyTextSelectionValue(DependencyProperty property, object value)
        {
            if (_activeTextBox == null || _isUpdatingTextToolbar) return;

            var range = GetTextFormattingRange();
            range.ApplyPropertyValue(property, value);
            _activeTextBox.Focus();
            UpdateTextToolbarFromSelection();
            UpdateTextAdorners();
        }

        private void TextColorButton_Click(object sender, RoutedEventArgs e)
        {
            MoreColors_Click(sender, e);
        }

        private void TextFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingTextToolbar || _activeTextBox == null) return;

            ApplyTextSelectionValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily(GetSelectedFontFamilyName()));
        }

        private void TextFormatButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTextBox == null || sender is not Button button || button.Tag is not string action) return;

            switch (action)
            {
                case "Bold":
                    ToggleTextProperty(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal);
                    break;
                case "Italic":
                    ToggleTextProperty(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal);
                    break;
                case "Underline":
                    ToggleTextDecoration(TextDecorationLocation.Underline);
                    break;
                case "Strike":
                    ToggleTextDecoration(TextDecorationLocation.Strikethrough);
                    break;
            }

            _activeTextBox.Focus();
            UpdateTextAdorners();
        }

        private void ToggleTextProperty(DependencyProperty property, object enabledValue, object disabledValue)
        {
            var range = GetTextFormattingRange();
            object current = range.GetPropertyValue(property);
            object next = Equals(current, enabledValue) ? disabledValue : enabledValue;
            range.ApplyPropertyValue(property, next);
        }

        private void ToggleTextDecoration(TextDecorationLocation location)
        {
            var range = GetTextFormattingRange();
            object currentValue = range.GetPropertyValue(Inline.TextDecorationsProperty);
            var current = currentValue as TextDecorationCollection;
            bool hasDecoration = current?.Any(decoration => decoration.Location == location) == true;

            var next = new TextDecorationCollection();
            if (current != null)
            {
                foreach (var decoration in current.Where(decoration => decoration.Location != location))
                    next.Add(decoration.Clone());
            }

            if (!hasDecoration)
                next.Add(new TextDecoration { Location = location });

            range.ApplyPropertyValue(Inline.TextDecorationsProperty, next);
        }

        private void UpdateTextAdorners()
        {
            if (_activeTextBox == null)
            {
                HideTextAdorners();
                return;
            }

            _activeTextBox.UpdateLayout();
            FloatingTextToolbar.UpdateLayout();

            double textLeft = SafeCanvasValue(Canvas.GetLeft(_activeTextBox));
            double textTop = SafeCanvasValue(Canvas.GetTop(_activeTextBox));
            double textWidth = Math.Max(_activeTextBox.ActualWidth, _activeTextBox.Width);
            double textHeight = Math.Max(_activeTextBox.ActualHeight, _activeTextBox.MinHeight);
            double toolbarWidth = Math.Max(FloatingTextToolbar.ActualWidth, 260);
            double toolbarHeight = Math.Max(FloatingTextToolbar.ActualHeight, 44);

            double toolbarLeft = Clamp(textLeft, 0, Math.Max(0, AnnotationCanvas.Width - toolbarWidth));
            double toolbarTop = textTop - toolbarHeight - 8;
            if (toolbarTop < 0)
                toolbarTop = Math.Min(AnnotationCanvas.Height - toolbarHeight, textTop + textHeight + 8);

            Canvas.SetLeft(FloatingTextToolbar, toolbarLeft);
            Canvas.SetTop(FloatingTextToolbar, Math.Max(0, toolbarTop));
            Canvas.SetLeft(TextMoveHandle, Clamp(textLeft - 12, 0, Math.Max(0, AnnotationCanvas.Width - TextMoveHandle.Width)));
            Canvas.SetTop(TextMoveHandle, Clamp(textTop - 12, 0, Math.Max(0, AnnotationCanvas.Height - TextMoveHandle.Height)));
        }

        private void TextMoveHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeTextBox == null) return;

            _isMovingText = true;
            _movingTextBox = _activeTextBox;
            _textMoveStart = e.GetPosition(AnnotationCanvas);
            _textMoveOrigin = new System.Windows.Point(
                SafeCanvasValue(Canvas.GetLeft(_movingTextBox)),
                SafeCanvasValue(Canvas.GetTop(_movingTextBox)));
            TextMoveHandle.CaptureMouse();
            e.Handled = true;
        }

        private void TextMoveHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMovingText || _movingTextBox == null || e.LeftButton != MouseButtonState.Pressed) return;

            var current = e.GetPosition(AnnotationCanvas);
            double nextLeft = _textMoveOrigin.X + current.X - _textMoveStart.X;
            double nextTop = _textMoveOrigin.Y + current.Y - _textMoveStart.Y;
            double maxLeft = Math.Max(0, AnnotationCanvas.Width - Math.Max(_movingTextBox.ActualWidth, _movingTextBox.Width));
            double maxTop = Math.Max(0, AnnotationCanvas.Height - Math.Max(_movingTextBox.ActualHeight, _movingTextBox.MinHeight));

            Canvas.SetLeft(_movingTextBox, Clamp(nextLeft, 0, maxLeft));
            Canvas.SetTop(_movingTextBox, Clamp(nextTop, 0, maxTop));
            UpdateTextAdorners();
            e.Handled = true;
        }

        private void TextMoveHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isMovingText) return;

            _isMovingText = false;
            _movingTextBox = null;
            TextMoveHandle.ReleaseMouseCapture();
            UpdateTextAdorners();
            e.Handled = true;
        }

        private bool IsTextInteractionSource(object? source)
        {
            if (source is not DependencyObject current) return false;
            DependencyObject? walker = current;

            while (walker != null)
            {
                if (walker is RichTextBox)
                    return true;

                walker = GetParent(walker);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject current)
        {
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(current);

            return LogicalTreeHelper.GetParent(current);
        }

        private static double SafeCanvasValue(double value) => double.IsNaN(value) ? 0 : value;

        private static double Clamp(double value, double min, double max)
        {
            if (max < min) max = min;
            return Math.Max(min, Math.Min(max, value));
        }

        // ────────────────────────────────────────────
        // RENDER FINAL IMAGE (screenshot + annotations)
        // ────────────────────────────────────────────

        private BitmapSource RenderFinalImage()
        {
            // Temporarily remove zoom transform so the Canvas renders at 1:1
            // into the image-sized render target. Annotations are stored at image
            // coordinates, so this produces correct output regardless of zoom level.
            double prevScaleX = ZoomTransform.ScaleX;
            double prevScaleY = ZoomTransform.ScaleY;
            ZoomTransform.ScaleX = 1;
            ZoomTransform.ScaleY = 1;
            ContentGrid.UpdateLayout();

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

            // Then render the annotation canvas on top
            renderTarget.Render(AnnotationCanvas);

            // Restore zoom transform
            ZoomTransform.ScaleX = prevScaleX;
            ZoomTransform.ScaleY = prevScaleY;

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
            string ext = _imageFormat.ToLower();
            string actualPath = System.IO.Path.Combine(
                _fileService.GetSaveFolder(),
                $"parallax_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{ext}");
            BitmapHelper.SaveBitmapSource(rendered, actualPath, ext);
            ShowStatus($"Saved: {System.IO.Path.GetFileName(actualPath)}");
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            FinalizeTextBox();
            var dialog = new SaveFileDialog
            {
                Title = "Save screenshot",
                Filter = "PNG image|*.png|JPEG image|*.jpg|Bitmap|*.bmp",
                DefaultExt = "png",
                FileName = $"parallax_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var rendered = RenderFinalImage();
                string ext = System.IO.Path.GetExtension(dialog.FileName).TrimStart('.').ToLower();
                BitmapHelper.SaveBitmapSource(rendered, dialog.FileName, ext);
                ShowStatus($"Saved: {System.IO.Path.GetFileName(dialog.FileName)}");
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
