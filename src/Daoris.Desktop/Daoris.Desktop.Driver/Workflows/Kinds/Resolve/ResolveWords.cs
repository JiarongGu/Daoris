namespace Daoris.Driver;

/// <summary>A <c>resolve</c>'s words: the ask its resolver is told, and the wait the card and the run view say under the step.</summary>
public static class ResolveWords
{
    /// <summary>
    /// The ask: the target, the stop's commit, each file with its hunks, what the replay settled, and that only these files are to change,
    /// staged, with no commit and no <c>rebase --continue</c>.
    /// </summary>
    public static string Ask(string target, ResolveStopAt at, int ask, int bound)
    {
        var files = string.Join("\n\n", at.Files.Select(file =>
            $"`{file.Path}` ({Hunks(file)}):\n" + string.Join("\n\n", file.Hunks.Select(hunk => $"```\n{hunk}\n```"))));
        return $"Daoris is replaying your work onto `{target}`, and it stopped at `{TextBounds.ShortCommit(at.Commit, 7)}` “{at.Subject}” "
            + $"(try {ask} of {bound}). Settle these files in your tree, and only these:\n\n{files}\n\n"
            + "Stage them when they are settled. Change no other file, make no commit and do not run `git rebase --continue`: your tree stands "
            + "mid-rebase on a detached head, and Daoris goes on with the replay once your turn ends.";
    }

    /// <summary>The ask's wait, said: "Waits on its session to settle `src/a.cs` (2 hunks): try 1 of 3, until 15:02."</summary>
    public static string Waits(ResolveStopAt at, int ask, int bound, string until) =>
        $"Waits on its session to settle {Listed(at)}: try {ask} of {bound}, until {until}.";

    /// <summary>Past the stop's bound: the person's to settle.</summary>
    public static string Spent(NodeStop stop, int bound) =>
        $"Waits on you: {string.Join(", ", stop.Paths.Take(3).Select(path => $"`{path}`"))} could not be settled in {bound} asks.";

    private static string Listed(ResolveStopAt at) =>
        string.Join(", ", at.Files.Take(3).Select(file => $"`{file.Path}` ({Hunks(file)})")) + (at.Files.Count > 3 ? $" and {at.Files.Count - 3} more" : "");

    private static string Hunks(ResolveFile file) => file.Hunks.Count == 1 ? "1 hunk" : $"{file.Hunks.Count} hunks";
}
