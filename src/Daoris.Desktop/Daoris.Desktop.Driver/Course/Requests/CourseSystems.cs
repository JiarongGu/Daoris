namespace Daoris.Driver;

/// <summary>
/// What a session's <c>system_plan</c> asks of the plugins: the judge that plans its changes through its system's adapter, over the home
/// and the loop's service. Plugins installs it when the driver loads.
/// </summary>
internal interface ICourseSystems
{
    /// <summary>The judge of a <c>system_plan</c> request: what this loop answers beside it.</summary>
    Func<SessionRequest, CancellationToken, Task<SystemPlanAnswer>> Plan(string home, Func<ServiceClient?> service);
}

/// <summary>The required plugin operations, resolved only where a loop's watch is built over a registry of processes.</summary>
internal static class CourseSystems
{
    private static ICourseSystems? _current;

    internal static ICourseSystems Current
    {
        get => _current ?? throw new InvalidOperationException("no course systems operations are installed: Plugins installs them when the driver loads");
        set => _current = value;
    }
}
