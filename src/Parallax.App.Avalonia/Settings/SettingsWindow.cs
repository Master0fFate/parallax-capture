using Avalonia.Controls;
using Avalonia.Layout;
using Parallax.Core.Hotkeys;
using Parallax.Core.Platform;
using Parallax.Core.Settings;

namespace Parallax.App.Avalonia.Settings;

public sealed class SettingsWindow : Window
{
    private readonly ParallaxSettings _settings;
    private readonly IPlatformBackend _platform;
    private readonly RuntimeSettingsApplier _runtimeSettings;
    private readonly string _executablePath;
    private readonly Action<RuntimeSettingsApplyResult>? _applied;
    private readonly SettingsWindowModel _model;
    private readonly TextBox _saveFolder;
    private readonly ComboBox _imageFormat;
    private readonly CheckBox _copyToClipboard;
    private readonly CheckBox _saveAutomatically;
    private readonly CheckBox _openAnnotationEditor;
    private readonly CheckBox _openVideoEditor;
    private readonly CheckBox _separateFolders;
    private readonly CheckBox _startWithSystem;
    private readonly CheckBox _hotkeyScreenshotEnabled;
    private readonly CheckBox _hotkeyFullscreenEnabled;
    private readonly CheckBox _hotkeyRegionVideoEnabled;
    private readonly TextBox _hotkeyScreenshot;
    private readonly TextBox _hotkeyFullscreen;
    private readonly TextBox _hotkeyRegionVideo;
    private readonly TextBlock _status;

    public SettingsWindow(
        ParallaxSettings settings,
        IPlatformBackend platform,
        RuntimeSettingsApplier runtimeSettings,
        string executablePath,
        Action<RuntimeSettingsApplyResult>? applied = null)
    {
        _settings = settings;
        _platform = platform;
        _runtimeSettings = runtimeSettings;
        _executablePath = executablePath;
        _applied = applied;
        _model = new SettingsWindowModel(settings);

        Title = "Parallax Capture Settings";
        Width = 560;
        Height = 760;
        MinWidth = 480;
        MinHeight = 520;

        var hotkeys = HotkeyPlanner.Plan(settings, platform.Capabilities.GlobalHotkeys);
        var saveFolder = SaveFolderPolicy.ValidateAndCreate(settings, platform.Locations, createDirectories: false);

        var root = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(18),
            Spacing = 12
        };

        root.Children.Add(Header("General"));
        root.Children.Add(Line($"Save folder validation: {saveFolder.Message}"));
        _saveFolder = TextBox(_model.SaveFolder);
        root.Children.Add(Labeled("Save folder", _saveFolder));
        _imageFormat = ComboBox(["png", "jpeg", "bmp"], _model.ImageFormat);
        root.Children.Add(Labeled("Image format", _imageFormat));
        _copyToClipboard = CheckBox("Copy screenshots to clipboard", _model.CopyToClipboardAfterCapture);
        root.Children.Add(_copyToClipboard);
        _saveAutomatically = CheckBox("Save screenshots automatically", _model.SaveAutomatically);
        root.Children.Add(_saveAutomatically);
        _openAnnotationEditor = CheckBox("Open annotation editor after screenshot", _model.OpenAnnotationEditorAfterScreenshot);
        root.Children.Add(_openAnnotationEditor);
        _openVideoEditor = CheckBox("Open video editor after recording", _model.OpenVideoEditorAfterRecording);
        root.Children.Add(_openVideoEditor);
        _separateFolders = CheckBox("Use separate image, video, and GIF folders", _model.SeparateFolders);
        root.Children.Add(_separateFolders);
        _startWithSystem = CheckBox("Start with system", _model.StartWithSystem);
        root.Children.Add(_startWithSystem);
        root.Children.Add(Line(saveFolder.Message));

