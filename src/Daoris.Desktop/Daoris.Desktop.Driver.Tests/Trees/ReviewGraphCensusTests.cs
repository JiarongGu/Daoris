using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-ii's census (the git-app design P1.4, P1.7), in the fast half: Review's graph run over every combination of a review's
/// conditions, git stood in for under the seam (<see cref="WorkingTree.Runs"/>) by a scripted world whose every answer is held until
/// the test releases it. Released a round at a time, each of the review's nodes must start in the round its generated depth names
/// (none before its needs answered, none later than they had), no two Π reads may run together, the answer must be today's chain's on
/// the same world, and the commands the chain's but for the graph's wasted speculative starts and the shared <c>--show-toplevel</c>.
/// The untracked window's held cases are held at w = 1 and w = 4.
/// </summary>
public sealed class ReviewGraphCensusTests : IDisposable
{
    private const string Session = "aurora";
    // The record's base, which an ordinary tree's line shares ("same"): one distinct candidate, so no I.
    private const string Recorded = "abc1234abc1234abc1234abc1234abc1234abc12";
    private const string Here = "1111111111111111111111111111111111111111";
    private const string OnOrigin = "2222222222222222222222222222222222222222";
    private const string Grown = "3333333333333333333333333333333333333333";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "census", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;
    private readonly string _tree;

