namespace Daoris.Driver;

/// <summary>The reporting line a landing's wait climbs past its deadline (<see cref="LandRetries"/>), as the run's step and the conversation say it.</summary>
internal interface ILandingWaitLine
{
    /// <summary>
    /// The line above a landing at <paramref name="repository"/> in <paramref name="workspace"/> on <paramref name="home"/>, climbed for its
    /// <c>accept</c>, said in one line.
    /// </summary>
    string Climb(string home, string repository, string? workspace);
}

/// <summary>The line a landing's wait climbs: the reporting line is Roles', above Workplace, which installs it when the driver loads.</summary>
internal static class LandingWaitLines
{
    private static ILandingWaitLine? _current;

    internal static ILandingWaitLine Current
    {
        get => _current ?? throw new InvalidOperationException("no landing's reporting line is installed: Roles installs it when the driver loads");
        set => _current = value;
    }
}
