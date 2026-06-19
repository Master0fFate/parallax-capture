using System.Diagnostics;

namespace Parallax.Core.Speech;

public sealed class ProcessWhisperCliExecutor : IWhisperCliProcessExecutor
{
    public async Task<WhisperCliProcessResult> ExecuteAsync(
        WhisperCliRunRequest request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new WhisperCliProcessResult(-1, TimedOut: false, string.Empty, "Whisper CLI could not be started.");
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new WhisperCliProcessResult(
                process.ExitCode,
                TimedOut: false,
                await stdout,
                await stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            return new WhisperCliProcessResult(
                -1,
                TimedOut: true,
                await ReadCompletedAsync(stdout),
                await ReadCompletedAsync(stderr));
        }
        catch
        {
            KillProcessTree(process);
            throw;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static async Task<string> ReadCompletedAsync(Task<string> task)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            return string.Empty;
        }
    }
}
