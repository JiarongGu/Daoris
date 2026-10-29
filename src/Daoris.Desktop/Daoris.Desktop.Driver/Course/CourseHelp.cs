namespace Daoris.Driver;

/// <summary>
/// What the session requests ask of Ask Daoris's judges: how a request to housekeep and a request for a chosen setting are answered, each
/// over the home and the loop's service (HELPMODE1c2a, HELPMODE1c3). Help installs it when the driver loads and builds exactly the judges the
/// watch built before it was a port.
/// </summary>
internal interface ICourseHelp
{
    /// <summary>The judge of a housekeeping request: what this loop answers beside it.</summary>
    Func<SessionRequest, CancellationToken, Task<HousekeepingAnswer>> Housekeeping(string home, Func<ServiceClient?> service);

    /// <summary>The judge of a chosen setting's request: what this loop answers beside it.</summary>
    Func<SessionRequest, CancellationToken, Task<SettingAnswer>> Settings(string home, Func<ServiceClient?> service);
}

/// <summary>The required Help operations, resolved only where a loop's watch is built over a registry of processes.</summary>
internal static class CourseHelp
{
    private static ICourseHelp? _current;

    internal static ICourseHelp Current
    {
        get => _current ?? throw new InvalidOperationException("no course help operations are installed: Help installs them when the driver loads");
        set => _current = value;
    }
}
