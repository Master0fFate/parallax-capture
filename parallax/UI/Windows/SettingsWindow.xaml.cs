using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using parallax.Core.Models;
using parallax.Core.Services;

namespace parallax.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;
        private bool _updatingThemeControls;

        private sealed record HotkeyInput(string Name, bool Enabled, string Gesture);
        private sealed record ThemePreset(string Family, string Mode, string DisplayName);

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _settings = settingsService.Load();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtSaveFolder.Text = _settings.SaveFolder;
            ChkCopyClipboard.IsChecked = _settings.CopyToClipboardAfterCapture;
            ChkAutoSave.IsChecked = _settings.SaveAutomatically;
            ChkOpenAnnotationEditor.IsChecked = _settings.OpenAnnotationEditorAfterScreenshot;
            ChkOpenVideoEditor.IsChecked = _settings.OpenVideoEditorAfterRecording;
            ChkSeparateFolders.IsChecked = _settings.SeparateFolders;
            ChkStartWindows.IsChecked = _settings.StartWithWindows;
            ChkHotkeyScreenshotEnabled.IsChecked = _settings.HotkeyScreenshotEnabled;
            ChkHotkeyFullscreenEnabled.IsChecked = _settings.HotkeyFullscreenEnabled;
            ChkHotkeyRegionVideoEnabled.IsChecked = _settings.HotkeyRegionVideoEnabled;
            TxtHotkeyScreenshot.Text = _settings.HotkeyScreenshot;
            TxtHotkeyFullscreen.Text = _settings.HotkeyFullscreen;
            TxtHotkeyRegionVideo.Text = _settings.HotkeyRegionVideo;

            // Select the matching format in the combobox
            foreach (System.Windows.Controls.ComboBoxItem item in CmbFormat.Items)
            {
                if (item.Tag?.ToString() == _settings.ImageFormat)
                {
                    CmbFormat.SelectedItem = item;
                    break;
                }
            }

            SelectThemePreset(_settings.ThemeFamily, _settings.ThemeMode);

            UpdateHotkeyInputState();
            UpdateHotkeyStatus();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            Close();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = TxtSaveFolder.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TxtSaveFolder.Text = dialog.SelectedPath;
        }

        private void ThemeMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _updatingThemeControls)
                return;

            string mode = ChkUseDarkMode.IsChecked == true ? AppThemeService.ModeDark : AppThemeService.ModeLight;
            string family = TryReadSelectedThemePreset(out var preset) ? preset.Family : _settings.ThemeFamily;
            SelectThemePreset(family, mode, preview: true);
        }

        private void ThemePreset_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _updatingThemeControls || !TryReadSelectedThemePreset(out var preset))
                return;

            _updatingThemeControls = true;
            ChkUseDarkMode.IsChecked = preset.Mode == AppThemeService.ModeDark;
            _updatingThemeControls = false;
            UpdateThemeSummary(preset);
            PreviewTheme(preset);
        }

        private void SelectThemePreset(string? family, string? mode, bool preview = false)
        {
            string themeFamily = AppThemeService.NormalizeThemeFamily(family);
            string themeMode = AppThemeService.NormalizeThemeMode(mode);
            string tag = BuildThemeTag(themeFamily, themeMode);
            System.Windows.Controls.ComboBoxItem? match = null;

            foreach (System.Windows.Controls.ComboBoxItem item in CmbThemePreset.Items)
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    match = item;
                    break;
                }
            }

            match ??= CmbThemePreset.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault();
            if (match == null)
                return;

            _updatingThemeControls = true;
            CmbThemePreset.SelectedItem = match;
            if (TryReadThemePreset(match, out var preset))
                ChkUseDarkMode.IsChecked = preset.Mode == AppThemeService.ModeDark;
            _updatingThemeControls = false;

            if (TryReadSelectedThemePreset(out var selectedPreset))
            {
                UpdateThemeSummary(selectedPreset);
                if (preview)
                    PreviewTheme(selectedPreset);
            }
        }

        private bool TryReadSelectedThemePreset(out ThemePreset preset)
        {
            preset = new ThemePreset(AppThemeService.FamilyMaterial, AppThemeService.ModeDark, "Material 3 Dark");
            return CmbThemePreset.SelectedItem is System.Windows.Controls.ComboBoxItem item
                && TryReadThemePreset(item, out preset);
        }

        private static bool TryReadThemePreset(System.Windows.Controls.ComboBoxItem item, out ThemePreset preset)
        {
            preset = new ThemePreset(AppThemeService.FamilyMaterial, AppThemeService.ModeDark, "Material 3 Dark");
            string? tag = item.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            string[] parts = tag.Split(new[] { '|' }, 2);
            if (parts.Length != 2)
                return false;

            string family = AppThemeService.NormalizeThemeFamily(parts[0]);
            string mode = AppThemeService.NormalizeThemeMode(parts[1]);
            string displayName = item.Content?.ToString() ?? AppThemeService.GetPalette(family, mode).DisplayName;
            preset = new ThemePreset(family, mode, displayName);
            return true;
        }

        private static string BuildThemeTag(string family, string mode)
        {
            return $"{AppThemeService.NormalizeThemeFamily(family)}|{AppThemeService.NormalizeThemeMode(mode)}";
        }

        private void UpdateThemeSummary(ThemePreset preset)
        {
            TxtThemeSummary.Text = $"{preset.DisplayName} selected. Save settings to keep the {preset.Mode.ToLowerInvariant()} palette.";
        }

        private static void PreviewTheme(ThemePreset preset)
        {
            AppThemeService.Apply(preset.Family, preset.Mode);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult != true)
                AppThemeService.Apply(_settings);

            base.OnClosed(e);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!TryValidateHotkeys(BuildHotkeyInputs(), out string hotkeyMessage))
            {
                System.Windows.MessageBox.Show(
                    hotkeyMessage,
                    "Parallax Capture - Shortcut issue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool previousStartWithWindows = _settings.StartWithWindows;
            bool requestedStartWithWindows = ChkStartWindows.IsChecked == true;
            string? startupWarning = null;
            if (requestedStartWithWindows != previousStartWithWindows
                && !TryApplyStartupSetting(requestedStartWithWindows, out string startupError))
            {
                requestedStartWithWindows = previousStartWithWindows;
                ChkStartWindows.IsChecked = previousStartWithWindows;
                startupWarning = $"Settings saved, but start with Windows was not changed: {startupError}";
            }

            _settings.SaveFolder = TxtSaveFolder.Text;
            _settings.CopyToClipboardAfterCapture = ChkCopyClipboard.IsChecked == true;
            _settings.SaveAutomatically = ChkAutoSave.IsChecked == true;
            _settings.OpenAnnotationEditorAfterScreenshot = ChkOpenAnnotationEditor.IsChecked == true;
            _settings.OpenVideoEditorAfterRecording = ChkOpenVideoEditor.IsChecked == true;
            _settings.SeparateFolders = ChkSeparateFolders.IsChecked == true;
            _settings.StartWithWindows = requestedStartWithWindows;
            _settings.HotkeyScreenshotEnabled = IsActiveHotkey(ChkHotkeyScreenshotEnabled.IsChecked == true, TxtHotkeyScreenshot.Text);
            _settings.HotkeyFullscreenEnabled = IsActiveHotkey(ChkHotkeyFullscreenEnabled.IsChecked == true, TxtHotkeyFullscreen.Text);
            _settings.HotkeyRegionVideoEnabled = IsActiveHotkey(ChkHotkeyRegionVideoEnabled.IsChecked == true, TxtHotkeyRegionVideo.Text);
            _settings.HotkeyScreenshot = NormalizeHotkeyText(TxtHotkeyScreenshot.Text);
            _settings.HotkeyFullscreen = NormalizeHotkeyText(TxtHotkeyFullscreen.Text);
            _settings.HotkeyRegionVideo = NormalizeHotkeyText(TxtHotkeyRegionVideo.Text);

            if (CmbFormat.SelectedItem is System.Windows.Controls.ComboBoxItem selected)
                _settings.ImageFormat = selected.Tag?.ToString() ?? "png";

            if (TryReadSelectedThemePreset(out var selectedTheme))
            {
                _settings.ThemeFamily = selectedTheme.Family;
                _settings.ThemeMode = selectedTheme.Mode;
            }

            _settingsService.Save(_settings);
            AppThemeService.Apply(_settings);

            if (startupWarning != null)
            {
                System.Windows.MessageBox.Show(
                    startupWarning,
                    "Parallax Capture - Startup issue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                System.Windows.MessageBox.Show("Settings saved.", "Parallax Capture", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            DialogResult = true;
            Close();
        }

        private void HotkeyInput_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            UpdateHotkeyInputState();
            UpdateHotkeyStatus();
        }

        private HotkeyInput[] BuildHotkeyInputs()
        {
            return
            [
                new HotkeyInput("Capture region", ChkHotkeyScreenshotEnabled.IsChecked == true, TxtHotkeyScreenshot.Text),
                new HotkeyInput("Capture full screen", ChkHotkeyFullscreenEnabled.IsChecked == true, TxtHotkeyFullscreen.Text),
                new HotkeyInput("Start or stop recording", ChkHotkeyRegionVideoEnabled.IsChecked == true, TxtHotkeyRegionVideo.Text)
            ];
        }

        private static bool TryValidateHotkeys(IReadOnlyList<HotkeyInput> inputs, out string message)
        {
            var problems = new List<string>();
            var activeShortcuts = new List<string>();
            var used = new Dictionary<(uint Modifiers, uint VirtualKey), string>();

            foreach (var input in inputs)
            {
                if (!input.Enabled)
                    continue;

                if (!HotkeyManager.TryParse(input.Gesture, out var parsed, out string parseMessage))
                {
                    problems.Add($"{input.Name}: {parseMessage}");
                    continue;
                }

                if (parsed.Disabled)
                    continue;

                var key = (parsed.Modifiers, parsed.VirtualKey);
                if (used.TryGetValue(key, out string? existingAction))
                {
                    problems.Add($"{input.Name}: {parsed.DisplayText} is already assigned to {existingAction}.");
                    continue;
                }

                used[key] = input.Name;
                activeShortcuts.Add($"{input.Name} ({parsed.DisplayText})");
            }

            if (problems.Count > 0)
            {
                message = string.Join(Environment.NewLine, problems);
                return false;
            }

            message = activeShortcuts.Count == 0
                ? "All shortcuts are off. Tray menu actions still work."
                : "Active shortcuts: " + string.Join(", ", activeShortcuts) + ".";
            return true;
        }

        private void UpdateHotkeyInputState()
        {
            TxtHotkeyScreenshot.IsEnabled = ChkHotkeyScreenshotEnabled.IsChecked == true;
            TxtHotkeyFullscreen.IsEnabled = ChkHotkeyFullscreenEnabled.IsChecked == true;
            TxtHotkeyRegionVideo.IsEnabled = ChkHotkeyRegionVideoEnabled.IsChecked == true;
        }

        private void UpdateHotkeyStatus()
        {
            if (!IsLoaded)
                return;

            TryValidateHotkeys(BuildHotkeyInputs(), out string message);
            TxtHotkeyStatus.Text = message;
        }

        private static bool IsActiveHotkey(bool enabled, string gesture)
        {
            return enabled
                && HotkeyManager.TryParse(gesture, out var parsed, out _)
                && !parsed.Disabled;
        }

        private static string NormalizeHotkeyText(string gesture)
        {
            return HotkeyManager.TryParse(gesture, out var parsed, out _) && !parsed.Disabled
                ? parsed.DisplayText
                : gesture.Trim();
        }

        private static bool TryApplyStartupSetting(bool enable, out string errorMessage)
        {
            errorMessage = string.Empty;
            const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "parallax";

            try
            {
                if (enable)
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(exePath) || !System.IO.File.Exists(exePath))
                    {
                        errorMessage = "The app executable could not be located.";
                        return false;
                    }

                    using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regKey, writable: true);
                    if (key == null)
                    {
                        errorMessage = "The Windows startup registry key could not be opened.";
                        return false;
                    }

                    key.SetValue(appName, $"\"{exePath}\"", Microsoft.Win32.RegistryValueKind.String);
                    return true;
                }

                using var existingKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
                if (existingKey == null)
                {
                    return true;
                }

                existingKey.DeleteValue(appName, throwOnMissingValue: false);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = NormalizeMessage(ex.Message);
                return false;
            }
        }

        private static string NormalizeMessage(string message)
        {
            string normalized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return string.IsNullOrWhiteSpace(normalized)
                ? "Windows did not provide details."
                : normalized;
        }
    }
}
