namespace Parallax.Core.Platform;

public sealed class PlannedStartupService : IStartupService
{
    private readonly IPlatformLocations _locations;

    public PlannedStartupService(IPlatformLocations locations)
    {
        _locations = locations;
    }

    public StartupRegistrationPlan CreatePlan(bool enable, string executablePath)
    {
        return StartupRegistrationPolicy.CreatePlan(_locations.Platform, _locations, enable, executablePath);
    }

    public StartupRegistrationResult SetEnabled(bool enable, string executablePath)
    {
        var plan = CreatePlan(enable, executablePath);
        return new StartupRegistrationResult(true, plan, plan.Description);
    }
}
