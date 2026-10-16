using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-MILESTONE1a: the milestone in a terminal's words (<c>trees state</c> and <c>work state</c>, D50), the gather over a real home
/// (read-only: no record is written), and the host's wiring of both verbs. The console reads its words from <see cref="WorkWords"/>,
/// so the sentences are held here and the verbs' shape by the host's source, as the other trees verbs are (DriverCommandTests).
/// </summary>
public sealed class WorkWordsTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Directory.GetCurrentDirectory(), "_inventory-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkWordsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver.Host"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("the desktop's source folder was not found");
    }

    private static InventoryFacts Facts(GrownBranch[] branches, LandedBranch[]? landings = null, SessionTree[]? trees = null, string[]? inUse = null) =>
        new(branches, landings ?? [], trees ?? [], [], [], (inUse ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase), _ => false, Then);

    [Fact]
    public void A_merged_and_cleaned_unit_is_said_end_result_cleanup_and_recovery()
    {
        var held = new HeldFact(Then, Then, EndBy.Merged, "a1b2c3d4e5f6", "main", "main", "m1", "repo");
        var unit = Assert.Single(WorkInventory.Derive(Facts([new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a", Held = held, GoneAt = Then }])));

        var said = string.Join("\n", WorkWords.Milestone(unit));

        Assert.Contains("unit-a  engine  daoris/s1", said);
        Assert.Contains("end       reached: every commit of it is on `main`, at a1b2c3d4", said);
        Assert.Contains("result    kept: `main`", said);
        Assert.Contains("cleanup   completed: removed session-branch `daoris/s1` (every commit of it is on `main`)", said);
        Assert.Contains("recovery  none retained", said);
    }

    [Fact]
    public void A_pending_cleanup_names_what_waits_and_a_blocked_entry_says_blocked()
    {
        var tree = new SessionTree(Path.GetFullPath("held-tree"), "default", "engine", "daoris/s1");
        var held = new HeldFact(Then, Then, EndBy.Merged, "a1b2c3d4e5f6", "main", "main", "m1", "repo");
        var unit = Assert.Single(WorkInventory.Derive(Facts([new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a", Held = held }], trees: [tree], inUse: [tree.Path])));

        var said = string.Join("\n", WorkWords.Milestone(unit, entries: true));

        Assert.Contains("cleanup   pending:", said);
        Assert.Contains($"waits    tree `{tree.Path}`: a session still running or waiting holds it", said);
        Assert.Contains("temporary pending  tree", said);

        var stray = Assert.Single(WorkInventory.Derive(Facts([], trees: [new SessionTree(Path.GetFullPath("stray"), "default", "engine", "daoris/stray")])));
        Assert.Contains("blocked  tree", string.Join("\n", WorkWords.Milestone(stray)));
    }

    [Fact]
    public void Nothing_recorded_is_said_not_left_silent()
    {
        Assert.Equal("work: no unit of work is recorded on this machine.", Assert.Single(WorkWords.All([])));
    }

    /// <summary>The gather reads the home as it stands and writes nothing: the records' files are the same bytes after it.</summary>
    [Fact]
    public async Task The_gather_reads_the_homes_records_and_writes_none()
    {
        new SessionBranches(_home).Record(new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a" });
        new LandedBranches(_home).Record(new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", null, null, Then) { Units = ["unit-a"] });
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1"));
        var before = Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order().Select(file => (file, File.ReadAllBytes(file))).ToList();
        var unread = new List<string>();

        var units = await WorkInventories.MilestonesAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), unread);

        var unit = Assert.Single(units);
        Assert.Equal("handed", unit.End.State);
        Assert.Contains(unit.Entries, entry => entry.Kind == "history" && entry.Resource == "s1");
        Assert.Single(unread);
        Assert.Contains("no checkout here", unread[0]);
        var after = Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order().Select(file => (file, File.ReadAllBytes(file))).ToList();
        Assert.Equal(before.Select(each => each.file), after.Select(each => each.file));
        Assert.All(before.Zip(after), pair => Assert.Equal(pair.First.Item2, pair.Second.Item2));
    }

    /// <summary>The terminal's two doors (D50): `trees state` with no name, and `work state [unit]`, both usage-listed and read before the plugin ask.</summary>
    [Fact]
    public void The_host_answers_trees_state_with_no_name_and_work_state_over_the_one_inventory()
    {
        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        var work = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "WorkConsole.cs"));
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));

        Assert.Contains("case [\"state\"] or [\"state\", \"--entries\"]:", console);
        Assert.True(console.IndexOf("case [\"state\"] or", StringComparison.Ordinal) < console.IndexOf("case [\"state\", var named", StringComparison.Ordinal));
        Assert.Contains("| state [--entries]", console);
        Assert.Contains("| state [--entries]", DriverCommand.Usage);
        Assert.Contains("WorkConsole.ReadAsync(home)", console);
        Assert.Contains("args is [\"work\", \"state\", ..", program);
        Assert.Contains("WorkConsole.MilestonesAsync(", program);
        Assert.Contains("WorkInventories.MilestonesAsync(", work);
        Assert.Contains("work state [<unit>]", DriverCommand.Usage);
        Assert.Contains("work state [<unit>]", program);
        // A look: no lease taken, no record written, no window told.
        Assert.DoesNotContain("Told(", work);
    }
}
