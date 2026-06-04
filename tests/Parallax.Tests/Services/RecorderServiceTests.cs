using parallax.Core.Services;

namespace Parallax.Tests.Services;

public class RecorderServiceTests : IDisposable
{
    private readonly RecorderService _service = new();

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void Constructor_IsRecording_False()
    {
        Assert.False(_service.IsRecording);
    }

    [Fact]
    public void Constructor_Events_CanSubscribe()
    {
        // Events can only appear on left side of += or -= outside declaring class
        _service.RecordingCompleted += (path) => { };
        _service.RecordingFailed += (error) => { };
        Assert.True(true);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var service = new RecorderService();
        service.Dispose();
        // If we get here, dispose succeeded
        Assert.True(true);
    }

    [Fact]
    public void StopRecording_WhenNotRecording_DoesNotThrow()
    {
        var service = new RecorderService();
        service.StopRecording();
        Assert.False(service.IsRecording);
        service.Dispose();
    }

    // NOTE: Actual recording tests require native ScreenRecorderLib and
    // cannot run headlessly. The audio configuration fix was verified
    // by inspecting the RecorderOptions construction in RecorderService.cs
    // against the official ScreenRecorderLib v5.x wiki documentation:
    // https://github.com/sskodje/ScreenRecorderLib/wiki/Quickstart-guide-v5.x.x-and-newer
    //
    // Root cause: FindAudioOutputDevice() enumerated OutputDevices and blindly
    // picked devices[0] which on the dev machine was "Dummy Output (Voicemod)"
    // — a virtual device that produces no audio. Passing this device name to
    // AudioOutputDevice caused ScreenRecorderLib to capture from the silent
    // virtual device instead of the actual speakers.
    //
    // Fix: Remove device enumeration. Set IsAudioEnabled=true and
    // IsOutputDeviceEnabled=true, and leave AudioOutputDevice at its default
    // empty string. Per the wiki: "Passing empty string or null uses system
    // default playback device."
}
