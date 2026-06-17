using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Parallax.Core.Capture;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Annotation;

public sealed class AnnotationEditorWindow : Window
{
    private readonly AnnotationEditorWindowModel _model;
    private readonly ParallaxSettings _settings;
    private readonly TextBlock _status;

    public AnnotationEditorWindow(AnnotationEditorWindowModel model, ParallaxSettings settings)
    {
        _model = model;
        _settings = settings;
        Title = "Parallax Capture - Annotation";
        Width = Math.Min(1200, Math.Max(720, model.Document.Source.Width + 80));
        Height = Math.Min(900, Math.Max(520, model.Document.Source.Height + 160));
        MinWidth = 520;
        MinHeight = 420;

        _status = new TextBlock
        {
            Text = model.StatusMessage,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var image = new Image
        {
            Source = CreateBitmap(model.Document.Source),
            Stretch = global::Avalonia.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var copy = new Button { Content = "Copy", MinWidth = 88, MinHeight = 34 };
        copy.Click += (_, _) => _status.Text = _model.Copy().Message;
        var save = new Button { Content = "Save", MinWidth = 88, MinHeight = 34 };
        save.Click += (_, _) => _status.Text = _model.Save(_settings).Message;
        var close = new Button { Content = "Close", MinWidth = 88, MinHeight = 34 };
        close.Click += (_, _) => Close();
        buttons.Children.Add(copy);
        buttons.Children.Add(save);
        buttons.Children.Add(close);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };
        footer.Children.Add(_status);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        Content = new Grid
        {
            Margin = new global::Avalonia.Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new Border
                {
                    Background = global::Avalonia.Media.Brushes.Black,
                    Child = image
                },
                footer
            }
        };
        Grid.SetRow(footer, 1);
    }

    private static Bitmap CreateBitmap(CaptureImage image)
    {
        byte[] png = SimpleImageEncoder.Encode(image, ImageFileFormat.Png);
        return new Bitmap(new MemoryStream(png));
    }
}
