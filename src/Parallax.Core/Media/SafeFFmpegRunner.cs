using System.Diagnostics;
using System.Text;
using Parallax.Core.Platform;
using Parallax.Core.Recording;

namespace Parallax.Core.Media;

public interface IFFmpegOutputFileSystem
{
    bool FileExists(string path);

    long GetFileLength(string path);

    void DeleteFile(string path);
}

public interface IFFmpegProcessExecutor
{
    FFmpegProcessResult Execute(FFmpegRunRequest request);
}

public sealed class PhysicalFFmpegOutputFileSystem : IFFmpegOutputFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public long GetFileLength(string path)
    {
        return new FileInfo(path).Length;
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }
}

public sealed class ProcessFFmpegExecutor : IFFmpegProcessExecutor
{
    public FFmpegProcessResult Execute(FFmpegRunRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stderr.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            return new FFmpegProcessResult(-1, TimedOut: false, "FFmpeg could not be started.");
        }

        process.BeginErrorReadLine();
        int timeoutMilliseconds = request.Timeout.TotalMilliseconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)request.Timeout.TotalMilliseconds);
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup after timeout.
            }

            try
            {
                process.WaitForExit(5000);
            }
            catch
            {
                // Best-effort cleanup after timeout.
            }

            return new FFmpegProcessResult(-1, TimedOut: true, stderr.ToString());
        }

        process.WaitForExit();
        return new FFmpegProcessResult(process.ExitCode, TimedOut: false, stderr.ToString());
    }
}

public sealed class SafeFFmpegRunner : IFFmpegRunner
{
    public const int MaxLogChars = 4000;

    private readonly IFFmpegProcessExecutor _processExecutor;
    private readonly IFFmpegOutputFileSystem _fileSystem;

    public SafeFFmpegRunner(
        IFFmpegProcessExecutor? processExecutor = null,
        IFFmpegOutputFileSystem? fileSystem = null)
    {
        _processExecutor = processExecutor ?? new ProcessFFmpegExecutor();
        _fileSystem = fileSystem ?? new PhysicalFFmpegOutputFileSystem();
    }

    public FFmpegRunResult Run(FFmpegRunRequest request)
    {
        try
        {
            if (request.Arguments.Count == 0)
            {
                return new FFmpegRunResult(false, "FFmpeg arguments are required.", null);
            }

            if (_fileSystem.FileExists(request.OutputPath))
            {
                return new FFmpegRunResult(false, "FFmpeg output path already exists. A generated output path is required.", null);
            }

            if (WouldOverwriteInput(request))
            {
                return new FFmpegRunResult(false, "FFmpeg output path must not overwrite the source media.", null);
            }

            var result = _processExecutor.Execute(request);
            if (result.TimedOut)
            {
                DeletePartialOutput(request.OutputPath);
                return new FFmpegRunResult(
                    false,
                    $"FFmpeg timed out after {request.Timeout.TotalMinutes:0} minutes. The source media was kept. {FormatLog(result.StandardError)}",
                    null);
            }

            if (result.ExitCode != 0)
            {
                DeletePartialOutput(request.OutputPath);
                return new FFmpegRunResult(
                    false,
                    $"FFmpeg failed with exit code {result.ExitCode}. {FormatLog(result.StandardError)}",
                    null);
            }

            if (!_fileSystem.FileExists(request.OutputPath) || _fileSystem.GetFileLength(request.OutputPath) <= 0)
            {
                DeletePartialOutput(request.OutputPath);
                return new FFmpegRunResult(false, "FFmpeg finished without creating a valid output file.", null);
            }

            return new FFmpegRunResult(true, $"FFmpeg export succeeded: {request.OutputPath}", request.OutputPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            DeletePartialOutput(request.OutputPath);
            return new FFmpegRunResult(false, $"FFmpeg export failed safely: {ex.Message}", null);
        }
    }

    private static bool WouldOverwriteInput(FFmpegRunRequest request)
    {
        string outputPath = Path.GetFullPath(request.OutputPath);
        for (int index = 0; index < request.Arguments.Count - 1; index++)
        {
            if (request.Arguments[index] != "-i")
            {
                continue;
            }

            string inputPath = request.Arguments[index + 1];
            if (string.Equals(Path.GetFullPath(inputPath), outputPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DeletePartialOutput(string outputPath)
    {
        try
        {
            if (_fileSystem.FileExists(outputPath))
            {
                _fileSystem.DeleteFile(outputPath);
            }
        }
        catch
        {
            // Best-effort cleanup. Failure is reflected by the failed export result.
        }
    }

    private static string FormatLog(string log)
    {
        string normalized = log.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "FFmpeg did not provide details.";
        }

        return normalized.Length > MaxLogChars
            ? $"FFmpeg reported: {normalized[..MaxLogChars]}..."
            : $"FFmpeg reported: {normalized}";
    }
}