        root.Children.Add(Header("Hotkeys"));
        _hotkeyScreenshotEnabled = CheckBox("Enable region screenshot shortcut", _model.HotkeyScreenshotEnabled);
        _hotkeyScreenshot = TextBox(_model.HotkeyScreenshot);
        root.Children.Add(_hotkeyScreenshotEnabled);
        root.Children.Add(Labeled("Region screenshot", _hotkeyScreenshot));
        _hotkeyFullscreenEnabled = CheckBox("Enable full-screen screenshot shortcut", _model.HotkeyFullscreenEnabled);
        _hotkeyFullscreen = TextBox(_model.HotkeyFullscreen);
        root.Children.Add(_hotkeyFullscreenEnabled);
        root.Children.Add(Labeled("Full-screen screenshot", _hotkeyFullscreen));
        _hotkeyRegionVideoEnabled = CheckBox("Enable region recording shortcut", _model.HotkeyRegionVideoEnabled);
        _hotkeyRegionVideo = TextBox(_model.HotkeyRegionVideo);
        root.Children.Add(_hotkeyRegionVideoEnabled);
        root.Children.Add(Labeled("Region recording", _hotkeyRegionVideo));
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

        _status = Line("Changes are applied when you choose Save.");
        root.Children.Add(_status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var save = new Button { Content = "Save", MinWidth = 96, MinHeight = 36 };
        save.Click += (_, _) => SaveChanges();
        var cancel = new Button { Content = "Cancel", MinWidth = 96, MinHeight = 36 };
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root };
    }

    public RuntimeSettingsApplyResult SaveForTesting()
    {
        SyncModelFromControls();
        return _model.Save(_settings, _runtimeSettings, _executablePath);
    }

    private void SaveChanges()
    {
        SyncModelFromControls();
        var result = _model.Save(_settings, _runtimeSettings, _executablePath);
        _status.Text = result.Saved
            ? $"Saved. {result.Startup.Message}"
            : result.SaveFolder.Message;

        if (result.Saved)
        {
            _applied?.Invoke(result);
        }
    }

    private void SyncModelFromControls()
    {
        _model.SaveFolder = _saveFolder.Text ?? string.Empty;
        _model.ImageFormat = SelectedString(_imageFormat, _model.ImageFormat);
        _model.CopyToClipboardAfterCapture = _copyToClipboard.IsChecked == true;
        _model.SaveAutomatically = _saveAutomatically.IsChecked == true;
        _model.OpenAnnotationEditorAfterScreenshot = _openAnnotationEditor.IsChecked == true;
        _model.OpenVideoEditorAfterRecording = _openVideoEditor.IsChecked == true;
        _model.SeparateFolders = _separateFolders.IsChecked == true;
        _model.StartWithSystem = _startWithSystem.IsChecked == true;
        _model.HotkeyScreenshotEnabled = _hotkeyScreenshotEnabled.IsChecked == true;
        _model.HotkeyFullscreenEnabled = _hotkeyFullscreenEnabled.IsChecked == true;
        _model.HotkeyRegionVideoEnabled = _hotkeyRegionVideoEnabled.IsChecked == true;
        _model.HotkeyScreenshot = _hotkeyScreenshot.Text ?? string.Empty;
        _model.HotkeyFullscreen = _hotkeyFullscreen.Text ?? string.Empty;
        _model.HotkeyRegionVideo = _hotkeyRegionVideo.Text ?? string.Empty;
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

    private static Control Labeled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(control);
        return panel;
    }

    private static TextBox TextBox(string text)
    {
        return new TextBox
        {
            Text = text,
            MinHeight = 32
        };
    }

    private static CheckBox CheckBox(string label, bool isChecked)
    {
        return new CheckBox
        {
            Content = label,
            IsChecked = isChecked
        };
    }

    private static ComboBox ComboBox(IReadOnlyList<string> items, string selected)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = items,
            MinHeight = 32
        };
        comboBox.SelectedItem = items.FirstOrDefault(item => string.Equals(item, selected, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault();
        return comboBox;
    }

    private static string SelectedString(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem as string ?? fallback;
    }
}
