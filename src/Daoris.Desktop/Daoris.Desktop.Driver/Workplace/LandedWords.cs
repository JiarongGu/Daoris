using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>A landed branch's row in words — the terminal's line, and the sentence the screen's row says in its own catalogue.</summary>
public static class LandedWords
{
    /// <summary>
    /// The row: what proved it or keeps it, and on a row that is kept what its pull request's answer was and why it does not
    /// clear the branch, or why nothing is asked. A row that goes says what proved it, and no more.
    /// </summary>
    public static string Describe(LandedItem item) => Proof(item)
        + (item.Removable ? "" : PullRequestWords.Row(item.State, item.StateCode, item.AskFailed, item.NotAsked));

    private static string Proof(LandedItem item) => $"{item.Repository}  {item.Branch}  " + Clause(item);

    /// <summary>What proved a row or keeps it, with no repository or branch before it: a refusal says it after its own subject.</summary>
    public static string Clause(LandedItem item) => item.Kind switch
    {
        LandedKind.OnLine => $"its pull request reached `{item.Where}`: every file it changed reads there as it left it",
        LandedKind.Merged => $"merged into `{item.Where}`",
        LandedKind.Inside => $"inside `{item.Where}`, whose work is on the line",
        LandedKind.PullRequest => "its pull request completed" + (item.State?.How is { } how ? $" by {how}" : "")
            + (item.State?.Plugin is { } plugin ? $", as `{plugin}` answered," : "") + $" and its merge commit is on `{item.Where}`",
        LandedKind.Differs => (item.Files.Count == 1 ? "1 file still differs" : $"{item.Files.Count} files still differ")
            + $" on the line: {string.Join(", ", item.Files.Take(3))}" + (item.Files.Count > 3 ? $" and {item.Files.Count - 3} more" : ""),
        LandedKind.CheckedOut => "checked out in a working tree",
        LandedKind.AheadOfRemote => $"{item.Commits} commit(s) its remote does not have",
        LandedKind.LeanedOn => $"session branch `{item.Where}` still leans on it",
        _ => $"git could not tell: {item.Detail}",
    };

    /// <summary>
    /// How long a row the line does not hold has stood, and whether its quest is closed: <c>; landed 29 hours ago, and
    /// its quest #q2 is closed</c>, to say after <see cref="Describe"/>. Nothing where the line holds it (it goes by its proof) or the
    /// record gave no moment.
    /// </summary>
    public static string Standing(LandedItem item, DateTimeOffset now)
    {
        if (item.Removable || item.LandedAt is not { } at) return "";
        var age = $"; landed {LandedStanding.Age(now - at)} ago";
        return item.QuestClosed && item.Quest is { Length: > 0 } quest ? $"{age}, and its quest #{quest} is closed" : age;
    }

    /// <summary>
    /// The door that withdraws a row whose files still differ on the line (<see cref="LandedWithdrawals"/>), said under it: the
    /// one way past the clean-up, which keeps such a branch for ever. Null for a row the clean-up already takes or keeps for another reason.
    /// </summary>
    public static string? Door(LandedItem item) => item.Kind == LandedKind.Differs
        ? $"if the line will never hold its work: `daoris tree remove {item.Branch} --repository {item.Repository} --why \"…\" [--plan]` "
            + "withdraws it, keeping its commits on a recovery ref"
        : null;
}
