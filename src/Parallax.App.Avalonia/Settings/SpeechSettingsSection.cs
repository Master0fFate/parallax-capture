using System.Globalization;
using Avalonia.Controls;
using Parallax.Core.Platform;
using Parallax.Core.Speech;

namespace Parallax.App.Avalonia.Settings;

internal sealed class SpeechSettingsSection
{
    private readonly CheckBox _enabled;
    private readonly TextBox _shortcut;
    private readonly ComboBox _shortcutMode;
    private readonly ComboBox _provider;
    private readonly TextBox _apiBaseUrl;
    private readonly TextBox _apiKey;
    private readonly TextBox _model;
    private readonly TextBlock _modelStatus;
    private readonly TextBox _language;
    private readonly TextBox _microphoneDevice;
    private readonly TextBox _feedbackOutputDevice;
    private readonly CheckBox _feedbackSounds;
    private readonly CheckBox _hiddenLauncher;
    private readonly CheckBox _showTrayIcon;
    private readonly TextBox _trayOrderPosition;
    private readonly ComboBox _unloadPolicy;
    private readonly TextBox _unloadMinutes;
    private readonly ComboBox _pasteMethod;
    private readonly CheckBox _copyToClipboard;
    private readonly CheckBox _autoSubmit;
    private readonly TextBox _customWords;
    private readonly CheckBox _appendTrailingSpace;
    private readonly TextBox _maxHistoryEntries;
    private readonly TextBox _historyRetentionDays;

    private readonly IPlatformLocations _locations;

    public SpeechSettingsSection(SettingsWindowModel model, IPlatformLocations locations)
    {
        _locations = locations;
        _enabled = CheckBox("Enable speech-to-text shortcut", model.SpeechToTextEnabled);
        _shortcut = TextBox(model.SpeechShortcut);
        _shortcutMode = ComboBox(EnumNames<SpeechShortcutMode>(), model.SpeechShortcutMode.ToString());
        _provider = ComboBox(EnumNames<SpeechProviderKind>(), model.SpeechProvider.ToString());
        _apiBaseUrl = TextBox(model.SpeechApiBaseUrl);
        _apiKey = TextBox(model.SpeechApiKey);
        _model = TextBox(model.SpeechModel);
        _modelStatus = Line($"Local models: {locations.SpeechModelsDirectory}");
        _language = TextBox(model.SpeechLanguage);
        _microphoneDevice = TextBox(model.SpeechMicrophoneDeviceId);
        _feedbackOutputDevice = TextBox(model.SpeechFeedbackOutputDeviceId);
        _feedbackSounds = CheckBox("Play feedback sounds", model.SpeechFeedbackSoundsEnabled);
        _hiddenLauncher = CheckBox("Hide launcher on start", model.SpeechHiddenLauncherOnStart);
        _showTrayIcon = CheckBox("Show tray icon", model.SpeechShowTrayIcon);
        _trayOrderPosition = TextBox(model.SpeechTrayOrderPosition.ToString(CultureInfo.InvariantCulture));
        _unloadPolicy = ComboBox(EnumNames<SpeechModelUnloadPolicy>(), model.SpeechModelUnloadPolicy.ToString());
        _unloadMinutes = TextBox(model.SpeechUnloadModelAfterMinutes.ToString(CultureInfo.InvariantCulture));
        _pasteMethod = ComboBox(EnumNames<SpeechPasteMethod>(), model.SpeechPasteMethod.ToString());
        _copyToClipboard = CheckBox("Copy transcription to clipboard", model.SpeechCopyToClipboard);
        _autoSubmit = CheckBox("Auto-submit after paste", model.SpeechAutoSubmit);
        _customWords = TextBox(model.SpeechCustomWords);
        _appendTrailingSpace = CheckBox("Append trailing space", model.SpeechAppendTrailingSpace);
        _maxHistoryEntries = TextBox(model.SpeechMaxHistoryEntries.ToString(CultureInfo.InvariantCulture));
        _historyRetentionDays = TextBox(model.SpeechHistoryRetentionDays.ToString(CultureInfo.InvariantCulture));
    }

    public CheckBox EnabledControl => _enabled;

    public TextBox ShortcutControl => _shortcut;

    public void AddTo(StackPanel root)
    {
        root.Children.Add(Header("Speech-to-text"));
        root.Children.Add(Labeled("Shortcut mode", _shortcutMode));
        root.Children.Add(Labeled("Provider", _provider));
        root.Children.Add(Labeled("OpenAI-compatible base URL", _apiBaseUrl));
        root.Children.Add(Labeled("API key", _apiKey));
        root.Children.Add(Labeled("Model", _model));
        root.Children.Add(LocalModelDownloads());
        root.Children.Add(_modelStatus);
        root.Children.Add(Labeled("Language", _language));
        root.Children.Add(Labeled("Microphone device", _microphoneDevice));
        root.Children.Add(Labeled("Feedback sound output", _feedbackOutputDevice));
        root.Children.Add(_feedbackSounds);
        root.Children.Add(_hiddenLauncher);
        root.Children.Add(_showTrayIcon);
        root.Children.Add(Labeled("Tray order position", _trayOrderPosition));
        root.Children.Add(Labeled("Unload local model", _unloadPolicy));
        root.Children.Add(Labeled("Unload after idle minutes", _unloadMinutes));
        root.Children.Add(Labeled("Paste method", _pasteMethod));
        root.Children.Add(_copyToClipboard);
        root.Children.Add(_autoSubmit);
        root.Children.Add(Labeled("Custom words", _customWords));
        root.Children.Add(_appendTrailingSpace);
        root.Children.Add(Labeled("Maximum history entries", _maxHistoryEntries));
        root.Children.Add(Labeled("Auto-delete recordings after days (0 = never)", _historyRetentionDays));
    }

