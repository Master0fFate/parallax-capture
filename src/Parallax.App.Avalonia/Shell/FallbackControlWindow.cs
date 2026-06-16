using Avalonia.Controls;
using Avalonia.Layout;
using Parallax.Core.Shell;

namespace Parallax.App.Avalonia.Shell;

public sealed class FallbackControlWindow : Window
{
    public FallbackControlWindow(TraySurfaceModel surface, Action<ShellActionId>? executeAction = null)
    {
        Title = "Parallax Capture";
        Width = 360;
        Height = 520;
        MinWidth = 320;
        MinHeight = 420;
        ShowInTaskbar = true;

        var panel = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(16),
            Spacing = 8
        };

        panel.Children.Add(new TextBlock
        {
            Text = surface.FallbackMessage ?? surface.ActivationHint,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        });

        foreach (var item in surface.MenuItems.Where(item => item.IsVisible))
        {
            var button = new Button
            {
                Content = item.Label,
                IsEnabled = item.IsEnabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 36
            };
            button.Click += (_, _) => executeAction?.Invoke(item.Action);
            panel.Children.Add(button);
        }

        Content = new ScrollViewer { Content = panel };
    }
}
