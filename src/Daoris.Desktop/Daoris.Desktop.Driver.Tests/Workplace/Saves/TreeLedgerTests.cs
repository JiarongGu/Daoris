using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The daily tree ledger: each reading measures a repository's trees by the one walk with a hard link counted once, notes the trees made
/// and removed since the last reading (a tree set aside under <c>.tidied</c> only once purged), keeps the drive's free space and the
/// space keeping's floor beside what the trees wrote and freed, and writes a notice once a day for a budget breached.
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

    private string Aside(string name) => Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), ".tidied", "aurora", "engine", name);

    private string Write(string tree, string file, int bytes)
    {
        var path = Path.Combine(Tree(tree), file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private LedgerWorld World(long? budget = null) =>
        new(_home, () => _now, _ => new VolumeReading("D", "D:\\", _free, 500 * Gb), _ => 30 * Gb, budget);

    private IReadOnlyList<Notice> Alerts() => (Notices.Read(_home, _now) ?? []).Where(notice => notice.Kind == Notices.Look).ToList();

    private LedgerDay Day(DateOnly day) => new TreeLedger(_home).Days(day).Single();

    [Fact]
    public void A_reading_counts_a_link_once_and_the_trees_made_and_removed_since_the_last()
    {
        // A hard link is read as one on Windows alone (FileLinks), where Daoris places dependencies by them.
        if (!OperatingSystem.IsWindows()) return;
        var shared = Write("s-a", "node_modules/x.js", 1000);
        Assert.True(FileLinks.Make(Path.Combine(Directory.CreateDirectory(Path.Combine(Tree("s-b"), "node_modules")).FullName, "x.js"), shared));
        Write("s-b", "own.txt", 10);

        TreeLedgerReadings.Record(World());
        var first = Day(new DateOnly(2026, 11, 27));
        Assert.Equal((2, 1010L, 1000L, 0, 0), (first.Trees, first.SlotBytes, first.LinkedBytes, first.Made, first.Removed));

        _now = _now.AddHours(2);
        _free -= 400;
        Write("s-c", "built.bin", 500);
        GitSeed.Remove(Tree("s-a"));
        TreeLedgerReadings.Record(World());

        var row = Day(new DateOnly(2026, 11, 27));
        Assert.Equal(("aurora", "engine", "2026-11-27"), (row.Workspace, row.Repository, row.Day));
        Assert.Equal((2, 1510L, 0L), (row.Trees, row.SlotBytes, row.LinkedBytes));
        Assert.Equal((1, 1), (row.Made, row.Removed));
        Assert.Equal((500L, 0L), (row.Written, row.Freed));
        Assert.Equal((100 * Gb, 100 * Gb - 400, 30 * Gb), (row.FreeFirst, row.FreeLast, row.Floor));

        var text = TreeLedgerPrint.Text([row]);
        Assert.Contains("made 1, removed 1, restored 0", text);
        Assert.Contains("aurora/engine", text);
        Assert.Contains("floor 30.0 GB", text);
    }

    [Fact]
    public void A_tree_moved_aside_is_counted_apart_and_removed_only_once_purged()
    {
        Write("s-a", "a.bin", 300);
        TreeLedgerReadings.Record(World());

        _now = _now.AddHours(1);
        var aside = Aside("s-a-20261127T110000000Z");
        Directory.CreateDirectory(Path.GetDirectoryName(aside)!);
        Directory.Move(Tree("s-a"), aside);
        TreeLedgerReadings.Record(World());
        var moved = Day(new DateOnly(2026, 11, 27));
        Assert.Equal((0, 1, 300L, 300L, 0, 0), (moved.Trees, moved.Aside, moved.AsideBytes, moved.SlotBytes, moved.Made, moved.Removed));

        _now = _now.AddHours(1);
        GitSeed.Remove(aside);
        TreeLedgerReadings.Record(World());
        var purged = Day(new DateOnly(2026, 11, 27));
        Assert.Equal((0, 0, 1, 300L), (purged.Aside, purged.Made, purged.Removed, purged.Freed));
    }

    [Fact]
    public void A_budget_breached_is_one_notice_a_day_and_the_ledger_writes_no_floor_notice_of_its_own()
    {
        Write("s-a", "a.bin", 100);
        Write("s-b", "b.bin", 100);
        _free = 20 * Gb;
        TreeLedgerReadings.Record(World(budget: 150));
        TreeLedgerReadings.Record(World(budget: 150));

        Assert.Equal([LookSaid.TreesBudget], Alerts().Select(notice => notice.Code));
        Assert.All(Alerts(), notice => Assert.Contains(notice.Code, LookSaid.NewsCodes));
        Assert.Contains("under it", TreeLedgerPrint.Text([Day(new DateOnly(2026, 11, 27))]));

        _now = _now.AddDays(1);
        TreeLedgerReadings.Record(World(budget: 150));
        Assert.Equal(2, Alerts().Count);
    }

    [Fact]
    public void A_day_is_the_utc_date_whatever_the_clocks_offset()
    {
        Write("s-a", "a.bin", 100);
        _now = new DateTimeOffset(2026, 11, 27, 22, 30, 0, TimeSpan.FromHours(-5));

        TreeLedgerReadings.Record(World());

        Assert.Equal("2026-11-28", Day(new DateOnly(2026, 11, 28)).Day);
        Assert.Empty(new TreeLedger(_home).Days(new DateOnly(2026, 11, 27)).Where(row => row.Day == "2026-11-27"));
    }

    [Fact]
    public void The_budget_is_a_setting_of_the_machines_choices_in_its_bounds()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "drivable": [], "holds": [], "cap": 2, "pollSeconds": 15, "treeBudgetGb": 9000 }""");
        Assert.Null(TreeBudgetSetting.At(path));

        DriverConfigDisk.Edit(path, config => config.WithTreeBudget(40));

        Assert.Equal(40, TreeBudgetSetting.At(path));
        Assert.Equal(40L << 30, TreeBudgetSetting.Bytes(TreeBudgetSetting.At(path)));
        Assert.Equal(40L << 30, LedgerWorld.OfThisMachine(_home, new MovedClock()).BudgetBytes);
        Assert.Equal((int?[])[null, null, 1, 4096, null], new long?[] { 0, -1, 1, 4096, 4097 }.Select(TreeBudgetSetting.Read));
        Assert.Throws<DriverException>(() => DriverConfig.Load(path).WithTreeBudget(0));
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
