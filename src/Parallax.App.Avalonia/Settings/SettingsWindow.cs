using Avalonia.Controls;
using Avalonia.Layout;
using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Settings;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(ParallaxSettings settings, IPlatformBackend platform)
    {
        Title = "Parallax Capture Settings";
        Width = 560;
        Height = 620;
        MinWidth = 480;
        MinHeight = 520;

        var hotkeys = HotkeyPlanner.Plan(settings, platform.Capabilities.GlobalHotkeys);
        var saveFolder = SaveFolderPolicy.ValidateAndCreate(settings, platform.Locations, createDirectories: false);
        var theme = ThemeCatalog.Resolve(settings.ThemeFamily, settings.ThemeMode);

        var root = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(18),
            Spacing = 12
        };

        root.Children.Add(Header("General"));
        root.Children.Add(Line($"Save folder: {saveFolder.RootFolder}"));
        root.Children.Add(Line(saveFolder.Message));
        root.Children.Add(Line($"Copy screenshots to clipboard: {Format(settings.CopyToClipboardAfterCapture)}"));
        root.Children.Add(Line($"Save automatically: {Format(settings.SaveAutomatically)}"));
        root.Children.Add(Line($"Use separate image, video, and GIF folders: {Format(settings.SeparateFolders)}"));
        root.Children.Add(Line($"Start with system: {Format(settings.StartWithSystem)}"));

        root.Children.Add(Header("Theme"));
        root.Children.Add(Line($"{theme.DisplayName} previews immediately and is saved as {theme.Family} / {theme.Mode}."));

        root.Children.Add(Header("Hotkeys"));
        foreach (var hotkey in hotkeys)
        {
            root.Children.Add(Line($"{hotkey.Name}: {hotkey.DisplayText} ({hotkey.State})"));
            if (hotkey.State != PlannedHotkeyState.Registered)
            {
                root.Children.Add(Line(hotkey.Message));
            }
        }

        root.Children.Add(Header("Platform"));
        root.Children.Add(Line(platform.Capabilities.GlobalHotkeys.Message));
        root.Children.Add(Line(platform.Capabilities.StartupRegistration.Message));

        Content = new ScrollViewer { Content = root };
    }

    private static TextBlock Header(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 18,
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Margin = new global::Avalonia.Thickness(0, 8, 0, 0)
        };
    }

    private static TextBlock Line(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        };
    }

    private static string Format(bool value) => value ? "on" : "off";
}
