using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FFMpegCore;
using parallax.Core.Services;

namespace parallax.UI.Windows
{
    public partial class VideoEditorWindow : Window, IDisposable
    {
        // ── File state
        private string _videoPath;
        private readonly FileService _fileService;
        private readonly Action<string>? _onSaved;    // callback with permanent path
        private bool _isTempFile;                      // was recorded to temp dir?
#pragma warning disable CS0414 // reserved for future use (unsaved-changes confirmation)
        private bool _hasBeenSaved;
#pragma warning restore CS0414
        private bool _isPlaying = false;
        private bool _isDraggingSlider = false;
        private bool _mediaEnded = false;
        private Duration _naturalDuration = Duration.Automatic;
        private bool _isMuted = false;

        // ── FFmpeg state
        private bool _ffmpegAvailable = false;
        private bool _ffmpegDownloading = false;

        // ── Timer for updating timeline during playback
        private readonly DispatcherTimer _playbackTimer;

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

        private async Task CheckFFmpegAvailability()
        {
            try
            {
                Directory.CreateDirectory(ToolsDir);

                // Check tools directory first
                if (File.Exists(FfmpegPath))
                {
                    GlobalFFOptions.Configure(opt => opt.BinaryFolder = ToolsDir);
                    _ffmpegAvailable = true;
                    TxtFFmpegStatus.Text = "FFmpeg ready";
                    TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    BtnSaveTrimmed.IsEnabled = true;
                    return;
                }

                // Also check FFMpegCore's global config (legacy app dir installs)
                string? globalPath = GlobalFFOptions.GetFFMpegBinaryPath();
                if (!string.IsNullOrEmpty(globalPath) && File.Exists(globalPath))
                {
                    _ffmpegAvailable = true;
                    TxtFFmpegStatus.Text = "FFmpeg ready";
                    TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    BtnSaveTrimmed.IsEnabled = true;
                    return;
                }

                // Not found
                _ffmpegAvailable = false;
                TxtFFmpegStatus.Text = "FFmpeg not found — trim disabled";
                TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Orange;
                BtnDownloadFFmpeg.Visibility = Visibility.Visible;
                BtnSaveTrimmed.IsEnabled = false;
                BtnSaveTrimmed.ToolTip = "Install FFmpeg to enable trimming";
            }
            catch
            {
                _ffmpegAvailable = false;
                TxtFFmpegStatus.Text = "FFmpeg not found — trim disabled";
                BtnDownloadFFmpeg.Visibility = Visibility.Visible;
                BtnSaveTrimmed.IsEnabled = false;
            }
        }

        private async void BtnDownloadFFmpeg_Click(object sender, RoutedEventArgs e)
        {
            if (_ffmpegDownloading) return;
            _ffmpegDownloading = true;
            BtnDownloadFFmpeg.IsEnabled = false;
            BtnDownloadFFmpeg.Content = "Downloading...";

            try
            {
                Directory.CreateDirectory(ToolsDir);
                string extractDir = Path.Combine(Path.GetTempPath(), "parallax_ffmpeg_extract");
                string zipPath = Path.Combine(Path.GetTempPath(), "parallax_ffmpeg.zip");

                // Download FFmpeg essentials build from gyan.dev
                string downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

                TxtFFmpegStatus.Text = "Downloading FFmpeg...";

                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    byte[] zipData = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(zipPath, zipData);
                }

                TxtFFmpegStatus.Text = "Extracting...";
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
                });

                // Clean up zip
                try { File.Delete(zipPath); } catch { }

                // Find and copy ffmpeg, ffplay, ffprobe to persistent tools directory
                var extractedDirs = Directory.GetDirectories(extractDir);
                bool foundFfmpeg = false;
                foreach (var exe in new[] { "ffmpeg.exe", "ffplay.exe", "ffprobe.exe" })
                {
                    string? exePath = null;
                    foreach (var dir in extractedDirs)
                    {
                        string candidate = Path.Combine(dir, exe);
                        if (File.Exists(candidate)) { exePath = candidate; break; }
                        candidate = Path.Combine(dir, "bin", exe);
                        if (File.Exists(candidate)) { exePath = candidate; break; }
                    }
                    if (exePath != null)
                    {
                        File.Copy(exePath, Path.Combine(ToolsDir, exe), overwrite: true);
                        if (exe == "ffmpeg.exe") foundFfmpeg = true;
                    }
                }

