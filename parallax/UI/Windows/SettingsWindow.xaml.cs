using System.Windows;
using System.Windows.Forms;
using parallax.Core.Models;
using parallax.Core.Services;

namespace parallax.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

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
            ChkSeparateFolders.IsChecked = _settings.SeparateFolders;
            ChkStartWindows.IsChecked = _settings.StartWithWindows;

            // Select the matching format in the combobox
            foreach (System.Windows.Controls.ComboBoxItem item in CmbFormat.Items)
            {
                if (item.Tag?.ToString() == _settings.ImageFormat)
                {
                    CmbFormat.SelectedItem = item;
                    break;
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = TxtSaveFolder.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TxtSaveFolder.Text = dialog.SelectedPath;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.SaveFolder = TxtSaveFolder.Text;
            _settings.CopyToClipboardAfterCapture = ChkCopyClipboard.IsChecked == true;
            _settings.SaveAutomatically = ChkAutoSave.IsChecked == true;
            _settings.SeparateFolders = ChkSeparateFolders.IsChecked == true;
            _settings.StartWithWindows = ChkStartWindows.IsChecked == true;

            if (CmbFormat.SelectedItem is System.Windows.Controls.ComboBoxItem selected)
                _settings.ImageFormat = selected.Tag?.ToString() ?? "png";

            _settingsService.Save(_settings);

            // Apply startup registry key if needed
            ApplyStartupSetting(_settings.StartWithWindows);

            System.Windows.MessageBox.Show("Settings saved!", "parallax", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private static void ApplyStartupSetting(bool enable)
        {
            const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "parallax";
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
            if (key == null) return;

            if (enable)
                key.SetValue(appName, $"\"{exePath}\"");
            else
                key.DeleteValue(appName, throwOnMissingValue: false);
        }
    }
}
