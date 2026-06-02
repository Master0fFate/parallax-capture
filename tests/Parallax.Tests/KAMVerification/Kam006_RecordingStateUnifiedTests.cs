namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #6: Recording state must have a single source of truth.
/// Verifies TrayIconManager._isRecording is removed and all reads go through RecorderService.
/// </summary>
public class Kam006_RecordingStateUnifiedTests
{
    [Fact]
    public void TrayIconManager_NoIsRecordingField()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // _isRecording field should be removed
        Assert.DoesNotContain("private bool _isRecording", source);
    }

    [Fact]
    public void TrayIconManager_UsesRecorderServiceIsRecording()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // All recording state checks should go through _recorderService.IsRecording
        Assert.True(
            source.Contains("_recorderService.IsRecording"),
            "KAM #6 FAIL: TrayIconManager must read recording state from _recorderService.IsRecording"
        );
    }

    [Fact]
    public void TriggerRegionVideo_ChecksRecorderServiceIsRecording()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "TriggerRegionVideo");

        // Must check _recorderService.IsRecording instead of _isRecording
        Assert.True(
            method.Contains("_recorderService.IsRecording"),
            "KAM #6 FAIL: TriggerRegionVideo must check _recorderService.IsRecording"
        );
        Assert.DoesNotContain("_isRecording", method);
    }

    [Fact]
    public void StopRecording_ChecksRecorderServiceIsRecording()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "StopRecording");

        Assert.True(
            method.Contains("_recorderService.IsRecording"),
            "KAM #6 FAIL: StopRecording must check _recorderService.IsRecording"
        );
        Assert.DoesNotContain("_isRecording", method);
    }

    [Fact]
    public void UpdateRecordingMenuState_UsesRecorderService()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "UpdateRecordingMenuState");

        Assert.True(
            method.Contains("_recorderService.IsRecording"),
            "KAM #6 FAIL: UpdateRecordingMenuState must derive state from _recorderService.IsRecording"
        );
        Assert.DoesNotContain("_isRecording", method);
    }

    [Fact]
    public void OnRecordingCompleted_NoIsRecordingAssignment()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // _isRecording assignments should not exist
        Assert.DoesNotContain("_isRecording =", source);
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
            methodIdx = source.IndexOf($"public void {methodName}(");

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