                // Clean up extract dir
                try { Directory.Delete(extractDir, recursive: true); } catch { }

                if (foundFfmpeg)
                {
                    GlobalFFOptions.Configure(opt => opt.BinaryFolder = ToolsDir);

                    _ffmpegAvailable = true;
                    TxtFFmpegStatus.Text = "FFmpeg ready";
                    TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    BtnSaveTrimmed.IsEnabled = true;
                    BtnDownloadFFmpeg.Visibility = Visibility.Collapsed;
                    ShowEditorStatus("FFmpeg + ffplay installed \u2014 all codecs supported.", false);
                }
                else
                {
                    TxtFFmpegStatus.Text = "Extraction failed — try manual install";
                    TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                TxtFFmpegStatus.Text = $"Download failed: {ex.Message}";
                TxtFFmpegStatus.Foreground = System.Windows.Media.Brushes.Red;
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
                double totalSec = _naturalDuration.TimeSpan.TotalSeconds;
                TimelineSlider.Maximum = totalSec;
                TxtTotalTime.Text = FormatTime(_naturalDuration.TimeSpan);
                TxtTrimOut.Text = FormatTime(_naturalDuration.TimeSpan);
                UpdateTrimDuration();
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            _mediaEnded = true;
            _playbackTimer.Stop();
            UpdatePlayButton();
            PlayOverlay.Visibility = Visibility.Visible;
        }

