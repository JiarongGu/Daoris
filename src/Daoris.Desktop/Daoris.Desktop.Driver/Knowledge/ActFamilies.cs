namespace Daoris.Driver;

/// <summary>
/// The families of Daoris's acts a person may grant Ask Daoris in restricted mode (<see cref="ConfiguredAccess.Acts"/>), the one shape
/// configured access grants: each names the driver's routes it covers, and a proved press of one of them runs with no card, as the
/// person's own, where its family is granted. A route in no family is asked, as every press is by default; a look is a read and
/// asks no grant.
/// </summary>
public static class ActFamilies
{
    /// <summary>An ask's or a quest's pause and its resume, a repository's hold and its release, and a session's stop.</summary>
    public const string Pause = "pause";

    /// <summary>The clean-up's press, <c>daoris tree clean --yes</c>: the branches and trees its judges clear, nothing forced.</summary>
    public const string CleanUp = "clean-up";

    /// <summary>One session branch discarded, <c>daoris tree remove &lt;session|branch&gt;</c>: kept where its judge keeps it.</summary>
    public const string Discard = "discard";

    /// <summary>
    /// The chosen settings: <c>daoris settings notify|cap|cooloff|quiet</c> and a session language, <c>daoris place language</c>;
    /// never the mode, the grants, an account or a safeguard.
    /// </summary>
    public const string Settings = "settings";

    /// <summary>The families this build grants, in the page's order.</summary>
    public static readonly IReadOnlyList<string> Built = [Pause, CleanUp, Discard, Settings];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Routes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Pause] = ["WORK_PAUSE", "WORK_RESUME", "SET_HOLD", "STOP_SESSION"],
        [CleanUp] = ["SWEEP"],
        [Discard] = ["DISCARD_SESSION_BRANCH"],
        [Settings] = ["SET_NOTIFY", "SET_LANGUAGE", "SET_CAP", "SET_COOLOFF", "SET_QUIET"],
    };

    /// <summary>The driver's routes <paramref name="family"/> covers; none for a family this build does not know.</summary>
    public static IReadOnlyList<string> RoutesOf(string family) => Routes.GetValueOrDefault(family) ?? [];

    /// <summary>Whether one of <paramref name="granted"/> covers the driver's route <paramref name="type"/>.</summary>
    public static bool Covers(IReadOnlyList<string> granted, string type) =>
        granted.Any(family => RoutesOf(family).Contains(type, StringComparer.Ordinal));
}
