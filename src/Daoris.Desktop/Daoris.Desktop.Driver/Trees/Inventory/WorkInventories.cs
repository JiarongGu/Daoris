namespace Daoris.Driver;

/// <summary>
/// What the inventory is read from, gathered (FABRIC-MILESTONE1a): the home's records as they stand, the trees and the folders moved
/// aside, and each repository's recovery refs. Read-only: no record is written, no lease taken, and a repository git cannot read is
/// said, never taken for having none (D57).
/// </summary>
public static class WorkInventories
{
    private const string HeadsPrefix = "refs/heads/daoris/";

    /// <summary>The facts of this home. <paramref name="roots"/> names each repository's checkout; none is read for a repository without one.</summary>
    /// <param name="inUse">The trees a session still running or waiting holds, from the service.</param>
    /// <param name="unread">Said where a repository's recovery refs were not read, and why.</param>
    public static async Task<InventoryFacts> GatherAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ICollection<string> unread,
        CancellationToken ct = default, DateTimeOffset? now = null)
    {
        var trees = await new SessionTrees(home).ListAsync(ct).ConfigureAwait(false);
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

            var (code, listed, err) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", "refs/daoris/discarded/", HeadsPrefix], ct).ConfigureAwait(false);
            if (code != 0)
            {
                unread.Add($"git could not list `{repository}`'s recovery refs: {err.Trim()}");
                continue;
            }

            // FABRIC-UNKNOWN1: the same listing carries the repository's daoris/* branches (one more pattern, no more git starts).
            foreach (var each in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (each.StartsWith(HeadsPrefix, StringComparison.Ordinal)) standing.Add(new StandingBranch(repository, each["refs/heads/".Length..]));
                else refs.Add(new RecoveryRef(repository, each));
            }
        }

        return new InventoryFacts(
            branches, landings, trees, new AutoLandings(home).All(), refs, inUse,
            session => Directory.Exists(DaorisHome.Entry(home, HomeEntry.Sessions, session)), now ?? DateTimeOffset.UtcNow)
        { Standing = standing };
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
