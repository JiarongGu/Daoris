using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The daily tree ledger: each reading measures a repository's copies with a hard link counted once, notes the
/// copies made and removed since the last reading, keeps the drive's free space beside what the copies wrote and freed, and writes a
/// notice once a day for a budget breached, the floor crossed and copies made with none reused in 24 hours.
/// </summary>
public sealed class TreeLedgerTests : IDisposable
{
    private const long Gb = 1L << 30;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-ledger", Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private long _free = 100 * Gb;

    public TreeLedgerTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Tree(string name) => Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "aurora", "engine", name);

    private string Write(string tree, string file, int bytes)
    {
        var path = Path.Combine(Tree(tree), file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private LedgerWorld World(long? budget = null) =>
        new(_home, () => _now, _ => new VolumeReading("D", "D:\\", _free, 500 * Gb), 30 * Gb, budget);

    private IReadOnlyList<Notice> Alerts() => (Notices.Read(_home, _now) ?? []).Where(notice => notice.Kind == Notices.Look).ToList();

    [Fact]
    public void A_reading_counts_a_link_once_and_the_copies_made_and_removed_since_the_last()
    {
        var shared = Write("s-a", "node_modules/x.js", 1000);
        Assert.True(FileLinks.Make(Path.Combine(Directory.CreateDirectory(Path.Combine(Tree("s-b"), "node_modules")).FullName, "x.js"), shared));
        Write("s-b", "own.txt", 10);

        TreeLedgerReadings.Record(World());
        var first = new TreeLedger(_home).Days(new DateOnly(2026, 11, 27)).Single();
        Assert.Equal((2, 1010L, 1000L, 0, 0), (first.Trees, first.SlotBytes, first.LinkedBytes, first.Made, first.Removed));

        _now = _now.AddHours(2);
        _free -= 400;
        Write("s-c", "built.bin", 500);
        GitSeed.Remove(Tree("s-a"));
        TreeLedgerReadings.Record(World());

        var row = new TreeLedger(_home).Days(new DateOnly(2026, 11, 27)).Single();
        Assert.Equal(("aurora", "engine", "2026-11-27"), (row.Workspace, row.Repository, row.Day));
        Assert.Equal((2, 1510L, 0L), (row.Trees, row.SlotBytes, row.LinkedBytes));
        Assert.Equal((1, 0, 1), (row.Made, row.Reused, row.Removed));
        Assert.Equal((500L, 0L), (row.Written, row.Freed));
        Assert.Equal((100 * Gb, 100 * Gb - 400), (row.FreeFirst, row.FreeLast));
        Assert.Equal(0.0, row.ReuseRate);

        var text = TreeLedgerPrint.Text([row]);
        Assert.Contains("made 1, reused 0, removed 1 (reuse 0%)", text);
        Assert.Contains("aurora/engine", text);
    }

    [Fact]
    public void A_floor_crossed_a_budget_breached_and_copies_made_with_none_reused_are_each_one_notice_a_day()
    {
        Write("s-a", "a.bin", 100);
        TreeLedgerReadings.Record(World());
        Assert.Empty(Alerts());

        _now = _now.AddHours(1);
        _free = 20 * Gb;
        Write("s-b", "b.bin", 100);
        var reading = TreeLedgerReadings.Record(World(budget: 150));
        TreeLedgerReadings.Record(World(budget: 150));

        Assert.Equal([LookSaid.TreesBudget, LookSaid.TreesFloor, LookSaid.TreesNoReuse], Alerts().Select(notice => notice.Code).Order(StringComparer.Ordinal));
        Assert.Equal(3, reading.Alerts.Count);
        Assert.All(Alerts(), notice => Assert.Contains(notice.Code, LookSaid.NewsCodes));

        // A copy reused in the day ends the alert's cause, which a later day's reading then does not write.
        new TreeLedger(_home).Note(new TreeMove("aurora", "engine", Tree("s-b"), TreeMoves.Reused, 0, 0, _now, ReadyMs: 1200));
        _now = _now.AddDays(1);
        _free = 100 * Gb;
        TreeLedgerReadings.Record(World());
        Assert.Equal(3, Alerts().Count);
    }

    [Fact]
    public void The_floor_and_budget_set_by_hand_in_the_machines_choices_stay_across_their_writers_edit()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "drivable": [], "holds": [], "cap": 2, "pollSeconds": 15, "treeFloorGb": 50, "treeBudgetGb": 9000 }""");
        Assert.Equal((50L << 30, (long?)null), (TreeLedgerSettings.Of(path).FloorBytes, TreeLedgerSettings.Of(path).BudgetBytes));

        DriverConfigDisk.Edit(path, config => config with { Cap = 3 });

        Assert.Equal(3, DriverConfig.Load(path).Cap);
        Assert.Equal(50, TreeLedgerSettings.Of(path).FloorGb);
        Assert.Equal(30L << 30, TreeLedgerSettings.Of(path + ".absent").FloorBytes);
    }

    [Fact]
    public void The_door_reads_its_days_and_refuses_a_word_it_does_not_take()
    {
        var said = new StringWriter();
        Assert.Equal(2, TreeLedgerPrint.Run(["--days", "0"], _home, () => _now, said));
        Assert.Equal(2, TreeLedgerPrint.Run(["--weeks"], _home, () => _now, said));
        Assert.Equal(0, TreeLedgerPrint.Run(["--days", "3"], _home, () => _now, said));
        Assert.Contains("no reading", said.ToString());
    }
}
