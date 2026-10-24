using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Why the press kept a unit it was asked to remove (KEEP1e, the cleanup design §3.3), beside each kind's own codes.</summary>
public static class SpaceKept
{
    /// <summary>It changed since the list the person read: a folder that held no files of its own now does, or a day no longer past.</summary>
    public const string Changed = "changed";

    /// <summary>It is no longer there.</summary>
    public const string Gone = "gone";

    /// <summary>Its name is none the list could have shown: never joined into a path.</summary>
    public const string Refused = "refused";

    /// <summary>Its removal failed part way; what went stays gone, and the rest waits.</summary>
    public const string Failed = "failed";

    /// <summary>The browser runs, so its caches stay (the cleanup design §3.2).</summary>
    public const string Running = "running";

    /// <summary>No Daoris runs to say whether the browser does, so its caches stay (§3.2).</summary>
    public const string NoShell = "no-shell";

    /// <summary>A program holds it: its rename aside was refused.</summary>
    public const string InUse = "in-use";

    /// <summary>An entry of this build names it, so it is no earlier build's (§3.5).</summary>
    public const string Named = "named";

    /// <summary>An update is under way, so its folders stay (§3.5).</summary>
    public const string Updating = "updating";
}

/// <summary>
/// One thing the press lists (KEEP1e, the cleanup design §3.3): its kind, its id (what a press sends back), whether its rule takes it
/// now or it could go early by the person's word, and what the list says of it.
/// </summary>
/// <param name="Kind"><c>set-aside</c>, <c>downloads</c>, <c>logs</c>, <c>browser-cache</c>, <c>left-over</c> or <c>update-failed</c>.</param>
/// <param name="Id">A set-aside folder's <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;folder&gt;</c>, a download's
/// <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>, a log's file name, a cache's or an earlier build's path, the failed update's.</param>
/// <param name="Early">It goes before its rule's time, by the person's word; false where its rule takes it now.</param>
/// <param name="Bytes">Its size, where it is known: a download's and a path's walked as listed, a set-aside folder's from its kind's walk.</param>
public sealed record SpaceUnit(string Kind, string Id, bool Early, long? Bytes)
{
    /// <summary>What the rule takes of it now: <c>past</c> (its days ended), <c>slim</c> (its package folders, an hour after its move) or
    /// <c>unfinished</c> (a download an earlier clearing moved aside); for an early one, <c>early</c> or <c>kept</c> (a set-aside folder past
    /// its days the purge keeps, since it holds files of its own).</summary>
    public string? Why { get; init; }

    /// <summary>When its rule takes it: a set-aside folder's end of days, a download's day; null where none is known.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>A set-aside folder's workspace and repository.</summary>
    public string? Workspace { get; init; }

    /// <inheritdoc cref="Workspace"/>
    public string? Repository { get; init; }

    /// <summary>A set-aside folder holds files of its own (written after its move, or a link in it): the person's to weigh.</summary>
    public bool? Own { get; init; }
}

/// <summary>What the press lists (<see cref="SpaceFree.Plan"/>): what each rule takes now, what could go early in groups, and the doors of the kinds with presses of their own.</summary>
public sealed record SpacePlan(DateTimeOffset At, IReadOnlyList<SpaceUnit> Units, IReadOnlyDictionary<string, string> Unread)
{
    /// <summary>The early groups, in the card's order: each kind with something that could go early.</summary>
    public static IReadOnlyList<string> Groups { get; } = ["set-aside", "downloads", "browser-cache", "left-over", "update-failed"];

    /// <summary>The kinds with a press of their own (§3.3 (c)): Clear history… on a workspace's page, Clean up… on its Branches.</summary>
    public static IReadOnlyList<string> Doors { get; } = ["history", "trees"];
}

/// <summary>One unit a press asked removed, as the page or the terminal sends it back from the list.</summary>
public sealed record SpaceAsked(string Kind, string Id, bool Early, string? Why, bool Own);

/// <summary>What the press is handed (<see cref="SpaceFree"/>): the home, and the reads and seams a test stands in for.</summary>
public sealed record SpaceFreeWorld(string Home)
{
    /// <summary>The sizes counted, whose set-aside walk says each folder's bytes; a run's end counts the kinds it removed from again.</summary>
    public SpaceSizes Sizes { get; init; } = SpaceSizes.Shared;

    /// <summary>How the downloads are judged right before each goes (KEEP1b's act).</summary>
    public Func<DownloadJudge>? Judge { get; init; }

    /// <summary>The shell's browser host; null with no shell to say whether the browser runs, which keeps its caches.</summary>
    public IBrowserCaches? Caches { get; init; }

