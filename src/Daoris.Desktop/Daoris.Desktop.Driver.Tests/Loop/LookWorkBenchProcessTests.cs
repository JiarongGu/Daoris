using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PERF2: the look's <c>overlaps</c> and the workflow <c>nodes</c> on a benchmark home (ten open trees, each a driven session's, and thirty
/// runs bound to a shipped graph with its gates), each read warm, the git it starts counted through the process gate
/// (<see cref="GitStartTally"/>). A look where nothing moved starts no git for either; a look after one tree moved reads that tree alone.
/// </summary>
/// <remarks>
/// On the install (909 sessions, two to four driven ones) a look took 16 to 22 s: <c>overlaps</c> 8 to 10 s, and the nodes about 11 s a
/// pass, two passes a look. Here the overlaps started 20 git a look, two a tree, and a whole pass of the nodes one. Its claims are counts
/// and one bound of a second, so it runs alone.
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class LookWorkBenchProcessTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const int TreeCount = 10;
    private const int RunCount = 30;

    private static readonly TimeSpan StageBudget = TimeSpan.FromSeconds(1);

    private const string Repository = RunHome.Repository;

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "look-work", Guid.NewGuid().ToString("N")[..8]);
    private readonly StandInLedger _ledger = new();
    private string _root = "";
    private string _home = "";

    // Each open tree with its driven session and the commit it was opened at.
    private readonly List<(string Session, string Tree, string BaseCommit)> _trees = [];

    public async Task InitializeAsync()
    {
        _root = await GitSeed.PlainAsync(Path.Combine(_scratch, "checkouts", Repository), Repository);
        _home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(_home);
        _ledger.Register(Repository, _root);
        var trees = new SessionTrees(_home);
        for (var at = 0; at < TreeCount; at++)
        {
            var tree = await trees.OpenAsync(_root, Repository, "default");
            var baseCommit = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
            await File.WriteAllTextAsync(Path.Combine(tree.Path, $"part-{at}.txt"), $"part {at}\n");
            await GitAsync(tree.Path, "add", "-A");
            await GitAsync(tree.Path, "commit", "--quiet", "-m", $"part {at}");
            _ledger.Publish($"r{at:D2}", Repository);
            _ledger.Driven($"s{at}", $"r{at:D2}", tree.Path);
            _trees.Add(($"s{at}", tree.Path, baseCommit));
        }
    }

    public Task DisposeAsync()
    {
        _ledger.Dispose();
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Overlaps_read_a_tree_again_only_where_it_moved()
    {
        // Two of the trees change one file, so the look has an overlap to tell; each running session works its own quest.
        foreach (var (_, tree, _) in _trees.Take(2)) await File.WriteAllTextAsync(Path.Combine(tree, "shared.txt"), "edited\n");
        foreach (var (_, tree, _) in _trees.Take(2)) await GitAsync(tree, "add", "shared.txt");
        var snapshot = new Snapshot([], [], [.. _trees.Select((each, at) =>
            new SessionView(each.Session, Repository, "working") { Quest = $"q{at}", Tree = each.Tree, BaseCommit = each.BaseCommit })]);
        WorkOverlaps.Diff diff = TreeChangesKept.Of(_home).ReadAsync;
        Task<Snapshot> LookAsync() => WorkOverlaps.LookAsync(snapshot, new HashSet<string>(), diff, CancellationToken.None);

        await LookAsync();
        var (still, stillMs, looked) = await CountedAsync("overlaps where nothing moved", LookAsync);
        Assert.Equal(["part-0.txt", "shared.txt"], looked.Changed["q0"]);

        var moved = _trees[3].Tree;
        await File.WriteAllTextAsync(Path.Combine(moved, "moved.txt"), "moved\n");
        await GitAsync(moved, "add", "moved.txt");
        await GitAsync(moved, "commit", "--quiet", "-m", "moved");
        var (one, _, again) = await CountedAsync("overlaps after one tree moved", LookAsync);

        Assert.Empty(still.Starts);
        Assert.True(stillMs <= StageBudget.TotalMilliseconds, $"the look's overlaps took {stillMs} ms where nothing moved");
        Assert.All(one.Starts, start => Assert.EndsWith(Path.GetFileName(moved), start, StringComparison.Ordinal));
        Assert.Single(one.Starts);
        Assert.Contains("moved.txt", again.Changed["q3"]);
    }

    [Fact]
    public async Task Nodes_read_a_run_again_only_where_its_records_moved()
    {
        var home = _home;
        var digest = RunHome.SaveGraph(home, WorkflowPresets.WithGraphGates(WorkflowDefaults.All[0].Graph(), opinion: true, look: true).ToJsonString());
        var now = DateTimeOffset.UtcNow;
        var units = new List<WorkUnit>();
        for (var at = 0; at < RunCount; at++)
        {
            var run = $"r{at:D2}";
            RunHome.Bind(home, run, digest, now.AddMinutes(-RunCount + at));
            // The runs with a tree are done, so their content, opinion and look are read; ten more work on; the rest are done with no branch.
            units.Add(RunHome.QuestUnit(RunHome.Quest(run, at is >= TreeCount and < 2 * TreeCount ? "Taken" : "Done")));
            if (at >= TreeCount) continue;
            var (session, tree, _) = _trees[at];
            units.Add(RunHome.Branch($"branch-{at}", TreeTips.Branch(tree)!, now.AddHours(-1), [run]) with { Executors = [session] });
        }

        var read = new NodeUnitsRead(units, new Dictionary<string, string> { [Repository] = _root });
        var runtime = new NodeRuntime(new NodeRecordFiles(home, () => true), new ObservedPort(), () => DateTimeOffset.UtcNow, () => true);
        using var client = _ledger.Client();
        Task<NodeRuntimePass> PassAsync(bool whole)
        {
            var gates = new NodeGates(home, client);
            return runtime.PassAsync(new NodeRuntimeLook(DriverConfig.Empty, whole)
            {
                Units = _ => Task.FromResult(read),
                Steps = () => [],
                Tips = NodeTips.ReadAsync,
                Remotes = NodeTips.RemotesAsync,
                Gates = gates.ReadAsync,
                Content = gates.ContentAsync,
                Bound = NodeRuntime.BoundIn(home),
            }, CancellationToken.None);
        }

        await PassAsync(whole: true);
        var (whole, wholeMs, _) = await CountedAsync("a whole pass where nothing moved", () => PassAsync(whole: true));
        var (wake, _, _) = await CountedAsync("a wake's pass where nothing moved", () => PassAsync(whole: false));
        Assert.Equal(NodeStates.Active, new NodeRecordFiles(home).Read(RunUnits.IdOf("r00"))!.Statuses["look"].State);

        var moved = _trees[3].Tree;
        await File.WriteAllTextAsync(Path.Combine(moved, "moved.txt"), "moved\n");
        await GitAsync(moved, "add", "moved.txt");
        await GitAsync(moved, "commit", "--quiet", "-m", "moved");
        var (one, _, _) = await CountedAsync("a whole pass after one tree moved", () => PassAsync(whole: true));

        Assert.Empty(whole.Starts);
        Assert.Empty(wake.Starts);
        Assert.True(wholeMs <= StageBudget.TotalMilliseconds, $"a whole pass took {wholeMs} ms where nothing moved");
        var others = _trees.Select(each => each.Tree).Where(tree => tree != moved).Select(Path.GetFileName).ToList();
        Assert.DoesNotContain(one.Starts, start => others.Any(other => start.EndsWith(other!, StringComparison.Ordinal)));
        Assert.True(one.Starts.Count <= MovedTreeStarts, one.Report("a whole pass after one tree moved"));
    }

    // The moved tree's gates read again for its run's opinion and look, as a landing door reads them, with its repository's origin and
    // refs: measured at 12, held there.
    private const int MovedTreeStarts = 12;

    [Fact]
    public async Task A_staged_edit_is_read_at_the_next_look_and_one_not_staged_once_the_kept_read_is_old()
    {
        var at = DateTimeOffset.UtcNow;
        var kept = new TreeChangesKept(() => at);
        var (_, tree, baseCommit) = _trees[5];
        Assert.Equal(["part-5.txt"], await kept.ReadAsync(tree, baseCommit, CancellationToken.None));

        await File.WriteAllTextAsync(Path.Combine(tree, "staged.txt"), "staged\n");
        await GitAsync(tree, "add", "staged.txt");
        Assert.Equal(["part-5.txt", "staged.txt"], await kept.ReadAsync(tree, baseCommit, CancellationToken.None));

        // The part's file gone from the working tree moves no file of git's: the kept read stands until it is Again old.
        File.Delete(Path.Combine(tree, "part-5.txt"));
        Assert.Equal(["part-5.txt", "staged.txt"], await kept.ReadAsync(tree, baseCommit, CancellationToken.None));
        at += TreeChangesKept.Again;
        using var tally = GitStartTally.Open(_scratch);
        Assert.Equal(["staged.txt"], await kept.ReadAsync(tree, baseCommit, CancellationToken.None));
        Assert.Single(tally.Starts);
    }

    /// <summary>A read through the process gate, its git starts said with their folders and its time.</summary>
    private async Task<(GitStartTally Tally, long Ms, T Result)> CountedAsync<T>(string what, Func<Task<T>> read)
    {
        using var tally = GitStartTally.Open(_scratch);
        var watch = Stopwatch.StartNew();
        var result = await read();
        var ms = watch.ElapsedMilliseconds;
        output.WriteLine($"{tally.Report(what)}\n  in {ms} ms");
        return (tally, ms, result);
    }
}
