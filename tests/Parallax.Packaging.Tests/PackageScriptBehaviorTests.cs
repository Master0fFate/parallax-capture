using static Parallax.Packaging.Tests.PackagingTestSupport;

namespace Parallax.Packaging.Tests;

public sealed class PackageScriptBehaviorTests
{
    [Fact]
    public void PackageScriptsNormalizeValidTagVersionsAtPublishBoundary()
    {
        using var workspace = TestWorkspace.Create();
        WriteFakeDotnetTools(workspace.ToolsDirectory);

        var linux = RunBashPackageScript(
            "package-linux.sh",
            "linux-x64",
            "v1.1.0-ulwqa",
            workspace.LinuxArtifacts,
            workspace.ToolsDirectory,
            workspace.LinuxDotnetLog);
        AssertCommandSucceeded(linux);
        AssertPublishUsedNormalizedVersion(workspace.LinuxDotnetLog, "1.1.0-ulwqa");

        var mac = RunBashPackageScript(
            "package-macos.sh",
            "osx-x64",
            "v1.1.0-ulwqa",
            workspace.MacArtifacts,
            workspace.ToolsDirectory,
            workspace.MacDotnetLog,
            new Dictionary<string, string?> { ["MACOS_ALLOW_UNSIGNED"] = "true" });
        AssertCommandSucceeded(mac);
        AssertPublishUsedNormalizedVersion(workspace.MacDotnetLog, "1.1.0-ulwqa");

        if (CommandExists("pwsh"))
        {
            var windows = RunProcess(
                "pwsh",
                new[]
                {
                    "-NoProfile",
                    "-File",
                    Path.Combine(RepoRoot, "scripts/package-windows.ps1"),
                    "-RuntimeIdentifier",
                    "win-x64",
                    "-Version",
                    "v1.1.0-ulwqa",
                    "-ArtifactsDirectory",
                    workspace.WindowsArtifacts
                },
                new Dictionary<string, string?>
                {
                    ["PATH"] = workspace.ToolsDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                    ["DOTNET_ARG_LOG"] = workspace.WindowsDotnetLog
                });
            AssertCommandSucceeded(windows);
            AssertPublishUsedNormalizedVersion(workspace.WindowsDotnetLog, "1.1.0-ulwqa");
        }

        CleanupPackageWorkDirectories();
    }

    [Fact]
    public void PackageScriptsRejectUnsafeReleaseVersions()
    {
        using var workspace = TestWorkspace.Create();
        WriteFakeDotnetTools(workspace.ToolsDirectory);

        var linux = RunBashPackageScript(
            "package-linux.sh",
            "linux-x64",
            "v1.2.3;whoami",
            workspace.LinuxArtifacts,
            workspace.ToolsDirectory,
            workspace.LinuxDotnetLog);
        AssertCommandFailedWithVersionMessage(linux);
        Assert.False(File.Exists(workspace.LinuxDotnetLog), "Invalid Linux version must fail before dotnet publish.");

        var mac = RunBashPackageScript(
            "package-macos.sh",
            "osx-x64",
            "v1.2.3+build",
            workspace.MacArtifacts,
            workspace.ToolsDirectory,
            workspace.MacDotnetLog,
            new Dictionary<string, string?> { ["MACOS_ALLOW_UNSIGNED"] = "true" });
        AssertCommandFailedWithVersionMessage(mac);
        Assert.False(File.Exists(workspace.MacDotnetLog), "Invalid macOS version must fail before dotnet publish.");

        if (CommandExists("pwsh"))
        {
            var windows = RunProcess(
                "pwsh",
                new[]
                {
                    "-NoProfile",
                    "-File",
                    Path.Combine(RepoRoot, "scripts/package-windows.ps1"),
                    "-RuntimeIdentifier",
                    "win-x64",
                    "-Version",
                    "v1.2.3;whoami",
                    "-ArtifactsDirectory",
                    workspace.WindowsArtifacts
                },
                new Dictionary<string, string?>
                {
                    ["PATH"] = workspace.ToolsDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                    ["DOTNET_ARG_LOG"] = workspace.WindowsDotnetLog
                });
            AssertCommandFailedWithVersionMessage(windows);
            Assert.False(File.Exists(workspace.WindowsDotnetLog), "Invalid Windows version must fail before dotnet publish.");
        }
    }

    [Fact]
    public void MacReleasePackagingFailsClosedWithoutSigningAndNotarizationInputs()
    {
        using var workspace = TestWorkspace.Create();
        WriteFakeDotnetTools(workspace.ToolsDirectory);

        var result = RunBashPackageScript(
            "package-macos.sh",
            "osx-x64",
            "v1.1.0",
            workspace.MacArtifacts,
            workspace.ToolsDirectory,
            workspace.MacDotnetLog,
            new Dictionary<string, string?> { ["RELEASE_BUILD"] = "true" });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("MACOS_CODESIGN_IDENTITY", result.CombinedOutput);
    }
}
