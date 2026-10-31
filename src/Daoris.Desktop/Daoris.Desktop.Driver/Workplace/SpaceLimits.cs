namespace Daoris.Driver;

/// <summary>
/// The size limits (KEEPSIZE1d, D195's keep-size note): a kind over its limit lets go of its oldest eligible units first, through the
/// press's own removal (<see cref="SpaceFree.RunAsync"/>), so every guard each removal has holds as it does for the press. Beside them the
/// allowance over the whole (§4.3's stopping rule): where the whole is still over it, the eligible units go in order, but only where all
/// of them would bring the whole within it. Only what can be made again or is only a copy is ever taken: the set-aside folders whose record
/// is kept and whose tip a recovery ref holds (judged again right before each goes), the browser's caches, and the downloads nothing uses,
/// never under <c>never</c>. A kind whose size is not counted takes nothing, and asks to be counted.
/// </summary>
public static class SpaceLimits
{
    private const long Gb = 1L << 30;

    /// <summary>
    /// The oldest of <paramref name="candidates"/>, by when each goes (unknown last), enough that the kind falls to its limit: none where
    /// <paramref name="total"/> is within <paramref name="limit"/>. A candidate whose size is unknown is never taken, since what it frees
    /// is not known.
    /// </summary>
    public static IReadOnlyList<T> Oldest<T>(IEnumerable<(T Item, long? Bytes, DateTimeOffset? At)> candidates, long total, long limit)
    {
        var over = total - limit;
        var picked = new List<T>();
        if (over <= 0) return picked;
        long freed = 0;
        foreach (var (item, bytes, _) in candidates.Where(each => each.Bytes is > 0).OrderBy(each => each.At is null).ThenBy(each => each.At))
        {
            if (freed >= over) break;
            picked.Add(item);
            freed += bytes!.Value;
        }

        return picked;
    }

    /// <summary>One pass: each kind over its limit, then the whole over its allowance, asked to be cleaned, run, and what went returned by kind.</summary>
    /// <param name="world">The home and its seams; its registry and record door let a recorded folder's holding check run.</param>
    /// <param name="door">The door the run came through, for <c>keeping.json</c> and the machine log.</param>
    /// <param name="runId">The run lock's id.</param>
    /// <param name="unit">The bytes in a limit's GB; a test's seam, so a limit of one is a few kilobytes.</param>
    public static async Task<SpaceLimitsPass> PassAsync(SpaceFreeWorld world, string door, string runId, CancellationToken ct, long unit = Gb)
    {
        var asked = new List<SpaceAsked>();
        var policy = world.Policy(world.Home);
        var uncounted = false;
        try
        {
            // The kinds in the allowance's order (§4.1): the set-aside folders, the browser's caches, the downloads.
            var plans = new[]
            {
                (Plan: SetAside(world, policy, ct, ref uncounted), Limit: policy.Limits.SetAsideGb),
                (Plan: BrowserCache(world, policy, ref uncounted), Limit: policy.Limits.BrowserCacheGb),
                (Plan: Downloads(world, policy, ct), Limit: policy.Limits.DownloadsGb),
            };
            var taken = new List<Unit>();
            foreach (var (plan, limit) in plans)
            {
                if (plan is null || limit is not { } gb) continue;
                taken.AddRange(Oldest(plan.Eligible.Select(each => (each, (long?)each.Bytes, each.At)), plan.Total, gb * unit));
            }

            if (policy.Limits.AllowanceGb is { } allowance)
            {
                taken.AddRange(Whole(world, allowance * unit, plans.SelectMany(each => each.Plan?.Eligible ?? []).ToList(), taken, ref uncounted));
            }

            asked.AddRange(taken.SelectMany(each => each.Together ?? [each.Asked]));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HomeEntryTwiceException or DriverException)
        {
            // What does not read is not counted, and the unknown keeps.
            uncounted = true;
        }

        if (asked.Count == 0) return new([], uncounted);
        var run = new SpaceRun(runId, door, world.Now(), asked.Select(each => each.Kind).Distinct());
        await SpaceFree.RunAsync(world, asked, run, ct).ConfigureAwait(false);
        return new(run.Kinds, uncounted);
    }

    /// <summary>
    /// The stopping rule (§4.3): the whole is every kind's last count; it takes nothing where any is not counted or partial, and nothing
    /// where all that may go would not bring the whole within the allowance, since emptying what can be made again with no hope of
    /// reaching the bound costs a fetch and gains nothing. Else the eligible units in the allowance's order until it is within.
    /// </summary>
    private static IReadOnlyList<Unit> Whole(SpaceFreeWorld world, long allowance, IReadOnlyList<Unit> eligible, IReadOnlyList<Unit> taken, ref bool uncounted)
    {
        var readings = SpaceKinds.Table.Select(kind => (kind.Id, Reading: world.Sizes.Of(world.Home, kind.Id))).ToList();
        var missing = readings.Where(each => each.Reading is null).Select(each => each.Id).ToList();
        if (missing.Count > 0)
        {
            world.Sizes.Start(world.Home, missing, WiringBudget.Default);
            uncounted = true;
            return [];
        }

        if (readings.Any(each => each.Reading!.Partial is not null))
        {
            uncounted = true;
            return [];
        }

        var over = readings.Sum(each => each.Reading!.Bytes) - allowance - taken.Sum(each => each.Bytes);
        if (over <= 0) return [];
        var rest = eligible.Except(taken).Where(each => each.Bytes > 0).ToList();
        if (rest.Sum(each => each.Bytes) < over) return [];
        var picked = new List<Unit>();
        long freed = 0;
        foreach (var unit in rest)
        {
            if (freed >= over) break;
            picked.Add(unit);
            freed += unit.Bytes;
        }

        return picked;
    }

