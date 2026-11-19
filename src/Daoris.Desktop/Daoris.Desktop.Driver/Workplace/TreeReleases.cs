namespace Daoris.Driver;

/// <summary>What ending one run-owned tree came to: whether it is gone, and the words either way.</summary>
/// <param name="Said">The removal's own sentence; for a tree that stays, the reason the record keeps (<see cref="GrownBranch.Kept"/>).</param>
/// <param name="Freed">The bytes the removed tree held, read before it went; 0 where it stays.</param>
/// <param name="ByDays">Whether the tree was taken for its unused days (<see cref="TreeReleases.Unused"/>), not for its run being over.</param>
public sealed record TreeReleased(string Repository, string Branch, bool Gone, string Said, long Freed = 0, bool ByDays = false);

/// <summary>
/// The one act that ends a workflow run's trees: the clean-up's own removal (<see cref="SessionTrees.RemoveBranchAsync"/>), unforced, so a
/// tree whose work is uncommitted or on no branch of the person's stays, and its reason is written on its branch's record where the tree
/// list reads it. The run's <c>release</c> step takes it for the branches its run owns, and the sweep (<see cref="SweepAsync"/>) for the
/// branches whose part the run's tidy ended and those of runs that ended or whose record is gone; a tree no run owns is neither's, except by its days: a tree no run owns that no session runs or waits in and that nothing has used
/// for <see cref="Unused"/> goes too, its branch standing and holding every commit, and only where it holds no uncommitted work.
/// </summary>
/// <param name="home">The Daoris home whose session branches and run records are read.</param>
/// <param name="clock">The time unused trees are read against; null for now.</param>
public sealed class TreeReleases(string home, Func<DateTimeOffset>? clock = null)
{
    /// <summary>How long a tree no run owns and no session is in stands unused before the sweep takes it: two weeks.</summary>
    public static readonly TimeSpan Unused = TimeSpan.FromDays(14);

    /// <summary>
    /// <paramref name="branch"/>'s tree and branch ended behind the proof; where either stays, why is kept on its record, and where it goes the
    /// reason is dropped. <paramref name="treeOnly"/> ends the tree alone, its branch standing.
    /// </summary>
    public async Task<TreeReleased> ReleaseAsync(string root, string repository, string branch, CancellationToken ct = default, bool treeOnly = false)
    {
        var removal = await new SessionTrees(home).RemoveBranchAsync(root, repository, branch, force: false, ct, treeOnly: treeOnly).ConfigureAwait(false);
        var grown = new SessionBranches(home);
        // One line, since a list and a step's words carry it; the removal's refusal names the commits it would lose on lines of their own.
        var said = string.Join(' ', removal.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        // A branch the record still stands for is read after the removal: a removed one is a trace, which keeps no reason.
        if (removal.Removed) grown.Keep(repository, branch, null);
        else if (grown.Of(repository, branch) is not null) grown.Keep(repository, branch, said);
        return new TreeReleased(repository, branch, removal.Removed, said, removal.Freed, treeOnly);
    }

    /// <summary>
    /// The branches of <paramref name="repository"/> a run owns that are over for their tree, each ended behind the proof: the line holds the branch
    /// (<see cref="GrownBranch.Held"/>), its <c>tidy</c> ended the part (<see cref="TidiedParts"/>), or its run ended or is gone (<see cref="IWorkplaceRunTrees"/>).
    /// </summary>
    /// <param name="inUse">The trees sessions still run or wait in, which stay; null where none is known. Where it cannot be read nothing is ended.</param>
    public async Task<IReadOnlyList<TreeReleased>> SweepAsync(
        string root, string repository, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null, CancellationToken ct = default)
    {
        var here = new SessionBranches(home).All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)).ToList();
        var ownedHere = here.Where(entry => entry.Run is not null).ToList();
        // Trees no run owns are judged by their days, and only where the sessions in the repository's trees are known.
        var unowned = inUse is null ? [] : here.Where(entry => entry.Run is null).ToList();
        if (ownedHere.Count == 0 && unowned.Count == 0) return [];
        var runs = WorkplaceRunTrees.Current;
        var landings = new LandedBranches(home).Entries();
        var due = ownedHere.Where(entry => (entry.Held is not null && entry.GoneAt is null) || TidiedParts.Ended(landings, entry) || runs.Due(home, entry.Run!)).ToList();

        if (due.Count == 0 && unowned.Count == 0) return [];
        IReadOnlySet<string> busy = new HashSet<string>();
        if (inUse is not null)
        {
            try
            {
                busy = await inUse(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                return [];
            }
        }

        // No session starts in the repository's trees while they are ended; a start that holds them is tried again at the next look.
        using var hold = TreeLock.TryReplaying(home, root, repository, TreeLock.Tidying);
        if (hold is null) return [];
        var trees = await TreeWorktrees.WorktreesAsync(root, ct).ConfigureAwait(false);
        var done = new List<TreeReleased>();
        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        foreach (var owned in due)
        {
            if (trees.GetValueOrDefault(owned.Branch) is { } tree && busy.Any(path => TreeGit.SamePath(path, tree))) continue;
            var released = await ReleaseAsync(root, repository, owned.Branch, ct).ConfigureAwait(false);
            // An ended run's tree the proof keeps (commits no branch of the person's holds) goes by its days too, its branch standing.
            if (!released.Gone && inUse is not null && trees.GetValueOrDefault(owned.Branch) is { } kept && now - TreeUse.LastAt(kept) > Unused)
                released = await ReleaseAsync(root, repository, owned.Branch, ct, treeOnly: true).ConfigureAwait(false);
            done.Add(released);
        }

        foreach (var each in unowned)
        {
            if (trees.GetValueOrDefault(each.Branch) is not { } tree || busy.Any(path => TreeGit.SamePath(path, tree))) continue;
            if (now - TreeUse.LastAt(tree) <= Unused) continue;
            done.Add(await ReleaseAsync(root, repository, each.Branch, ct, treeOnly: true).ConfigureAwait(false));
        }

        return done;
    }
}
