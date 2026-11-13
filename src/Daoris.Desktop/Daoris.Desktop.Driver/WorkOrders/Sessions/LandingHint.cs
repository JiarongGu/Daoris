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

    private static string Door(SessionGrouping row) =>
        row.Waits?.FirstOrDefault(wait => wait.Why == PersonWaits.Accept) is { } accept
            ? $"daoris workflow accept {accept.Run} {accept.Node}"
            : SessionBranchDoors.OpenRun(row.Session);
}
