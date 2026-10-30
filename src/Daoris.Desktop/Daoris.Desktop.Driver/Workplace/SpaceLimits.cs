namespace Daoris.Driver;

/// <summary>
/// The size limit of each kind that takes one, beside its days (KEEPSIZE1d, D195's keep-size note): a kind over its limit lets go of its
/// oldest eligible units first, through the press's own removal (<see cref="SpaceFree.RunAsync"/>), so every guard each removal has holds
/// as it does for the press. Only what can be made again or is only a copy is ever taken: the set-aside folders whose record is kept and
/// whose tip a recovery ref holds (judged again right before each goes), and the downloads nothing uses, never under <c>never</c>. A
/// kind whose size is not counted takes nothing, and asks to be counted.
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

    /// <summary>One pass: each kind over its limit asked to be cleaned, run, and what went returned by kind.</summary>
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
            asked.AddRange(SetAside(world, policy, ct, unit, ref uncounted));
            asked.AddRange(Downloads(world, policy, ct, unit));
            asked.AddRange(BrowserCache(world, policy, unit, ref uncounted));
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

    private static IEnumerable<SpaceAsked> SetAside(SpaceFreeWorld world, KeepPolicy policy, CancellationToken ct, long unit, ref bool uncounted)
    {
        if (policy.Limits.SetAsideGb is not { } gb || policy.SetAside.Whole) return [];
        var home = world.Home;
        var sizes = world.Sizes.Of(home, "set-aside")?.Folders;
        if (sizes is null)
        {
            // Not counted yet: nothing is taken, the count is asked for, and a later pass judges it.
            world.Sizes.Start(home, ["set-aside"], WiringBudget.Default);
            uncounted = true;
            return [];
        }

        ct.ThrowIfCancellationRequested();
        var folders = new SessionTrees(home).ListSetAside(policy, world.Now()).Where(folder => Directory.Exists(folder.Folder)).ToList();
        long? Size(SetAsideListed folder) => sizes.TryGetValue(folder.Id, out var bytes) ? bytes : null;
        var total = folders.Sum(folder => Size(folder) ?? 0);
        var eligible = folders
            .Where(folder => folder.Class == SetAsideClass.Recorded && folder.Kept is not SetAsideKept.Unread)
            .Select(folder => (folder, Size(folder), (DateTimeOffset?)folder.Moved));
        return Oldest(eligible, total, gb * unit).Select(folder =>
        {
            var past = folder.Past && folder.Kept is null;
            return new SpaceAsked("set-aside", folder.Id, Early: !past, past ? "past" : "early", folder.Own)
            {
                Class = ListedClass.Of(folder), Ref = folder.Ref,
            };
        });
    }

    /// <summary>
    /// The browser's caches, all of them together (KEEPSIZE1e), when the kind's count is over its limit; whether the browser runs is the
    /// press's own judgement, which keeps them while it does. A count not made yet, or partial and within the limit, takes nothing.
    /// </summary>
    private static IEnumerable<SpaceAsked> BrowserCache(SpaceFreeWorld world, KeepPolicy policy, long unit, ref bool uncounted)
    {
        if (policy.Limits.BrowserCacheGb is not { } gb || world.Caches is null) return [];
        var reading = world.Sizes.Of(world.Home, "browser-cache");
        if (reading is null)
        {
            world.Sizes.Start(world.Home, ["browser-cache"], WiringBudget.Default);
            uncounted = true;
            return [];
        }

        var limit = gb * unit;
        if (reading.Bytes <= limit)
        {
            uncounted |= reading.Partial is not null;
            return [];
        }

        return SpaceKinds.BrowserCaches.Select(path => new SpaceAsked("browser-cache", path, Early: true, "early", Own: false));
    }

    private static IEnumerable<SpaceAsked> Downloads(SpaceFreeWorld world, KeepPolicy policy, CancellationToken ct, long unit)
    {
        if (policy.Limits.DownloadsGb is not { } gb || policy.Downloads.Days is null) return [];
        var now = world.Now();
        var listing = DownloadClearing.List(world.Home, world.JudgeOrFiles().Uses(), now, ct);
        if (listing.Why is not null) return [];
        var record = KeepingRecord.Read(world.Home);
        var rows = listing.Versions.Where(row => !row.Unfinished).ToList();
        var total = rows.Sum(row => row.Bytes);
        var eligible = rows
            .Where(row => row.Keeps is null)
            .Select(row => (row, (long?)row.Bytes, DownloadKeeping.UnusedSince(row, record)));
        return Oldest(eligible, total, gb * unit).Select(row =>
        {
            var goes = DownloadKeeping.Goes(row, record, policy.Downloads);
            var past = goes is { } at && at <= now;
            return new SpaceAsked("downloads", DownloadKeeping.Key(row), Early: !past, past ? "past" : "early", Own: false);
        });
    }
}

/// <summary>What one pass of the size limits did (<see cref="SpaceLimits.PassAsync"/>): each kind run, and whether a kind was not counted yet, so a later pass judges it.</summary>
public sealed record SpaceLimitsPass(IReadOnlyList<SpaceRunKind> Kinds, bool Uncounted);
