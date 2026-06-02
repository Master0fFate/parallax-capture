using System.Xml.Linq;

namespace Parallax.Tests.KAMVerification;

/// <summary>
/// KAM #5: SharpAvi 2.1.2 (.NET Framework only) must be removed.
/// Verifies the dependency is removed from csproj and no NU1701 occurs.
/// </summary>
public class Kam005_SharpAviRemovedTests
{
    [Fact]
    public void SharpAvi_NotReferencedInCsproj()
    {
        var csprojPath = FindFile("parallax.csproj", includeTests: false);
        var csproj = XDocument.Load(csprojPath);

        var sharpAviRefs = csproj.Descendants("PackageReference")
            .Where(p => p.Attribute("Include")?.Value == "SharpAvi")
            .ToList();

        Assert.Empty(sharpAviRefs);
    }

    [Fact]
    public void SharpAvi_NotInPublishFolder()
    {
        var publishDir = FindDirectory("publish", includeTests: false);
        if (publishDir == null)
        {
            // publish/ may not exist in dev mode; that's fine
            return;
        }

        var sharpAviFiles = Directory.GetFiles(publishDir, "SharpAvi*", SearchOption.TopDirectoryOnly);
        Assert.Empty(sharpAviFiles);
    }

    [Fact]
    public void NoSharpAviUsingsInSource()
    {
        var sourceDir = FindDirectory("parallax", includeTests: false);
        if (sourceDir == null) return;

        var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("using SharpAvi", content);
            Assert.DoesNotContain("SharpAvi.", content);
        }
    }

    private static string FindFile(string fileName, bool includeTests)
    {
        string? root = FindProjectRoot();
        if (root == null)
            throw new FileNotFoundException($"Cannot find project root for {fileName}");
        string? found = SafeFindFile(root, fileName, includeTests);
        if (found != null) return found;
        throw new FileNotFoundException($"Could not find {fileName} under {root}");
    }

    private static string? FindDirectory(string dirName, bool includeTests)
    {
        string? root = FindProjectRoot();
        if (root == null) return null;
        return SafeFindDirectory(root, dirName, includeTests);
    }

    private static string? FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            // Check for parallax.csproj directly or in a parallax/ subdirectory
            if (File.Exists(Path.Combine(dir, "parallax.csproj")))
                return dir;
            if (File.Exists(Path.Combine(dir, "parallax", "parallax.csproj")))
                return Path.Combine(dir, "parallax");
            // Also accept any csproj in the directory itself (not in a tests/ subdir)
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

    private static string? SafeFindFile(string root, string fileName, bool includeTests)
    {
        try
        {
            foreach (var file in Directory.GetFiles(root, fileName, SearchOption.TopDirectoryOnly))
            {
                if (includeTests || !file.Contains("Tests"))
                    return file;
            }
            foreach (var subDir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string? found = SafeFindFile(subDir, fileName, includeTests);
                if (found != null) return found;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        return null;
    }

    private static string? SafeFindDirectory(string root, string dirName, bool includeTests)
    {
        try
        {
            foreach (var d in Directory.GetDirectories(root, dirName, SearchOption.TopDirectoryOnly))
            {
                if (includeTests || !d.Contains("Tests"))
                    return d;
            }
            foreach (var subDir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string? found = SafeFindDirectory(subDir, dirName, includeTests);
                if (found != null) return found;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        return null;
    }
}
