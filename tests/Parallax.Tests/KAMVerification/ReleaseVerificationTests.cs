namespace Parallax.Tests.KAMVerification;

public class ReleaseVerificationTests
{
    [Fact]
    public void RecordingHudAndBorder_RequestCaptureExclusionByContract()
    {
        string controls = ReadSource("RecordingControlsWindow.xaml.cs");
        string border = ReadSource("RecordingBorderWindow.xaml.cs");

        Assert.Contains("WDA_EXCLUDEFROMCAPTURE", controls);
        Assert.Contains("SetWindowDisplayAffinity", controls);
        Assert.Contains("IsCaptureExcluded", controls);
        Assert.Contains("SourceInitialized", controls);

        Assert.Contains("WDA_EXCLUDEFROMCAPTURE", border);
        Assert.Contains("SetWindowDisplayAffinity", border);
        Assert.Contains("IsCaptureExcluded", border);
        Assert.Contains("SourceInitialized", border);
    }

    [Fact]
    public void RecordingHudAndBorder_AreCleanedUpAcrossRecordingPaths()
    {
        string tray = ReadSource("TrayIconManager.cs");

        Assert.Contains("HideRecordingBorder();", tray);
        Assert.Contains("OnRecordingCompleted", tray);
        Assert.Contains("OnRecordingFailed", tray);
        Assert.Contains("_recordingControls?.Close()", tray);
        Assert.Contains("_recordingControls = null", tray);
        Assert.Contains("_recordingBorder?.Close()", tray);
        Assert.Contains("_recordingBorder = null", tray);
        Assert.Contains("Dispose()", tray);
    }

    [Fact]
    public void VideoEditor_FfmpegProcessExecutionRemainsHardened()
    {
        string editor = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("ProcessStartInfo", editor);
        Assert.Contains("UseShellExecute = false", editor);
        Assert.Contains("CreateNoWindow = true", editor);
        Assert.Contains("RedirectStandardError = true", editor);
        Assert.Contains("ArgumentList.Add", editor);
        Assert.Contains("\"-n\"", editor);
        Assert.Contains("FFmpegProcessTimeout", editor);
        Assert.Contains("WaitForExitAsync(timeout.Token)", editor);
        Assert.Contains("Kill(entireProcessTree: true)", editor);
        Assert.Contains("MaxFFmpegErrorChars", editor);
        Assert.Contains("ValidateFFmpegOutput", editor);
        Assert.Contains("TryDeleteFile(outputPath)", editor);
    }

    [Fact]
    public void VideoEditor_TrimValidationCoversInvalidBoundaryStates()
    {
        string editor = ReadSource("VideoEditorWindow.xaml.cs");

        Assert.Contains("Invalid trim times", editor);
        Assert.Contains("Trim times cannot be negative", editor);
        Assert.Contains("Trim range is outside this video's duration", editor);
        Assert.Contains("Trim end must be after trim start", editor);
        Assert.Contains("TryGetValidatedTrimRange", editor);
    }

    [Fact]
    public void SettingsWindow_ValidatesDuplicateEnabledShortcutsBeforeSaving()
    {
        string settings = ReadSource("SettingsWindow.xaml.cs");

        Assert.Contains("TryValidateHotkeys", settings);
        Assert.Contains("new Dictionary<(uint Modifiers, uint VirtualKey), string>", settings);
        Assert.Contains("used.TryGetValue", settings);
        Assert.Contains("is already assigned", settings);
        Assert.Contains("HotkeyScreenshotEnabled", settings);
        Assert.Contains("HotkeyFullscreenEnabled", settings);
        Assert.Contains("HotkeyRegionVideoEnabled", settings);
    }

    [Fact]
    public void Documentation_StatesCaptureExclusionAndFfmpegTrustBoundaries()
    {
        string readme = File.ReadAllText(Path.Combine(FindRepoRoot(), "README.md"));
        string product = File.ReadAllText(Path.Combine(FindRepoRoot(), "PRODUCT.md"));
        string design = File.ReadAllText(Path.Combine(FindRepoRoot(), "DESIGN.md"));

        Assert.Contains("best-effort only", readme);
        Assert.Contains("not DRM", readme);
        Assert.Contains("does not verify FFmpeg signatures or hashes", readme);
        Assert.Contains("not a security guarantee", product);
        Assert.Contains("does not currently verify FFmpeg signatures or hashes", product);
        Assert.Contains("best-effort only", design);
        Assert.Contains("Until signature or hash verification exists", design);
    }

    private static string ReadSource(string fileName)
    {
        string repoRoot = FindRepoRoot();
        string? found = Directory
            .EnumerateFiles(repoRoot, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}parallax{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        if (found == null)
        {
            throw new FileNotFoundException($"Could not find source file {fileName} under {repoRoot}.");
        }

        return File.ReadAllText(found);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "README.md"))
                && File.Exists(Path.Combine(dir, "parallax", "parallax.csproj")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