        private void VideoPlayer_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            // Try falling back to ffplay if available
            if (File.Exists(FfplayPath))
            {
                var result = MessageBox.Show(
                    "This video uses an unsupported codec.\n\nOpen with external player (ffplay)?",
                    "Unsupported Codec",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(FfplayPath, $"\"{_videoPath}\"");
                    }
                    catch { /* best effort */ }
                }
            }
            else
            {
                var result = MessageBox.Show(
                    "This video uses an unsupported codec.\n\nDownload FFmpeg + ffplay to enable playback of all video formats?",
                    "Extended Codec Support Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    BtnDownloadFFmpeg_Click(this, new RoutedEventArgs());
                }
            }

            ShowEditorStatus(
                "Cannot play this file \u2014 unsupported format or missing codec.",
                isError: true);
            PlayOverlay.Visibility = Visibility.Collapsed;
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_isDraggingSlider) return;
            if (VideoPlayer.Source == null) return;

            try
            {
                if (_naturalDuration.HasTimeSpan)
                {
                    double pos = VideoPlayer.Position.TotalSeconds;
                    TimelineSlider.Value = pos;
                    TxtCurrentTime.Text = FormatTime(VideoPlayer.Position);
                }
            }
            catch { /* clock drift after media end */ }
        }

        private void UpdatePlayButton()
        {
            if (_mediaEnded)
            {
                BtnPlayPause.Content = "\u25B6  Replay";
                PlayOverlay.Visibility = Visibility.Visible;
            }
            else if (_isPlaying)
            {
                BtnPlayPause.Content = "\u23F8  Pause";
                PlayOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnPlayPause.Content = "\u25B6  Play";
                PlayOverlay.Visibility = Visibility.Visible;
            }
        }

        private void TogglePlayPause()
        {
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

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider && _naturalDuration.HasTimeSpan)
            {
                var pos = TimeSpan.FromSeconds(e.NewValue);
                VideoPlayer.Position = pos;
                TxtCurrentTime.Text = FormatTime(pos);
            }
        }

        // We track mouse down/up on the slider for scrubbing
        private void TimelineSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
            _playbackTimer.Stop();
        }

        private void TimelineSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            if (_isPlaying)
                _playbackTimer.Start();
        }

        // ────────────────────────────────────────────
        // MUTE
        // ────────────────────────────────────────────

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            VideoPlayer.Volume = _isMuted ? 0.0 : 1.0;
            BtnMute.Content = _isMuted ? "🔇  Unmute" : "🔊  Mute";
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

        // Move trim start back 5 seconds
        private void BtnTrimMinus5_Click(object sender, RoutedEventArgs e)
        {
            var start = ParseTrimTime(TxtTrimStart.Text);
            if (start != null)
            {
                var newStart = TimeSpan.FromSeconds(Math.Max(0, start.Value.TotalSeconds - 5));
                TxtTrimStart.Text = FormatTime(newStart);
                UpdateTrimDuration();
            }
        }

        // Move trim end forward 5 seconds
        private void BtnTrimPlus5_Click(object sender, RoutedEventArgs e)
        {
            var end = ParseTrimTime(TxtTrimOut.Text);
            if (end != null && _naturalDuration.HasTimeSpan)
            {
                double maxSec = _naturalDuration.TimeSpan.TotalSeconds;
                var newEnd = TimeSpan.FromSeconds(Math.Min(maxSec, end.Value.TotalSeconds + 5));
                TxtTrimOut.Text = FormatTime(newEnd);
                UpdateTrimDuration();
            }
        }

        // Updates the trim duration display based on current From/To values
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
            if (TimeSpan.TryParse($"00:{text}", out var result))
                return result;

            // Also try parsing as seconds
            if (double.TryParse(text, out double seconds))
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
                File.Copy(_videoPath, destPath, overwrite: true);
                _hasBeenSaved = true;
                _onSaved?.Invoke(destPath);
                ShowEditorStatus($"Saved \u2014 {Path.GetFileName(destPath)}", false);
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
                MessageBox.Show("FFmpeg is required for trimming.\nClick 'Download FFmpeg' or install manually.",
                    "FFmpeg Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var trimStart = ParseTrimTime(TxtTrimStart.Text);
            var trimEnd = ParseTrimTime(TxtTrimOut.Text);

            if (trimStart == null || trimEnd == null)
            {
                MessageBox.Show("Invalid trim times. Use MM:SS format (e.g. 01:30).",
                    "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (trimEnd <= trimStart)
            {
                MessageBox.Show("Trim end must be after trim start.",
                    "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnSaveTrimmed.IsEnabled = false;
                BtnSaveTrimmed.Content = "Trimming...";

                string outputPath = _fileService.GetVideoFilePath("trimmed.mp4");

                // Re-encode the trimmed segment with proper timestamps.
                // -ss before -i for fast seek, -to for end, re-encode with libx264 + AAC.
                string start = trimStart.Value.ToString(@"hh\:mm\:ss\.fff");
                string duration = (trimEnd.Value - trimStart.Value).ToString(@"hh\:mm\:ss\.fff");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = FfmpegPath,
                    Arguments = $"-y -ss {start} -i \"{_videoPath}\" -t {duration} " +
                                "-c:v libx264 -preset fast -crf 23 " +
                                "-c:a aac -b:a 128k -movflags +faststart " +
                                $"\"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                await Task.Run(() =>
                {
                    using var process = System.Diagnostics.Process.Start(psi)!;
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new Exception($"ffmpeg exited with code {process.ExitCode}\n{stderr}");
                });

                _hasBeenSaved = true;
                _onSaved?.Invoke(outputPath);
                ShowEditorStatus($"Saved \u2014 {Path.GetFileName(outputPath)}", false);
            }
            catch (Exception ex)
            {
                ShowEditorStatus($"Trim failed: {ex.Message}", true);
            }
            finally
            {
                BtnSaveTrimmed.IsEnabled = _ffmpegAvailable;
                BtnSaveTrimmed.Content = "Save Trimmed";
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

            // Reset to default text after 4 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                TxtEditorStatus.Text = "Save to keep this recording, or close to discard.";
                TxtEditorStatus.Foreground = System.Windows.Media.Brushes.Gray;
            };
            timer.Start();
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
            TimelineSlider.Value = 0;
            TimelineSlider.Maximum = 1;
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
