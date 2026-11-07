using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The person's <i>Free up space…</i>: listed first, then pressed (D88's shape, as
/// CACHECLEAR1's). The list changes nothing: per kind, what its rule takes now, and what could go early by the person's word (set-aside
/// folders before their date, unused downloads within their days, the browser's caches, what an earlier build left, a failed update's
/// folder). A run removes exactly the units a press names, each judged again right before it goes through its kind's one removal; one
/// changed since the list is kept and counted. The window's route, <c>daoris space free</c> and nothing else call it.
/// </summary>
/// <remarks>
/// <para><b>Each kind's one removal</b>: the set-aside folders through the purge and the slim (<see cref="SetAsideKeeping"/>),
/// one before its date whole with its manifest; the downloads through CACHECLEAR1's act (<see cref="DownloadClearing.ClearAsync"/>); the
/// log's days through its prune (<see cref="MachineLog.Remove"/>); the browser's caches through the shell's host
/// (<see cref="BrowserCacheClearing"/>); an earlier build's two folders and a failed update's by their exact paths, moved aside first.</para>
/// <para><b>One run at a time</b>: every run holds the home's run lock (<see cref="KeepingRun"/>), taken by <see cref="SpaceRuns"/>.</para>
/// </remarks>
public static class SpaceFree
{
    /// <summary>The kinds a run removes, in the order it removes them.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["set-aside", "downloads", "logs", "browser-cache", "left-over", "update-failed"];

    /// <summary>The card's kind a run's kind is counted under, for the sizes counted again at its end.</summary>
    public static string CardKind(string kind) => kind == "update-failed" ? "rest" : kind;

    /// <summary>The machine log's days by the person's rule, which the press lists and removes by as the look's prune does.</summary>
    private static int LogDays(KeepPolicy policy) => (int)policy.Logs.Days.TotalDays;

