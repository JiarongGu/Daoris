namespace Daoris.Driver;

/// <summary>
/// The families of Daoris's acts a person may grant Ask Daoris in restricted mode (<see cref="ConfiguredAccess.Acts"/>): each names the
/// driver's routes it covers, and a proved press of one of them runs with no card, as the person's own, where its family is granted.
/// A route in no family is asked, as every press is by default.
/// </summary>
public static class ActFamilies
{
    /// <summary>An ask's or a quest's pause and its resume, a repository's hold and its release, and a session's stop.</summary>
    public const string Pause = "pause";

    /// <summary>The families this build grants, in the page's order.</summary>
    public static readonly IReadOnlyList<string> Built = [Pause];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Routes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Pause] = ["WORK_PAUSE", "WORK_RESUME", "SET_HOLD", "STOP_SESSION"],
    };

    /// <summary>The driver's routes <paramref name="family"/> covers; none for a family this build does not know.</summary>
    public static IReadOnlyList<string> RoutesOf(string family) => Routes.GetValueOrDefault(family) ?? [];

    /// <summary>Whether one of <paramref name="granted"/> covers the driver's route <paramref name="type"/>.</summary>
    public static bool Covers(IReadOnlyList<string> granted, string type) =>
        granted.Any(family => RoutesOf(family).Contains(type, StringComparer.Ordinal));
}
