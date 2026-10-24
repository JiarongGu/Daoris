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
    /// its occasion, its tree gone, its work landed (a landing recorded for it since it was owed, FABRIC-CLOSE1h18, or nothing in its
    /// tree that no branch of the person's, nor an older landing's squash, holds, where no automatic landing holds that branch's push
    /// back), or, under <c>steps</c>, no next step of its chain left to hold.
    /// </summary>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f): under a named workflow, whether its version still reads at the occasion. One that cannot be
    /// read here closes nothing, since it may read once it can. Null reads the rule live, as before.
    /// </param>
    /// <param name="pushHeld">
    /// The sessions whose automatic landing holds its push back (FABRIC-CLOSE1h15, <see cref="PushHeld"/>): a landing whose push one of
    /// them holds has not landed. Null or empty, a landing recorded since the due was owed closes it.
    /// </param>
    public static async Task<string?> ClosesAsync(
        OpinionDue due, QuestView? quest, IReadOnlyList<QuestView> chain, DriverConfig config, LandedBranches landed, CancellationToken ct,
        WorkflowProcess? process = null, IReadOnlyCollection<string>? pushHeld = null)
    {
        // FABRIC-CLOSE1h (§E5): in a closure graph its submission is the done it waits on. CHATFLOW1d: a chat run's has no quest, and closes
        // `landed`, `gone` or `off`, never `undone`.
        if (due.Run is null && !QuestResults.Judged(quest)) return OpinionDueClosed.Undone;
        var reads = process is null
            ? OpinionRules.Resolve(config, due.Repository, due.Workspace) is { Rule: { IsNone: false } rule } && rule.On.Contains(due.Occasion)
            : process.Problem is not null || process.Unread is not null || OpinionGate.Reads(process.Opinion, process.OpinionCannot, due.Occasion);
        if (!reads) return OpinionDueClosed.Off;

        if (TreeFolders.TreeGone(due.Tree)) return OpinionDueClosed.Gone;
        // FABRIC-CLOSE1h15: a branch made whose push is held back has not landed, so the opinion that holds that push stays owed and the
        // look asks it, as the gate says; it closes once the push lands at the landing's tip, or once no automatic landing holds it back.
        // FABRIC-CLOSE1h18: and its record lands it only where it was made since the due was owed. One older, a session whose words went
        // on after its landing (D137), lands only the work it held then, so its tree is read below as any session's is.
        var landing = landed.Landing(due.Session);
        var held = landing is not null && HoldsPush(landing, pushHeld);
        if (landing is not null && !held && MadeSince(landing, due)) return OpinionDueClosed.Landed;
        if (due.Occasion == OpinionRules.Steps
            && !chain.Any(step => string.Equals(step.Parent, due.Quest, StringComparison.OrdinalIgnoreCase) && step.Status == "Open"))
        {
            return OpinionDueClosed.Past;
        }

        // Its tree's commits are all on the branch made, so git alone would call it landed.
        if (held) return null;
        var unlanded = await TreeFollows.UnlandedInTreeAsync(due.Tree, "HEAD", ct).ConfigureAwait(false);
        if (unlanded == 0) return OpinionDueClosed.Landed;

        // FABRIC-CLOSE1h18: an older landing that squashed the tree's work holds it while the tree stands at the commit it squashed, or
        // behind it (the record's own word, PROCESS1b-ii), though no branch of the person's holds those commits: so a due owed after a
        // squashed landing whose push was held back (FABRIC-CLOSE1h15) closes once that push lands.
        return unlanded > 0 && landing?.Squashed is { } squashed
               && await TreeRefs.IsAncestorAsync(due.Tree, "HEAD", squashed, ct).ConfigureAwait(false)
            ? OpinionDueClosed.Landed
            : null;
    }

    /// <summary>
    /// The sessions whose automatic landing made its branch and holds its push back (FABRIC-CLOSE1h15): each open entry whose last try is
    /// <c>push-held</c>, read once for a look's opinions. A due list that does not read is none.
    /// </summary>
    public static IReadOnlySet<string> PushHeld(string home) =>
        new AutoLandings(home).Open().Where(entry => entry.Last?.Code == AutoLandingCode.PushHeld).Select(entry => entry.Session)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="landing"/> was made since <paramref name="due"/> was owed (FABRIC-CLOSE1h18): its session's work accepted onto
    /// it at or after the due's time, by the landing itself or by the advance the session's later done made (LAND2c), as the automatic
    /// landing reads a landing made since its entry's done. One older lands only the work it held then.
    /// </summary>
    internal static bool MadeSince(LandedBranch landing, OpinionDue due) => landing.AcceptedAt(due.Session) >= due.At;

    /// <summary>
    /// Whether <paramref name="landing"/>'s push is held back: a branch that stands, not pushed at its tip, whose push one of
    /// <paramref name="pushHeld"/> holds, the session that made it or one whose done moved it on (CLOSE1h10), since the hand-on pushes
    /// the whole.
    /// </summary>
    private static bool HoldsPush(LandedBranch landing, IReadOnlyCollection<string>? pushHeld) =>
        pushHeld is { Count: > 0 } && landing.GoneAt is null
        && !(landing.Pushed && string.Equals(landing.PushedTip, landing.Tip, StringComparison.OrdinalIgnoreCase))
        && pushHeld.Any(landing.Names);
}