    /// <summary>
    /// The finished trees' record pass at the press's run, before it removes: each stopped or refused
    /// folder resumed and each record due posted first, from its manifest, so a folder whose branch went reaches its record before the
    /// person's word can remove it, and a recorded folder's hour is checked. The list never runs it: it reads the classes as they stand, and
    /// changes nothing (the second maker's read, finding 7). Nothing where it has no service, or its registry did not read: then no recorded
    /// folder's hour is due (finding 1).
    /// </summary>
    /// <returns>The registry's checkouts, for the run's holding checks; null where it has none, or it did not read.</returns>
    private static async Task<Func<string, string, string?>?> RecordAsync(SpaceFreeWorld world, Func<string, bool>? only, CancellationToken ct)
    {
        if (world.Registry is not { } registry) return null;
        IReadOnlyList<RepoView>? rows;
        try
        {
            rows = await registry(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            rows = null;
        }

        var roots = SessionTrees.RootsOf(rows);
        try
        {
            await new SessionTrees(world.Home).RecordSetAsideAsync(roots, world.PostRecord, world.Now, ct, only).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HomeEntryTwiceException)
        {
            // The run reads the folders as they stand.
        }

        return roots;
    }

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
                    Class = ListedClass.Of(folder), Ref = folder.Ref,
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

        // The machine log's days past the person's days, which the look's daily prune takes: what is left until the next.
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
                    case "set-aside":
                    {
                        // The listed folders' steps again, a record due posted first, before any of them goes. The last fixes'
                        // check, finding 3: each listed class is compared with the class read before this run's own record pass, which moves a
                        // stopped folder to its record by the press's own word, so it is no change since the list.
                        var before = ClassesNow(world);
                        var checkouts = await RecordAsync(world, null, ct).ConfigureAwait(false);
                        await SetAsideAsync(world, mine, tally, checkouts, before, ct).ConfigureAwait(false);
                        break;
                    }
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

    /// <summary>Each set-aside folder's class and ref as the list words them, by its id, read now; none where the folders do not read.</summary>
    private static Dictionary<string, (string Class, string? Ref)> ClassesNow(SpaceFreeWorld world)
    {
        try
        {
            return new SessionTrees(world.Home).ListSetAside(world.Policy(world.Home), world.Now())
                .ToDictionary(folder => folder.Id, folder => (ListedClass.Of(folder), folder.Ref), StringComparer.Ordinal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HomeEntryTwiceException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    /// <param name="checkouts">The registry's checkouts, for a recorded folder's holding check right before its rename; with none, none goes by the hour.</param>
    /// <param name="before">Each folder's class and ref read before the run's record pass (<see cref="ClassesNow"/>), which its listed ones are compared with.</param>
    private static async Task SetAsideAsync(
        SpaceFreeWorld world, IReadOnlyList<SpaceAsked> units, SpaceTally tally, Func<string, string, string?>? checkouts,
        IReadOnlyDictionary<string, (string Class, string? Ref)> before, CancellationToken ct)
    {
        var trees = new SessionTrees(world.Home);
        var roots = trees.SetAsideRoots;
        var same = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var folders = new Dictionary<SpaceAsked, string>();
        foreach (var unit in units)
        {
            // A listed id names its folder at whichever place of the trees holds it, the new first; where neither does, the new
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
            var done = await trees.KeepSetAsideAsync(world.Policy(world.Home), world.Now(), TimeSpan.MaxValue, ct, checkouts,
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
            if (unit.Class is not null && before.TryGetValue(unit.Id, out var was) && (unit.Class, unit.Ref) != was)
            {
                tally.Keep(SpaceKept.Changed, early: true);
                continue;
            }

            // What it reads now, after the run's record pass: the removal compares this with what it reads right before its rename.
            var judged = TreeSetAside.MovedAt(Path.GetFileName(folder)) is { } moved ? SetAsideEligibility.Of(folder, moved, TimeSpan.Zero, whole: true) : null;
            var now = judged is null ? null : ListedClass.Of(judged.Class, judged.Manifest?.Why, judged.Manifest?.Finished is not null);
            // The last fixes' check, finding 4: a folder listed recorded, or recorded now, is checked for its holder right before it goes, as the
            // hour's door checks it, and kept `changed` unless the check passes; with no registry, it cannot pass.
            if ((unit.Class == SetAsideClass.Recorded || now == SetAsideClass.Recorded) && judged?.Manifest is { Finished: { } finished } manifest)
            {
                var (held, _) = await SetAsideEligibility.HoldingAsync(checkouts?.Invoke(finished.Workspace, manifest.Repository), manifest, ct).ConfigureAwait(false);
                if (held != Holding.Passed)
                {
                    tally.Keep(SpaceKept.Changed, early: true);
                    continue;
                }
            }

            var (bytes, kept) = trees.RemoveSetAsideEarly(folder, unit.Own, unit.Class is null ? null : now, judged?.Manifest?.Finished?.Ref);
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

    /// <summary>
    /// How many of a group's set-aside folders are of each listed class (<see cref="SpaceUnit.Class"/>): what the list can say of
    /// each and nothing more (the second maker's read, finding 9).
    /// </summary>
    private static JsonObject Classes(IReadOnlyList<SpaceUnit> units) => new()
    {
        ["recorded"] = units.Count(unit => unit.Class == SetAsideClass.Recorded),
        ["awaiting"] = units.Count(unit => unit.Class == SetAsideClass.Awaiting),
        ["stands"] = units.Count(unit => unit.Class == ListedClass.Stands),
        ["stopped"] = units.Count(unit => unit.Class is SetAsideClass.Stopped or SetAsideClass.Refused),
        ["unheld"] = units.Count(unit => unit.Class == ListedClass.Unheld),
        // The last fixes' check, finding 9: a folder whose manifest does not read is listed early too, and counted as what it is.
        ["unread"] = units.Count(unit => unit.Class == SetAsideClass.Unread),
        ["unmarked"] = units.Count(unit => unit.Class is null or SetAsideClass.Unmarked),
    };

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
                // The set-aside folders by class, each said with what the list can say of it.
                ["classes"] = group == "set-aside" ? Classes(units) : null,
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
        ["class"] = unit.Class, ["ref"] = unit.Ref,
    };

    /// <summary>
    /// The units a press names, as the page and the terminal send them: <c>{ units: [...], early: [...] }</c>, each <c>{ kind, id, why?,
    /// own? }</c> as the list gave it. A call naming none, or a unit without its kind and id, is malformed.
    /// </summary>
    public static IReadOnlyList<SpaceAsked> Asked(JsonElement? payload) =>
        payload is not { ValueKind: JsonValueKind.Object } body
            ? throw new ArgumentException("a press names `units` or `early` to remove.")
            : Asked(body.TryGetProperty("units", out var units) ? units : null, body.TryGetProperty("early", out var listed) ? listed : null);

    /// <summary><see cref="Asked(JsonElement?)"/> from the two lists as sent: the <paramref name="units"/> and those that could go <paramref name="earlyList"/>; null where not sent.</summary>
    public static IReadOnlyList<SpaceAsked> Asked(JsonElement? units, JsonElement? earlyList)
    {
        var asked = new List<SpaceAsked>();
        foreach (var (field, early, sent) in new[] { ("units", false, units), ("early", true, earlyList) })
        {
            if (sent is not { } list) continue;
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
                asked.Add(new SpaceAsked(kind, id, early, Text("why"), own) { Class = Text("class"), Ref = Text("ref") });
            }
        }

        if (asked.Count == 0) throw new ArgumentException("a press names `units` or `early` to remove.");
        return asked;
    }
}

