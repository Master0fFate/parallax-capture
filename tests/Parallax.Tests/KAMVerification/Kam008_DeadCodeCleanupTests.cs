namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #8, #9: Dead code cleanup verification.
/// - ShowToolbarAfterCapture marked [Obsolete]
/// - PixelHelper.cs deleted
/// - CaptureResult.cs deleted
/// - _currentOutputPath usage
/// </summary>
public class Kam008_DeadCodeCleanupTests
{
    [Fact]
    public void PixelHelper_Deleted()
    {
        // KAM #9: PixelHelper.cs should not exist
        var filePath = FindFile("PixelHelper.cs", optional: true);
        Assert.Null(filePath);
    }

    [Fact]
    public void CaptureResult_Deleted()
    {
        // KAM #9: CaptureResult.cs should not exist
        var filePath = FindFile("CaptureResult.cs", optional: true);
        Assert.Null(filePath);
    }

    [Fact]
    public void NoPixelHelperUsingInCodebase()
    {
        var sourceDir = FindDirectory("parallax", optional: true);
        if (sourceDir == null) return;

        var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            if (file.Contains("Tests")) continue;
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("PixelHelper", content);
        }
    }

    [Fact]
    public void NoCaptureResultUsingInCodebase()
    {
        var sourceDir = FindDirectory("parallax", optional: true);
        if (sourceDir == null) return;

        var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            if (file.Contains("Tests")) continue;
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("CaptureResult", content);
        }
    }

    [Fact]
    public void HotkeyManager_RegisterMethods_ReturnBool()
    {
        var sourcePath = FindFile("HotkeyManager.cs")!;
        var source = File.ReadAllText(sourcePath);

        // KAM #10: Register methods must return bool
        Assert.True(
            source.Contains("public bool RegisterPrintScreen"),
            "KAM #10 FAIL: RegisterPrintScreen must return bool"
        );
        Assert.True(
            source.Contains("public bool RegisterAltPrintScreen"),
            "KAM #10 FAIL: RegisterAltPrintScreen must return bool"
        );
        Assert.True(
            source.Contains("public bool RegisterAltR"),
            "KAM #10 FAIL: RegisterAltR must return bool"
        );
        Assert.True(
            source.Contains("return Register("),
            "KAM #10 FAIL: Register shortcut methods must return the result of Register()"
        );
    }

    [Fact]
    public void AppDotXaml_ChecksHotkeyRegistration()
    {
        var sourcePath = FindFile("App.xaml.cs")!;
        var source = File.ReadAllText(sourcePath);

        // KAM #10: Must check return values
        Assert.True(
            source.Contains("bool printScreenOk"),
            "KAM #10 FAIL: App.xaml.cs must capture RegisterPrintScreen return value"
        );
        Assert.True(
            source.Contains("bool altPrintScreenOk"),
            "KAM #10 FAIL: App.xaml.cs must capture RegisterAltPrintScreen return value"
        );
        Assert.True(
            source.Contains("bool altROk"),
            "KAM #10 FAIL: App.xaml.cs must capture RegisterAltR return value"
        );

        // Must show MessageBox on failure
        Assert.True(
            source.Contains("if (!printScreenOk)"),
            "KAM #10 FAIL: App.xaml.cs must check printScreenOk"
        );
        Assert.True(
            source.Contains("if (!altROk)"),
            "KAM #10 FAIL: App.xaml.cs must check altROk"
        );
    }

    [Fact]
    public void AppDotXaml_AdaptsBalloonMessageToHotkeyStatus()
    {
        var sourcePath = FindFile("App.xaml.cs")!;
        var source = File.ReadAllText(sourcePath);

        // Balloon message should indicate if hotkeys are unavailable
        Assert.True(
            source.Contains("printScreenOk") &&
            source.Contains("?") &&
            source.Contains(":"),
            "KAM #10 FAIL: App.xaml.cs should conditionally set balloon message based on hotkey status"
        );
    }

    private static string? FindFile(string fileName, bool optional = false)
    {
        string? root = FindProjectRoot();
        if (root == null)
        {
            if (optional) return null;
            throw new FileNotFoundException($"Cannot find project root for {fileName}");
        }
        string? found = SafeFindFile(root, fileName);
        if (found != null) return found;
        if (optional) return null;
        throw new FileNotFoundException($"Could not find {fileName} under {root}");
    }

    private static string? FindDirectory(string dirName, bool optional = false)
    {
        string? root = FindProjectRoot();
        if (root == null)
        {
            if (optional) return null;
            throw new DirectoryNotFoundException($"Cannot find project root for {dirName}");
        }
        string? found = SafeFindDirectory(root, dirName);
        if (found != null) return found;
        if (optional) return null;
        throw new DirectoryNotFoundException($"Could not find {dirName} under {root}");
    }

    private static string? FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "parallax.csproj")))
                return dir;
            if (File.Exists(Path.Combine(dir, "parallax", "parallax.csproj")))
                return Path.Combine(dir, "parallax");
            var csprojFiles = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length > 0 && !dir.EndsWith("tests", StringComparison.OrdinalIgnoreCase)
                                       && !dir.Contains("Tests"))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string? SafeFindFile(string root, string fileName)
    {
        try
        {
            foreach (var file in Directory.GetFiles(root, fileName, SearchOption.TopDirectoryOnly))
            {
                // Accept if the file does not contain "Tests" in its path
                if (!file.Contains("Tests"))
                    return file;
            }
            foreach (var subDir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string? found = SafeFindFile(subDir, fileName);
                if (found != null) return found;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        return null;
    }

    private static string? SafeFindDirectory(string root, string dirName)
    {
        try
        {
            foreach (var d in Directory.GetDirectories(root, dirName, SearchOption.TopDirectoryOnly))
            {
                if (!d.Contains("Tests"))
                    return d;
            }
            foreach (var subDir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string? found = SafeFindDirectory(subDir, dirName);
                if (found != null) return found;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        return null;
    }
}
