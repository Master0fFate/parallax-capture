namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #4a-c: DispatcherTimer lifecycle fixes in three locations.
/// Verifies all timers are stored as fields, not local variables, and are properly stopped.
/// </summary>
public class Kam004_TimerLifecycleTests
{
    [Fact]
    public void TrayIconManager_HasPendingActionTimerField()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // KAM #4a: field must exist
        Assert.True(
            source.Contains("_pendingActionTimer"),
            "KAM #4a FAIL: TrayIconManager must have _pendingActionTimer field"
        );
    }

    [Fact]
    public void TriggerScreenshot_CancelsPendingTimer()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "TriggerRegionScreenshot");

        // Must cancel previous timer before creating new one
        Assert.True(
            method.Contains("_pendingActionTimer?.Stop()") &&
            method.Contains("_pendingActionTimer = null"),
            "KAM #4a FAIL: TriggerRegionScreenshot must cancel _pendingActionTimer before creating new one"
        );
    }

    [Fact]
    public void TriggerFullScreenshot_CancelsPendingTimer()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "TriggerFullScreenshot");

        Assert.True(
            method.Contains("_pendingActionTimer?.Stop()") &&
            method.Contains("_pendingActionTimer = null"),
            "KAM #4a FAIL: TriggerFullScreenshot must cancel _pendingActionTimer"
        );
    }

    [Fact]
    public void TriggerRegionVideo_CancelsPendingTimer()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "TriggerRegionVideo");

        Assert.True(
            method.Contains("_pendingActionTimer?.Stop()") &&
            method.Contains("_pendingActionTimer = null"),
            "KAM #4a FAIL: TriggerRegionVideo must cancel _pendingActionTimer"
        );
    }

    [Fact]
    public void AnnotationWindow_StatusTimer_StoppedOnClose()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // KAM #4b: closed event must stop status timer
        Assert.True(
            source.Contains("_statusTimer.Stop()") &&
            source.Contains("_statusTimer.Tick -= OnStatusTick"),
            "KAM #4b FAIL: AnnotationWindow must stop _statusTimer and unsubscribe Tick on close"
        );
    }

    [Fact]
    public void VideoEditorWindow_ShowEditorStatus_ReusesTimerField()
    {
        var sourcePath = FindSourceFile("VideoEditorWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // KAM #4c: must have _statusTimer field
        Assert.True(
            source.Contains("DispatcherTimer? _statusTimer"),
            "KAM #4c FAIL: VideoEditorWindow must have _statusTimer field"
        );
    }

    [Fact]
    public void VideoEditorWindow_CancelsPreviousStatusTimer()
    {
        var sourcePath = FindSourceFile("VideoEditorWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "ShowEditorStatus");

        // Must stop previous timer before starting new one
        Assert.True(
            method.Contains("_statusTimer?.Stop()"),
            "KAM #4c FAIL: ShowEditorStatus must stop previous _statusTimer before starting new one"
        );
    }

    private static string FindSourceFile(string fileName)
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories)
                .FirstOrDefault(f => f.Contains("parallax") && !f.Contains("Tests"));
            if (candidate != null) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"Could not find {fileName}");
    }

    private static string ExtractMethod(string source, string methodName)
    {
        int methodIdx = source.IndexOf($"void {methodName}(");
        if (methodIdx < 0)
            methodIdx = source.IndexOf($"void {methodName} (");

        if (methodIdx < 0)
        {
            // Try public method
            methodIdx = source.IndexOf($"public void {methodName}(");
        }

        if (methodIdx < 0)
            throw new InvalidOperationException($"Method {methodName} not found");

        int openBrace = source.IndexOf('{', methodIdx);
        if (openBrace < 0) return "";

        int depth = 1;
        int pos = openBrace + 1;
        while (depth > 0 && pos < source.Length)
        {
            if (source[pos] == '{') depth++;
            else if (source[pos] == '}') depth--;
            pos++;
        }

        return source.Substring(methodIdx, pos - methodIdx);
    }
}
