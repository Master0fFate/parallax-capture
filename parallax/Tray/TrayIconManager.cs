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

        private bool _isRecording = false;

        // ── Dynamic menu item references for toggling recording state
        private MenuItem? _recordMenuItem;
        private MenuItem? _stopMenuItem;

        // ── Recording border overlay window reference
        private RecordingBorderWindow? _recordingBorder;

        // ── Last recording path for video editor
        private string? _lastRecordingPath;
        private MenuItem? _videoEditorMenuItem;
        private VideoEditorWindow? _openVideoEditor;

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
                ToolTipText = "parallax",
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
                Header = "parallax",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(header);
            menu.Items.Add(new Separator());

            var regionShot = new MenuItem { Header = "Region Screenshot   (Print Screen)" };
            regionShot.Click += (s, e) => TriggerRegionScreenshot();
            menu.Items.Add(regionShot);

            var fullShot = new MenuItem { Header = "Full Screenshot      (Alt+PrtSc)" };
            fullShot.Click += (s, e) => TriggerFullScreenshot();
            menu.Items.Add(fullShot);

            menu.Items.Add(new Separator());

            // Dynamic recording items — only one visible at a time
            _recordMenuItem = new MenuItem { Header = "Start Region Recording   (Alt+R)" };
            _recordMenuItem.Click += (s, e) => TriggerRegionVideo();
            menu.Items.Add(_recordMenuItem);

            _stopMenuItem = new MenuItem { Header = "Stop Recording" };
            _stopMenuItem.Click += (s, e) => StopRecording();
            menu.Items.Add(_stopMenuItem);

            menu.Items.Add(new Separator());

            _videoEditorMenuItem = new MenuItem { Header = "Open Video Editor..." };
            _videoEditorMenuItem.Click += (s, e) => OpenVideoEditor();
            menu.Items.Add(_videoEditorMenuItem);

            var openImage = new MenuItem { Header = "Open Image Editor..." };
            openImage.Click += (s, e) => OpenImageEditor();
            menu.Items.Add(openImage);

            var openFolder = new MenuItem { Header = "Open Save Folder" };
            openFolder.Click += (s, e) => _fileService.OpenSaveFolder();
            menu.Items.Add(openFolder);

            var settings = new MenuItem { Header = "Settings" };
            settings.Click += (s, e) => OpenSettings();
            menu.Items.Add(settings);

            menu.Items.Add(new Separator());

            var exit = new MenuItem { Header = "Exit" };
            exit.Click += (s, e) => ExitApp();
            menu.Items.Add(exit);

            return menu;
        }

        // Toggles visibility of Start Recording vs Stop Recording menu items
        private void UpdateRecordingMenuState()
        {
            if (_recordMenuItem != null)
                _recordMenuItem.Visibility = _isRecording ? Visibility.Collapsed : Visibility.Visible;
            if (_stopMenuItem != null)
                _stopMenuItem.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;

            // Update tooltip to show recording state
            if (_trayIcon != null)
                _trayIcon.ToolTipText = _isRecording ? "parallax - RECORDING" : "parallax";
        }

        // ────────────────────────────────────────────
        // CAPTURE ACTIONS
        // ────────────────────────────────────────────

        public void TriggerRegionScreenshot()
        {
            // Non-blocking delay via DispatcherTimer (avoids Thread.Sleep and async void crash)
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try
                {
                    var overlay = new OverlayWindow();
                    overlay.ShowDialog();

                    if (!overlay.SelectionConfirmed) return;

                    var region = overlay.SelectedRegion;
                    var bitmap = _screenshotService.CaptureRegion(region.X, region.Y, region.Width, region.Height);

                    if (_settings.CopyToClipboardAfterCapture)
                        _clipboardService.CopyBitmapToClipboard(bitmap);

                    if (_settings.SaveAutomatically)
                    {
                        string path = _fileService.SaveScreenshot(bitmap);
                        ShowBalloon("Screenshot saved", path);
                    }

                    var annotWindow = new AnnotationWindow(bitmap, _clipboardService, _fileService);
                    annotWindow.Show();
                    annotWindow.Activate();
                }
                catch (Exception ex)
                {
                    ShowBalloon("Screenshot failed", ex.Message);
                }
            };
            timer.Start();
        }

        public void TriggerFullScreenshot()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try
                {
                    var bitmap = _screenshotService.CaptureFullScreen();

                    if (_settings.CopyToClipboardAfterCapture)
                        _clipboardService.CopyBitmapToClipboard(bitmap);

                    if (_settings.SaveAutomatically)
                    {
                        string path = _fileService.SaveScreenshot(bitmap);
                        ShowBalloon("Screenshot saved", path);
                    }

                    // Show annotation window directly (no ShowDialog before this, so no flush needed)
                    var annotWindow = new AnnotationWindow(bitmap, _clipboardService, _fileService);
                    annotWindow.Show();
                    annotWindow.Activate();
                }
                catch (Exception ex)
                {
                    ShowBalloon("Screenshot failed", ex.Message);
                }
            };
            timer.Start();
        }

        public void TriggerRegionVideo()
        {
            if (_isRecording)
            {
                ShowBalloon("Already recording", "Stop the current recording first.");
                return;
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
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
                    _isRecording = true;
                    UpdateRecordingMenuState();

                    _recorderService.StartRegionRecording(region.X, region.Y, region.Width, region.Height, outputPath);

                    ShowBalloon("Recording started", "Press Alt+R or use tray menu to stop.");
                }
                catch (Exception ex)
                {
                    // Balloon tips are silently suppressed by Windows Focus Assist /
                    // notification settings. Use MessageBox so the user always sees errors.
                    _isRecording = false;
                    UpdateRecordingMenuState();
                    System.Windows.MessageBox.Show(
                        $"Recording failed: {ex.Message}",
                        "parallax - Recording Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            };
            timer.Start();
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _recorderService.StopRecording();
        }

        private void OnRecordingCompleted(string filePath)
        {
            // ScreenRecorderLib fires this on a background thread — dispatch to UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _isRecording = false;
                _recorderService.RecordingCompleted -= OnRecordingCompleted;
                _recorderService.RecordingFailed    -= OnRecordingFailed;
                HideRecordingBorder();
                UpdateRecordingMenuState();

                _lastRecordingPath = filePath;

                ShowBalloon("Recording complete", "Open the video editor to save or trim.");

                // Auto-open the video editor with the temp recording
                try
                {
                    // Close previous editor if still open
                    _openVideoEditor?.Close();

                    // Callback when user saves inside the editor: track the permanent path
                    var editor = new VideoEditorWindow(filePath, _fileService, permanentPath =>
                    {
                        _lastRecordingPath = permanentPath;
                    });
                    editor.Closed += (s, e) => _openVideoEditor = null;
                    _openVideoEditor = editor;
                    editor.Show();
                    editor.Activate();
                }
                catch (Exception ex)
                {
                    ShowBalloon("Video editor error", ex.Message);
                }
            });
        }

        private void OnRecordingFailed(string error)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _isRecording = false;
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
                _recordingBorder?.Close();
                _recordingBorder = new RecordingBorderWindow(x, y, width, height);
                _recordingBorder.Show();
            });
        }

        private void HideRecordingBorder()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _recordingBorder?.Close();
                _recordingBorder = null;
            });
        }

        // ────────────────────────────────────────────
        // VIDEO EDITOR
        // ────────────────────────────────────────────

        private void OpenVideoEditor()
        {
            // If editor is already open (from auto-open after recording), bring to front
            if (_openVideoEditor != null && _openVideoEditor.IsLoaded)
            {
                _openVideoEditor.Activate();
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Video",
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv|All Files|*.*",
                DefaultExt = "mp4"
            };

            // Pre-select the last recording if it exists
            if (!string.IsNullOrEmpty(_lastRecordingPath) && System.IO.File.Exists(_lastRecordingPath))
                dialog.FileName = _lastRecordingPath;

            if (dialog.ShowDialog() != true) return;

            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _openVideoEditor?.Close();
                    var editor = new VideoEditorWindow(dialog.FileName, _fileService);
                    editor.Closed += (s, e) => _openVideoEditor = null;
                    _openVideoEditor = editor;
                    editor.Show();
                    editor.Activate();
                });
            }
            catch (Exception ex)
            {
                ShowBalloon("Video editor error", ex.Message);
            }
        }

        private void OpenImageEditor()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*",
                DefaultExt = "png"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var bitmap = new System.Drawing.Bitmap(dialog.FileName);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var annotWindow = new AnnotationWindow(bitmap, _clipboardService, _fileService);
                    annotWindow.Show();
                    annotWindow.Activate();
                });
            }
            catch (Exception ex)
            {
                ShowBalloon("Image editor error", ex.Message);
            }
        }

        // ────────────────────────────────────────────
        // UTILITY
        // ────────────────────────────────────────────

        private void OpenSettings()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new SettingsWindow(_settingsService);
                    win.ShowDialog();
                    _settings = _settingsService.Load();
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
            _recordingBorder?.Close();
            _trayIcon?.Dispose();
        }
    }
}
