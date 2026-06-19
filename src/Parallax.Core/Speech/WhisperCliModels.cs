using Parallax.Core.Platform;

namespace Parallax.Core.Speech;

public interface IWhisperCliExecutableLocator
{
    string? FindExecutable(IPlatformLocations locations);
}

public interface IWhisperCliProcessExecutor
{
    Task<WhisperCliProcessResult> ExecuteAsync(
        WhisperCliRunRequest request,
        CancellationToken cancellationToken);
}

public sealed record WhisperCliRunRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string OutputTextPath,
    TimeSpan Timeout);

public sealed record WhisperCliProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);