    /// <summary>The machine log the run writes <c>space.freed</c> and <c>space.kept</c> to.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>What time it is.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The rules kept by; the home's by default.</summary>
    public Func<string, KeepPolicy> Policy { get; init; } = KeepPolicy.Of;

    /// <summary>A rename aside; a test's refuses, as a program holding the folder makes Windows refuse.</summary>
    public Action<string, string>? Move { get; init; }

    /// <summary>The terminal's world alone: this home's downloads judged from its files, its running sessions by the process markers.</summary>
    public static SpaceFreeWorld OfThisTerminal(string home, MachineLog? log) => new(home)
    {
        Log = log,
        Judge = () => DownloadJudge.Of(home, HarnessSettings.ResolvePath(home), DriverConfig.ResolvePath(home),
            token => LiveSessions.ReadAsync(null, DaorisHome.Entry(home, HomeEntry.SessionPids), token)),
    };

    internal DownloadJudge JudgeOrFiles() => Judge?.Invoke() ?? DownloadJudge.Of(
        Home, HarnessSettings.ResolvePath(Home), DriverConfig.ResolvePath(Home),
        token => LiveSessions.ReadAsync(null, DaorisHome.Entry(Home, HomeEntry.SessionPids), token));
}

/// <summary>
/// The person's <i>Free up space…</i> (KEEP1e, D195 point 4; the cleanup design §3.3): listed first, then pressed (D88's shape, as
/// CACHECLEAR1's). The list changes nothing: per kind, what its rule takes now, and what could go early by the person's word (set-aside
/// folders before their date, unused downloads within their days, the browser's caches, what an earlier build left, a failed update's
/// folder). A run removes exactly the units a press names, each judged again right before it goes through its kind's one removal; one
/// changed since the list is kept and counted. The window's route, <c>daoris-driver space free</c> and nothing else call it.
/// </summary>
/// <remarks>
/// <para><b>Each kind's one removal</b> (§2.1, §6.6): the set-aside folders through the purge and the slim (<see cref="SetAsideKeeping"/>),
/// one before its date whole with its manifest; the downloads through CACHECLEAR1's act (<see cref="DownloadClearing.ClearAsync"/>); the
/// log's days through its prune (<see cref="MachineLog.Remove"/>); the browser's caches through the shell's host
/// (<see cref="BrowserCacheClearing"/>); an earlier build's two folders and a failed update's by their exact paths, moved aside first.</para>
/// <para><b>One run at a time</b> (§3.1): every run holds the home's run lock (<see cref="KeepingRun"/>), taken by <see cref="SpaceRuns"/>.</para>
/// </remarks>
public static class SpaceFree
{
    /// <summary>The kinds a run removes, in the order it removes them.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["set-aside", "downloads", "logs", "browser-cache", "left-over", "update-failed"];

    /// <summary>The card's kind a run's kind is counted under, for the sizes counted again at its end.</summary>
    public static string CardKind(string kind) => kind == "update-failed" ? "rest" : kind;

    /// <summary>The machine log's days by the person's rule (KEEP1d), which the press lists and removes by as the look's prune does.</summary>
    private static int LogDays(KeepPolicy policy) => (int)policy.Logs.Days.TotalDays;

