namespace Daoris.Driver;

/// <summary>
/// What the inventory is read from, gathered (FABRIC-MILESTONE1a): the home's records as they stand, the trees and the folders moved
/// aside, and each repository's recovery refs. Read-only: no record is written, no lease taken, and a repository git cannot read is
/// said, never taken for having none (D57).
/// </summary>
public static class WorkInventories
{
    /// <summary>The facts of this home. <paramref name="roots"/> names each repository's checkout; none is read for a repository without one.</summary>
    /// <param name="inUse">The trees a session still running or waiting holds, from the service.</param>
    /// <param name="unread">Said where a repository's recovery refs were not read, and why.</param>
    public static async Task<InventoryFacts> GatherAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ICollection<string> unread,
        CancellationToken ct = default, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var sessionTrees = new SessionTrees(home);
        // CLEANUP1, after review: a folder moved aside past its date that the purge keeps, read as the purge reads it, so it is said to the
        // person. Only past its date, where the purge has decided: a folder it deletes is walked once more before the next pass takes it.
        // Re-check: one whose delete the purge could not finish, which no read repeats, as the purge kept it.
        var failed = sessionTrees.SetAsideFailed();
        var trees = (await sessionTrees.ListAsync(ct).ConfigureAwait(false))
            .Select(tree => tree.MovedAside is { } moved && at - moved >= BranchTidying.SetAsideFor
                            && (sessionTrees.SetAsideKept(tree.Path, moved)?.Sentence ?? failed.GetValueOrDefault(Path.GetFullPath(tree.Path))) is { } kept
                ? tree with { PurgeKept = kept }
                : tree)
            .ToList();
        var branches = new SessionBranches(home).Entries();
        var landings = new LandedBranches(home).Entries();
        var refs = new List<RecoveryRef>();
        var standing = new List<StandingBranch>();
        var repositories = branches.Select(entry => entry.Repository).Concat(landings.Select(entry => entry.Repository))
            .Concat(trees.Select(entry => entry.Repository)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in repositories)
        {
            if (!roots.TryGetValue(repository, out var root) || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                unread.Add($"`{repository}` has no checkout here, so its recovery refs were not read");
                continue;
            }

            // CLEANUP1: each with its commit, its date and whether a branch or a tag of the person's holds it, which its retention reads.
            // FABRIC-UNKNOWN1: the same listing names the repository's daoris/* branches, so they cost no further git start.
            var (listed, err, branchesHere) = await RecoveryRetention.ReadWithBranchesAsync(root, repository, RecoveryRetention.Made(landings, repository), ct).ConfigureAwait(false);
            if (listed is null)
            {
                unread.Add($"git could not list `{repository}`'s recovery refs: {err}");
                continue;
            }

            refs.AddRange(listed);
            standing.AddRange(branchesHere);
        }

        return new InventoryFacts(
            branches, landings, trees, new AutoLandings(home).All(), refs, inUse,
            session => Directory.Exists(DaorisHome.Entry(home, HomeEntry.Sessions, session)), at)
        { Standing = standing };
    }

    /// <summary>The milestone of every unit this home names, and Daoris's own recoveries no unit names (CLEANUP1), from one read.</summary>
    public static async Task<(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<InventoryEntry> Recovery)> ReadAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ICollection<string> unread, CancellationToken ct = default)
    {
        var facts = await GatherAsync(home, roots, inUse, unread, ct).ConfigureAwait(false);
        return (WorkInventory.Derive(facts), WorkInventory.Recoveries(facts));
    }

    /// <summary>The milestone of every unit this home names.</summary>
    public static async Task<IReadOnlyList<WorkMilestone>> MilestonesAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ICollection<string> unread, CancellationToken ct = default) =>
        WorkInventory.Derive(await GatherAsync(home, roots, inUse, unread, ct).ConfigureAwait(false));

    /// <summary>The units a name picks: a unit's id, its session, or its session or landing branch, by name or as its folder is called.</summary>
    public static IReadOnlyList<WorkMilestone> Named(IReadOnlyList<WorkMilestone> all, string named) =>
        [.. all.Where(unit => string.Equals(unit.Unit, named, StringComparison.OrdinalIgnoreCase)
            || string.Equals(unit.Session, named, StringComparison.OrdinalIgnoreCase)
            || string.Equals(unit.Branch, named, StringComparison.Ordinal)
            || string.Equals(unit.Branch, TreeFollows.SessionPrefix + named, StringComparison.Ordinal)
            || unit.Entries.Any(entry => entry.Kind is InventoryKind.LandingBranch or InventoryKind.SessionBranch && entry.Resource == named))];
}
