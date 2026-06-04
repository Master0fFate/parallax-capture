using ScreenRecorderLib;
using parallax.Core.Models;

namespace parallax.Core.Services
{
    public class RecorderService : IDisposable
    {
        private Recorder? _recorder;
        private string? _currentOutputPath;
        public bool IsRecording { get; private set; } = false;

        public event Action<string>? RecordingCompleted;
        public event Action<string>? RecordingFailed;

        // Gets the primary display device name for ScreenRecorderLib v5
        private string GetPrimaryDisplayName()
        {
            var displays = Recorder.GetDisplays();
            if (displays != null && displays.Count > 0)
                return displays[0].DeviceName;
            return @"\\.\DISPLAY1";
        }

        // Starts recording a specific screen region
        // x, y = screen coordinates of top-left corner of region
        // width, height = size of region
        // outputPath = full file path where .mp4 will be saved
        public void StartRegionRecording(int x, int y, int width, int height, string outputPath)
        {
            if (IsRecording) return;

            // Dispose previous recorder to prevent native resource leak (KAM #3)
            _recorder?.Dispose();

            _currentOutputPath = outputPath;

            // Audio configuration per ScreenRecorderLib v5 docs:
            // IsAudioEnabled=true turns on audio capture.
            // IsOutputDeviceEnabled=true captures system audio (loopback).
            // AudioOutputDevice defaults to empty string which means "use system default
            // playback device". Explicitly enumerating devices and picking the first
            // one (e.g. Voicemod Dummy) causes silent audio failure.
            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    OutputFrameSize = new ScreenSize { Width = width, Height = height }
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality
                    },
                    Quality = 70,
                    Framerate = 30,
                    IsFixedFramerate = true
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = true,
                    IsOutputDeviceEnabled = true
                    // AudioOutputDevice intentionally omitted — defaults to empty string
                    // which tells ScreenRecorderLib to use the system default playback device.
                },
                SourceOptions = new SourceOptions
                {
                    RecordingSources = new List<RecordingSourceBase>
                    {
                        new DisplayRecordingSource
                        {
                            DeviceName = GetPrimaryDisplayName(),
                            RecorderApi = RecorderApi.WindowsGraphicsCapture,
                            SourceRect = new ScreenRect(x, y, width, height)
                        }
                    }
                }
            };

            _recorder = Recorder.CreateRecorder(options);

            _recorder.OnRecordingComplete += (s, args) =>
            {
                IsRecording = false;
                RecordingCompleted?.Invoke(args.FilePath);
            };

            _recorder.OnRecordingFailed += (s, args) =>
            {
                IsRecording = false;
                RecordingFailed?.Invoke(args.Error);
            };

            _recorder.Record(outputPath);
            IsRecording = true;
        }

        // Starts recording the full primary screen
        public void StartFullScreenRecording(string outputPath)
        {
            if (IsRecording) return;

            // Dispose previous recorder to prevent native resource leak (KAM #3)
            _recorder?.Dispose();

            _currentOutputPath = outputPath;

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = 30,
                    Quality = 70,
                    IsFixedFramerate = true
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = true,
                    IsOutputDeviceEnabled = true
                    // AudioOutputDevice intentionally omitted — defaults to empty string
                    // which tells ScreenRecorderLib to use the system default playback device.
                },
                SourceOptions = new SourceOptions
                {
                    RecordingSources = new List<RecordingSourceBase>
                    {
                        new DisplayRecordingSource
                        {
                            DeviceName = GetPrimaryDisplayName(),
                            RecorderApi = RecorderApi.WindowsGraphicsCapture
                        }
                    }
                }
            };

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += (s, args) =>
            {
                IsRecording = false;
                RecordingCompleted?.Invoke(args.FilePath);
            };
            _recorder.OnRecordingFailed += (s, args) =>
            {
                IsRecording = false;
                RecordingFailed?.Invoke(args.Error);
            };

            _recorder.Record(outputPath);
            IsRecording = true;
        }

        // Stops the current recording
        public void StopRecording()
        {
            if (!IsRecording || _recorder == null) return;
            _recorder.Stop();
        }

        public void Dispose()
        {
            _recorder?.Dispose();
        }
    }
}