    public ReviewGraphCensusTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        _tree = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "games", "engine", Session);
        Directory.CreateDirectory(_tree);
        File.WriteAllText(Path.Combine(_tree, "own.txt"), "mine\n");
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
        GC.SuppressFinalize(this);
    }

    /// <summary>One review's conditions: what each of its git reads answers.</summary>
    public sealed record World(
        string Line = "set", bool RecordHeld = true, string Grown = "none", string Bases = "same", bool Incomparable = false,
        bool MergeConflicts = false, int Untracked = 0, bool Top = true, bool WFails = false, bool SFails = false, bool DFails = false,
        string Base = Recorded, bool PFails = false)
    {
        public override string ToString() => JsonSerializer.Serialize(this);
    }

    private (int Code, string Stdout, string Stderr) Answer(World world, IReadOnlyList<string> arguments)
    {
        var command = GitLaunch.Command(arguments);
        string Joined() => string.Join(' ', command);
        switch (command)
        {
            case ["rev-parse", "--show-toplevel"]:
                return (0, world.Top ? _tree : _scratch, "");
            case ["rev-parse", "--path-format=absolute", "--git-common-dir"]:
                return world.PFails ? (128, "", "fatal: no") : (0, Path.Combine(_scratch, "engine", ".git"), "");
            case ["rev-parse", "--abbrev-ref", "HEAD"]:
                return (0, $"daoris/{Session}\n", "");
            case ["merge-base", "--is-ancestor", var commit, "HEAD"]:
                return (commit == Recorded ? world.RecordHeld : world.Grown == "held") ? (0, "", "") : (1, "", "");
            case ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"]:
                return world.Line is "origin" or "dangling" ? (0, "origin/main\n", "") : (128, "", "fatal: ref refs/remotes/origin/HEAD is not a symbolic ref");
            case ["rev-parse", "--verify", "--quiet", var reference]:
                return reference == $"refs/heads/{world.Line}^{{commit}}" ? (0, Here + "\n", "") : (1, "", "");
            case ["merge-base", "HEAD", var reference]:
                var origin = reference.StartsWith("refs/remotes/", StringComparison.Ordinal);
                return (world.Bases, origin) switch
                {
                    ("none", _) or ("here", true) or ("origin", false) => (1, "", ""),
                    ("same", _) => (0, Recorded + "\n", ""),
                    _ => (0, (origin ? OnOrigin : Here) + "\n", ""),
                };
            case ["merge-base", "--independent", ..]:
                var candidates = command.Skip(2).ToArray();
                return (0, string.Join('\n', world.Incomparable ? candidates : candidates.Take(1)) + "\n", "");
            case ["rev-list", "--count", ..]:
                return (0, "0\n", "");
            case ["merge-tree", "--write-tree", ..]:
                return world.MergeConflicts ? (1, "4444444444444444444444444444444444444444\nconflict\n", "") : (0, "5555555555555555555555555555555555555555\n", "");
            case ["symbolic-ref", "--short", "-q", "HEAD"]:
                return (0, $"daoris/{Session}\n", "");
            case ["status", ..]:
                return world.SFails ? (128, "", "fatal") : (0, string.Concat(Enumerable.Range(0, world.Untracked).Select(at => $"?? u{at}.txt\0")), "");
            case ["diff", ..] when arguments.Contains("--no-index"):
                return (1, $"diff --git a/{arguments[^1]} b/{arguments[^1]}\nnew file mode 100644\n--- /dev/null\n+++ b/{arguments[^1]}\n@@ -0,0 +1 @@\n+one\n", "");
            case ["diff", .., "HEAD"]:
                return world.WFails ? (128, "", "fatal") : (0, "", "");
            case ["diff", ..]:
                return world.DFails ? (128, "", "fatal: bad revision") : (0, "", "");
            default:
                return (1, "", $"the census world does not answer `git {Joined()}`");
        }
    }

    /// <summary>The stand-in: each git answered from the world, held until released where <paramref name="held"/> is given.</summary>
    private Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>> Stand(
        World world, ConcurrentQueue<(IReadOnlyList<string> Arguments, TaskCompletionSource Release)>? held) =>
        async (arguments, _, ct) =>
        {
            if (held is not null)
            {
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                held.Enqueue((arguments, release));
                await release.Task.WaitAsync(ct);
            }

            var (code, stdout, stderr) = Answer(world, arguments);
            return new ChildRun(true, code, stdout, stderr, false);
        };

    private SessionTrees Trees(World world)
    {
        if (world.Line == "set")
        {
            DriverConfig.Empty.WithLine("engine", "main").Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        }
        else
        {
            File.Delete(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        }

        var trees = new SessionTrees(_home);
        File.Delete(trees.Grown.FilePath);
        if (world.Grown != "none")
        {
            trees.Grown.Record(new GrownBranch("engine", Session, $"daoris/{Session}", "main", Grown, "topic", DateTimeOffset.UnixEpoch));
        }

        return trees;
    }

    private static async Task<string> OutcomeAsync(Func<Task<SessionReview?>> read)
    {
        try
        {
            return await read() is { } review ? JsonSerializer.Serialize(review) : "refused";
        }
        catch (DriverException error)
        {
            return error.Message;
        }
    }

    /// <summary>One combination through the census: the chain on the world at once, then the graph held, round by round.</summary>
    private async Task<ReviewGraph> CensusAsync(World world, string? baseCommit = Recorded)
    {
        var trees = Trees(world);
        Task<string?> NoCheckout(string _, CancellationToken __) => Task.FromResult<string?>(null);

        string chain;
        string[] chainCommands;
        WorkingTree.Runs.Value = Stand(world, null);
        try
        {
            using var count = GitLaunch.Count();
            chain = await OutcomeAsync(() => SessionReviews.ChainReadAsync(Session, _tree, baseCommit, trees, NoCheckout, CancellationToken.None));
            chainCommands = [.. count.Starts.Select(start => string.Join(' ', start.Arguments))];
        }
        finally
        {
            WorkingTree.Runs.Value = null;
        }

        var held = new ConcurrentQueue<(IReadOnlyList<string> Arguments, TaskCompletionSource Release)>();
        var graph = new ReviewGraph();
        var rounds = new Dictionary<ReviewNode, int>();
        Task<string> read;
        GitCount graphCount;
        WorkingTree.Runs.Value = Stand(world, held);
        try
        {
            using (graphCount = GitLaunch.Count())
            {
                using (GitLaunch.Within(GitBudget.Review))
                {
                    read = OutcomeAsync(() => ReviewGraphReads.ReadAsync(graph, Session, _tree, baseCommit, trees, NoCheckout, CancellationToken.None));
                }

                for (var round = 1; ; round++)
                {
                    await QuietAsync(graph, held, read, world);
                    foreach (var node in graph.Nodes.Where(node => node.Order >= 0 && !rounds.ContainsKey(node))) rounds[node] = round;
                    var running = held.ToArray();
                    // Π: one chain, one read at a time.
                    Assert.True(
                        graph.Nodes.Count(node => node.Program && node.Order >= 0 && !node.Answered.IsCompleted) <= 1,
                        $"two program-capable reads ran together on {world}");
                    if (running.Length == 0) break;
                    held.Clear();
                    foreach (var (_, release) in running) release.TrySetResult();
                    Assert.True(round < 64, $"the graph did not end on {world}");
                }

                Assert.Equal(chain, await read);
            }
        }
        finally
        {
            WorkingTree.Runs.Value = null;
        }

        // Each of the review's nodes started in the round its depth names: none before its needs answered, none after.
        foreach (var (node, round) in rounds.Where(pair => pair.Key.Budget is GitBudget.Review or GitBudget.Untracked))
        {
            Assert.True(round == node.Depth, $"{node} started in round {round}, its depth {node.Depth}, on {world}");
        }

        // The commands: the chain's, but the shared --show-toplevel; the graph's extras are its wasted speculative starts.
        var left = graphCount.Starts.Select(start => string.Join(' ', start.Arguments)).ToList();
        var shared = 0;
        foreach (var command in chainCommands)
        {
            if (left.Remove(command)) continue;
            Assert.Equal("rev-parse --show-toplevel", command);
            shared++;
        }

        Assert.True(shared <= 1, $"the graph shared {shared} --show-toplevel on {world}");
        Assert.Equal(graph.Wasted, left.Count);
        return graph;
    }

    /// <summary>
    /// Quiet: every node started and not answered holds one git in the stand-in, every node not started waits on a need not answered,
    /// and it stays so a moment, for a flow that makes its next node once an answer arrives. Bounded, so a graph that hangs fails.
    /// </summary>
    private static async Task QuietAsync(
        ReviewGraph graph, ConcurrentQueue<(IReadOnlyList<string> Arguments, TaskCompletionSource Release)> held, Task read, World world)
    {
        bool Quiet() =>
            graph.Nodes.Count(node => node.Order >= 0 && !node.Answered.IsCompleted) == held.Count
            && graph.Nodes.All(node => node.Order >= 0 || node.Needs.Any(need => !need.Answered.IsCompleted));

        // A node the flow makes once an answer arrives is made by a continuation on the thread pool, so "a moment" is the pool's own lag
        // as it is now, measured by a round trip and kept at its worst: a fixed 40 ms closed a round early under a loaded run, and its
        // node read a round past its depth (FLAKE1, the CIs of 2026-10-24 and the stage on e8f1aed).
        // The moment and the bound are both kept in time, not in polls: counted in polls, one spike of the pool's lag asked for more
        // polls than the bound allowed, and a graph that had gone quiet read as one that never did (FLAKE1, most loaded runs of
        // 2026-10-25 and 26).
        var lag = TimeSpan.Zero;
        var bound = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch? quiet = null;
        while (!(read.IsCompleted && held.IsEmpty))
        {
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(120), $"the graph never went quiet on {world}");
            var probe = System.Diagnostics.Stopwatch.StartNew();
            await Task.Run(() => { });
            if (probe.Elapsed > lag) lag = probe.Elapsed;
            await Task.Delay(10);
            if (!Quiet()) quiet = null;
            else if ((quiet ??= System.Diagnostics.Stopwatch.StartNew()).Elapsed >= TimeSpan.FromMilliseconds(Math.Max(40, 2 * lag.TotalMilliseconds))) break;
        }
    }

    public static TheoryData<string, string, string, bool> Combinations()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach (var line in new[] { "set", "origin", "dangling", "main", "master", "none" })
        foreach (var record in new[] { "held", "grown-held", "grown-not", "displaced", "no-id" })
        foreach (var bases in new[] { "same", "both", "here", "origin", "none" })
        foreach (var incomparable in new[] { false, true })
        {
            if (incomparable && bases != "both") continue;
            data.Add(line, record, bases, incomparable);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task Every_combination_of_the_range_starts_each_node_at_its_depth_and_answers_as_today(
        string line, string record, string bases, bool incomparable)
    {
        var world = new World(
            Line: line == "dangling" ? "dangling" : line, RecordHeld: record == "held",
            Grown: record switch { "grown-held" => "held", "grown-not" => "not", _ => "none" }, Bases: bases, Incomparable: incomparable,
            Untracked: 2);

        await CensusAsync(world, record == "no-id" ? "HEAD~1" : Recorded);
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public async Task The_routes_branches_answer_as_today(bool notTop, bool wFails, bool sFails, bool dFails, bool pFails)
    {
        var world = new World(Top: !notTop, WFails: wFails, SFails: sFails, DFails: dFails, PFails: pFails, Untracked: 1, Incomparable: true, Bases: "both");

        await CensusAsync(world);
        await CensusAsync(world, baseCommit: null);
        await CensusAsync(world with { MergeConflicts = true });
    }

    /// <summary>The design's P1.4 table, generated: where the graph's numbers differ from the table, the generated ones stand.</summary>
    [Fact]
    public async Task The_named_paths_have_the_tables_depth_and_starts()
    {
        var ordinary = await CensusAsync(new World());
        Assert.Equal((4, 12, 1), (ordinary.Depth, Started(ordinary), ordinary.Wasted));

        var guessed = await CensusAsync(new World(Line: "origin"));
        Assert.Equal((4, 15, 3), (guessed.Depth, Started(guessed), guessed.Wasted));

        var comparable = await CensusAsync(new World(Bases: "both"));
        Assert.Equal((4, 13), (comparable.Depth, Started(comparable)));

        var incomparable = await CensusAsync(new World(Bases: "both", Incomparable: true));
        Assert.Equal((5, 14), (incomparable.Depth, Started(incomparable)));

        var ceiling = await CensusAsync(new World(Line: "master", RecordHeld: false, Grown: "held", Bases: "both", Incomparable: true));
        Assert.Equal((6, 18), (ceiling.Depth, Started(ceiling)));

        var dirtyOnly = await CensusAsync(new World(Untracked: 1), baseCommit: null);
        Assert.Equal((4, 5), (dirtyOnly.Depth, Started(dirtyOnly)));
    }

    private static int Started(ReviewGraph graph) => graph.Nodes.Count(node => node.Order >= 0);

    // The untracked window's held cases (P1.3), at w = 1 (part one's) and w = 4 (the protocol part two would widen).
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task The_window_holds_its_slots_until_the_fold_passes_and_wastes_at_most_w_less_one(int window)
    {
        var paths = Enumerable.Range(0, 8).Select(at => $"u{at}.txt").ToArray();
        var started = new ConcurrentDictionary<string, TaskCompletionSource<(string?, int?)>>();
        Task<(string? Patch, int? Added)> Read(string path, CancellationToken ct)
        {
            var answer = new TaskCompletionSource<(string?, int?)>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => answer.TrySetCanceled(ct));
            started[path] = answer;
            return answer.Task;
        }

        // The first held while later ones complete: no slot is given back until the fold passes the first.
        var fold = new UntrackedFold(new WorkingTree.TreeDiff([], null, "HEAD"));
        var folding = UntrackedWindow.FoldAsync(paths, fold, window, Read, CancellationToken.None);
        Assert.Equal(window, started.Count);
        foreach (var later in started.Keys.Where(path => path != "u0.txt")) started[later].TrySetResult(("+later\n", 1));
        await Task.Delay(20);
        Assert.Equal(window, started.Count);

        // The fold reaching the budget mid-window: what was started ahead is stopped, wasted, and listed with no patch.
        started["u0.txt"].TrySetResult((new string('+', WorkingTree.PatchBudget), 1));
        var wasted = await folding;
        Assert.Equal(window - 1, wasted);
        Assert.True(wasted <= window - 1);
        var answer = fold.End(WorkingTree.UncommittedRest);
        Assert.Equal(paths, answer.Files.Select(file => file.Path));
        Assert.Equal(1, answer.Files.Count(file => file.Patch is not null));

        // W spending the whole budget: no untracked read starts at all.
        started.Clear();
        var spent = new UntrackedFold(new WorkingTree.TreeDiff([new WorkingTree.DiffFile("own.txt", "modified", 1, 0, new string('+', WorkingTree.PatchBudget))], null, "HEAD"));
        Assert.Equal(0, await UntrackedWindow.FoldAsync(paths, spent, window, Read, CancellationToken.None));
        Assert.Empty(started);
    }
}
