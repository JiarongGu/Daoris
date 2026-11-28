namespace Daoris.Driver;

/// <summary>What a ledger reading is handed: the home, the time, the drive's reading, the drive's floor and the trees' budget in bytes.</summary>
/// <param name="Floor">The space keeping's one floor of a volume (<c>keep.floor</c>, <see cref="FloorPolicy.Of"/>); null where it is off.</param>
/// <param name="BudgetBytes">The most a repository's trees may hold (<see cref="TreeBudgetSetting"/>); null where none is set.</param>
public sealed record LedgerWorld(string Home, Func<DateTimeOffset> Clock, Func<string, VolumeReading?> Volume, Func<VolumeReading, long?> Floor, long? BudgetBytes)
{
    /// <summary>This machine's: its clock, its volumes, its space keeping's floor and its trees' budget.</summary>
    public static LedgerWorld OfThisMachine(string home, TimeProvider clock)
    {
        var floor = KeepPolicy.Of(home).Floor;
        return new(home, clock.GetUtcNow, SpaceVolumes.Read, volume => floor.Of(volume.Total), TreeBudgetSetting.Bytes(TreeBudgetSetting.At(DriverConfig.ResolvePath(home))));
    }
}

/// <summary>One alert a reading found: its notice's code and what it says of the figures.</summary>
/// <param name="Figures">What the notice carries for its words to name: the repository and its figures, no sentence.</param>
public sealed record LedgerAlert(string Code, string Workspace, string Repository, string Said, string Figures);

/// <summary>What one reading kept and found.</summary>
public sealed record LedgerReading(IReadOnlyList<TreeReading> Readings, IReadOnlyList<LedgerAlert> Alerts);

/// <summary>
/// One reading of every repository's trees under the trees home: each tree measured by the one walk every measure of bytes shares
/// (<see cref="PlacesWalk"/>, a link never followed, a hard link counted once a repository), the trees made and removed since the last
/// reading noted where no door noted them, the saves pending, the drive's free space and its floor kept, and a notice written once a day
/// for a budget breached.
/// </summary>
/// <remarks>
/// <para>A tree moved aside into the trees home's <c>.tidied</c> still holds its bytes: it is counted apart, and is removed only when the
/// purge deletes it, never when it moves. The floor is the space keeping's, which What needs you already says when crossed; the ledger
/// keeps it beside the free space and says no notice of its own. Reads files and the store, never git. The first reading of a repository
/// is its baseline and notes no move.</para>
/// </remarks>
public static class TreeLedgerReadings
{
    public static LedgerReading Record(LedgerWorld world)
    {
        var now = world.Clock();
        var root = DaorisHome.Entry(world.Home, HomeEntry.Trees);
        var aside = Path.Combine(root, TreeSetAside.SetAsideFolder);
        var ledger = new TreeLedger(world.Home);
        var saves = new TreeSaveRecords(world.Home);
        var volume = Directory.Exists(root) ? world.Volume(root) : null;
        var floor = volume is null ? null : world.Floor(volume);
        var readings = new List<TreeReading>();
        var alerts = new List<LedgerAlert>();
        var repositories = Repositories(root).Concat(Repositories(aside)).Concat(ledger.Repositories()).Distinct().Order();
        foreach (var (workspace, name) in repositories)
        {
            var once = new FileRecords(FileLinks.RecordOf);
            var trees = Measured(Path.Combine(root, workspace, name), once);
            var setAside = Measured(Path.Combine(aside, workspace, name), once);
            var listed = trees.Concat(setAside).ToDictionary(pair => pair.Key, pair => pair.Value.Bytes - pair.Value.Linked, StringComparer.Ordinal);

            if (ledger.Last(workspace, name) is { } last)
            {
                var noted = ledger.Since(workspace, name, last.At).Select(move => move.Tree).ToHashSet(StringComparer.Ordinal);
                foreach (var (tree, bytes) in trees.Where(pair => !last.Listed.ContainsKey(pair.Key) && !noted.Contains(pair.Key)))
                {
                    ledger.Note(new TreeMove(workspace, name, tree, TreeMoves.Made, 0, bytes.Bytes - bytes.Linked, now));
                }

                // A tree gone from its place but set aside is not removed: it goes when the purge deletes its set-aside folder.
                foreach (var (tree, bytes) in last.Listed.Where(pair => !listed.ContainsKey(pair.Key) && !noted.Contains(pair.Key)
                             && !setAside.Keys.Any(moved => Path.GetFileName(moved).StartsWith(Path.GetFileName(pair.Key) + "-", StringComparison.Ordinal))))
                {
                    ledger.Note(new TreeMove(workspace, name, tree, TreeMoves.Removed, bytes, 0, now));
                }
            }

            var pending = saves.Pending(workspace, name);
            var slot = trees.Values.Sum(size => size.Bytes) + setAside.Values.Sum(size => size.Bytes);
            var reading = new TreeReading(workspace, name, now, trees.Count, slot, trees.Values.Sum(size => size.Linked) + setAside.Values.Sum(size => size.Linked),
                setAside.Count, setAside.Values.Sum(size => size.Bytes), pending.Count, pending.Sum(save => save.Bytes), volume, floor, listed);
            ledger.Keep(reading);
            readings.Add(reading);

            if (world.BudgetBytes is { } budget && slot > budget)
            {
                alerts.Add(new(LookSaid.TreesBudget, workspace, name, $"{workspace}/{name}: {SpaceCommand.Size(slot)} in trees, over {SpaceCommand.Size(budget)}",
                    $"{workspace}/{name} {SpaceCommand.Size(slot)} / {SpaceCommand.Size(budget)}"));
            }
        }

        // Once a day each: the id names the day and the repository, read against the notices once.
        var notices = alerts.Count == 0 ? [] : Notices.Read(world.Home, now) ?? [];
        foreach (var alert in alerts)
        {
            var id = $"{alert.Code}-{TreeLedger.DayOf(now)}-{alert.Workspace}-{alert.Repository}";
            if (notices.Any(notice => notice.Id == id)) continue;
            Notices.Add(world.Home, new Notice(id, Notices.Look, now, Workspace: alert.Workspace, Repository: alert.Repository, Act: alert.Figures, Code: alert.Code), now);
        }

        return new(readings, alerts);
    }

    /// <summary>Each tree folder under <paramref name="folder"/>, by its key, with its bytes and the linked part of them; none where it is absent.</summary>
    private static Dictionary<string, (long Bytes, long Linked)> Measured(string folder, FileRecords once)
    {
        var sizes = new Dictionary<string, (long Bytes, long Linked)>(StringComparer.Ordinal);
        foreach (var tree in Folders(folder))
        {
            var measure = PlacesWalk.Measure([tree], [], WiringBudget.Default, CancellationToken.None, once: once);
            sizes[TreeSaveRecords.Key(tree)] = (measure.Bytes, measure.Linked);
        }

        return sizes;
    }

    /// <summary>Each repository under <paramref name="root"/>, laid out <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;tree&gt;</c>; a name led by a dot is none.</summary>
    private static IEnumerable<(string Workspace, string Repository)> Repositories(string root) =>
        Folders(root).SelectMany(workspace => Folders(workspace).Select(repository => (Path.GetFileName(workspace), Path.GetFileName(repository))));

    private static IEnumerable<string> Folders(string folder) =>
        !Directory.Exists(folder) ? []
        : new DirectoryInfo(folder).EnumerateDirectories()
            .Where(each => !each.Name.StartsWith('.') && !each.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(each => each.FullName).Order(StringComparer.Ordinal);
}
