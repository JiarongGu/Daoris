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
    public static Task<InventoryFacts> GatherAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ICollection<string> unread,
        CancellationToken ct = default, DateTimeOffset? now = null) =>
        GatherAsync(home, roots, inUse, (_, said) => unread.Add(said), only: null, ct, now);

    /// <summary>
    /// One part of the inventory (FACTSPLIT1a): the units, the recoveries no unit names and what could not be read, of the
    /// repositories <paramref name="only"/> holds alone, from their trees, their records and their checkouts' refs. Another
    /// repository's trees are never listed, so they cost no git. Each line not read names its repository as data.
    /// </summary>
    /// <param name="only">Whether a repository the trees or the records name is this part's.</param>
    public static async Task<(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<InventoryEntry> Recovery, IReadOnlyList<InventoryUnread> Unread)> ReadPartAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, Func<string, bool> only, CancellationToken ct = default)
    {
        var unread = new List<InventoryUnread>();
        var facts = await GatherAsync(home, roots, inUse, (repository, said) => unread.Add(new(repository, said)), only, ct, now: null).ConfigureAwait(false);
        return (WorkInventory.Derive(facts), WorkInventory.Recoveries(facts), unread);
    }

    private static async Task<InventoryFacts> GatherAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, Action<string, string> unread,
        Func<string, bool>? only, CancellationToken ct, DateTimeOffset? now)
    {
        IReadOnlyList<T> Ours<T>(IReadOnlyList<T> all, Func<T, string> repository) => only is null ? all : [.. all.Where(each => only(repository(each)))];

        var at = now ?? DateTimeOffset.UtcNow;
        var sessionTrees = new SessionTrees(home);
        // CLEANUP1, after review: a folder moved aside past its date that the purge keeps, read as the purge reads it, so it is said to the
        // person. Only past its date, where the purge has decided: a folder it deletes is walked once more before the next pass takes it.
        // Re-check: one whose delete the purge could not finish, which no read repeats, as the purge kept it.
        var failed = sessionTrees.SetAsideFailed();
        var trees = (await sessionTrees.ListAsync(only, ct).ConfigureAwait(false))
            .Select(tree => tree.MovedAside is { } moved && at - moved >= BranchTidying.SetAsideFor
                            && (sessionTrees.SetAsideKept(tree.Path, moved)?.Sentence ?? failed.GetValueOrDefault(Path.GetFullPath(tree.Path))) is { } kept
                ? tree with { PurgeKept = kept }
                : tree)
            .ToList();
        // FACTSPLIT1a: a part reads its own repositories' records, so it derives no unit of another's.
        var branches = Ours(new SessionBranches(home).Entries(), entry => entry.Repository);
        var landings = Ours(new LandedBranches(home).Entries(), entry => entry.Repository);
        var refs = new List<RecoveryRef>();
        var standing = new List<StandingBranch>();
        List<string> named = [.. branches.Select(entry => entry.Repository).Concat(landings.Select(entry => entry.Repository))
            .Concat(trees.Select(entry => entry.Repository)).Distinct(StringComparer.OrdinalIgnoreCase)];
        // FABRIC-UNKNOWN1b: every registered repository with a checkout here, this part's, is read too, one listing each, so a daoris/*
        // branch no record and no tree names is seen where nothing of Daoris's is recorded. One whose checkout is not here has nothing
        // recorded to say of, so nothing is said.
        var registered = roots.Keys.Where(repository => !named.Contains(repository, StringComparer.OrdinalIgnoreCase) && (only is null || only(repository))
            && roots[repository] is { } root && !string.IsNullOrWhiteSpace(root) && Directory.Exists(root));
        foreach (var repository in named.Concat(registered).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!roots.TryGetValue(repository, out var root) || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                unread(repository, $"`{repository}` has no checkout here, so its recovery refs were not read");
                continue;
            }

            // CLEANUP1: each with its commit, its date and whether a branch or a tag of the person's holds it, which its retention reads.
            // FABRIC-UNKNOWN1: the same listing names the repository's daoris/* branches, so they cost no further git start.
            var (listed, err, branchesHere) = await RecoveryRetention.ReadWithBranchesAsync(root, repository, RecoveryRetention.Made(landings, repository), ct).ConfigureAwait(false);
            if (listed is null)
            {
                unread(repository, $"git could not list `{repository}`'s recovery refs: {err}");
                continue;
            }

            refs.AddRange(listed);
            standing.AddRange(branchesHere);
        }

        return new InventoryFacts(
            branches, landings, trees, Ours(new AutoLandings(home).All(), entry => entry.Repository), refs, inUse,
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

/// <summary>What one repository's part of the inventory could not read (FACTSPLIT1a): the repository, as data, and the clause `work state` says.</summary>
public sealed record InventoryUnread(string Repository, string Said);