    /// <summary>The list, read and never written.</summary>
    public static SpacePlan Plan(SpaceFreeWorld world, CancellationToken ct = default)
    {
        var home = world.Home;
        var now = world.Now();
        var policy = world.Policy(home);
        var units = new List<SpaceUnit>();
        var unread = new Dictionary<string, string>(StringComparer.Ordinal);

        // The set-aside folders: past their days and nothing written since, the purge's now; a slim due, its now; the rest early.
        try
        {
            var sizes = world.Sizes.Of(home, "set-aside")?.Folders;
            foreach (var folder in new SessionTrees(home).ListSetAside(policy, now))
            {
                long? bytes = sizes is not null && sizes.TryGetValue(folder.Id, out var size) ? size : null;
                var unit = new SpaceUnit("set-aside", folder.Id, Early: false, bytes)
                {
                    At = folder.Goes, Workspace = folder.Workspace, Repository = folder.Repository, Own = folder.Own,
                };
                if (folder.Past && folder.Kept is null) units.Add(unit with { Why = "past" });
                if (folder.Slim) units.Add(unit with { Why = "slim", Bytes = null });
                if (!folder.Past && folder.Kept is not SetAsideKept.Unread) units.Add(unit with { Early = true, Why = "early" });
                else if (folder.Past && folder.Own) units.Add(unit with { Early = true, Why = "kept" });
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HomeEntryTwiceException)
        {
            unread["set-aside"] = "unreadable";
        }

        // The downloads nothing uses: past their days (or unfinished) now; within them, or under `never`, early.
        try
        {
            var listing = DownloadClearing.List(home, world.JudgeOrFiles().Uses(), now, ct);
            if (listing.Why is { } why) unread["downloads"] = why;
            var record = KeepingRecord.Read(home);
            foreach (var row in listing.Versions)
            {
                var id = DownloadKeeping.Key(row);
                if (row.Unfinished)
                {
                    units.Add(new SpaceUnit("downloads", id, false, row.Bytes) { Why = "unfinished" });
                    continue;
                }

                if (row.Keeps is not null) continue;
                var goes = DownloadKeeping.Goes(row, record, policy.Downloads);
                units.Add(goes is { } at && at <= now
                    ? new SpaceUnit("downloads", id, false, row.Bytes) { Why = "past", At = at }
                    : new SpaceUnit("downloads", id, true, row.Bytes) { Why = "early", At = goes });
            }
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or JsonException)
        {
            unread["downloads"] = "unreadable";
        }

        // The machine log's days past the person's days (KEEP1d), which the look's daily prune takes: what is left until the next.
        try
        {
            var logs = DaorisHome.Entry(home, HomeEntry.Logs);
            foreach (var name in MachineLog.Due(logs, now, LogDays(policy))) units.Add(new SpaceUnit("logs", name, false, Length(Path.Combine(logs, name))) { Why = "past" });
        }
        catch (HomeEntryTwiceException)
        {
            unread["logs"] = "twice";
        }

        // The browser's caches, each of the table's paths that is there; whether the browser runs is judged at the press.
        foreach (var (kind, id, path) in EarlyPaths(home))
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(path)) continue;
            if (kind == "left-over" && NamedByThisBuild(home, path)) continue;
            var (bytes, _, _) = WiringSizes.MeasureFolder(path, WiringBudget.Default, ct);
            units.Add(new SpaceUnit(kind, id, true, bytes) { Why = "early" });
        }

