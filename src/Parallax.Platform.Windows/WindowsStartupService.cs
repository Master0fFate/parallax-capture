using Microsoft.Win32;
using Parallax.Core.Platform;

namespace Parallax.Platform.Windows;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly IPlatformLocations _locations;

    public WindowsStartupService(IPlatformLocations locations)
    {
        _locations = locations;
    }

    public StartupRegistrationPlan CreatePlan(bool enable, string executablePath)
    {
        return StartupRegistrationPolicy.CreatePlan(PlatformKind.Windows, _locations, enable, executablePath);
    }

    public StartupRegistrationResult SetEnabled(bool enable, string executablePath)
    {
        var plan = CreatePlan(enable, executablePath);
        if (!OperatingSystem.IsWindows())
        {
            return new StartupRegistrationResult(false, plan, "Windows startup registration is only available on Windows.");
        }

        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey == null)
            {
                return new StartupRegistrationResult(false, plan, "Could not open the current-user Run registry key.");
            }

            if (enable)
            {
                runKey.SetValue(StartupRegistrationPolicy.DisplayName, QuoteExecutablePath(executablePath), RegistryValueKind.String);
                return new StartupRegistrationResult(true, plan, "Parallax Capture will start when this user signs in.");
            }

            runKey.DeleteValue(StartupRegistrationPolicy.DisplayName, throwOnMissingValue: false);
            return new StartupRegistrationResult(true, plan, "Parallax Capture startup registration was removed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new StartupRegistrationResult(false, plan, $"Current-user startup registration was denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            return new StartupRegistrationResult(false, plan, $"Current-user startup registration failed: {ex.Message}");
        }
    }

    private static string QuoteExecutablePath(string executablePath)
    {
        string normalized = executablePath.Trim().Trim('"');
        return $"\"{normalized}\"";
    }
}
