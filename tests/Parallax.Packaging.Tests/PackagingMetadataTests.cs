using System.Text.RegularExpressions;

namespace Parallax.Packaging.Tests;

public sealed class PackagingMetadataTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void WindowsInstallerScriptUsesPerUserPathsAndChecksumVerification()
    {
        string installer = ReadRepoFile("scripts/install-windows.ps1");
        string packager = ReadRepoFile("scripts/package-windows.ps1");

        Assert.Contains("Get-FileHash", installer);
        Assert.Contains("SHA256SUMS", installer);
        Assert.Contains("Checksum mismatch", installer);
        Assert.Contains("$env:LOCALAPPDATA", installer);
        Assert.Contains("Programs", installer);
        Assert.Contains("Expand-Archive", installer);
        Assert.DoesNotContain("Program Files", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-Verb RunAs", installer, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("win-x64", packager);
        Assert.Contains("Compress-Archive", packager);
        Assert.Contains("Generate-Checksums", packager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PrivilegesRequired=lowest", ReadRepoFile("parallax/installer.iss"));
    }

    [Fact]
    public void UnixInstallerScriptVerifiesChecksumsAndUsesNoAdminPlatformPaths()
    {
        string installer = ReadRepoFile("scripts/install-unix.sh");

        Assert.Contains("SHA256SUMS", installer);
        Assert.Contains("checksum mismatch", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sha256sum", installer);
        Assert.Contains("shasum -a 256", installer);
        Assert.Contains("$HOME/Applications", installer);
        Assert.Contains("$HOME/.local/bin", installer);
        Assert.Contains("$HOME/.local/share/applications", installer);
        Assert.DoesNotContain("sudo ", installer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MacPackagingContainsBundlePermissionAndSigningMetadata()
    {
        string script = ReadRepoFile("scripts/package-macos.sh");
        string plist = ReadRepoFile("packaging/macos/Info.plist");
        string entitlements = ReadRepoFile("packaging/macos/Entitlements.plist");
        string cask = ReadRepoFile("packaging/macos/parallax-capture.rb");

        Assert.Contains(".app", script);
        Assert.Contains("codesign", script);
        Assert.Contains("notarytool", script);
        Assert.Contains("unsigned", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hdiutil", script);
        Assert.Contains("osx-x64", script);
        Assert.Contains("osx-arm64", script);

        Assert.Contains("CFBundleIdentifier", plist);
        Assert.Contains("com.master0ffate.parallax-capture", plist);
        Assert.Contains("NSMicrophoneUsageDescription", plist);
        Assert.Contains("Screen Recording", plist);
        Assert.Contains("Accessibility", plist);
        Assert.Contains("Input Monitoring", plist);
        Assert.Contains("NSDocumentsFolderUsageDescription", plist);

        Assert.Contains("com.apple.security.device.audio-input", entitlements);
        Assert.Contains("com.apple.security.files.user-selected.read-write", entitlements);
        Assert.Contains("cask \"parallax-capture\"", cask);
        Assert.Contains("sha256", cask);
    }

    [Fact]
    public void LinuxPackagingContainsDesktopMetadataAndInstallerIntegration()
    {
        string script = ReadRepoFile("scripts/package-linux.sh");
        string desktop = ReadRepoFile("packaging/linux/parallax-capture.desktop");
        string metainfo = ReadRepoFile("packaging/linux/parallax-capture.metainfo.xml");

        Assert.Contains("linux-x64", script);
        Assert.Contains("AppDir", script);
        Assert.Contains("appimagetool", script);
        Assert.Contains("dpkg-deb", script);
        Assert.Contains("rpmbuild", script);
        Assert.Contains("SHA256SUMS", script);

        Assert.Contains("Type=Application", desktop);
        Assert.Contains("Exec=parallax-capture", desktop);
        Assert.Contains("Icon=parallax-capture", desktop);
        Assert.Contains("Categories=Graphics;AudioVideo;Utility;", desktop);

        Assert.Contains("<id>com.master0ffate.parallax-capture</id>", metainfo);
        Assert.Contains("Wayland", metainfo);
        Assert.Contains("xdg-desktop-portal", metainfo);
    }

    [Fact]
    public void CiMatrixCoversRequiredRidsAndPackagingSmoke()
    {
        string ci = ReadRepoFile(".github/workflows/ci.yml");

        foreach (string os in new[] { "windows-latest", "macos-13", "macos-latest", "ubuntu-latest" })
        {
            Assert.Contains(os, ci);
        }

        foreach (string rid in new[] { "win-x64", "linux-x64", "osx-x64", "osx-arm64" })
        {
            Assert.Contains(rid, ci);
        }

        Assert.Contains("dotnet restore", ci);
        Assert.Contains("dotnet build", ci);
        Assert.Contains("dotnet test", ci);
        Assert.Contains("package-windows.ps1", ci);
        Assert.Contains("package-macos.sh", ci);
        Assert.Contains("package-linux.sh", ci);
        Assert.Contains("SHA256SUMS", ci);
    }

    [Fact]
    public void ReadmeDocumentsPlatformInstallBuildTestPermissionsAndFfmpeg()
    {
        string readme = ReadRepoFile("README.md");

        foreach (string section in new[]
                 {
                     "Windows",
                     "macOS",
                     "Linux",
                     "Build",
                     "Test",
                     "Packaging",
                     "Permissions",
                     "FFmpeg"
                 })
        {
            Assert.Contains(section, readme);
        }

        Assert.Contains("install-windows.ps1", readme);
        Assert.Contains("install-unix.sh", readme);
        Assert.Contains("checksum", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no admin", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Screen Recording", readme);
        Assert.Contains("xdg-desktop-portal", readme);
        Assert.Contains("best-effort only", readme);
    }

    [Fact]
    public void PackagingAssetsContainNoSecretMaterial()
    {
        string[] packagingFiles = Directory
            .EnumerateFiles(RepoRoot, "*", SearchOption.AllDirectories)
            .Where(IsPackagingAsset)
            .ToArray();

        Assert.NotEmpty(packagingFiles);

        Regex secretPattern = new(
            "(BEGIN (RSA |EC |OPENSSH |DSA |)?PRIVATE KEY|AKIA[0-9A-Z]{16}|ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[baprs]-[A-Za-z0-9-]{10,}|password\\s*=\\s*[^\\s#]+|client_secret\\s*=\\s*[^\\s#]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        string[] offenders = packagingFiles
            .Select(path => new { path, text = File.ReadAllText(path) })
            .Where(item => secretPattern.IsMatch(item.text))
            .Select(item => Path.GetRelativePath(RepoRoot, item.path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsPackagingAsset(string path)
    {
        string relative = Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
        return relative.StartsWith("scripts/", StringComparison.Ordinal)
               || relative.StartsWith("packaging/", StringComparison.Ordinal)
               || relative.StartsWith(".github/workflows/", StringComparison.Ordinal)
               || relative is "README.md" or "parallax/installer.iss";
    }

    private static string ReadRepoFile(string relativePath)
    {
        string path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Expected repository file to exist: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ParallaxCapture.sln"))
                && File.Exists(Path.Combine(dir, "README.md")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
