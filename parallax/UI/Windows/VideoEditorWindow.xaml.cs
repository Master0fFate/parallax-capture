using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FFMpegCore;
using parallax.Core.Services;
using ShapePath = System.Windows.Shapes.Path;

namespace parallax.UI.Windows
{
    public partial class VideoEditorWindow : Window, IDisposable
    {
        public sealed record FFmpegEnsureResult(bool Available, bool UserDeclined, string Message);

        // ── File state
        private string _videoPath;
        private readonly FileService _fileService;
        private readonly Action<string>? _onSaved;    // callback with permanent path
        private bool _isTempFile;                      // was recorded to temp dir?
#pragma warning disable CS0414 // reserved for future use (unsaved-changes confirmation)
        private bool _hasBeenSaved;
#pragma warning restore CS0414
        private bool _isPlaying = false;
        private TimelineDragMode _timelineDragMode = TimelineDragMode.None;
        private bool _mediaEnded = false;
        private Duration _naturalDuration = Duration.Automatic;
        private bool _isMuted = false;
        private bool _isPreviewingTrim = false;
        private TimeSpan _previewTrimEnd = TimeSpan.Zero;

        // ── FFmpeg state
        private const long MaxFFmpegDownloadBytes = 250L * 1024L * 1024L;
        private const int MaxFFmpegErrorChars = 4000;
        private const int MaxStatusMessageChars = 280;
        private static readonly TimeSpan FFmpegProcessTimeout = TimeSpan.FromMinutes(30);
        private static readonly Uri FFmpegDownloadUri = new("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip");
        private static readonly string[] ExpectedFFmpegBinaries =
        {
            "ffmpeg.exe",
            "ffplay.exe",
            "ffprobe.exe"
        };
        private bool _ffmpegAvailable = false;
        private bool _ffmpegDownloading = false;
        private bool _isClosing = false;

        // ── Timer for updating timeline during playback
        private readonly DispatcherTimer _playbackTimer;

        // ── Status auto-fade timer (KAM #4c): stored as field so we cancel before starting a new one
        private DispatcherTimer? _statusTimer;

        private enum TimelineDragMode
        {
            None,
            Playhead,
            TrimStart,
            TrimEnd
        }

        private static readonly Geometry PlayIconGeometry = Geometry.Parse("M 3 2 L 14 8 L 3 14 Z");
        private static readonly Geometry PauseIconGeometry = Geometry.Parse("M 3 2 H 6 V 14 H 3 Z M 10 2 H 13 V 14 H 10 Z");
        private static readonly Geometry RestartIconGeometry = Geometry.Parse("M 4 4 V 1 L 1 4 L 4 7 V 5 C 6 3 10 3 12 6 C 14 9 12 13 8 13 C 5.5 13 3.5 11.8 2.5 10");
        private static readonly Geometry VolumeIconGeometry = Geometry.Parse("M 2 6 H 5 L 9 3 V 13 L 5 10 H 2 Z M 11 5 C 12 6 12.5 7 12.5 8 C 12.5 9 12 10 11 11");
        private static readonly Geometry MutedIconGeometry = Geometry.Parse("M 2 6 H 5 L 9 3 V 13 L 5 10 H 2 Z M 11 6 L 15 10 M 15 6 L 11 10");
        private static readonly Geometry SaveIconGeometry = Geometry.Parse("M 3 2 H 13 L 15 4 V 14 H 1 V 2 Z M 4 3 V 6 H 12 V 3 Z M 4 10 H 12 V 14 H 4 Z");
        private static readonly Geometry FrameIconGeometry = Geometry.Parse("M 2 3 H 14 V 13 H 2 Z M 4 5 H 12 V 11 H 4 Z");
        private static readonly Geometry GifIconGeometry = Geometry.Parse("M 2 4 H 14 V 12 H 2 Z M 5 6 H 11 M 5 8 H 9 M 5 10 H 12");
        private static readonly Geometry OpenIconGeometry = Geometry.Parse("M 2 5 H 7 L 8.5 7 H 14 V 13 H 2 Z M 2 4 H 7 L 8 5 H 14");

        public VideoEditorWindow(string videoPath, FileService fileService, Action<string>? onSaved = null)
        {
            InitializeComponent();

            _videoPath = videoPath;
            _fileService = fileService;
            _onSaved = onSaved;

            // Detect if this is a temp recording — files in %TEMP%\parallax\ are ephemeral
            string tempDir = Path.Combine(Path.GetTempPath(), "parallax");
            _isTempFile = videoPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase);

            TxtFileName.Text = Path.GetFileName(videoPath);

            // Set default trim to full video
            TxtTrimStart.Text = "00:00";
            TxtTrimOut.Text = "00:00";
            InitializeIconContent();

            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            VideoPlayer.MediaFailed += VideoPlayer_MediaFailed;

            Loaded += VideoEditorWindow_Loaded;
        }

        private async void VideoEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load the video
            VideoPlayer.Source = new Uri(_videoPath);
            VideoPlayer.Play();
            VideoPlayer.Pause(); // Wait for user to press play
            _isPlaying = false;
            UpdatePlayButton();