        return new SpacePlan(now, units, unread);
    }

    /// <summary>
    /// A run of the units <paramref name="asked"/> names, each kind in <see cref="Kinds"/>' order, holding the run lock its caller took:
    /// each unit judged again right before it goes, each kind's end told on <paramref name="run"/>, its <c>last</c> written and its lines
    /// in the machine log. A stop is heard between units, never inside one.
    /// </summary>
    public static async Task RunAsync(SpaceFreeWorld world, IReadOnlyList<SpaceAsked> asked, SpaceRun run, CancellationToken ct)
    {
        var home = world.Home;
        foreach (var kind in Kinds)
        {
            var mine = asked.Where(unit => unit.Kind == kind).ToList();
            if (mine.Count == 0) continue;
            if (ct.IsCancellationRequested)
            {
                run.Stopped();
                return;
            }

            run.Begin(kind);
            var tally = new SpaceTally();
            try
            {
                switch (kind)
                {
                    case "set-aside": SetAside(world, mine, tally, ct); break;
                    case "downloads": await DownloadsAsync(world, mine, tally, run.Door, ct).ConfigureAwait(false); break;
                    case "logs": Logs(world, mine, tally); break;
                    case "browser-cache": await CachesAsync(world, mine, tally, ct).ConfigureAwait(false); break;
                    default: Paths(world, kind, mine, tally); break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Said(world, kind, tally, run);
                run.End(kind, tally);
                run.Stopped();
                return;
            }

            Said(world, kind, tally, run);
            run.End(kind, tally);
        }

        run.Done();
        world.Sizes.Start(home, run.Kinds.Where(each => each.Removed > 0).Select(each => CardKind(each.Id)), WiringBudget.Default);
    }

    /// <summary>A kind's end said: its <c>last</c> in <c>keeping.json</c>, and <c>space.freed</c> and <c>space.kept</c> in the machine log.</summary>
    private static void Said(SpaceFreeWorld world, string kind, SpaceTally tally, SpaceRun run)
    {
        if (tally.Removed == 0 && tally.Kept.Count == 0) return;
        KeepingRecord.WriteLast(world.Home, KeepingRecord.Read(world.Home), kind,
            new KeepingLast(world.Now(), run.Door, run.Id, tally.Removed, tally.Bytes, tally.Kept));
        foreach (var early in new[] { false, true })
        {
            var (count, bytes) = early ? (tally.EarlyRemoved, tally.EarlyBytes) : (tally.Removed - tally.EarlyRemoved, tally.Bytes - tally.EarlyBytes);
            if (count > 0) world.Log?.Info("space.freed", ("kind", kind), ("count", count), ("bytes", bytes), ("door", run.Door), ("early", early), ("run", run.Id));
        }

        foreach (var (why, count) in tally.Kept.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            world.Log?.Info("space.kept", ("kind", kind), ("why", why), ("count", count));
        }
    }

    private static void SetAside(SpaceFreeWorld world, IReadOnlyList<SpaceAsked> units, SpaceTally tally, CancellationToken ct)
    {
        var trees = new SessionTrees(world.Home);
        var roots = trees.SetAsideRoots;
        var same = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var folders = new Dictionary<SpaceAsked, string>();
        foreach (var unit in units)
        {
            // KEEPSIZE1a: a listed id names its folder at whichever place of the trees holds it, the new first; where neither does, the new
            // place's path, which the removal finds gone.
            var at = roots.Select(root => Under(root, unit.Id, 3)).ToList();
            if (at[0] is not null) folders[unit] = at.FirstOrDefault(Directory.Exists) ?? at[0]!;
            else tally.Keep(SpaceKept.Refused, unit.Early);
        }

        // Now: the purge of exactly the folders listed past their days, and the slim of exactly those listed due; each judged as the pass judges.
        var purge = folders.Where(each => !each.Key.Early && each.Key.Why == "past").Select(each => Path.GetFullPath(each.Value)).ToHashSet(same);
        var slim = folders.Where(each => !each.Key.Early && each.Key.Why == "slim").Select(each => Path.GetFullPath(each.Value)).ToHashSet(same);
        if (purge.Count > 0 || slim.Count > 0)
        {
            var done = trees.KeepSetAside(world.Policy(world.Home), world.Now(), TimeSpan.MaxValue, ct,
                folder => purge.Contains(Path.GetFullPath(folder)), folder => slim.Contains(Path.GetFullPath(folder)));
            var met = new HashSet<string>(same);
            foreach (var folder in done.Purged.Where(folder => purge.Contains(Path.GetFullPath(folder.Folder))))
            {
                met.Add("past|" + Path.GetFullPath(folder.Folder));
                if (folder.Purged) tally.Remove(folder.Bytes, early: false);
                else tally.Keep(folder.Code ?? SetAsideKept.Unread, early: false);
            }

            foreach (var result in done.Slims.Where(result => slim.Contains(Path.GetFullPath(result.Folder))))
            {
                met.Add("slim|" + Path.GetFullPath(result.Folder));
                if (result.Removed.Count > 0) tally.Remove(result.Bytes, early: false);
                else if (result.Unread is not null) tally.Keep(SetAsideKept.Unread, early: false);
                else if (result.Failed.Count > 0) tally.Keep(SpaceKept.Failed, early: false);
                else tally.Keep(result.Kept.FirstOrDefault()?.Why ?? SpaceKept.Changed, early: false);
            }

            // A folder listed and not met went, or is no longer what the list said (no longer past, or its slim no longer due).
            foreach (var (unit, folder) in folders.Where(each => !each.Key.Early && each.Key.Why is "past" or "slim"))
            {
                if (met.Contains($"{unit.Why}|{Path.GetFullPath(folder)}")) continue;
                tally.Keep(Directory.Exists(folder) ? SpaceKept.Changed : SpaceKept.Gone, early: false);
            }
        }

        foreach (var (unit, folder) in folders.Where(each => each.Key.Early))
        {
            ct.ThrowIfCancellationRequested();
            var (bytes, kept) = trees.RemoveSetAsideEarly(folder, unit.Own);
            if (kept is null) tally.Remove(bytes, early: true);
            else tally.Keep(kept, early: true);
        }

        foreach (var unit in folders.Keys.Where(unit => !unit.Early && unit.Why is not ("past" or "slim"))) tally.Keep(SpaceKept.Refused, early: false);
    }

    private static async Task DownloadsAsync(SpaceFreeWorld world, IReadOnlyList<SpaceAsked> units, SpaceTally tally, string door, CancellationToken ct)
    {
        var asked = new List<(SpaceAsked Unit, DownloadAsked Version)>();
        foreach (var unit in units)
        {
            var parts = unit.Id.Split('/');
            if (parts is [("agent" or "tool") and var kind, { Length: > 0 } name, { Length: > 0 } version]) asked.Add((unit, new DownloadAsked(kind, name, version, unit.Why == "unfinished")));
            else tally.Keep(SpaceKept.Refused, unit.Early);
        }

        if (asked.Count == 0) return;
        var record = KeepingRecord.Read(world.Home);
        var judge = world.JudgeOrFiles() with { FirstUnused = key => record.Unused.TryGetValue(key, out var since) ? since : null };
        var cleared = await DownloadClearing.ClearAsync(
            world.Home, [.. asked.Select(each => each.Version)], judge, world.Log, world.Move, ct, door).ConfigureAwait(false);
        for (var at = 0; at < cleared.Count; at++)
        {
            var early = asked[at].Unit.Early;
            switch (cleared[at].Outcome)
            {
                case "removed" or "unfinished": tally.Remove(cleared[at].Version?.Bytes ?? 0, early); break;
                case "kept": tally.Keep(cleared[at].Version?.Keeps ?? "unread", early); break;
                default: tally.Keep(SpaceKept.Gone, early); break;
            }
        }
    }

    private static void Logs(SpaceFreeWorld world, IReadOnlyList<SpaceAsked> units, SpaceTally tally)
    {
        string folder;
        try
        {
            folder = DaorisHome.Entry(world.Home, HomeEntry.Logs);
        }
        catch (HomeEntryTwiceException)
        {
            foreach (var unit in units) tally.Keep(SetAsideKept.Unread, unit.Early);
            return;
        }

        // Judged again at the person's days as they are now: a day the list named that a longer rule keeps since is changed, and stays.
        var days = LogDays(world.Policy(world.Home));
        foreach (var unit in units)
        {
            if (MachineLog.Remove(folder, unit.Id, world.Now(), days) is { } bytes) tally.Remove(bytes, unit.Early);
            else tally.Keep(File.Exists(Path.Combine(folder, Path.GetFileName(unit.Id))) ? SpaceKept.Changed : SpaceKept.Gone, unit.Early);
        }
    }

    private static async Task CachesAsync(SpaceFreeWorld world, IReadOnlyList<SpaceAsked> units, SpaceTally tally, CancellationToken ct)
    {
        if (world.Caches is not { } caches)
        {
            foreach (var unit in units) tally.Keep(SpaceKept.NoShell, unit.Early);
            return;
        }

        var paths = units.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).ToList();
        var cleared = await caches.WhileStoppedAsync(() => BrowserCacheClearing.Clear(world.Home, paths, world.Move), ct).ConfigureAwait(false);
        foreach (var unit in units)
        {
            if (cleared is null) tally.Keep(SpaceKept.Running, unit.Early);
            else if (cleared.Removed.TryGetValue(unit.Id, out var bytes)) tally.Remove(bytes, unit.Early);
            else tally.Keep(cleared.Kept.GetValueOrDefault(unit.Id, SpaceKept.Gone), unit.Early);
        }
    }

    /// <summary>An earlier build's two folders and a failed update's, each by its exact path: moved aside first, then removed.</summary>
    private static void Paths(SpaceFreeWorld world, string kind, IReadOnlyList<SpaceAsked> units, SpaceTally tally)
    {
        var known = EarlyPaths(world.Home).Where(each => each.Kind == kind).ToDictionary(each => each.Id, each => each.Path, StringComparer.Ordinal);
        foreach (var unit in units)
        {
            if (!known.TryGetValue(unit.Id, out var path))
            {
                tally.Keep(SpaceKept.Refused, unit.Early);
                continue;
            }

            var kept = KeptPath(world.Home, kind, path);
            if (kept is not null)
            {
                tally.Keep(kept, unit.Early);
                continue;
            }

            var aside = $"{path}.{Guid.NewGuid().ToString("N")[..8]}{DownloadClearing.Aside}";
            try
            {
                AtomicFile.MoveFolder(path, aside, 5, 200, world.Move);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                tally.Keep(SpaceKept.InUse, unit.Early);
                continue;
            }

            try
            {
                tally.Remove(TreeSetAside.DeleteNeverFollowing(aside), unit.Early);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                tally.Keep(SpaceKept.Failed, unit.Early);
            }
        }
    }

    /// <summary>Why an exact path stays, judged right before it goes; null where it may go.</summary>
    private static string? KeptPath(string home, string kind, string path)
    {
        if (!Directory.Exists(path)) return SpaceKept.Gone;
        if (Linked(DaorisHome.FolderOf(home).Folder, path) || Linked(home, path)) return SetAsideKept.Linked;
        if (kind == "left-over" && NamedByThisBuild(home, path)) return SpaceKept.Named;
        if (kind == "update-failed")
        {
            var install = DaorisHome.FolderOf(home).Folder;
            if (StagedBuild.UnderWay(install)) return SpaceKept.Updating;
            if (StagedBuild.RunningFrom(path).Count > 0) return SpaceKept.InUse;
        }

        return null;
    }

    /// <summary>The paths the early groups name by their exact place: the browser's caches, an earlier build's two folders, a failed update's.</summary>
    private static IEnumerable<(string Kind, string Id, string Path)> EarlyPaths(string home)
    {
        string? engine;
        try
        {
            engine = DaorisHome.Entry(home, HomeEntry.BrowserEngine);
        }
        catch (HomeEntryTwiceException)
        {
            engine = null;
        }

        if (engine is not null)
        {
            foreach (var path in SpaceKinds.BrowserCaches) yield return ("browser-cache", path, Path.Combine([engine, .. path.Split('/')]));
        }

        // The card's places for what an earlier build left, in the table's order: the one place each exact path is named.
        var left = SpaceKinds.Places(home, "left-over").Places;
        for (var at = 0; at < SpaceKinds.EarlierBuild.Count; at++) yield return ("left-over", SpaceKinds.EarlierBuild[at], left[at]);
        var folder = DaorisHome.FolderOf(home).Folder;
        if (File.Exists(Path.Combine(folder, DaorisHome.InstallMarker)))
        {
            yield return ("update-failed", $"{StagedBuild.Folder}/{StagedBuild.Failed}", Path.Combine(folder, StagedBuild.Folder, StagedBuild.Failed));
        }
    }

    /// <summary>Whether an entry of this build's table lies at or under <paramref name="path"/>: then it is this build's, never an earlier one's.</summary>
    private static bool NamedByThisBuild(string home, string path)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var entry in HomeEntry.Table)
        {
            IReadOnlyList<string> places;
            try
            {
                places = DaorisHome.Places(home, entry);
            }
            catch (Exception error) when (error is HomeEntryTwiceException or ArgumentException)
            {
                return true;
            }

            foreach (var place in places)
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(place));
                if (string.Equals(full, top, same) || full.StartsWith(top + Path.DirectorySeparatorChar, same)) return true;
            }
        }

        return false;
    }

    /// <summary>Whether a link lies on the way from <paramref name="root"/> down to <paramref name="path"/>, it included.</summary>
    private static bool Linked(string root, string path)
    {
        var under = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (Path.IsPathRooted(under) || under.StartsWith("..", StringComparison.Ordinal)) return false;
        var at = Path.GetFullPath(root);
        foreach (var part in under.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            at = Path.Combine(at, part);
            var attributes = new DirectoryInfo(at).Attributes;
            if ((int)attributes != -1 && attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
        }

        return false;
    }

    /// <summary><paramref name="id"/>'s place under <paramref name="root"/>, exactly <paramref name="depth"/> plain segments; null for any other id.</summary>
    private static string? Under(string root, string id, int depth)
    {
        var parts = id.Split('/');
        if (parts.Length != depth || parts.Any(part => part.Length == 0 || part is "." or ".." || part.IndexOfAny(['\\', ':']) >= 0
                                                       || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            return null;
        }

        return Path.Combine([root, .. parts]);
    }

    private static long? Length(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The list as the route and <c>--json</c> answer it, field for field: every field added, never renamed.</summary>
    public static JsonObject Json(SpacePlan plan)
    {
        var now = new JsonArray();
        foreach (var unit in plan.Units.Where(unit => !unit.Early)) now.Add(Unit(unit));
        var early = new JsonArray();
        foreach (var group in SpacePlan.Groups)
        {
            var units = plan.Units.Where(unit => unit.Early && unit.Kind == group).ToList();
            if (units.Count == 0) continue;
            var listed = new JsonArray();
            foreach (var unit in units) listed.Add(Unit(unit));
            early.Add(new JsonObject
            {
                ["group"] = group,
                ["count"] = units.Count,
                ["bytes"] = units.All(unit => unit.Bytes is not null) ? units.Sum(unit => unit.Bytes!.Value) : null,
                ["own"] = units.Count(unit => unit.Own == true),
                ["units"] = listed,
            });
        }

        var unread = new JsonObject();
        foreach (var (kind, why) in plan.Unread.OrderBy(each => each.Key, StringComparer.Ordinal)) unread[kind] = why;
        return new JsonObject
        {
            ["schema"] = SystemSpace.Schema,
            ["at"] = Moments.Write(plan.At),
            ["now"] = now,
            ["early"] = early,
            ["doors"] = new JsonArray([.. SpacePlan.Doors.Select(door => (JsonNode?)JsonValue.Create(door))]),
            ["unread"] = unread,
        };
    }

    private static JsonObject Unit(SpaceUnit unit) => new()
    {
        ["kind"] = unit.Kind, ["id"] = unit.Id, ["early"] = unit.Early, ["why"] = unit.Why, ["bytes"] = unit.Bytes,
        ["at"] = Moments.Write(unit.At), ["workspace"] = unit.Workspace, ["repository"] = unit.Repository, ["own"] = unit.Own,
    };

    /// <summary>
    /// The units a press names, as the page and the terminal send them: <c>{ units: [...], early: [...] }</c>, each <c>{ kind, id, why?,
    /// own? }</c> as the list gave it. A call naming none, or a unit without its kind and id, is malformed.
    /// </summary>
    public static IReadOnlyList<SpaceAsked> Asked(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) throw new ArgumentException("a press names `units` or `early` to remove.");
        var asked = new List<SpaceAsked>();
        foreach (var (field, early) in new[] { ("units", false), ("early", true) })
        {
            if (!body.TryGetProperty(field, out var list)) continue;
            if (list.ValueKind != JsonValueKind.Array) throw new ArgumentException($"`{field}` must be a list of the listed units.");
            foreach (var each in list.EnumerateArray())
            {
                string? Text(string name) =>
                    each.ValueKind == JsonValueKind.Object && each.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (Text("kind") is not { } kind || !Kinds.Contains(kind) || Text("id") is not { Length: > 0 } id)
                {
                    throw new ArgumentException($"each of `{field}` must be `{{ kind, id }}` as the list gave it.");
                }

                var own = each.TryGetProperty("own", out var said) && said.ValueKind == JsonValueKind.True;
                asked.Add(new SpaceAsked(kind, id, early, Text("why"), own));
            }
        }

        if (asked.Count == 0) throw new ArgumentException("a press names `units` or `early` to remove.");
        return asked;
    }
}

/// <summary>What a run's kind removed and kept, as it goes.</summary>
internal sealed class SpaceTally
{
    public int Removed { get; private set; }

    public long Bytes { get; private set; }

    public int EarlyRemoved { get; private set; }

    public long EarlyBytes { get; private set; }

    public Dictionary<string, int> Kept { get; } = new(StringComparer.Ordinal);

    public void Remove(long bytes, bool early)
    {
        Removed++;
        Bytes += bytes;
        if (!early) return;
        EarlyRemoved++;
        EarlyBytes += bytes;
    }

    public void Keep(string why, bool early) => Kept[why] = Kept.GetValueOrDefault(why) + 1;
}

/// <summary>One kind of a run, as <c>SYSTEM_SPACE</c>'s <c>run</c> says it: waiting, running or done, with what it removed and kept.</summary>
public sealed record SpaceRunKind(string Id, string State, int Removed, long Bytes, IReadOnlyDictionary<string, int> Kept);

/// <summary>
/// A press's run (KEEP1e, the cleanup design §3.3, Codex 5.3): its id, its door, when it started and ended, how it ended, and each kind
/// waiting, running or done. The page reads it in <c>SYSTEM_SPACE</c>'s <c>run</c>, the current or last run of the process, and the
/// terminal reads it until it ends. Each kind's end is told (<see cref="KindEnded"/>), the whole's end completes <see cref="Finished"/>.
/// </summary>
public sealed class SpaceRun
{
    private readonly object _gate = new();
    private readonly List<SpaceRunKind> _kinds;
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal SpaceRun(string id, string door, DateTimeOffset started, IEnumerable<string> kinds)
    {
        (Id, Door, Started) = (id, door, started);
        _kinds = [.. kinds.Select(kind => new SpaceRunKind(kind, "waiting", 0, 0, new Dictionary<string, int>()))];
    }

    /// <summary>Its id, which <c>keeping.json</c>'s <c>last</c> and the machine log's <c>space.freed</c> name: the run lock's.</summary>
    public string Id { get; }

    /// <summary>Its door: <c>press</c> from a window (a terminal's over the pipe included), <c>terminal</c> alone.</summary>
    public string Door { get; }

    public DateTimeOffset Started { get; }

    public DateTimeOffset? Ended { get; private set; }

    /// <summary><c>done</c> (every unit went), <c>kept</c> (one was kept) or <c>stopped</c> (it ended before its last unit); null while it runs.</summary>
    public string? Outcome { get; private set; }

    /// <summary>Each kind, in the order it runs.</summary>
    public IReadOnlyList<SpaceRunKind> Kinds
    {
        get
        {
            lock (_gate) return [.. _kinds];
        }
    }

    /// <summary>Told each kind's id as it ends.</summary>
    public event Action<string>? KindEnded;

    /// <summary>Completes once the run has ended, however it ended.</summary>
    public Task Finished => _finished.Task;

    internal void Begin(string kind) => Set(kind, each => each with { State = "running" });

    internal void End(string kind, SpaceTally tally)
    {
        Set(kind, each => each with { State = "done", Removed = tally.Removed, Bytes = tally.Bytes, Kept = new Dictionary<string, int>(tally.Kept) });
        try
        {
            KindEnded?.Invoke(kind);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Telling is best-effort: the run is read whole by its next reader.
        }
    }

    internal void Done() => Finish(Kinds.Any(each => each.Kept.Count > 0) ? "kept" : "done");

    internal void Stopped() => Finish("stopped");

    private void Finish(string outcome)
    {
        lock (_gate)
        {
            if (Outcome is not null) return;
            (Outcome, Ended) = (outcome, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>The run lock let go: <see cref="Finished"/> completes, so a run after it finds the lock free.</summary>
    internal void Release() => _finished.TrySetResult();

    private void Set(string kind, Func<SpaceRunKind, SpaceRunKind> change)
    {
        lock (_gate)
        {
            var at = _kinds.FindIndex(each => each.Id == kind);
            if (at >= 0) _kinds[at] = change(_kinds[at]);
        }
    }

    /// <summary>The run as <c>SYSTEM_SPACE</c>'s <c>run</c> answers it.</summary>
    public JsonObject Json()
    {
        var kinds = new JsonArray();
        foreach (var each in Kinds)
        {
            var kept = new JsonObject();
            foreach (var (why, count) in each.Kept.OrderBy(pair => pair.Key, StringComparer.Ordinal)) kept[why] = count;
            kinds.Add(new JsonObject
            {
                ["id"] = each.Id, ["state"] = each.State,
                ["removed"] = new JsonObject { ["count"] = each.Removed, ["bytes"] = each.Bytes }, ["kept"] = kept,
            });
        }

        return new JsonObject
        {
            ["id"] = Id, ["door"] = Door, ["started"] = Moments.Write(Started), ["ended"] = Moments.Write(Ended), ["outcome"] = Outcome,
            ["kinds"] = kinds,
        };
    }
}

/// <summary>How a press to start a run was answered (<see cref="SpaceRuns.Start"/>).</summary>
/// <param name="Run">The run started, or the one under way it joined; null where another holds the home's run lock.</param>
/// <param name="Joined">A run was under way in this process, so the press answered its id.</param>
/// <param name="Held">Who holds the run lock elsewhere; null where none, or none named.</param>
public sealed record SpaceStart(SpaceRun? Run, bool Joined, KeepingHolder? Held);

/// <summary>
/// The press's runs, per home, in this process (KEEP1e): one at a time, under the home's run lock, tried and never waited for (the cleanup
/// design §3.1). A press while a run is under way here joins it; one while another process, or the look's pass, holds the lock is refused
/// naming its holder. The last run is kept, so a page opened again reads the same run.
/// </summary>
public sealed class SpaceRuns
{
    /// <summary>This process's.</summary>
    public static SpaceRuns Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, SpaceRun> _last = new(StringComparer.Ordinal);

    /// <summary>The run under way under <paramref name="home"/>, or the last that ended; null where this process ran none.</summary>
    public SpaceRun? Last(string home)
    {
        lock (_gate) return _last.TryGetValue(Key(home), out var run) ? run : null;
    }

    /// <summary>
    /// Start a run of <paramref name="asked"/> through <paramref name="door"/>, in the background: the run lock taken here and held to its
    /// end. <paramref name="told"/> hears each kind's end and the whole's.
    /// </summary>
    public SpaceStart Start(
        SpaceFreeWorld world, string door, IReadOnlyList<SpaceAsked> asked, Action<SpaceRun>? told = null, CancellationToken ct = default)
    {
        var key = Key(world.Home);
        lock (_gate)
        {
            if (_last.TryGetValue(key, out var under) && under.Outcome is null) return new(under, true, null);
            var held = KeepingRun.TryTake(world.Home, door);
            if (held is null) return new(null, false, KeepingRun.Holder(world.Home));
            var kinds = SpaceFree.Kinds.Where(kind => asked.Any(unit => unit.Kind == kind));
            var run = new SpaceRun(held.Id, door, world.Now(), kinds);
            if (told is not null) run.KindEnded += _ => told(run);
            _last[key] = run;
            _ = Task.Run(async () =>
            {
                try
                {
                    await SpaceFree.RunAsync(world, asked, run, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or OperationCanceledException
                                                  or JsonException or InvalidOperationException)
                {
                    // A read that failed mid-run stops it: what it removed is said in its kinds done, and the rest stays.
                    run.Stopped();
                }
                finally
                {
                    held.Dispose();
                    run.Stopped();
                    run.Release();
                    try
                    {
                        told?.Invoke(run);
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        // Best-effort, as each kind's.
                    }
                }
            }, CancellationToken.None);
            return new(run, false, null);
        }
    }

    private static string Key(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }
}
