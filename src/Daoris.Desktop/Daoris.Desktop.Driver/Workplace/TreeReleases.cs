namespace Daoris.Driver;

/// <summary>What ending one run-owned tree came to: whether it is gone, and the words either way.</summary>
/// <param name="Said">The removal's own sentence; for a tree that stays, the reason the record keeps (<see cref="GrownBranch.Kept"/>).</param>
public sealed record TreeReleased(string Repository, string Branch, bool Gone, string Said);

/// <summary>
/// The one act that ends a workflow run's trees: the clean-up's own removal (<see cref="SessionTrees.RemoveBranchAsync"/>), unforced, so a
/// tree whose work is uncommitted or on no branch of the person's stays, and its reason is written on its branch's record where the tree
/// list reads it. The run's <c>release</c> step takes it for the branches its run owns, and the sweep (<see cref="SweepAsync"/>) for the
/// branches whose part the run's tidy ended and those of runs that ended or whose record is gone; a tree no run owns is neither's.
/// </summary>
/// <param name="home">The Daoris home whose session branches and run records are read.</param>
public sealed class TreeReleases(string home)
{
    /// <summary>
    /// <paramref name="branch"/>'s tree and branch ended behind the proof; where either stays, why is kept on its record, and where it goes the
    /// reason is dropped.
    /// </summary>
    public async Task<TreeReleased> ReleaseAsync(string root, string repository, string branch, CancellationToken ct = default)
    {
        var removal = await new SessionTrees(home).RemoveBranchAsync(root, repository, branch, force: false, ct).ConfigureAwait(false);
        var grown = new SessionBranches(home);
        // One line, since a list and a step's words carry it; the removal's refusal names the commits it would lose on lines of their own.
        var said = string.Join(' ', removal.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        // A branch the record still stands for is read after the removal: a removed one is a trace, which keeps no reason.
        if (removal.Removed) grown.Keep(repository, branch, null);
        else if (grown.Of(repository, branch) is not null) grown.Keep(repository, branch, said);
        return new TreeReleased(repository, branch, removal.Removed, said);
    }

    /// <summary>
    /// The standing branches of <paramref name="repository"/> a run owns whose run is over for its tree, each ended behind the proof: the run's
    /// <c>tidy</c> ended the branch's part once its pull request merged (<see cref="TidiedParts"/>), whether or not its graph carries a
    /// <c>release</c>; or the run's record ended with no attempt unsettled, or none is kept here at all (<see cref="IWorkplaceRunTrees"/>). A record
    /// that does not read is unknown.
    /// </summary>
    /// <param name="inUse">The trees sessions still run or wait in, which stay; null where none is known. Where it cannot be read nothing is ended.</param>
    public async Task<IReadOnlyList<TreeReleased>> SweepAsync(
        string root, string repository, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null, CancellationToken ct = default)
    {
        var ownedHere = new SessionBranches(home).All()
            .Where(entry => entry.Run is not null && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (ownedHere.Count == 0) return [];
        var runs = WorkplaceRunTrees.Current;
        var landings = new LandedBranches(home).Entries();
        var due = ownedHere.Where(entry => TidiedParts.Ended(landings, entry) || runs.Due(home, entry.Run!)).ToList();

        if (due.Count == 0) return [];
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
        foreach (var owned in due)
        {
            if (trees.GetValueOrDefault(owned.Branch) is { } tree && busy.Any(path => TreeGit.SamePath(path, tree))) continue;
            done.Add(await ReleaseAsync(root, repository, owned.Branch, ct).ConfigureAwait(false));
        }

        return done;
    }
}
