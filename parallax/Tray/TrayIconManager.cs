using Microsoft.Win32;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using parallax.Core.Models;
using parallax.Core.Services;
using parallax.UI.Windows;

namespace parallax.Tray
{
    public class TrayIconManager : IDisposable
    {
        private TaskbarIcon? _trayIcon;

        private readonly ScreenshotService _screenshotService;
        private readonly RecorderService _recorderService;
        private readonly ClipboardService _clipboardService;
        private readonly FileService _fileService;
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        public event Action<AppSettings>? SettingsChanged;

        // ── Dynamic menu item references for toggling recording state
        private MenuItem? _recordMenuItem;
        private MenuItem? _stopMenuItem;

        // ── Recording overlay windows
        private RecordingBorderWindow? _recordingBorder;
        private RecordingControlsWindow? _recordingControls;

        // ── Last recording path for video editor
        private string? _lastRecordingPath;
        private MenuItem? _videoEditorMenuItem;
        private VideoEditorWindow? _openVideoEditor;

        // ── Pending action timer (KAM #4a): single field so we can cancel on rapid re-trigger
        private System.Windows.Threading.DispatcherTimer? _pendingActionTimer;

        public TrayIconManager(
            ScreenshotService screenshotService,
            RecorderService recorderService,
            ClipboardService clipboardService,
            FileService fileService,
            SettingsService settingsService,
            AppSettings settings)
        {
            _screenshotService = screenshotService;
            _recorderService   = recorderService;
            _clipboardService  = clipboardService;
            _fileService       = fileService;
            _settingsService   = settingsService;
            _settings          = settings;
        }

