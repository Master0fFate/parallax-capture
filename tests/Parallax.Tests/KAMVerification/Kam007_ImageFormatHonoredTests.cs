namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #7: BtnSave must honor the user's configured ImageFormat instead of hard-coding "png".
/// </summary>
public class Kam007_ImageFormatHonoredTests
{
    [Fact]
    public void AnnotationWindow_Constructor_AcceptsImageFormat()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // Constructor must accept imageFormat parameter
        Assert.True(
            source.Contains("string imageFormat = \"png\""),
            "KAM #7 FAIL: AnnotationWindow constructor must accept imageFormat parameter with default 'png'"
        );
    }

    [Fact]
    public void AnnotationWindow_StoresImageFormatField()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        // Must have _imageFormat field
        Assert.True(
            source.Contains("private readonly string _imageFormat"),
            "KAM #7 FAIL: AnnotationWindow must have _imageFormat field"
        );
    }

    [Fact]
    public void BtnSave_UsesImageFormatInsteadOfHardcodedPng()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "BtnSave_Click");

        // Must NOT hard-code "png"
        Assert.DoesNotContain("\"png\"", method);

        // Must use _imageFormat
        Assert.True(
            method.Contains("_imageFormat"),
            "KAM #7 FAIL: BtnSave_Click must use _imageFormat"
        );
    }

    [Fact]
    public void BtnSave_ConstructsPathWithFormatExtension()
    {
        var sourcePath = FindSourceFile("AnnotationWindow.xaml.cs");
        var source = File.ReadAllText(sourcePath);

        var method = ExtractMethod(source, "BtnSave_Click");

        // Must use the format extension in the file path
        Assert.True(
            method.Contains("$\"parallax_") && method.Contains(".{ext}\")") ||
            method.Contains("$\"parallax_") && method.Contains(".{ext}\"") ||
            method.Contains(".\" + ext") ||
            method.Contains(".{ext}"),
            "KAM #7 FAIL: BtnSave_Click must construct file path with dynamic extension"
        );
    }

    [Fact]
    public void TrayIconManager_PassesImageFormatToAnnotationWindow()
    {
        var sourcePath = FindSourceFile("TrayIconManager.cs");
        var source = File.ReadAllText(sourcePath);

        // All AnnotationWindow constructor calls must pass _settings.ImageFormat
        var calls = FindAllConstructorCalls(source, "AnnotationWindow");

        Assert.NotEmpty(calls);
        foreach (var call in calls)
        {
            Assert.True(
                call.Contains("_settings.ImageFormat"),
                $"KAM #7 FAIL: AnnotationWindow constructor call must pass _settings.ImageFormat: {call}"
            );
        }
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

    private static List<string> FindAllConstructorCalls(string source, string typeName)
    {
        var calls = new List<string>();
        int searchIdx = 0;
        while (true)
        {
            int callIdx = source.IndexOf($"new {typeName}(", searchIdx);
            if (callIdx < 0) break;

            // Find the closing parenthesis of the constructor call
            int parenDepth = 1;
            int pos = callIdx + $"new {typeName}(".Length;
            while (parenDepth > 0 && pos < source.Length)
            {
                if (source[pos] == '(') parenDepth++;
                else if (source[pos] == ')') parenDepth--;
                pos++;
            }

            calls.Add(source.Substring(callIdx, pos - callIdx));
            searchIdx = pos;
        }
        return calls;
    }
}
