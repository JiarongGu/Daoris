namespace Daoris.Driver;

/// <summary>What a ledger reading is handed: the home, the time, the drive's reading, the floor and the copies' budget, each in bytes.</summary>
/// <param name="BudgetBytes">The most a repository's copies may hold; null where none is set.</param>
public sealed record LedgerWorld(string Home, Func<DateTimeOffset> Clock, Func<string, VolumeReading?> Volume, long FloorBytes, long? BudgetBytes)
{
    /// <summary>This machine's: its clock, its volumes and its settings (<see cref="TreeLedgerSettings"/>).</summary>
    public static LedgerWorld OfThisMachine(string home, TreeLedgerSettings settings, TimeProvider clock) =>
        new(home, clock.GetUtcNow, SpaceVolumes.Read, settings.FloorBytes, settings.BudgetBytes);
}

/// <summary>One alert a reading found: its notice's code and what it says of the figures.</summary>
/// <param name="Figures">What the notice carries for its words to name: the repository and its figures, no sentence.</param>
public sealed record LedgerAlert(string Code, string Workspace, string Repository, string Said, string Figures);

/// <summary>What one reading kept and found.</summary>
public sealed record LedgerReading(IReadOnlyList<TreeReading> Readings, IReadOnlyList<LedgerAlert> Alerts);

/// <summary>
/// One reading of every repository's copies under the trees home: each copy measured (a link never followed, a hard link
/// counted once a repository), the copies made and removed since the last reading noted where no door noted them, the saves pending and
/// the drive's free space kept, and a notice written once a day for a budget breached, the floor crossed, and copies made in 24 hours with
/// none reused.
/// </summary>
/// <remarks>Reads files and the store, never git. The first reading of a repository is its baseline and notes no move.</remarks>
public static class TreeLedgerReadings
{
    public static LedgerReading Record(LedgerWorld world)
    {
        var now = world.Clock();
        var root = DaorisHome.Entry(world.Home, HomeEntry.Trees);
        var ledger = new TreeLedger(world.Home);
        var saves = new TreeSaveRecords(world.Home);
        var volume = Directory.Exists(root) ? world.Volume(root) : null;
        var readings = new List<TreeReading>();
        var alerts = new List<LedgerAlert>();
        foreach (var repository in Repositories(root))
        {
            var (workspace, name) = (repository.Workspace, repository.Repository);
            var seen = new FolderBytes.Seen();
            var listed = new Dictionary<string, long>(StringComparer.Ordinal);
            long slot = 0, linked = 0;
            foreach (var tree in repository.Trees)
            {
                var size = FolderBytes.Measure(tree, seen);
                listed[TreeSaveRecords.Key(tree)] = size.Own;
                slot += size.Own + size.Linked;
                linked += size.Linked;
            }

            if (ledger.Last(workspace, name) is { } last)
            {
                var noted = ledger.Since(workspace, name, last.At).Select(move => move.Tree).ToHashSet(StringComparer.Ordinal);
                foreach (var (tree, bytes) in listed.Where(pair => !last.Listed.ContainsKey(pair.Key) && !noted.Contains(pair.Key)))
                {
                    ledger.Note(new TreeMove(workspace, name, tree, TreeMoves.Made, 0, bytes, now));
                }

                foreach (var (tree, bytes) in last.Listed.Where(pair => !listed.ContainsKey(pair.Key) && !noted.Contains(pair.Key)))
                {
                    ledger.Note(new TreeMove(workspace, name, tree, TreeMoves.Removed, bytes, 0, now));
                }
            }

            var pending = saves.Pending(workspace, name);
            var reading = new TreeReading(workspace, name, now, listed.Count, slot, linked, pending.Count, pending.Sum(save => save.Bytes), volume, listed);
            ledger.Keep(reading);
            readings.Add(reading);

            if (world.BudgetBytes is { } budget && slot > budget)
            {
                alerts.Add(new(LookSaid.TreesBudget, workspace, name, $"{workspace}/{name}: {SpaceCommand.Size(slot)} in copies, over {SpaceCommand.Size(budget)}",
                    $"{workspace}/{name} {SpaceCommand.Size(slot)} / {SpaceCommand.Size(budget)}"));
            }

            var day = ledger.Since(workspace, name, now.AddHours(-24));
            if (day.Any(move => move.Move == TreeMoves.Made) && !day.Any(move => move.Move == TreeMoves.Reused))
            {
                alerts.Add(new(LookSaid.TreesNoReuse, workspace, name, $"{workspace}/{name}: {day.Count(move => move.Move == TreeMoves.Made)} copies made in 24 hours, none reused",
                    $"{workspace}/{name} {day.Count(move => move.Move == TreeMoves.Made)}"));
            }
        }

        if (volume is not null && volume.Free < world.FloorBytes)
        {
            alerts.Add(new(LookSaid.TreesFloor, "", "", $"{volume.Root}: {SpaceCommand.Size(volume.Free)} free, under {SpaceCommand.Size(world.FloorBytes)}",
                $"{volume.Root} {SpaceCommand.Size(volume.Free)} / {SpaceCommand.Size(world.FloorBytes)}"));
        }

        // Once a day each: the id names the day, and a second write of it is the record's own refusal.
        foreach (var alert in alerts)
        {
            var id = $"{alert.Code}-{TreeLedger.DayOf(now)}" + (alert.Repository.Length == 0 ? "" : $"-{alert.Workspace}-{alert.Repository}");
            if ((Notices.Read(world.Home, now) ?? []).Any(notice => notice.Id == id)) continue;
            Notices.Add(world.Home, new Notice(id, Notices.Look, now, Workspace: alert.Workspace.Length == 0 ? null : alert.Workspace,
                Repository: alert.Repository.Length == 0 ? null : alert.Repository, Act: alert.Figures, Code: alert.Code), now);
        }

        return new(readings, alerts);
    }

    /// <summary>Each repository's copies under <paramref name="root"/>, laid out <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;copy&gt;</c>; a name led by a dot is no copy.</summary>
    private static IEnumerable<(string Workspace, string Repository, IReadOnlyList<string> Trees)> Repositories(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var workspace in Folders(root))
        {
            foreach (var repository in Folders(workspace))
            {
                yield return (Path.GetFileName(workspace), Path.GetFileName(repository), Folders(repository).ToList());
            }
        }
    }

    private static IEnumerable<string> Folders(string folder) =>
        new DirectoryInfo(folder).EnumerateDirectories()
            .Where(each => !each.Name.StartsWith('.') && !each.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(each => each.FullName).Order(StringComparer.Ordinal);
}
