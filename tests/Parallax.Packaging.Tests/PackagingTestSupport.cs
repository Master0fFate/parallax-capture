using System.Diagnostics;

namespace Parallax.Packaging.Tests;

internal static class PackagingTestSupport
{
    internal static readonly string RepoRoot = FindRepoRoot();

    internal static string ReadRepoFile(string relativePath)
    {
        string path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Expected repository file to exist: {relativePath}");
        return File.ReadAllText(path);
    }

    internal static CommandResult RunBashPackageScript(
        string scriptName,
        string rid,
        string version,
        string artifactsDirectory,
        string toolsDirectory,
        string dotnetArgLog,
        IReadOnlyDictionary<string, string?>? extraEnvironment = null)
    {
        string bash = ResolveBash();
        Assert.False(string.IsNullOrEmpty(bash), "bash is required to smoke-test Unix packaging scripts.");

        string command = """
set -euo pipefail
tools="$(cygpath -u "$FAKE_TOOL_DIR" 2>/dev/null || printf '%s' "$FAKE_TOOL_DIR")"
repo="$(cygpath -u "$REPO_ROOT_FOR_BASH" 2>/dev/null || printf '%s' "$REPO_ROOT_FOR_BASH")"
artifacts="$(cygpath -u "$ARTIFACT_ROOT" 2>/dev/null || printf '%s' "$ARTIFACT_ROOT")"
export PATH="$tools:$PATH"
export VERSION="$PACKAGE_VERSION_INPUT"
export ARTIFACTS_DIRECTORY="$artifacts"
"$BASH" "$repo/scripts/$PACKAGE_SCRIPT" "$PACKAGE_RID"
""";

        var environment = new Dictionary<string, string?>
        {
            ["FAKE_TOOL_DIR"] = toolsDirectory,
            ["REPO_ROOT_FOR_BASH"] = RepoRoot,
            ["ARTIFACT_ROOT"] = artifactsDirectory,
            ["PACKAGE_VERSION_INPUT"] = version,
            ["PACKAGE_SCRIPT"] = scriptName,
            ["PACKAGE_RID"] = rid,
            ["DOTNET_ARG_LOG"] = dotnetArgLog
        };

        if (extraEnvironment is not null)
        {
            foreach (var (key, value) in extraEnvironment)
            {
                environment[key] = value;
            }
        }

        return RunProcess(bash, new[] { "-lc", command }, environment);
    }

    internal static CommandResult RunProcess(
        string fileName,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.WorkingDirectory = RepoRoot;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(120_000), $"Command timed out: {fileName}");

        return new CommandResult(process.ExitCode, stdout, stderr);
    }

    internal static bool CommandExists(string command)
    {
        return TryResolveCommand(command, out _);
    }

    internal static void AssertCommandSucceeded(CommandResult result)
    {
        Assert.True(result.ExitCode == 0, result.ToString());
    }

    internal static void AssertCommandFailedWithVersionMessage(CommandResult result)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Version must be SemVer", result.CombinedOutput);
    }

    internal static void AssertPublishUsedNormalizedVersion(string dotnetArgLog, string expectedVersion)
    {
        string log = File.ReadAllText(dotnetArgLog);
        Assert.Contains("publish", log);
        Assert.Contains($"-p:Version={expectedVersion}", log);
        Assert.DoesNotContain($"-p:Version=v{expectedVersion}", log);
    }

    internal static void WriteFakeDotnetTools(string toolsDirectory)
    {
        Directory.CreateDirectory(toolsDirectory);

        string unixDotnet = Path.Combine(toolsDirectory, "dotnet");
        File.WriteAllText(
            unixDotnet,
            """
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$DOTNET_ARG_LOG"
out=""
previous=""
for arg in "$@"; do
  if [ "$previous" = "-o" ]; then
    out="$arg"
  fi
  previous="$arg"
done
if [ -n "$out" ]; then
  mkdir -p "$out"
  : > "$out/Parallax.App.Avalonia"
  : > "$out/Parallax.App.Avalonia.exe"
fi
""");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(unixDotnet, UnixExecutableFileMode);
        }

        File.WriteAllText(
            Path.Combine(toolsDirectory, "dotnet.cmd"),
            """
@echo off
setlocal
>>"%DOTNET_ARG_LOG%" echo %*
set "out="
:next
if "%~1"=="" goto done
if "%~1"=="-o" set "out=%~2"
shift
goto next
:done
if not "%out%"=="" (
    mkdir "%out%" >nul 2>nul
    type nul > "%out%\Parallax.App.Avalonia.exe"
)
exit /b 0
""");
    }

    internal static void CleanupPackageWorkDirectories()
    {
        foreach (string relativePath in new[]
                 {
                     "artifacts/publish/linux-x64",
                     "artifacts/publish/osx-x64",
                     "artifacts/publish/osx-arm64",
                     "artifacts/publish/win-x64",
                     "artifacts/package/linux-x64",
                     "artifacts/package/osx-x64",
                     "artifacts/package/osx-arm64",
                     "artifacts/package/win-x64"
                 })
        {
            string path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        foreach (string relativePath in new[] { "artifacts/publish", "artifacts/package" })
        {
            string path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
    }

    private static UnixFileMode UnixExecutableFileMode =>
        UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute;

    private static string ResolveBash()
    {
        if (TryResolveCommand("bash", out string bash))
        {
            return bash;
        }

        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        foreach (string candidate in new[]
                 {
                     @"C:\Program Files\Git\bin\bash.exe",
                     @"C:\Program Files\Git\usr\bin\bash.exe",
                     @"C:\Program Files (x86)\Git\bin\bash.exe"
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static bool TryResolveCommand(string command, out string resolvedPath)
    {
        string finder = OperatingSystem.IsWindows() ? "where.exe" : "which";
        var result = RunProcess(finder, new[] { command });
        resolvedPath = result.StandardOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return result.ExitCode == 0 && resolvedPath.Length > 0;
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
