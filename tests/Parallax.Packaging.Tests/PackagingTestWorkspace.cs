namespace Parallax.Packaging.Tests;

internal sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    internal string CombinedOutput => StandardOutput + StandardError;

    public override string ToString()
    {
        return $"Exit code: {ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{StandardError}";
    }
}

internal sealed class TestWorkspace : IDisposable
{
    private TestWorkspace(string root)
    {
        Root = root;
        ToolsDirectory = Path.Combine(root, "tools");
        LinuxArtifacts = Path.Combine(root, "linux-artifacts");
        MacArtifacts = Path.Combine(root, "mac-artifacts");
        WindowsArtifacts = Path.Combine(root, "windows-artifacts");
        LinuxDotnetLog = Path.Combine(root, "linux-dotnet-args.log");
        MacDotnetLog = Path.Combine(root, "mac-dotnet-args.log");
        WindowsDotnetLog = Path.Combine(root, "windows-dotnet-args.log");
    }

    internal string Root { get; }

    internal string ToolsDirectory { get; }

    internal string LinuxArtifacts { get; }

    internal string MacArtifacts { get; }

    internal string WindowsArtifacts { get; }

    internal string LinuxDotnetLog { get; }

    internal string MacDotnetLog { get; }

    internal string WindowsDotnetLog { get; }

    internal static TestWorkspace Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "parallax-packaging-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    public void Dispose()
    {
        PackagingTestSupport.CleanupPackageWorkDirectories();

        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