    private static Plan? SetAside(SpaceFreeWorld world, KeepPolicy policy, CancellationToken ct, ref bool uncounted)
    {
        if (policy.SetAside.Whole) return null;
        var home = world.Home;
        var sizes = world.Sizes.Of(home, "set-aside")?.Folders;
        if (sizes is null)
        {
            // Not counted yet: nothing is taken, the count is asked for, and a later pass judges it.
            world.Sizes.Start(home, ["set-aside"], WiringBudget.Default);
            uncounted = true;
            return null;
        }

        ct.ThrowIfCancellationRequested();
        var folders = new SessionTrees(home).ListSetAside(policy, world.Now()).Where(folder => Directory.Exists(folder.Folder)).ToList();
        long? Size(SetAsideListed folder) => sizes.TryGetValue(folder.Id, out var bytes) ? bytes : null;
        var eligible = folders
            .Where(folder => folder.Class == SetAsideClass.Recorded && folder.Kept is not SetAsideKept.Unread && Size(folder) is > 0)
            .OrderBy(folder => folder.Moved)
            .Select(folder =>
            {
                var past = folder.Past && folder.Kept is null;
                var asked = new SpaceAsked("set-aside", folder.Id, Early: !past, past ? "past" : "early", folder.Own)
                {
                    Class = ListedClass.Of(folder), Ref = folder.Ref,
                };
                return new Unit(asked, Size(folder)!.Value, folder.Moved);
            })
            .ToList();
        return new(folders.Sum(folder => Size(folder) ?? 0), eligible);
    }

    /// <summary>
    /// The browser's caches, all of them together (KEEPSIZE1e), one unit; whether the browser runs is the press's own judgement, which keeps
    /// them while it does. A count not made yet takes nothing, and a partial one within its limit is said.
    /// </summary>
    private static Plan? BrowserCache(SpaceFreeWorld world, KeepPolicy policy, ref bool uncounted)
    {
        if (world.Caches is null) return null;
        var reading = world.Sizes.Of(world.Home, "browser-cache");
        if (reading is null)
        {
            world.Sizes.Start(world.Home, ["browser-cache"], WiringBudget.Default);
            uncounted = true;
            return null;
        }

        if (policy.Limits.BrowserCacheGb is { } gb && reading.Bytes <= gb * Gb) uncounted |= reading.Partial is not null;
        var units = SpaceKinds.BrowserCaches.Select(path => new SpaceAsked("browser-cache", path, Early: true, "early", Own: false)).ToList();
        // The caches go together: one unit carrying every path's ask, so a part or the whole takes them all or none.
        return new(reading.Bytes, [new Unit(units[0], reading.Bytes, null) { Together = units }]);
    }

    private static Plan? Downloads(SpaceFreeWorld world, KeepPolicy policy, CancellationToken ct)
    {
        if (policy.Downloads.Days is null) return null;
        var now = world.Now();
        var listing = DownloadClearing.List(world.Home, world.JudgeOrFiles().Uses(), now, ct);
        if (listing.Why is not null) return null;
        var record = KeepingRecord.Read(world.Home);
        var rows = listing.Versions.Where(row => !row.Unfinished).ToList();
        var eligible = rows
            .Where(row => row.Keeps is null && row.Bytes > 0)
            .OrderBy(row => DownloadKeeping.UnusedSince(row, record) is null)
            .ThenBy(row => DownloadKeeping.UnusedSince(row, record))
            .Select(row =>
            {
                var goes = DownloadKeeping.Goes(row, record, policy.Downloads);
                var past = goes is { } at && at <= now;
                var asked = new SpaceAsked("downloads", DownloadKeeping.Key(row), Early: !past, past ? "past" : "early", Own: false);
                return new Unit(asked, row.Bytes, DownloadKeeping.UnusedSince(row, record));
            })
            .ToList();
        return new(rows.Sum(row => row.Bytes), eligible);
    }

    private sealed record Plan(long Total, IReadOnlyList<Unit> Eligible);

    private sealed record Unit(SpaceAsked Asked, long Bytes, DateTimeOffset? At)
    {
        /// <summary>The asks a unit stands for where it is several (the browser's caches).</summary>
        public IReadOnlyList<SpaceAsked>? Together { get; init; }
    }
}

/// <summary>What one pass of the size limits did (<see cref="SpaceLimits.PassAsync"/>): each kind run, and whether a kind was not counted yet, so a later pass judges it.</summary>
public sealed record SpaceLimitsPass(IReadOnlyList<SpaceRunKind> Kinds, bool Uncounted);