        public void Initialize()
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "Parallax Capture",
                Icon = LoadTrayIcon(),
                MenuActivation = PopupActivationMode.RightClick,
                ContextMenu = BuildContextMenu()
            };
            UpdateRecordingMenuState();
        }

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();

            var header = new MenuItem
            {
                Header = "Parallax Capture",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(header);
            menu.Items.Add(new Separator());

            var regionShot = new MenuItem { Header = $"Capture region   {FormatHotkeyLabel(_settings.HotkeyScreenshotEnabled, _settings.HotkeyScreenshot)}" };
            regionShot.Click += (s, e) => TriggerRegionScreenshot();
            menu.Items.Add(regionShot);

            var fullShot = new MenuItem { Header = $"Capture full screen   {FormatHotkeyLabel(_settings.HotkeyFullscreenEnabled, _settings.HotkeyFullscreen)}" };
            fullShot.Click += (s, e) => TriggerFullScreenshot();
            menu.Items.Add(fullShot);

            menu.Items.Add(new Separator());

            // Dynamic recording items — only one visible at a time
            _recordMenuItem = new MenuItem { Header = $"Record region   {FormatHotkeyLabel(_settings.HotkeyRegionVideoEnabled, _settings.HotkeyRegionVideo)}" };
            _recordMenuItem.Click += (s, e) => TriggerRegionVideo();
            menu.Items.Add(_recordMenuItem);

            _stopMenuItem = new MenuItem { Header = "Stop recording" };
            _stopMenuItem.Click += (s, e) => StopRecording();
            menu.Items.Add(_stopMenuItem);

            menu.Items.Add(new Separator());

            _videoEditorMenuItem = new MenuItem { Header = "Open video editor" };
            _videoEditorMenuItem.Click += (s, e) => OpenVideoEditor();
            menu.Items.Add(_videoEditorMenuItem);

            var openImage = new MenuItem { Header = "Open image editor" };
            openImage.Click += (s, e) => OpenImageEditor();
            menu.Items.Add(openImage);

            var openFolder = new MenuItem { Header = "Open save folder" };
            openFolder.Click += (s, e) => OpenSaveFolder();
            menu.Items.Add(openFolder);

            var settings = new MenuItem { Header = "Open settings" };
            settings.Click += (s, e) => OpenSettings();
            menu.Items.Add(settings);

            menu.Items.Add(new Separator());

            var exit = new MenuItem { Header = "Quit Parallax Capture" };
            exit.Click += (s, e) => ExitApp();
            menu.Items.Add(exit);

            return menu;
        }

        // Toggles visibility of Start Recording vs Stop Recording menu items
        // Recording state is derived from RecorderService.IsRecording — single source of truth (KAM #6)
        private void UpdateRecordingMenuState()
        {
            bool isRecording = _recorderService.IsRecording;
            if (_recordMenuItem != null)
                _recordMenuItem.Visibility = isRecording ? Visibility.Collapsed : Visibility.Visible;
            if (_stopMenuItem != null)
                _stopMenuItem.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;

            // Update tooltip to show recording state
            if (_trayIcon != null)
                _trayIcon.ToolTipText = isRecording ? "Parallax Capture is recording" : "Parallax Capture";
        }

        // ────────────────────────────────────────────
        // CAPTURE ACTIONS
        // ────────────────────────────────────────────

        public void TriggerRegionScreenshot()
        {
            // Cancel any pending trigger (KAM #4a — prevents duplicate windows on rapid key presses)
            _pendingActionTimer?.Stop();
            _pendingActionTimer = null;

            // Non-blocking delay via DispatcherTimer (avoids Thread.Sleep and async void crash)
            _pendingActionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _pendingActionTimer.Tick += (s, e) =>
            {
                _pendingActionTimer?.Stop();
                _pendingActionTimer = null;
                try
                {
                    var overlay = new OverlayWindow();
                    overlay.ShowDialog();

                    if (!overlay.SelectionConfirmed) return;

                    var region = overlay.SelectedRegion;
                    var bitmap = _screenshotService.CaptureRegion(region.X, region.Y, region.Width, region.Height);

                    if (_settings.CopyToClipboardAfterCapture)
                        _clipboardService.CopyBitmapToClipboard(bitmap);

                    bool openEditor = _settings.OpenAnnotationEditorAfterScreenshot;
                    if (_settings.SaveAutomatically || !openEditor)
                    {
                        string path = _fileService.SaveScreenshot(bitmap);
                        ShowBalloon("Screenshot saved", path);
                    }

                    if (openEditor)
                    {
                        var annotWindow = new AnnotationWindow(bitmap, _clipboardService, _fileService, _settings.ImageFormat);
                        annotWindow.Show();
                        annotWindow.Activate();
                    }
                    else
                    {
                        bitmap.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    ShowBalloon("Screenshot failed", ex.Message);
                }
            };
            _pendingActionTimer.Start();
        }

        public void TriggerFullScreenshot()
        {
            // Cancel any pending trigger (KAM #4a)
            _pendingActionTimer?.Stop();
            _pendingActionTimer = null;

            _pendingActionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _pendingActionTimer.Tick += (s, e) =>
            {
                _pendingActionTimer?.Stop();
                _pendingActionTimer = null;
                try
                {
                    var bitmap = _screenshotService.CaptureFullScreen();

                    if (_settings.CopyToClipboardAfterCapture)
                        _clipboardService.CopyBitmapToClipboard(bitmap);

                    bool openEditor = _settings.OpenAnnotationEditorAfterScreenshot;
                    if (_settings.SaveAutomatically || !openEditor)
                    {
                        string path = _fileService.SaveScreenshot(bitmap);
                        ShowBalloon("Screenshot saved", path);
                    }

                    if (openEditor)
                    {
                        // Show annotation window directly (no ShowDialog before this, so no flush needed)
                        var annotWindow = new AnnotationWindow(bitmap, _clipboardService, _fileService, _settings.ImageFormat);
                        annotWindow.Show();
                        annotWindow.Activate();
                    }
                    else
                    {
                        bitmap.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    ShowBalloon("Screenshot failed", ex.Message);
                }
            };
            _pendingActionTimer.Start();
        }

        public void TriggerRegionVideo()
        {
            if (_recorderService.IsRecording)
            {
                ShowBalloon("Already recording", "Stop the current recording first.");
                return;
            }

            if (IsVideoEditorOpen())
            {
                _openVideoEditor?.Activate();
                ShowBalloon("Video editor is open", "Save or close the current edit before starting another recording.");
                return;
            }

            // Cancel any pending trigger (KAM #4a)
            _pendingActionTimer?.Stop();
            _pendingActionTimer = null;

            _pendingActionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _pendingActionTimer.Tick += (s, e) =>
            {
                _pendingActionTimer?.Stop();
                _pendingActionTimer = null;
                try
                {
                    var overlay = new OverlayWindow();
                    overlay.ShowDialog();

                    if (!overlay.SelectionConfirmed) return;

                    var region = overlay.SelectedRegion;
                    string outputPath = _fileService.GetTempVideoPath("mp4");

                    _recorderService.RecordingCompleted += OnRecordingCompleted;
                    _recorderService.RecordingFailed    += OnRecordingFailed;

                    // Show the recording border BEFORE starting the recorder —
                    // gives the user immediate visual feedback even if the
                    // native recording engine fails asynchronously.
                    ShowRecordingBorder(region.X, region.Y, region.Width, region.Height);
                    UpdateRecordingMenuState();

                    _recorderService.StartRegionRecording(region.X, region.Y, region.Width, region.Height, outputPath);

                    // Update menu state again now that IsRecording is true (KAM #6 — single source)
                    UpdateRecordingMenuState();

                    ShowBalloon("Recording started", BuildRecordingStopHint());
                }
                catch (Exception ex)
                {
                    _recorderService.RecordingCompleted -= OnRecordingCompleted;
                    _recorderService.RecordingFailed    -= OnRecordingFailed;

                    // Balloon tips are silently suppressed by Windows Focus Assist /
                    // notification settings. Use MessageBox so the user always sees errors.
                    HideRecordingBorder(); // KAM #1 — always clean up the border on failure
                    UpdateRecordingMenuState();
                    System.Windows.MessageBox.Show(
                        $"Recording failed: {ex.Message}",
                        "Parallax Capture - Recording issue",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            };
            _pendingActionTimer.Start();
        }

        public void StopRecording()
        {
            if (!_recorderService.IsRecording) return;
            _recorderService.StopRecording();
        }

        private void OnRecordingCompleted(string filePath)
        {
            // ScreenRecorderLib fires this on a background thread — dispatch to UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _recorderService.RecordingCompleted -= OnRecordingCompleted;
                _recorderService.RecordingFailed    -= OnRecordingFailed;
                HideRecordingBorder();
                UpdateRecordingMenuState();

                HandleRecordingCompletedOnUi(filePath);
            });
        }

        private async void HandleRecordingCompletedOnUi(string filePath)
        {
            _lastRecordingPath = filePath;

            if (!_settings.OpenVideoEditorAfterRecording)
            {
                SaveCompletedRecording(filePath, "Recording saved", "Video editor auto-open is off.");
                return;
            }

            var ffmpeg = await VideoEditorWindow.EnsureFFmpegReadyWithConsentAsync();
            if (!ffmpeg.Available)
            {
                SaveCompletedRecording(filePath, "Recording saved", ffmpeg.Message);
                return;
            }

            try
            {
                OpenVideoEditorForPath(filePath, fromRecording: true);
            }
            catch (Exception ex)
            {
                SaveCompletedRecording(filePath, "Recording saved", $"Video editor could not open: {ex.Message}");
            }
        }

        private void OnRecordingFailed(string error)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _recorderService.RecordingCompleted -= OnRecordingCompleted;
                _recorderService.RecordingFailed    -= OnRecordingFailed;
                HideRecordingBorder();
                UpdateRecordingMenuState();
                ShowBalloon("Recording failed", error);
            });
        }

        // ────────────────────────────────────────────
        // RECORDING BORDER OVERLAY
        // ────────────────────────────────────────────

        private void ShowRecordingBorder(int x, int y, int width, int height)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _recordingControls?.Close();
                _recordingBorder?.Close();

                _recordingBorder = new RecordingBorderWindow(x, y, width, height);
                _recordingControls = new RecordingControlsWindow(x, y, width, height, StopRecording);

                _recordingBorder.Show();
                _recordingControls.Show();
            });
        }

        private void HideRecordingBorder()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _recordingControls?.Close();
                _recordingControls = null;
                _recordingBorder?.Close();
                _recordingBorder = null;
            });
        }

        // ────────────────────────────────────────────
        // VIDEO EDITOR
        // ────────────────────────────────────────────

        private async void OpenVideoEditor()
        {
            // If editor is already open (from auto-open after recording), bring to front
            if (IsVideoEditorOpen())
            {
                _openVideoEditor?.Activate();
                return;
            }

            var ffmpeg = await VideoEditorWindow.EnsureFFmpegReadyWithConsentAsync();
            if (!ffmpeg.Available)
            {
                ShowBalloon("Video editor unavailable", ffmpeg.Message);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open video",
                Filter = "Video files|*.mp4;*.avi;*.mov;*.wmv;*.mkv|All files|*.*",
                DefaultExt = "mp4"
            };

            // Pre-select the last recording if it exists
            if (!string.IsNullOrEmpty(_lastRecordingPath) && System.IO.File.Exists(_lastRecordingPath))
                dialog.FileName = _lastRecordingPath;

            if (dialog.ShowDialog() != true) return;

            try
            {
                OpenVideoEditorForPath(dialog.FileName, fromRecording: false);
            }
            catch (Exception ex)
            {
                ShowBalloon("Video editor error", ex.Message);
            }
        }

        private bool IsVideoEditorOpen()
        {
            return _openVideoEditor != null && _openVideoEditor.IsLoaded;
        }

        private void OpenVideoEditorForPath(string filePath, bool fromRecording)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _openVideoEditor?.Close();
                var editor = new VideoEditorWindow(filePath, _fileService, permanentPath =>
                {
                    _lastRecordingPath = permanentPath;
                });
                editor.Closed += (s, e) => _openVideoEditor = null;
                _openVideoEditor = editor;
                editor.Show();
                editor.Activate();
                if (fromRecording)
                {
                    ShowBalloon("Recording complete", "Video editor is ready.");
                }
            });
        }

        private string? SaveCompletedRecording(string sourcePath, string title, string reason)
        {
            try
            {
                string savedPath = _fileService.GetVideoFilePath("mp4");
                System.IO.File.Copy(sourcePath, savedPath, overwrite: false);
                _lastRecordingPath = savedPath;

                try { System.IO.File.Delete(sourcePath); }
                catch { /* best-effort cleanup only */ }

                ShowBalloon(title, $"{reason} Saved to {System.IO.Path.GetFileName(savedPath)}.");
                return savedPath;
            }
            catch (Exception ex)
            {
                ShowBalloon("Recording kept", $"{reason} Could not move the recording: {ex.Message}");
                return null;
            }
        }

        private void OpenImageEditor()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
                DefaultExt = "png"
            };

            if (dialog.ShowDialog() != true) return;

            System.Drawing.Bitmap? bitmap = null;
            try
            {
                bitmap = new System.Drawing.Bitmap(dialog.FileName);
                var capturedBitmap = bitmap; // prevent closure over mutable
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var annotWindow = new AnnotationWindow(capturedBitmap, _clipboardService, _fileService, _settings.ImageFormat);
                    annotWindow.Show();
                    annotWindow.Activate();
                });
                bitmap = null; // ownership transferred to AnnotationWindow
            }
            catch (Exception ex)
            {
                bitmap?.Dispose(); // KAM #3 — dispose on failure
                ShowBalloon("Image editor error", ex.Message);
            }
        }

        // ────────────────────────────────────────────
        // UTILITY
        // ────────────────────────────────────────────

        private void OpenSaveFolder()
        {
            try
            {
                _fileService.OpenSaveFolder();
            }
            catch (Exception ex)
            {
                ShowBalloon("Open folder failed", ex.Message);
            }
        }

        private void OpenSettings()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new SettingsWindow(_settingsService);
                    win.ShowDialog();
                    _settings = _settingsService.Load();
                    _fileService.UpdateSettings(_settings);

                    if (_trayIcon != null)
                        _trayIcon.ContextMenu = BuildContextMenu();

                    UpdateRecordingMenuState();
                    SettingsChanged?.Invoke(_settings);
                });
            }
            catch (Exception ex)
            {
                ShowBalloon("Settings error", ex.Message);
            }
        }

        public void ShowBalloon(string title, string message)
        {
            _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                // Load from embedded resource — works in both dev and single-file publish
                var uri = new Uri("pack://application:,,,/Assets/icon.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo?.Stream != null)
                {
                    using (streamInfo.Stream)
                        return new System.Drawing.Icon(streamInfo.Stream);
                }
            }
            catch { /* fall back to system icon below */ }

            // Ultimate fallback: system application icon
            return System.Drawing.SystemIcons.Application;
        }

        private static string FormatHotkeyLabel(bool enabled, string? gesture)
        {
            string display = HotkeyManager.FormatForDisplay(enabled, gesture);
            return display switch
            {
                "disabled" => "(disabled)",
                "invalid" => "(invalid)",
                _ => $"({display})"
            };
        }

        private string BuildRecordingStopHint()
        {
            string display = HotkeyManager.FormatForDisplay(_settings.HotkeyRegionVideoEnabled, _settings.HotkeyRegionVideo);
            return display is "disabled" or "invalid"
                ? "Use the on-screen stop button or tray menu to stop."
                : $"Use the on-screen stop button, press {display}, or use the tray menu to stop.";
        }

        private static void ExitApp()
        {
            try
            {
                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
                // Fallback: force kill the process if graceful shutdown fails
                Environment.Exit(0);
            }
        }

        public void Dispose()
        {
            HideRecordingBorder();
            _trayIcon?.Dispose();
        }
    }
}
