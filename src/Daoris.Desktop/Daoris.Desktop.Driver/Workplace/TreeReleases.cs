namespace Daoris.Driver;

/// <summary>What ending one run-owned tree came to: whether it is gone, and the words either way.</summary>
/// <param name="Said">The removal's own sentence; for a tree that stays, the reason the record keeps (<see cref="GrownBranch.Kept"/>).</param>
public sealed record TreeReleased(string Repository, string Branch, bool Gone, string Said);

/// <summary>
/// The one act that ends a workflow run's trees: the clean-up's own removal (<see cref="SessionTrees.RemoveBranchAsync"/>), unforced, so a
/// tree whose work is uncommitted or on no branch of the person's stays, and its reason is written on its branch's record where the tree
/// list reads it. The run's <c>release</c> step takes it for the branches its run owns, and the sweep (<see cref="SweepAsync"/>) for the
/// branches of runs that ended or whose record is gone; a tree no run owns is neither's.
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
        // A branch the record still stands for is read after the removal: a removed one is a trace, which keeps no reason.
        if (removal.Removed) grown.Keep(repository, branch, null);
        else if (grown.Of(repository, branch) is not null) grown.Keep(repository, branch, removal.Message);
        return new TreeReleased(repository, branch, removal.Removed, removal.Message);
    }

    /// <summary>
    /// The standing branches of <paramref name="repository"/> a run owns whose run is over for its tree, each ended behind the proof: the run's
    /// record ended with no attempt unsettled (<see cref="StepRefs.Releases"/>), or none kept here at all.
    /// </summary>
    public async Task<IReadOnlyList<TreeReleased>> SweepAsync(string root, string repository, CancellationToken ct = default)
    {
        var records = new NodeRecordFiles(home);
        var done = new List<TreeReleased>();
        foreach (var owned in new SessionBranches(home).All()
                     .Where(entry => entry.Run is not null && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            // A record that is not kept at all is a run whose record is gone; one that does not read is unknown, and ends nothing.
            var unit = RunUnits.IdOf(owned.Run!);
            if (File.Exists(NodeRecordFiles.PathOf(home, unit)) && !(records.Read(unit) is { } record && StepRefs.Releases(record))) continue;
            done.Add(await ReleaseAsync(root, repository, owned.Branch, ct).ConfigureAwait(false));
        }

        return done;
    }
}
