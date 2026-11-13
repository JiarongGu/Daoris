namespace Daoris.Driver;

/// <summary>
/// What the terminal's listing says of a session's unlanded commits: the verb that lands them. Work lands by its run's Accept, so the
/// hint names <c>workflow accept &lt;run&gt; &lt;node&gt;</c> where the run waits for it, and opens the run where none does;
/// <c>trees land</c> presses nothing and is never offered.
/// </summary>
internal static class LandingHint
{
    /// <summary>The line for a row <paramref name="lands"/> offers, in the terminal's words.</summary>
    public static string Of(SessionGrouping row, LandOffer lands) =>
        $"{lands.Commits} commit{(lands.Commits == 1 ? "" : "s")} on {lands.Branch} in its tree {lands.Tree}, not landed: "
        + $"its run's Accept lands it; {Door(row)}";

    /// <summary>The verb that opens the run a session follows, where its Accept is given; the one way the clean-up's list says it too.</summary>
    public static string OpenRun(string session) => $"daoris workflow run --session {session}";

    private static string Door(SessionGrouping row) =>
        row.Waits?.FirstOrDefault(wait => wait.Why == PersonWaits.Accept) is { } accept
            ? $"daoris workflow accept {accept.Run} {accept.Node}"
            : OpenRun(row.Session);
}
