namespace Daoris.Driver;

/// <summary>The rows in words — the terminal's lines, and the sentences the screen's rows say in their own catalogue.</summary>
public static class SyncWords
{
    public static string Describe(LinePull pull) => $"{pull.Repository}  {pull.Line ?? "(no line)"}  " + pull.Kind switch
    {
        PullKind.FastForward => $"fast-forwards {pull.Commits} commit(s) to `origin/{pull.Line}`",
        PullKind.UpToDate => $"up to date with `origin/{pull.Line}`",
        PullKind.Ahead => $"{pull.Commits} commit(s) `origin/{pull.Line}` does not have: nothing to pull, and Daoris never pushes",
        PullKind.Diverged => $"it and `origin/{pull.Line}` each hold commits the other lacks: only a fast-forward is Daoris's",
        PullKind.Dirty => "the repository's own checkout is on it with uncommitted work, so it stays",
        PullKind.CheckedOut => "checked out in another working tree, which Daoris does not move",
        PullKind.NoRemote => $"no `origin/{pull.Line}` here to pull from",
        PullKind.NoLocal => $"no local `{pull.Line}` to move: the line is read from `origin/{pull.Line}`",
        PullKind.NoLine => "no line is set and git names none",
        _ => $"git could not tell: {pull.Detail}",
    } + (pull.Fetch is not null ? " (not fetched)" : "");

    /// <summary>
    /// Said once, before the rows (WSR7): how many repositories were not fetched, grouped by git's reason; that each is
    /// judged against what origin said when it was last fetched here, and when that was; and what the git Daoris runs
    /// needs to reach an origin over SSH or HTTPS. Nothing where every repository fetched.
    /// </summary>
    /// <remarks>
    /// The person's own Git client may reach origin while the git on the path cannot: one carries its own git and ssh,
    /// and Daoris runs the one the path finds. So the words name what that git needs, and no product.
    /// </remarks>
    public static IReadOnlyList<string> NotFetched(IReadOnlyList<LinePull> lines, DateTimeOffset now)
    {
        var failed = lines.Where(pull => pull.Fetch is not null).ToList();
        if (failed.Count == 0) return [];
        var said = new List<string>
        {
            $"trees: {failed.Count} of {lines.Count} repositories were not fetched, so each is judged against what origin said when it was last fetched here:",
        };
        foreach (var reason in failed.GroupBy(pull => pull.Fetch!, StringComparer.Ordinal))
        {
            said.Add($"  {reason.Key} ({reason.Count()}): "
                + string.Join(", ", reason.Select(pull => $"{pull.Repository} ({LastFetched(pull.LastFetch, now)})")));
        }

        if (failed.Any(pull => pull.Reach == "ssh")) said.Add($"  {Reaching("ssh")}");
        if (failed.Any(pull => pull.Reach is "https" or "http")) said.Add($"  {Reaching("https")}");
        return said;
    }

    /// <summary>
    /// What the git Daoris runs needs to reach an origin by <paramref name="reach"/> (WSR7): over SSH a key, over HTTPS a helper that
    /// answers without asking; null for any other way. A landing whose fetch failed says it too (LANDSYNC1).
    /// </summary>
    public static string? Reaching(string? reach) => reach switch
    {
        "ssh" => "To reach an origin over SSH, the git Daoris runs (the one on the path) needs a key its own ssh reads, "
            + "or `core.sshCommand` naming an ssh that has one: a Git client with its own git and ssh does not lend them.",
        "https" or "http" => "To reach an origin over HTTPS, the git Daoris runs needs a credential helper that answers without asking, "
            + "since Daoris's fetch never prompts.",
        _ => null,
    };

    /// <summary>When a repository last heard from origin, in words: `last fetched 2 days ago`, or `never fetched`.</summary>
    public static string LastFetched(DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is not { } then) return "never fetched";
        var minutes = (int)Math.Max(0, (now - then).TotalMinutes);
        static string Of(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")} ago";
        return "last fetched " + (minutes < 1 ? "just now"
            : minutes < 60 ? Of(minutes, "minute")
            : minutes < 60 * 24 ? Of(minutes / 60, "hour")
            : Of(minutes / (60 * 24), "day"));
    }

    /// <summary>
    /// A branch's row: what the press does with it, and on a landed branch what its pull request's answer was and why it does not
    /// clear it, or why nothing was asked (PLUGHOOK1c, design §2.5), as the clean-up's rows say it.
    /// </summary>
    public static string Describe(RebaseItem item) => Replay(item)
        + (item.Landed ? PullRequestWords.Row(item.State, item.StateCode, item.AskFailed, item.NotAsked) : "");

    // What the line holds is said beside what the replay carries, in the press's own words (LANDSYNC3b; BranchReplays' answer).
    private static string Replay(RebaseItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        RebaseKind.Replay when item.Commits == 0 => $"moves onto `{item.Onto}`: it holds nothing of its own beyond work that reached the line"
            + (item.LeftOut > 0 ? $" ({item.LeftOut} commit(s) the line holds left out)" : ""),
        RebaseKind.Replay => $"replays {item.Commits} commit(s) of its own onto `{item.Onto}`" + item.CutBy switch
        {
            CutBy.Record => item.GrewFrom is { } from ? $", after `{from}`'s work, which reached the line" : ", after the work it grew from, which reached the line",
            CutBy.Content => ", after its newest commit whose work reads on the line",
            _ => "",
        } + (item.LeftOut > 0 ? $", leaving out {item.LeftOut} the line holds" : ""),
        RebaseKind.UpToDate => $"already on `{item.Onto}`",
        RebaseKind.InUse => "a session still running or waiting holds its tree",
        RebaseKind.Dirty => "its tree has uncommitted work",
        RebaseKind.CheckedOut => "checked out in a working tree that is not a session's own",
        RebaseKind.Pushed => "on its remote: replaying it would need a force push",
        RebaseKind.Waits => (item.GrewFrom is { } parent ? $"it grew from `{parent}`" : "it grew from a commit beyond the line")
            + ", whose work has not reached the line yet",
        _ => $"git could not tell: {item.Detail}",
    };

    /// <summary>The repositories a look or a press did not take (WSR7, D112), in one line, and how to include them; null for none.</summary>
    public static string? Apart(IReadOnlyList<SyncRepository> apart) => apart.Count == 0
        ? null
        : $"trees: not looked at, since they hold no branch of Daoris's ({apart.Count}): "
          + string.Join(", ", apart.Select(each => each.Repository))
          + ". `--all` includes them, and `--repository <name>` one of them.";
}
