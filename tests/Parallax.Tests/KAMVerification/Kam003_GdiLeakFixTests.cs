using System.Reflection;
using parallax.UI.Windows;

namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #3: GDI+ Bitmap handle leak in AnnotationWindow must be fixed.
/// Verifies _sourceBitmap is disposed when the window closes.
/// </summary>
public class Kam003_GdiLeakFixTests
{
    [Fact]
    public void AnnotationWindow_ClosedEvent_DisposesSourceBitmap()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // Verify the constructor subscribes to Closed event with bitmap cleanup
        Assert.True(
            source.Contains("Closed += (s, e) =>") &&
            source.Contains("_sourceBitmap?.Dispose()"),
            "KAM #3 FAIL: AnnotationWindow constructor must subscribe to Closed event and dispose _sourceBitmap"
        );
    }

    [Fact]
    public void AnnotationWindow_ClosedEvent_StopsStatusTimer()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // Fix KAM #4b: _statusTimer stopped on close
        Assert.True(
            source.Contains("_statusTimer.Stop()"),
            "KAM #4b FAIL: _statusTimer must be stopped on window close"
        );
    }

    [Fact]
    public void OpenImageEditor_DisposesBitmapOnException()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "OpenImageEditor");

        // Verify the bitmap variable is disposed in the catch block
        Assert.True(
            method.Contains("bitmap?.Dispose()") ||
            method.Contains("bitmap?.Dispose();"),
            "KAM #3 FAIL: OpenImageEditor must dispose bitmap on exception"
        );
    }

    [Fact]
    public void OpenImageEditor_UsesTryFinallyForBitmap()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "OpenImageEditor");

        // Verify bitmap is declared with ? nullable and assigned before try block
        Assert.Contains("System.Drawing.Bitmap? bitmap", method);
        Assert.Contains("bitmap = new System.Drawing.Bitmap", method);
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
            throw new InvalidOperationException($"Method {methodName} not found");

        // Find opening brace of method body
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