            // Check if FFmpeg is available
            await CheckFFmpegAvailability();
        }

        // ────────────────────────────────────────────
        // FFMPEG MANAGEMENT
        // ────────────────────────────────────────────

        // Persistent tools directory — survives app updates and single-file publish
        private static string ToolsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parallax", "tools");

        private static string FfmpegPath => Path.Combine(ToolsDir, "ffmpeg.exe");
        private static string FfplayPath => Path.Combine(ToolsDir, "ffplay.exe");
        private static string FfprobePath => Path.Combine(ToolsDir, "ffprobe.exe");

        public static bool IsFFmpegReady()
        {
            try
            {
                return TryGetFFmpegPath(out _);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<FFmpegEnsureResult> EnsureFFmpegReadyWithConsentAsync(Window? owner = null)
        {
            if (IsFFmpegReady())
            {
                return new FFmpegEnsureResult(true, false, "FFmpeg ready.");
            }

            MessageBoxResult consent = owner == null
                ? MessageBox.Show(
                    "The video editor needs FFmpeg before it can open. Download FFmpeg now?",
                    "Download FFmpeg?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question)
                : MessageBox.Show(
                    owner,
                    "The video editor needs FFmpeg before it can open. Download FFmpeg now?",
                    "Download FFmpeg?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (consent != MessageBoxResult.Yes)
            {
                return new FFmpegEnsureResult(false, true, "Video editor closed because FFmpeg is required.");
            }

            Window? progressWindow = null;
            TextBlock? progressText = null;
            try
            {
                progressWindow = CreateFFmpegProgressWindow(owner, out progressText);
                progressWindow.Show();

                await DownloadFFmpegToolsAsync(status =>
                {
                    if (progressText.Dispatcher.CheckAccess())
                    {
                        progressText.Text = status;
                    }
                    else
                    {
                        progressText.Dispatcher.Invoke(() => progressText.Text = status);
                    }
                });

                return IsFFmpegReady()
                    ? new FFmpegEnsureResult(true, false, "FFmpeg ready.")
                    : new FFmpegEnsureResult(false, false, "FFmpeg install did not produce a working ffmpeg.exe.");
            }
            catch (Exception ex)
            {
                return new FFmpegEnsureResult(false, false, ToStatusMessage("FFmpeg install failed", ex));
            }
            finally
            {
                progressWindow?.Close();
            }
        }

        private static Window CreateFFmpegProgressWindow(Window? owner, out TextBlock progressText)
        {
            progressText = new TextBlock
            {
                Text = "Preparing FFmpeg download...",
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(18)
            };
            panel.Children.Add(new TextBlock
            {
                Text = "Installing FFmpeg",
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(progressText);
            panel.Children.Add(new ProgressBar
            {
                IsIndeterminate = true,
                Height = 8,
                Minimum = 0,
                Maximum = 100
            });

            var window = new Window
            {
                Title = "Installing FFmpeg",
                Width = 420,
                Height = 170,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 32)),
                Content = panel,
                ShowInTaskbar = false
            };

            return window;
        }

        private Task CheckFFmpegAvailability()
        {
            try
            {
                Directory.CreateDirectory(ToolsDir);

                if (TryGetFFmpegPath(out _))
                {
                    if (File.Exists(FfmpegPath))
                    {
                        GlobalFFOptions.Configure(opt => opt.BinaryFolder = ToolsDir);
                    }

                    SetFFmpegAvailableUi();
                    return Task.CompletedTask;
                }

                SetFFmpegMissingUi();
            }
            catch
            {
                SetFFmpegMissingUi();
            }

            return Task.CompletedTask;
        }

        private async void BtnDownloadFFmpeg_Click(object sender, RoutedEventArgs e)
        {
            if (_ffmpegDownloading) return;
            _ffmpegDownloading = true;
            BtnDownloadFFmpeg.IsEnabled = false;
            BtnDownloadFFmpeg.Content = "Downloading...";

            try
            {
                bool foundFfmpeg = await DownloadFFmpegToolsAsync(status => TxtFFmpegStatus.Text = status);

                if (foundFfmpeg)
                {
                    SetFFmpegAvailableUi();
                    ShowEditorStatus("FFmpeg and ffplay installed. All codecs supported.", false);
                }
                else
                {
                    TxtFFmpegStatus.Text = "Extraction failed. Try manual install.";
                    TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Red;
                    ShowEditorStatus("FFmpeg install failed. The archive did not contain ffmpeg.exe.", true);
                }
            }
            catch (HttpRequestException ex)
            {
                ShowFFmpegDownloadFailure("FFmpeg download failed. Check your internet connection or install FFmpeg manually.", ex);
            }
            catch (TaskCanceledException ex)
            {
                ShowFFmpegDownloadFailure("FFmpeg download timed out. Check your connection and try again.", ex);
            }
            catch (InvalidOperationException ex)
            {
                ShowFFmpegDownloadFailure(ex.Message, ex);
            }
            catch (Exception ex)
            {
                ShowFFmpegDownloadFailure("FFmpeg install failed. Try Download FFmpeg again or install FFmpeg manually.", ex);
            }
            finally
            {
                _ffmpegDownloading = false;
                BtnDownloadFFmpeg.IsEnabled = true;
                BtnDownloadFFmpeg.Content = "Download FFmpeg";
            }
        }

        // ────────────────────────────────────────────
        // PLAYBACK
        // ────────────────────────────────────────────

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            _naturalDuration = VideoPlayer.NaturalDuration;
            if (_naturalDuration.HasTimeSpan)
            {
                TxtTotalTime.Text = FormatTime(_naturalDuration.TimeSpan);
                TxtTrimOut.Text = FormatTime(_naturalDuration.TimeSpan);
                UpdateTrimDuration();
                UpdateTimelineVisuals();
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            _mediaEnded = true;
            _isPreviewingTrim = false;
            _playbackTimer.Stop();
            UpdatePlayButton();
            PlayOverlay.Visibility = Visibility.Visible;
        }

        private void VideoPlayer_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            // Suppress during editor close -- VideoPlayer.Close() triggers this
            if (_isClosing) return;

            // Try falling back to ffplay if available
            if (File.Exists(FfplayPath))
            {
                var result = MessageBox.Show(
                    "This video format needs FFmpeg for playback.\n\nOpen with ffplay now?",
                    "Extended Codec Support",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = FfplayPath,
                            UseShellExecute = false
                        };
                        startInfo.ArgumentList.Add(_videoPath);
                        Process.Start(startInfo);
                    }
                    catch { /* best effort */ }
                }
            }
            else
            {
                var result = MessageBox.Show(
                    "For extended codec support and video editor capability please download FFmpeg + ffplay.\n\nWould you like to download it now?",
                    "Download FFmpeg?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    BtnDownloadFFmpeg_Click(this, new RoutedEventArgs());
                }
            }

            ShowEditorStatus(
                "Playback requires FFmpeg. Click 'Download FFmpeg' or install manually.",
                isError: true);
            PlayOverlay.Visibility = Visibility.Collapsed;
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_timelineDragMode != TimelineDragMode.None) return;
            if (VideoPlayer.Source == null) return;

            try
            {
                if (_naturalDuration.HasTimeSpan)
                {
                    TxtCurrentTime.Text = FormatTime(VideoPlayer.Position);
                    UpdateTimelineVisuals();

                    if (_isPreviewingTrim && VideoPlayer.Position >= _previewTrimEnd)
                    {
                        CompleteTrimPreview();
                    }
                }
            }
            catch { /* clock drift after media end */ }
        }

        private void InitializeIconContent()
        {
            BtnRestart.Content = CreateIconLabel(RestartIconGeometry, "Restart");
            BtnMute.Content = CreateIconLabel(VolumeIconGeometry, "Mute");
            BtnPreviewTrim.Content = CreateIconLabel(PlayIconGeometry, "Preview");
            BtnOpenVideo.Content = CreateIconLabel(OpenIconGeometry, "Open");
            BtnSaveTrimmed.Content = CreateIconLabel(SaveIconGeometry, "Save trim");
            BtnSaveFrame.Content = CreateIconLabel(FrameIconGeometry, "Frame");
            BtnExportGif.Content = CreateIconLabel(GifIconGeometry, "GIF");
            BtnSaveOriginal.Content = CreateIconLabel(SaveIconGeometry, "Save original");
            UpdatePlayButton();
        }

        private static StackPanel CreateIconLabel(Geometry iconGeometry, string label)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new ShapePath
                    {
                        Data = iconGeometry,
                        Width = 14,
                        Height = 14,
                        Stretch = Stretch.Uniform,
                        Stroke = System.Windows.Media.Brushes.White,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        StrokeThickness = 1.8,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Margin = new Thickness(0, 0, 7, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = label,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        private void UpdatePlayButton()
        {
            if (_mediaEnded)
            {
                BtnPlayPause.Content = CreateIconLabel(PlayIconGeometry, "Replay");
                PlayOverlay.Visibility = Visibility.Visible;
            }
            else if (_isPlaying)
            {
                BtnPlayPause.Content = CreateIconLabel(PauseIconGeometry, "Pause");
                PlayOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnPlayPause.Content = CreateIconLabel(PlayIconGeometry, "Play");
                PlayOverlay.Visibility = Visibility.Visible;
            }
        }

        private void TogglePlayPause()
        {
            _isPreviewingTrim = false;

            if (_mediaEnded)
            {
                VideoPlayer.Position = TimeSpan.Zero;
                _mediaEnded = false;
            }

            if (_isPlaying)
            {
                VideoPlayer.Pause();
                _isPlaying = false;
                _playbackTimer.Stop();
            }
            else
            {
                VideoPlayer.Play();
                _isPlaying = true;
                _playbackTimer.Start();
            }
            UpdatePlayButton();
        }

        private void PlayOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TogglePlayPause();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            _isPreviewingTrim = false;
            VideoPlayer.Position = TimeSpan.Zero;
            _mediaEnded = false;
            if (!_isPlaying)
            {
                VideoPlayer.Play();
                _isPlaying = true;
                _playbackTimer.Start();
            }
            UpdatePlayButton();
        }

        // ────────────────────────────────────────────
        // TIMELINE SEEKING
        // ────────────────────────────────────────────

        private void TrimTimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTimelineVisuals();

        private void TrimTimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_naturalDuration.HasTimeSpan) return;

            _isPreviewingTrim = false;
            _playbackTimer.Stop();
            System.Windows.Point point = e.GetPosition(TrimTimelineCanvas);
            double startX = TimeToTimelineX(ParseTrimTime(TxtTrimStart.Text) ?? TimeSpan.Zero);
            double endX = TimeToTimelineX(ParseTrimTime(TxtTrimOut.Text) ?? _naturalDuration.TimeSpan);

            if (Math.Abs(point.X - startX) <= 14)
                _timelineDragMode = TimelineDragMode.TrimStart;
            else if (Math.Abs(point.X - endX) <= 14)
                _timelineDragMode = TimelineDragMode.TrimEnd;
            else
                _timelineDragMode = TimelineDragMode.Playhead;

            TrimTimelineCanvas.CaptureMouse();
            UpdateTimelineFromPoint(point.X);
            e.Handled = true;
        }

        private void TrimTimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_timelineDragMode == TimelineDragMode.None || !_naturalDuration.HasTimeSpan) return;
            UpdateTimelineFromPoint(e.GetPosition(TrimTimelineCanvas).X);
            e.Handled = true;
        }

        private void TrimTimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_timelineDragMode == TimelineDragMode.None) return;

            _timelineDragMode = TimelineDragMode.None;
            TrimTimelineCanvas.ReleaseMouseCapture();
            if (_isPlaying)
                _playbackTimer.Start();
            e.Handled = true;
        }

        private void UpdateTimelineFromPoint(double x)
        {
            if (!_naturalDuration.HasTimeSpan) return;

            TimeSpan value = TimelineXToTime(x);
            TimeSpan start = ParseTrimTime(TxtTrimStart.Text) ?? TimeSpan.Zero;
            TimeSpan end = ParseTrimTime(TxtTrimOut.Text) ?? _naturalDuration.TimeSpan;
            TimeSpan minGap = TimeSpan.FromMilliseconds(100);

            switch (_timelineDragMode)
            {
                case TimelineDragMode.TrimStart:
                    value = ClampTime(value, TimeSpan.Zero, end - minGap);
                    TxtTrimStart.Text = FormatTime(value);
                    break;
                case TimelineDragMode.TrimEnd:
                    value = ClampTime(value, start + minGap, _naturalDuration.TimeSpan);
                    TxtTrimOut.Text = FormatTime(value);
                    break;
                case TimelineDragMode.Playhead:
                    SetPlaybackPosition(value);
                    break;
            }

            UpdateTrimDuration();
            UpdateTimelineVisuals();
        }

        private void SetPlaybackPosition(TimeSpan position)
        {
            TimeSpan clamped = ClampTime(position, TimeSpan.Zero, _naturalDuration.HasTimeSpan ? _naturalDuration.TimeSpan : position);
            _mediaEnded = false;
            VideoPlayer.Position = clamped;
            TxtCurrentTime.Text = FormatTime(clamped);
            UpdatePlayButton();
            UpdateTimelineVisuals();
        }

        private void UpdateTimelineVisuals()
        {
            if (TrimTimelineCanvas == null || TimelineTrack == null) return;

            double width = Math.Max(1, TrimTimelineCanvas.ActualWidth - 24);
            const double left = 12;
            Canvas.SetLeft(TimelineTrack, left);
            TimelineTrack.Width = width;

            TimeSpan duration = _naturalDuration.HasTimeSpan && _naturalDuration.TimeSpan > TimeSpan.Zero
                ? _naturalDuration.TimeSpan
                : TimeSpan.FromSeconds(1);
            TimeSpan trimStart = ClampTime(ParseTrimTime(TxtTrimStart.Text) ?? TimeSpan.Zero, TimeSpan.Zero, duration);
            TimeSpan trimEnd = ClampTime(ParseTrimTime(TxtTrimOut.Text) ?? duration, TimeSpan.Zero, duration);
            if (trimEnd < trimStart)
            {
                (trimStart, trimEnd) = (trimEnd, trimStart);
            }

            double startX = TimeToTimelineX(trimStart);
            double endX = TimeToTimelineX(trimEnd);
            double playheadX = TimeToTimelineX(ClampTime(VideoPlayer.Position, TimeSpan.Zero, duration));

            Canvas.SetLeft(TimelineSelectedRange, startX);
            TimelineSelectedRange.Width = Math.Max(2, endX - startX);
            Canvas.SetLeft(TrimInHandle, startX - (TrimInHandle.Width / 2));
            Canvas.SetLeft(TrimOutHandle, endX - (TrimOutHandle.Width / 2));
            TimelinePlayhead.X1 = playheadX;
            TimelinePlayhead.X2 = playheadX;
        }

        private double TimeToTimelineX(TimeSpan time)
        {
            double width = Math.Max(1, TrimTimelineCanvas.ActualWidth - 24);
            double durationSeconds = _naturalDuration.HasTimeSpan && _naturalDuration.TimeSpan.TotalSeconds > 0
                ? _naturalDuration.TimeSpan.TotalSeconds
                : 1;
            double ratio = Math.Clamp(time.TotalSeconds / durationSeconds, 0, 1);
            return 12 + (ratio * width);
        }

        private TimeSpan TimelineXToTime(double x)
        {
            double width = Math.Max(1, TrimTimelineCanvas.ActualWidth - 24);
            double ratio = Math.Clamp((x - 12) / width, 0, 1);
            double durationSeconds = _naturalDuration.HasTimeSpan && _naturalDuration.TimeSpan.TotalSeconds > 0
                ? _naturalDuration.TimeSpan.TotalSeconds
                : 1;
            return TimeSpan.FromSeconds(ratio * durationSeconds);
        }

        private static TimeSpan ClampTime(TimeSpan value, TimeSpan min, TimeSpan max)
        {
            if (max < min) max = min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ────────────────────────────────────────────
        // MUTE
        // ────────────────────────────────────────────

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            VideoPlayer.Volume = _isMuted ? 0.0 : 1.0;
            BtnMute.Content = _isMuted
                ? CreateIconLabel(MutedIconGeometry, "Unmute")
                : CreateIconLabel(VolumeIconGeometry, "Mute");
        }

        // ────────────────────────────────────────────
        // TRIM CONTROLS
        // ────────────────────────────────────────────

        private void BtnSetTrimIn_Click(object sender, RoutedEventArgs e)
        {
            if (_naturalDuration.HasTimeSpan)
            {
                TxtTrimStart.Text = FormatTime(VideoPlayer.Position);
                UpdateTrimDuration();
            }
        }

        private void BtnSetTrimOut_Click(object sender, RoutedEventArgs e)
        {
            if (_naturalDuration.HasTimeSpan)
            {
                TxtTrimOut.Text = FormatTime(VideoPlayer.Position);
                UpdateTrimDuration();
            }
        }

        private void BtnPreviewTrim_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetValidatedTrimRange(out var trimStart, out var trimEnd, out var errorMessage))
            {
                ShowEditorStatus(errorMessage, true);
                return;
            }

            _isPreviewingTrim = true;
            _previewTrimEnd = trimEnd;
            _mediaEnded = false;

            SetPlaybackPosition(trimStart);
            VideoPlayer.Play();
            _isPlaying = true;
            _playbackTimer.Start();
            UpdatePlayButton();
            ShowEditorStatus($"Previewing trim {FormatTime(trimStart)} to {FormatTime(trimEnd)}.", false);
        }

        private void BtnJumpTrimIn_Click(object sender, RoutedEventArgs e)
        {
            JumpToTrimPoint(TxtTrimStart.Text, "trim start");
        }

        private void BtnJumpTrimOut_Click(object sender, RoutedEventArgs e)
        {
            JumpToTrimPoint(TxtTrimOut.Text, "trim end");
        }

        // Existing -5s behavior: move trim start back 5 seconds.
        private void BtnTrimMinus5_Click(object sender, RoutedEventArgs e) => NudgeTrimStart(-5);

        private void BtnTrimStartMinus1_Click(object sender, RoutedEventArgs e) => NudgeTrimStart(-1);

        private void BtnTrimStartPlus1_Click(object sender, RoutedEventArgs e) => NudgeTrimStart(1);

        private void BtnTrimEndMinus1_Click(object sender, RoutedEventArgs e) => NudgeTrimEnd(-1);

        private void BtnTrimEndPlus1_Click(object sender, RoutedEventArgs e) => NudgeTrimEnd(1);

        // Existing +5s behavior: move trim end forward 5 seconds.
        private void BtnTrimPlus5_Click(object sender, RoutedEventArgs e) => NudgeTrimEnd(5);

        private void JumpToTrimPoint(string trimText, string label)
        {
            var position = ParseTrimTime(trimText);
            if (position == null)
            {
                ShowEditorStatus($"Invalid {label}. Use MM:SS, HH:MM:SS, or seconds.", true);
                return;
            }

            if (!IsPositionInsideVideo(position.Value))
            {
                ShowEditorStatus($"The {label} is outside this video's duration.", true);
                return;
            }

            _isPreviewingTrim = false;
            SetPlaybackPosition(position.Value);
        }

        private void NudgeTrimStart(double seconds)
        {
            if (!TryGetTrimTimes(out var start, out var end, out var errorMessage))
            {
                ShowEditorStatus(errorMessage, true);
                return;
            }

            double maxStart = Math.Max(0, end.TotalSeconds - 1);
            double newStart = Math.Clamp(start.TotalSeconds + seconds, 0, maxStart);
            TxtTrimStart.Text = FormatTime(TimeSpan.FromSeconds(newStart));
            UpdateTrimDuration();
        }

        private void NudgeTrimEnd(double seconds)
        {
            if (!TryGetTrimTimes(out var start, out var end, out var errorMessage))
            {
                ShowEditorStatus(errorMessage, true);
                return;
            }

            double minEnd = start.TotalSeconds + 1;
            double maxEnd = _naturalDuration.HasTimeSpan
                ? _naturalDuration.TimeSpan.TotalSeconds
                : Math.Max(end.TotalSeconds + Math.Abs(seconds), minEnd);
            double newEnd = Math.Clamp(end.TotalSeconds + seconds, minEnd, maxEnd);
            TxtTrimOut.Text = FormatTime(TimeSpan.FromSeconds(newEnd));
            UpdateTrimDuration();
        }

        private void CompleteTrimPreview()
        {
            _isPreviewingTrim = false;
            VideoPlayer.Pause();
            SetPlaybackPosition(_previewTrimEnd);
            _isPlaying = false;
            _playbackTimer.Stop();
            UpdatePlayButton();
            ShowEditorStatus("Trim preview complete.", false);
        }

        private bool TryGetTrimTimes(out TimeSpan trimStart, out TimeSpan trimEnd, out string errorMessage)
        {
            trimStart = TimeSpan.Zero;
            trimEnd = TimeSpan.Zero;
            errorMessage = string.Empty;

            var parsedStart = ParseTrimTime(TxtTrimStart.Text);
            var parsedEnd = ParseTrimTime(TxtTrimOut.Text);
            if (parsedStart == null || parsedEnd == null)
            {
                errorMessage = "Invalid trim times. Use MM:SS, HH:MM:SS, or seconds.";
                return false;
            }

            trimStart = parsedStart.Value;
            trimEnd = parsedEnd.Value;
            return true;
        }

        private bool TryGetValidatedTrimRange(out TimeSpan trimStart, out TimeSpan trimEnd, out string errorMessage)
        {
            if (!TryGetTrimTimes(out trimStart, out trimEnd, out errorMessage))
            {
                return false;
            }

            if (trimStart < TimeSpan.Zero || trimEnd < TimeSpan.Zero)
            {
                errorMessage = "Trim times cannot be negative.";
                return false;
            }

            if (_naturalDuration.HasTimeSpan)
            {
                if (trimStart > _naturalDuration.TimeSpan || trimEnd > _naturalDuration.TimeSpan)
                {
                    errorMessage = "Trim range is outside this video's duration.";
                    return false;
                }
            }

            if (trimEnd <= trimStart)
            {
                errorMessage = "Trim end must be after trim start.";
                return false;
            }

            return true;
        }

        private bool IsPositionInsideVideo(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
            {
                return false;
            }

            return !_naturalDuration.HasTimeSpan || position <= _naturalDuration.TimeSpan;
        }

        // Updates the trim duration display based on current From/To values
        private void TrimTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateTrimDuration();
            UpdateTimelineVisuals();
        }

        private void UpdateTrimDuration()
        {
            var start = ParseTrimTime(TxtTrimStart.Text);
            var end = ParseTrimTime(TxtTrimOut.Text);
            if (start != null && end != null && end > start)
            {
                var duration = end.Value - start.Value;
                TxtTrimDuration.Text = FormatTime(duration);
                TxtTrimDuration.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                TxtTrimDuration.Text = "00:00";
                TxtTrimDuration.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private TimeSpan? ParseTrimTime(string text)
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] minuteParts = text.Split(':');
            if (minuteParts.Length == 2
                && int.TryParse(minuteParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int totalMinutes)
                && int.TryParse(minuteParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int secondsPart)
                && totalMinutes >= 0
                && secondsPart is >= 0 and < 60)
            {
                return TimeSpan.FromMinutes(totalMinutes) + TimeSpan.FromSeconds(secondsPart);
            }

            string[] formats =
            {
                @"m\:ss",
                @"mm\:ss",
                @"h\:mm\:ss",
                @"hh\:mm\:ss",
                @"m\:ss\.fff",
                @"mm\:ss\.fff",
                @"h\:mm\:ss\.fff",
                @"hh\:mm\:ss\.fff"
            };

            if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var result))
                return result;

            // Also try parsing as seconds
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                return TimeSpan.FromSeconds(seconds);

            return null;
        }

        // ────────────────────────────────────────────
        // SAVE
        // ────────────────────────────────────────────

        private void BtnSaveOriginal_Click(object sender, RoutedEventArgs e)
        {
            string destPath = _fileService.GetVideoFilePath("mp4");
            try
            {
                File.Copy(_videoPath, destPath, overwrite: false);
                _hasBeenSaved = true;
                _onSaved?.Invoke(destPath);
                ShowEditorStatus($"Saved - {Path.GetFileName(destPath)}", false);
            }
            catch (Exception ex)
            {
                ShowEditorStatus($"Save failed: {ex.Message}", true);
            }
        }

        private async void BtnSaveTrimmed_Click(object sender, RoutedEventArgs e)
        {
            if (!_ffmpegAvailable)
            {
                ShowFFmpegRequired("save a trimmed video");
                return;
            }

            if (!TryGetValidatedTrimRange(out var trimStart, out var trimEnd, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Invalid Trim Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnSaveTrimmed.IsEnabled = false;
                BtnSaveTrimmed.Content = "Trimming...";

                string outputPath = _fileService.GetVideoFilePath("trimmed.mp4");

                // Re-encode the trimmed segment with proper timestamps.
                // -ss before -i for fast seek, -to for end, re-encode with libx264 + AAC.
                string start = FormatFFmpegTime(trimStart);
                string duration = FormatFFmpegTime(trimEnd - trimStart);

                await RunFFmpegAsync(
                    "Save trimmed video",
                    outputPath,
                    "-n",
                    "-ss", start,
                    "-i", _videoPath,
                    "-t", duration,
                    "-c:v", "libx264",
                    "-preset", "fast",
                    "-crf", "23",
                    "-c:a", "aac",
                    "-b:a", "128k",
                    "-movflags", "+faststart",
                    outputPath);

                _hasBeenSaved = true;
                _onSaved?.Invoke(outputPath);
                ShowEditorStatus($"Saved - {Path.GetFileName(outputPath)}", false);
            }
            catch (Exception ex)
            {
                ShowEditorStatus(ToStatusMessage("Trim failed", ex), true);
            }
            finally
            {
                BtnSaveTrimmed.IsEnabled = _ffmpegAvailable;
                BtnSaveTrimmed.Content = CreateIconLabel(SaveIconGeometry, "Save trim");
            }
        }

        private async void BtnSaveFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!_ffmpegAvailable)
            {
                ShowFFmpegRequired("save the current frame");
                return;
            }

            if (VideoPlayer.Source == null)
            {
                ShowEditorStatus("Open a video before saving a frame.", true);
                return;
            }

            try
            {
                BtnSaveFrame.IsEnabled = false;
                BtnSaveFrame.Content = "Saving...";

                TimeSpan position = ClampFramePosition(VideoPlayer.Position);
                string outputPath = _fileService.GetImageFilePath("png");

                await RunFFmpegAsync(
                    "Save current frame",
                    outputPath,
                    "-n",
                    "-ss", FormatFFmpegTime(position),
                    "-i", _videoPath,
                    "-frames:v", "1",
                    outputPath);

                ShowEditorStatus($"Frame saved - {Path.GetFileName(outputPath)}", false);
            }
            catch (Exception ex)
            {
                ShowEditorStatus(ToStatusMessage("Frame save failed", ex), true);
            }
            finally
            {
                BtnSaveFrame.IsEnabled = true;
                BtnSaveFrame.Content = CreateIconLabel(FrameIconGeometry, "Frame");
            }
        }

        private async void BtnExportGif_Click(object sender, RoutedEventArgs e)
        {
            if (!_ffmpegAvailable)
            {
                ShowFFmpegRequired("export a GIF");
                return;
            }

            if (!TryGetValidatedTrimRange(out var trimStart, out var trimEnd, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Invalid GIF Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnExportGif.IsEnabled = false;
                BtnExportGif.Content = "Exporting...";

                string outputPath = _fileService.GetVideoFilePath("gif");
                string start = FormatFFmpegTime(trimStart);
                string duration = FormatFFmpegTime(trimEnd - trimStart);

                await RunFFmpegAsync(
                    "Export GIF",
                    outputPath,
                    "-n",
                    "-i", _videoPath,
                    "-ss", start,
                    "-t", duration,
                    "-filter_complex", "[0:v]fps=12,scale=720:-2:flags=lanczos,split[v1][v2];[v1]palettegen=max_colors=128[p];[v2][p]paletteuse=dither=bayer:bayer_scale=5",
                    "-loop", "0",
                    outputPath);

                ShowEditorStatus($"GIF exported - {Path.GetFileName(outputPath)}", false);
            }
            catch (Exception ex)
            {
                ShowEditorStatus(ToStatusMessage("GIF export failed", ex), true);
            }
            finally
            {
                BtnExportGif.IsEnabled = true;
                BtnExportGif.Content = CreateIconLabel(GifIconGeometry, "GIF");
            }
        }

        // ────────────────────────────────────────────
        // KEYBOARD SHORTCUTS
        // ────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    TogglePlayPause();
                    e.Handled = true;
                    break;
                case Key.M:
                    BtnMute_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        // ────────────────────────────────────────────
        // UTILITY
        // ────────────────────────────────────────────

        private static bool TryGetFFmpegPath(out string ffmpegPath)
        {
            if (File.Exists(FfmpegPath))
            {
                ffmpegPath = FfmpegPath;
                return true;
            }

            string? globalPath = GlobalFFOptions.GetFFMpegBinaryPath();
            if (!string.IsNullOrWhiteSpace(globalPath) && File.Exists(globalPath))
            {
                ffmpegPath = globalPath;
                return true;
            }

            ffmpegPath = string.Empty;
            return false;
        }

        private void SetFFmpegAvailableUi()
        {
            _ffmpegAvailable = true;
            TxtFFmpegStatus.Text = "FFmpeg ready";
            TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
            BtnDownloadFFmpeg.Visibility = Visibility.Collapsed;
            BtnSaveTrimmed.IsEnabled = true;
            BtnSaveTrimmed.ToolTip = "Apply trim and save a new video";
            BtnSaveFrame.ToolTip = "Save the current video frame as a PNG";
            BtnExportGif.ToolTip = "Export the selected trim range as a GIF";
        }

        private void SetFFmpegMissingUi()
        {
            _ffmpegAvailable = false;
            TxtFFmpegStatus.Text = "FFmpeg not found - trimming, frame saves, and GIF export need FFmpeg.";
            TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Orange;
            BtnDownloadFFmpeg.Visibility = Visibility.Visible;
            BtnSaveTrimmed.IsEnabled = false;
            BtnSaveTrimmed.ToolTip = "Download FFmpeg to enable trimming";
            BtnSaveFrame.ToolTip = "Download FFmpeg to save video frames";
            BtnExportGif.ToolTip = "Download FFmpeg to export GIFs";
        }

        private static string CreateUniqueTempDirectory(string prefix)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "parallax");
            Directory.CreateDirectory(tempRoot);

            string directory = Path.Combine(tempRoot, $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static async Task<bool> DownloadFFmpegToolsAsync(Action<string>? reportStatus = null)
        {
            Directory.CreateDirectory(ToolsDir);
            string tempRoot = CreateUniqueTempDirectory("ffmpeg");
            string zipPath = Path.Combine(tempRoot, "ffmpeg.zip");
            string extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractDir);

            try
            {
                reportStatus?.Invoke("Downloading FFmpeg...");
                await DownloadFileWithLimitAsync(FFmpegDownloadUri, zipPath, MaxFFmpegDownloadBytes);

                reportStatus?.Invoke("Extracting FFmpeg...");
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);
                });

                reportStatus?.Invoke("Installing FFmpeg tools...");
                bool foundFfmpeg = await Task.Run(() => CopyExpectedFFmpegBinaries(extractDir));
                if (foundFfmpeg)
                {
                    GlobalFFOptions.Configure(opt => opt.BinaryFolder = ToolsDir);
                }

                return foundFfmpeg;
            }
            finally
            {
                TryDeleteFile(zipPath);
                TryDeleteDirectory(extractDir);
                TryDeleteDirectory(tempRoot);
            }
        }

        private static async Task DownloadFileWithLimitAsync(Uri url, string destinationPath, long maxBytes)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength && contentLength > maxBytes)
            {
                throw new InvalidOperationException("The FFmpeg download is larger than the app safety limit. Install FFmpeg manually.");
            }

            await using Stream input = await response.Content.ReadAsStreamAsync(cancellation.Token);
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            byte[] buffer = new byte[81920];
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellation.Token)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    throw new InvalidOperationException("The FFmpeg download exceeded the app safety limit. Install FFmpeg manually.");
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellation.Token);
            }
        }

        private static bool CopyExpectedFFmpegBinaries(string extractDir)
        {
            bool foundFfmpeg = false;
            foreach (string exeName in ExpectedFFmpegBinaries)
            {
                string? sourcePath = Directory
                    .EnumerateFiles(extractDir, exeName, SearchOption.AllDirectories)
                    .FirstOrDefault(path => string.Equals(Path.GetFileName(path), exeName, StringComparison.OrdinalIgnoreCase));

                if (sourcePath == null)
                {
                    continue;
                }

                File.Copy(sourcePath, Path.Combine(ToolsDir, exeName), overwrite: true);
                if (exeName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    foundFfmpeg = true;
                }
            }

            return foundFfmpeg;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private async Task RunFFmpegAsync(string actionName, string outputPath, params string[] arguments)
        {
            if (!TryGetFFmpegPath(out string ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg is not available. Click Download FFmpeg or install FFmpeg manually.");
            }

            if (File.Exists(outputPath))
            {
                throw new IOException($"{actionName} could not start because the output file already exists.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var stderr = new StringBuilder();
            object stderrLock = new();

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    AppendBoundedError(stderr, stderrLock, eventArgs.Data);
                }
            };

            bool outputValid = false;
            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException($"{actionName} could not start FFmpeg.");
                }

                process.BeginErrorReadLine();
                using var timeout = new CancellationTokenSource(FFmpegProcessTimeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                    process.WaitForExit();
                }
                catch (OperationCanceledException ex)
                {
                    TryKillProcess(process);
                    throw new TimeoutException($"{actionName} timed out after {FFmpegProcessTimeout.TotalMinutes:0} minutes. The source video was kept.", ex);
                }

                string boundedError = GetBoundedError(stderr, stderrLock);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{actionName} failed (FFmpeg exit code {process.ExitCode}). {FormatFFmpegError(boundedError)}");
                }

                ValidateFFmpegOutput(outputPath, actionName);
                outputValid = true;
            }
            finally
            {
                if (!outputValid)
                {
                    TryDeleteFile(outputPath);
                }
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort cleanup after a timed-out export.
            }

            try
            {
                process.WaitForExit(5000);
            }
            catch
            {
                // Best-effort cleanup after a timed-out export.
            }
        }

        private static void AppendBoundedError(StringBuilder stderr, object stderrLock, string line)
        {
            string normalized = NormalizeMessage(line);
            lock (stderrLock)
            {
                if (stderr.Length >= MaxFFmpegErrorChars)
                {
                    return;
                }

                if (stderr.Length > 0)
                {
                    stderr.Append(' ');
                }

                int remainingChars = MaxFFmpegErrorChars - stderr.Length;
                stderr.Append(normalized.Length > remainingChars ? normalized[..remainingChars] : normalized);
            }
        }

        private static string GetBoundedError(StringBuilder stderr, object stderrLock)
        {
            lock (stderrLock)
            {
                return stderr.ToString();
            }
        }

        private static void ValidateFFmpegOutput(string outputPath, string actionName)
        {
            var output = new FileInfo(outputPath);
            if (!output.Exists || output.Length == 0)
            {
                throw new InvalidOperationException($"{actionName} finished, but no output file was created.");
            }
        }

        private TimeSpan ClampFramePosition(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (_naturalDuration.HasTimeSpan && position >= _naturalDuration.TimeSpan)
            {
                if (_naturalDuration.TimeSpan <= TimeSpan.FromMilliseconds(10))
                {
                    return TimeSpan.Zero;
                }

                return _naturalDuration.TimeSpan - TimeSpan.FromMilliseconds(10);
            }

            return position;
        }

        private static string FormatFFmpegTime(TimeSpan value)
        {
            return $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}.{value.Milliseconds:D3}";
        }

        private void ShowFFmpegRequired(string action)
        {
            string message = $"FFmpeg is required to {action}. Click 'Download FFmpeg' or install FFmpeg manually.";
            ShowEditorStatus(message, true);
            MessageBox.Show(message, "FFmpeg Required", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowFFmpegDownloadFailure(string message, Exception ex)
        {
            string status = ToStatusMessage(message, ex);
            TxtFFmpegStatus.Text = TrimForStatus(status, MaxStatusMessageChars);
            TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Red;
            ShowEditorStatus(status, true);
        }

        private static string FormatFFmpegError(string stderr)
        {
            return string.IsNullOrWhiteSpace(stderr)
                ? "FFmpeg did not provide details."
                : $"FFmpeg reported: {TrimForStatus(stderr, MaxStatusMessageChars)}";
        }

        private static string ToStatusMessage(string prefix, Exception ex)
        {
            string detail = TrimForStatus(NormalizeMessage(ex.Message), MaxStatusMessageChars);
            return $"{prefix}: {detail}";
        }

        private static string TrimForStatus(string message, int maxChars)
        {
            if (message.Length <= maxChars)
            {
                return message;
            }

            return message[..Math.Max(0, maxChars - 3)] + "...";
        }

        private static string NormalizeMessage(string message)
        {
            string normalized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        private void ShowEditorStatus(string message, bool isError)
        {
            TxtEditorStatus.Text = message;
            TxtEditorStatus.Foreground = isError
                ? System.Windows.Media.Brushes.OrangeRed
                : System.Windows.Media.Brushes.LimeGreen;

            // Cancel any pending status timer (KAM #4c — prevents overlapping timers)
            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer?.Stop();
                _statusTimer = null;
                TxtEditorStatus.Text = "Save to keep this recording, or close to discard.";
                TxtEditorStatus.Foreground = System.Windows.Media.Brushes.Gray;
            };
            _statusTimer.Start();
        }

        private void BtnOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            // Warn if unsaved temp recording
            if (_isTempFile && !_hasBeenSaved && File.Exists(_videoPath))
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Open another video anyway?",
                    "Unsaved Recording",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No) return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Video",
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv|All Files|*.*",
                DefaultExt = "mp4"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadVideo(dialog.FileName);
            }
        }

        private void LoadVideo(string filePath)
        {
            // Stop timer and reset play state
            _playbackTimer.Stop();
            _isPlaying = false;
            _mediaEnded = false;

            _videoPath = filePath;
            _isTempFile = false;
            _hasBeenSaved = false;
            _naturalDuration = Duration.Automatic;

            // Reset trim
            TxtTrimStart.Text = "00:00";
            TxtTrimOut.Text = "00:00";
            TxtTrimDuration.Text = "00:00";
            TxtCurrentTime.Text = "00:00";
            TxtTotalTime.Text = "00:00";
            UpdateTimelineVisuals();
            PlayOverlay.Visibility = Visibility.Visible;

            // Update display
            TxtFileName.Text = Path.GetFileName(filePath);
            TxtEditorStatus.Text = "Save to keep this recording, or close to discard.";
            TxtEditorStatus.Foreground = System.Windows.Media.Brushes.Gray;

            // Load the new video — do NOT call Close() before setting Source.
            // Close() fully unloads the media player and it does not reinitialize
            // correctly from Source alone. Setting Source directly handles unload+load.
            VideoPlayer.Source = new Uri(filePath);
            VideoPlayer.Play();
            VideoPlayer.Pause();
            _isPlaying = false;
            UpdatePlayButton();
            BtnSaveTrimmed.IsEnabled = _ffmpegAvailable;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnCloseEditor_Click(object sender, RoutedEventArgs e) => Close();
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        public void Dispose()
        {
            _playbackTimer.Stop();
            VideoPlayer?.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;

            // Warn if closing with an unsaved temp recording
            if (_isTempFile && !_hasBeenSaved && File.Exists(_videoPath))
            {
                var result = MessageBox.Show(
                    "This recording is not saved. Close anyway?",
                    "Unsaved Recording",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _playbackTimer.Stop();
            VideoPlayer?.Close();

            // Clean up the temp file — whether saved or not, the temp was COPIED to permanent
            if (_isTempFile && File.Exists(_videoPath))
            {
                try { File.Delete(_videoPath); }
                catch { /* best-effort cleanup */ }
            }

            base.OnClosed(e);
        }
    }
}