    public void ApplyTo(SettingsWindowModel model)
    {
        model.SpeechToTextEnabled = _enabled.IsChecked == true;
        model.SpeechShortcut = _shortcut.Text ?? string.Empty;
        model.SpeechShortcutMode = SelectedEnum(_shortcutMode, model.SpeechShortcutMode);
        model.SpeechProvider = SelectedEnum(_provider, model.SpeechProvider);
        model.SpeechApiBaseUrl = _apiBaseUrl.Text ?? string.Empty;
        model.SpeechApiKey = _apiKey.Text ?? string.Empty;
        model.SpeechModel = _model.Text ?? string.Empty;
        model.SpeechLanguage = _language.Text ?? string.Empty;
        model.SpeechMicrophoneDeviceId = _microphoneDevice.Text ?? string.Empty;
        model.SpeechFeedbackOutputDeviceId = _feedbackOutputDevice.Text ?? string.Empty;
        model.SpeechFeedbackSoundsEnabled = _feedbackSounds.IsChecked == true;
        model.SpeechHiddenLauncherOnStart = _hiddenLauncher.IsChecked == true;
        model.SpeechShowTrayIcon = _showTrayIcon.IsChecked == true;
        model.SpeechTrayOrderPosition = ParseNonNegative(_trayOrderPosition.Text, model.SpeechTrayOrderPosition);
        model.SpeechModelUnloadPolicy = SelectedEnum(_unloadPolicy, model.SpeechModelUnloadPolicy);
        model.SpeechUnloadModelAfterMinutes = ParseNonNegative(_unloadMinutes.Text, model.SpeechUnloadModelAfterMinutes);
        model.SpeechPasteMethod = SelectedEnum(_pasteMethod, model.SpeechPasteMethod);
        model.SpeechCopyToClipboard = _copyToClipboard.IsChecked == true;
        model.SpeechAutoSubmit = _autoSubmit.IsChecked == true;
        model.SpeechCustomWords = _customWords.Text ?? string.Empty;
        model.SpeechAppendTrailingSpace = _appendTrailingSpace.IsChecked == true;
        model.SpeechMaxHistoryEntries = Math.Max(1, ParseNonNegative(_maxHistoryEntries.Text, model.SpeechMaxHistoryEntries));
        model.SpeechHistoryRetentionDays = ParseNonNegative(_historyRetentionDays.Text, model.SpeechHistoryRetentionDays);
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
        return new TextBlock { Text = text, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    }

    private Control LocalModelDownloads()
    {
        var panel = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8
        };

        foreach (var manifest in SpeechModelCatalog.BuiltInModels)
        {
            var button = new Button
            {
                Content = $"Download {manifest.DisplayName}",
                MinHeight = 32
            };
            button.Click += async (_, _) => await DownloadModelAsync(manifest);
            panel.Children.Add(button);
        }

        return panel;
    }

    private async Task DownloadModelAsync(SpeechModelManifest manifest)
    {
        try
        {
            var catalog = new SpeechModelCatalog(new HttpClient { Timeout = TimeSpan.FromMinutes(20) });
            var progress = new Progress<double>(value =>
            {
                _modelStatus.Text = $"Downloading {manifest.DisplayName}: {Math.Clamp(value, 0, 1):P0}";
            });
            string path = await catalog.DownloadAsync(manifest, _locations, progress, CancellationToken.None);
            _model.Text = manifest.Id;
            _modelStatus.Text = $"Downloaded {manifest.DisplayName}: {path}";
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or IOException
                                   or InvalidOperationException
                                   or UnauthorizedAccessException
                                   or TaskCanceledException)
        {
            _modelStatus.Text = $"Download failed: {ex.Message}";
        }
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
        return new TextBox { Text = text, MinHeight = 32 };
    }

    private static CheckBox CheckBox(string label, bool isChecked)
    {
        return new CheckBox { Content = label, IsChecked = isChecked };
    }

    private static ComboBox ComboBox(IReadOnlyList<string> items, string selected)
    {
        var comboBox = new ComboBox { ItemsSource = items, MinHeight = 32 };
        comboBox.SelectedItem = items.FirstOrDefault(item => string.Equals(item, selected, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault();
        return comboBox;
    }

    private static IReadOnlyList<string> EnumNames<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>();
    }

    private static TEnum SelectedEnum<TEnum>(ComboBox comboBox, TEnum fallback) where TEnum : struct, Enum
    {
        return comboBox.SelectedItem is string value && Enum.TryParse(value, out TEnum parsed) ? parsed : fallback;
    }

    private static int ParseNonNegative(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Max(0, parsed)
            : fallback;
    }
}
