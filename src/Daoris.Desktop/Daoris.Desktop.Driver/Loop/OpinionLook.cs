namespace Daoris.Driver;

/// <summary>
/// The second opinions owed by themselves (XAGENT1f, D155 point 2; the second-agent design §2.1): what a driven record's conclusion
/// makes owed, and why the look closes one.
/// </summary>
public static class OpinionLook
{
    /// <summary>
    /// A driven record that just concluded, read against the opinion rule standing for its repository (§2.1): a done in a tree of
    /// its own, under a rule whose occasions read it, is owed one at each occasion the rule names. Never throws: the record has
    /// concluded, and an opinion not owed leaves the person's <i>Ask now</i>.
    /// </summary>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f): under a named workflow, its version's opinion step says the occasions, one that cannot start
    /// among them, so it sits where it is owed. Null reads the rule live, as before.
    /// </param>
    /// <returns>The occasions it is owed at; empty where none.</returns>
    public static IReadOnlyList<string> Concluded(
        string home, DriverConfig config, string session, QuestView quest, string? status, string state, string tree, DateTimeOffset? at = null,
        WorkflowProcess? process = null)
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
    /// Why an opinion owed is closed now, or null where it is still owed: its quest no longer done, its rule no longer reading at
    /// its occasion, its tree gone, its work landed (a landing recorded for it, or nothing in its tree that no branch of the
    /// person's holds), or, under <c>steps</c>, no next step of its chain left to hold.
    /// </summary>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f): under a named workflow, whether its version still reads at the occasion. One that cannot be
    /// read here closes nothing, since it may read once it can. Null reads the rule live, as before.
    /// </param>
    public static async Task<string?> ClosesAsync(
        OpinionDue due, QuestView? quest, IReadOnlyList<QuestView> chain, DriverConfig config, LandedBranches landed, CancellationToken ct,
        WorkflowProcess? process = null)
    {
        // FABRIC-CLOSE1h (§E5): in a closure graph its submission is the done it waits on.
        if (!QuestResults.Judged(quest)) return OpinionDueClosed.Undone;
        var reads = process is null
            ? OpinionRules.Resolve(config, due.Repository, due.Workspace) is { Rule: { IsNone: false } rule } && rule.On.Contains(due.Occasion)
            : process.Problem is not null || process.Unread is not null || OpinionGate.Reads(process.Opinion, process.OpinionCannot, due.Occasion);
        if (!reads) return OpinionDueClosed.Off;

        if (TreeFolders.TreeGone(due.Tree)) return OpinionDueClosed.Gone;
        if (landed.Landing(due.Session) is { } landing && landing.Names(due.Session)) return OpinionDueClosed.Landed;
        if (due.Occasion == OpinionRules.Steps
            && !chain.Any(step => string.Equals(step.Parent, due.Quest, StringComparison.OrdinalIgnoreCase) && step.Status == "Open"))
        {
            return OpinionDueClosed.Past;
        }

        return await TreeFollows.UnlandedInTreeAsync(due.Tree, "HEAD", ct).ConfigureAwait(false) == 0 ? OpinionDueClosed.Landed : null;
    }
}
