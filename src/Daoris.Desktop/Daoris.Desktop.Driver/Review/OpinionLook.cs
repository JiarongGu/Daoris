namespace Daoris.Driver;

/// <summary>
/// The second opinions owed by themselves: what a driven record's conclusion
/// makes owed, and why the look closes one.
/// </summary>
public static class OpinionLook
{
    /// <summary>
    /// A driven record that just concluded, read against the opinion rule standing for its repository: a done in a tree of
    /// its own, under a rule whose occasions read it, is owed one at each occasion the rule names. Never throws: the record has
    /// concluded, and an opinion not owed leaves the person's <i>Ask now</i>.
    /// </summary>
    /// <param name="process">
    /// Its run's process: under a named workflow, its version's opinion step says the occasions, one that cannot start
    /// among them, so it sits where it is owed. Null reads the rule live.
    /// </param>
    /// <returns>The occasions it is owed at; empty where none.</returns>
    public static IReadOnlyList<string> Concluded(
        string home, DriverConfig config, string session, QuestView quest, string? status, string state, string tree, DateTimeOffset? at = null,
        IWorkplaceProcess? process = null)
    {
        try
        {
            if (status != "Done" || SessionStates.IsParked(state)) return [];
            var trees = new SessionTrees(home);
            if (!trees.Holds(tree)) return [];
            var (workspace, repository) = trees.Owner(tree);
            var resolved = process is null ? OpinionRules.Resolve(config, repository, workspace) : process.Opinion;
            if (resolved is null || (resolved.Rule.IsNone && process?.OpinionCannot is null)) return [];
            var rule = resolved.Rule;

            var dues = new OpinionDues(home);
            foreach (var occasion in rule.On)
            {
                dues.Due(new OpinionDue(session, quest.Id, repository, workspace, tree, occasion, at ?? DateTimeOffset.UtcNow));
            }

            return rule.On;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// Why an opinion owed is closed now, or null where it is still owed: its quest not done, its rule not reading at its occasion, its
    /// tree gone, its work landed (a landing recorded for it after it fell due, or nothing in its tree that no branch of the person's,
    /// nor a squash or a replay, holds), or, under <c>steps</c>, no next step of its chain left to hold.
    /// </summary>
    /// <param name="process">
    /// Its run's process: under a named workflow, whether its version still reads at the occasion. One that cannot be
    /// read here closes nothing, since it may read once it can. Null reads the rule live.
    /// </param>
    public static async Task<string?> ClosesAsync(
        OpinionDue due, QuestView? quest, IReadOnlyList<QuestView> chain, DriverConfig config, LandedBranches landed, CancellationToken ct,
        SessionTrees trees, IWorkplaceProcess? process = null)
    {
        // In a closure graph its submission is the done it waits on.
        if (!QuestResults.Judged(quest)) return OpinionDueClosed.Undone;
        var reads = process is null
            ? OpinionRules.Resolve(config, due.Repository, due.Workspace) is { Rule: { IsNone: false } rule } && rule.On.Contains(due.Occasion)
            : process.Problem is not null || process.Unread is not null || OpinionGate.Reads(process.Opinion, process.OpinionCannot, due.Occasion);
        if (!reads) return OpinionDueClosed.Off;

        if (TreeFolders.TreeGone(due.Tree)) return OpinionDueClosed.Gone;
        // Its record lands it only where it is made after the due fell owed. One older, a session whose words went
        // on after its landing, lands only the work it held then, so its tree is read below as any session's is.
        var landing = landed.Landing(due.Session);
        if (landing is not null && MadeSince(landing, due)) return OpinionDueClosed.Landed;
        if (due.Occasion == OpinionRules.Steps
            && !chain.Any(step => string.Equals(step.Parent, due.Quest, StringComparison.OrdinalIgnoreCase) && step.Status == "Open"))
        {
            return OpinionDueClosed.Past;
        }

        var work = await WorkAsync(due.Tree, trees, ct).ConfigureAwait(false);
        if (work is { Commits: 0 }) return OpinionDueClosed.Landed;

        // An older landing that squashed the tree's work holds it while the tree stands at or behind the commit it squashed.
        if (work is { Commits: > 0 } && landing?.Squashed is { } squashed
            && await TreeRefs.IsAncestorAsync(due.Tree, "HEAD", squashed, ct).ConfigureAwait(false))
        {
            return OpinionDueClosed.Landed;
        }

        return work is { Landed: true } ? OpinionDueClosed.Landed : null;
    }

    /// <summary>The tree's work as the review reads it, kept while what it rests on stands (<see cref="TreeReadMemo"/>): no git where nothing moved.</summary>
    private static Task<TreeWork?> WorkAsync(string tree, SessionTrees trees, CancellationToken ct) =>
        TreeReadMemo.Of(trees.Context.Home).Begin(trees).WorkAsync(tree, ct);

    /// <summary>
    /// Why an opinion owed on a chat run is closed now, or null where it is still owed: <c>off</c> where
    /// its run's process does not read at its occasion, <c>gone</c> with its tree, and <c>landed</c> once a landing of its line is recorded
    /// after it fell owed or its tree holds nothing the line does not. Never <c>undone</c>: a chat run serves no quest whose done it waits on.
    /// </summary>
    public static async Task<string?> RunClosesAsync(OpinionDue due, LandedBranches landed, IWorkplaceProcess process, CancellationToken ct, SessionTrees trees)
    {
        var reads = process.Problem is not null || process.Unread is not null || OpinionGate.Reads(process.Opinion, process.OpinionCannot, due.Occasion);
        if (!reads) return OpinionDueClosed.Off;
        if (TreeFolders.TreeGone(due.Tree)) return OpinionDueClosed.Gone;
        if (landed.Landing(due.Session) is { } landing && MadeSince(landing, due)) return OpinionDueClosed.Landed;
        return await WorkAsync(due.Tree, trees, ct).ConfigureAwait(false) is { Commits: 0 } or { Landed: true } ? OpinionDueClosed.Landed : null;
    }

    /// <summary>
    /// Whether <paramref name="landing"/> is made after <paramref name="due"/> fell owed: its session's work accepted onto
    /// it at or after the due's time, by the landing itself or by the advance the session's later done made, as the automatic
    /// landing reads a landing made after its entry's done. One older lands only the work it held then.
    /// </summary>
    internal static bool MadeSince(LandedBranch landing, OpinionDue due) => landing.AcceptedAt(due.Session) >= due.At;
}
