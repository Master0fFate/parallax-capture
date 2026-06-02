using System.Reflection;
using Moq;
using parallax.Core.Models;
using parallax.Core.Services;
using parallax.Tray;
using parallax.UI.Windows;

namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #1: Recording border must be cleaned up on ALL failure paths (synchronous exception).
/// We verify the TrayIconManager.TriggerRegionVideo catch block calls HideRecordingBorder().
/// </summary>
public class Kam001_RecordingBorderFixTests
{
    [Fact]
    public void TriggerRegionVideo_CatchBlock_CallsHideRecordingBorder()
    {
        // Arrange
        // We need to verify that the TrayIconManager code path includes a HideRecordingBorder call
        // in the catch block. Since we can't easily invoke the full path (requires overlay window),
        // we verify the source code contains the fix.

        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // The fix adds HideRecordingBorder() to the catch block in TriggerRegionVideo
        var catchSection = ExtractCatchBlock(source, "TriggerRegionVideo");

        Assert.True(
            catchSection.Contains("HideRecordingBorder"),
            $"KAM #1 FAIL: TriggerRegionVideo catch block must call HideRecordingBorder(). " +
            $"Found catch block:{Environment.NewLine}{catchSection}"
        );
    }

    [Fact]
    public void TriggerRegionVideo_CatchBlock_HasHideRecordingBeforeMessageBox()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);
        var catchSection = ExtractCatchBlock(source, "TriggerRegionVideo");

        int hideBorderIdx = catchSection.IndexOf("HideRecordingBorder");
        int msgBoxIdx = catchSection.IndexOf("MessageBox.Show");

        Assert.True(hideBorderIdx >= 0,
            "HideRecordingBorder must be present in catch block");

        Assert.True(hideBorderIdx < msgBoxIdx || msgBoxIdx < 0,
            "HideRecordingBorder should be called before MessageBox (cleanup first, then notify)");
    }

    [Fact]
    public void DisposeMethod_ClosesRecordingBorder()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // Verify Dispose() still closes the recording border
        Assert.Contains("_recordingBorder?.Close()", source);
    }

    private static string FindSourceFile(string fileName)
    {
        // Walk up from test assembly to find the source file
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories)
                .FirstOrDefault(f => f.Contains("parallax") && !f.Contains("Tests"));
            if (candidate != null) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"Could not find {fileName} in project tree");
    }

    private static string ExtractCatchBlock(string source, string methodName)
    {
        // Find the method
        int methodIdx = source.IndexOf($"void {methodName}(");
        if (methodIdx < 0)
            throw new InvalidOperationException($"Method {methodName} not found in source");

        // Find the catch block within this method
        int tryIdx = source.IndexOf("try", methodIdx);
        int catchIdx = source.IndexOf("catch", tryIdx);
        if (catchIdx < 0) return "";

        // Find the catch block body (after the exception variable)
        int openBrace = source.IndexOf('{', catchIdx);
        if (openBrace < 0) return "";

        // Find matching closing brace
        int depth = 1;
        int pos = openBrace + 1;
        while (depth > 0 && pos < source.Length)
        {
            if (source[pos] == '{') depth++;
            else if (source[pos] == '}') depth--;
            pos++;
        }

        return source.Substring(openBrace, pos - openBrace);
    }
}
