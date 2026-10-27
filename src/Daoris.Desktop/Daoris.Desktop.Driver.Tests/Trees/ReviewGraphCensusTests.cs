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
        World world, HeldReads? held, ReviewGraph? graph = null) =>
        async (arguments, _, ct) =>
        {
            if (held is not null)
            {
                Assert.NotNull(graph);
                held.RecordCall(graph, arguments, GitLaunch.Budget);
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

        // The settled replay supplies completeness, not the answer/depth oracle: those assertions remain below.
        var reference = new ReviewGraph();
        string[] referenceCommands;
        WorkingTree.Runs.Value = Stand(world, null);
        try
        {
            using var count = GitLaunch.Count();
            using (GitLaunch.Within(GitBudget.Review))
            {
                Assert.Equal(chain, await OutcomeAsync(() => ReviewGraphReads.ReadAsync(
                    reference, Session, _tree, baseCommit, Trees(world), NoCheckout, CancellationToken.None)));
            }

            await reference.SettledAsync().WaitAsync(Poll.Patience);
            referenceCommands = [.. count.Starts.Select(start => string.Join(' ', start.Arguments))];
        }
        finally
        {
            WorkingTree.Runs.Value = null;
        }

        trees = Trees(world);
        var graph = new ReviewGraph();
        var held = new HeldReads();
        var rounds = new Dictionary<ReviewNode, int>();
        using var owned = new CancellationTokenSource();
        Task<string>? read = null;
        Exception? failed = null;
        GitCount graphCount;
        WorkingTree.Runs.Value = Stand(world, held, graph);
        try
        {
            using (graphCount = GitLaunch.Count())
            {
                using (GitLaunch.Within(GitBudget.Review))
                {
                    read = OutcomeAsync(() => ReviewGraphReads.ReadAsync(graph, Session, _tree, baseCommit, trees, NoCheckout, owned.Token));
                }

                for (var round = 1; ; round++)
                {
                    var running = await FrontierAsync(graph, held, read, world, reference.Nodes, round, rounds: rounds);
                    // Π: one chain, one read at a time.
                    Assert.True(
                        graph.Nodes.Count(node => node.Program && node.Order >= 0 && !node.Answered.IsCompleted) <= 1,
                        $"two program-capable reads ran together on {world}");
                    if (running.Length == 0) break;
                    held.Acknowledge(running);
                    foreach (var (_, release) in running) release.TrySetResult();
                    Assert.True(round < 64, $"the graph did not end on {world}");
                }

                Assert.Equal(chain, await read);
                Assert.NotNull(MatchTopology(graph.Nodes, reference.Nodes, complete: true));
                held.AssertEveryNodeCalledOnce(graph);
            }
        }
        catch (Exception error)
        {
            failed = error;
            throw;
        }
        finally
        {
            try
            {
                await SettleOwnedAsync(held, graph, read, failed is null ? null : owned);
            }
            catch (Exception teardownError) when (failed is not null)
            {
                throw new AggregateException("the census failed and its owned reads failed during teardown", failed, teardownError);
            }
            finally
            {
                WorkingTree.Runs.Value = null;
            }
        }

        // Each of the review's nodes started in the round its depth names: none before its needs answered, none after.
        foreach (var (node, round) in rounds.Where(pair => pair.Key.Budget is GitBudget.Review or GitBudget.Untracked))
        {
            Assert.True(round == node.Depth, $"{node} started in round {round}, its depth {node.Depth}, on {world}");
        }

        // The commands: the chain's, but the shared --show-toplevel; the graph's extras are its wasted speculative starts.
        Assert.Equal(referenceCommands.Order(StringComparer.Ordinal),
            graphCount.Starts.Select(start => string.Join(' ', start.Arguments)).Order(StringComparer.Ordinal));
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

    private sealed class HeldReads
    {
        private readonly Lock _gate = new();
        private readonly Queue<(IReadOnlyList<string> Arguments, TaskCompletionSource Release)> _held = [];
        private readonly Dictionary<ReviewNode, GitBudget> _calls = [];
        private bool _settling;
        private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Changed { get { lock (_gate) return _changed.Task; } }

        public void Enqueue((IReadOnlyList<string> Arguments, TaskCompletionSource Release) read)
        {
            lock (_gate)
            {
                if (_settling) read.Release.TrySetResult();
                else _held.Enqueue(read);
                var changed = _changed;
                _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                changed.TrySetResult();
            }
        }

        public (IReadOnlyList<string> Arguments, TaskCompletionSource Release)[] Snapshot()
        {
            lock (_gate) return [.. _held];
        }

        public void Acknowledge((IReadOnlyList<string> Arguments, TaskCompletionSource Release)[] reads)
        {
            lock (_gate)
            {
                foreach (var read in reads) Assert.Same(read.Release, _held.Dequeue().Release);
            }
        }

        public void RecordCall(ReviewGraph graph, IReadOnlyList<string> arguments, GitBudget budget)
        {
            static string CommandName(ReviewNode node) => node.Name switch { "K′" => "K", "N↑" => "N1", "N↓" => "N2", _ => node.Name };
            lock (_gate)
            {
                // After an assertion fails, teardown runs the scripted reads freely; it no longer measures a held frontier.
                if (_settling) return;
                var name = GitLaunch.NodeOf(arguments, budget);
                // In these Worlds, repeated N reads occur in different frontiers and U runs at w=1. Require a unique,
                // live, not-yet-called owner; do not choose among duplicate names by creation/start order.
                var owner = Assert.Single(graph.Nodes, node => node.Order >= 0 && !node.Answered.IsCompleted
                    && node.Budget == budget && CommandName(node) == name && !_calls.ContainsKey(node));
                _calls.Add(owner, budget);
            }
        }

        public void AssertEveryNodeCalledOnce(ReviewGraph graph)
        {
            lock (_gate)
            {
                Assert.Equal(graph.Nodes.Count, _calls.Count);
                foreach (var node in graph.Nodes) Assert.Equal(node.Budget, _calls[node]);
            }
        }

        public void BeginSettlement()
        {
            lock (_gate) _settling = true;
        }

        public void ReleaseAllAndFuture()
        {
            lock (_gate)
            {
                _settling = true;
                while (_held.TryDequeue(out var read)) read.Release.TrySetResult();
            }
        }
    }

    private static async Task SettleOwnedAsync(
        HeldReads held, ReviewGraph graph, Task? read, CancellationTokenSource? cancel = null)
    {
        // Keep releasing late registrations too: settling a parent can create another child after the first drain.
        held.BeginSettlement();
        try
        {
            if (cancel is not null) await cancel.CancelAsync();
        }
        finally
        {
            held.ReleaseAllAndFuture();
            try
            {
                if (read is not null) await read.WaitAsync(Poll.Patience);
            }
            catch (OperationCanceledException error) when (cancel is { IsCancellationRequested: true }
                && error.CancellationToken == cancel.Token)
            {
                // This cancellation belongs to failed-fixture cleanup; the primary assertion is still propagated by its caller.
            }
            finally
            {
                await graph.SettledAsync().WaitAsync(Poll.Patience);
            }
        }
    }

    // An injective DAG match, extended by backtracking through indistinguishable peers. Needs refer to mapped node identities,
    // not recursive strings: two U nodes with the same label/needs remain two nodes, and shared versus separate parents differ.
    private static Dictionary<ReviewNode, ReviewNode>? MatchTopology(
        IReadOnlyList<ReviewNode> actual, IReadOnlyList<ReviewNode> reference, bool complete = false)
    {
        if (actual.Count > reference.Count || (complete && actual.Count != reference.Count)) return null;
        var heights = new Dictionary<ReviewNode, int>();
        int Height(ReviewNode node) => heights.TryGetValue(node, out var height) ? height
            : heights[node] = 1 + node.Needs.Select(Height).DefaultIfEmpty(0).Max();
        // Only traversal is topological; neither creation order nor Start.Order is used as identity.
        var nodes = actual.OrderBy(Height).ToArray();
        var map = new Dictionary<ReviewNode, ReviewNode>();
        var taken = new HashSet<ReviewNode>();
        bool Match(int at)
        {
            if (at == nodes.Length) return true;
            var node = nodes[at];
            foreach (var candidate in reference)
            {
                if (taken.Contains(candidate) || node.Name != candidate.Name || node.Budget != candidate.Budget
                    || node.Program != candidate.Program || node.Speculative != candidate.Speculative
                    || (complete && node.Used != candidate.Used)
                    || node.Needs.Count != candidate.Needs.Count
                    || !node.Needs.GroupBy(need => map[need]).All(group =>
                        candidate.Needs.Count(need => ReferenceEquals(need, group.Key)) == group.Count())) continue;
                map.Add(node, candidate);
                taken.Add(candidate);
                if (Match(at + 1)) return true;
                taken.Remove(candidate);
                map.Remove(node);
            }

            return false;
        }

        return Match(0) ? map : null;
    }

    // Completeness comes from the settled scripted topology, never an elapsed quiet interval. Each started node in this census
    // makes one held git call. Wait for those arrivals as well as starts; Order is published before the stand-in is entered.
    private static async Task<(IReadOnlyList<string> Arguments, TaskCompletionSource Release)[]> FrontierAsync(
        ReviewGraph graph, HeldReads held, Task read, World world, IReadOnlyList<ReviewNode> reference, int round,
        Action? waitingForStarts = null, Dictionary<ReviewNode, int>? rounds = null, TimeProvider? clock = null)
    {
        using var stop = new CancellationTokenSource();
        var expired = Task.Delay(Poll.Patience, clock ?? TimeProvider.System, stop.Token);
        try
        {
            while (true)
            {
                if (expired.IsCompleted)
                    throw new TimeoutException($"frontier round {round} did not arrive on {world}; nodes: "
                        + string.Join(", ", graph.Nodes.Select(node => $"{node.Name}: start={node.Order}, depth={node.Depth}, answered={node.Answered.IsCompleted}")));
                // Subscribe before observing state, so an arrival between observation and await cannot be lost.
                var changed = held.Changed;
                var finished = read.IsCompleted;
                var nodes = graph.Nodes;
                // A released stand-in can still be returning through StartAsync's finally. Its Answered signal matters too:
                // when it is the last lagging completion, no further arrival need occur to wake this frontier.
                var answers = nodes.Select(node => node.Answered).Where(answer => !answer.IsCompleted).ToArray();
                var map = MatchTopology(nodes, reference);
                Assert.True(map is not null, $"unexpected graph topology on {world}");
                var observed = nodes.Where(node => node.Order >= 0).ToArray();
                foreach (var node in observed) rounds?.TryAdd(node, round);
                var running = held.Snapshot();
                var started = observed.Select(node => map[node]).ToHashSet();
                var complete = reference.Where(node => node.Depth <= round).All(started.Contains);
                if (finished)
                {
                    Assert.True(MatchTopology(nodes, reference, complete: true) is not null && started.Count == reference.Count,
                        $"the read ended with missing reference nodes on {world}");
                    Assert.Empty(running);
                    return running;
                }

                if (complete && running.Length > 0
                    && nodes.Count(node => node.Order >= 0 && !node.Answered.IsCompleted) == running.Length) return running;
                waitingForStarts?.Invoke();
                // One bound for the whole frontier, not renewed by each arrival or completion.
                await Task.WhenAny([changed, read, .. answers, expired]);
            }
        }
        finally
        {
            stop.Cancel();
        }
    }

    [Fact]
    public async Task A_frontier_waits_for_a_producer_paused_after_its_parent_answer_before_registering_its_child()
    {
        static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both runs use the same producer. Only the test's registration and answer gates differ.
        static (ReviewGraph Graph, Task Read) Script(
            Task parentRelease, Task registration, Task childRelease,
            TaskCompletionSource producerPaused, TaskCompletionSource childStarted,
            HeldReads held,
            TaskCompletionSource release)
        {
            var graph = new ReviewGraph();
            var (parent, answer) = graph.Run("E", [], async () => { await parentRelease; return true; });
            async Task ProduceAsync()
            {
                await answer;
                producerPaused.TrySetResult();
                await registration;
                var (_, child) = graph.Run("K′", [parent], async () =>
                {
                    held.Enqueue((new[] { "merge-base", "--is-ancestor", Grown, "HEAD" }, release));
                    childStarted.TrySetResult();
                    await childRelease;
                    return true;
                });
                await child;
            }

            return (graph, graph.Track(ProduceAsync()));
        }

        var reference = Script(Task.CompletedTask, Task.CompletedTask, Task.CompletedTask,
            Signal(), Signal(), new(), Signal());
        await reference.Read.WaitAsync(Poll.Patience);
        await reference.Graph.SettledAsync().WaitAsync(Poll.Patience);

        var parentRelease = Signal();
        var registration = Signal();
        var childRelease = Signal();
        var producerPaused = Signal();
        var childStarted = Signal();
        var waiting = Signal();
        var held = new HeldReads();
        var actual = Script(parentRelease.Task, registration.Task, childRelease.Task,
            producerPaused, childStarted, held, childRelease);
        var rounds = new Dictionary<ReviewNode, int> { [actual.Graph.Nodes[0]] = 1 };
        Task? frontier = null;
        try
        {
            parentRelease.TrySetResult();
            await producerPaused.Task.WaitAsync(Poll.Patience);
            Assert.Single(actual.Graph.Nodes);
            Assert.True(actual.Graph.Nodes[0].Answered.IsCompleted);

            frontier = FrontierAsync(actual.Graph, held, actual.Read, new World(), reference.Graph.Nodes,
                round: 2, waitingForStarts: () => waiting.TrySetResult(), rounds: rounds);
            var first = await Task.WhenAny(waiting.Task, frontier).WaitAsync(Poll.Patience);
            Assert.Same(waiting.Task, first);
            Assert.False(frontier.IsCompleted);

            registration.TrySetResult();
            await childStarted.Task.WaitAsync(Poll.Patience);
            await frontier.WaitAsync(Poll.Patience);
            Assert.Equal(new[] { "E", "K′" }, actual.Graph.Nodes.Select(node => node.Name));
            Assert.Equal(2, rounds[actual.Graph.Nodes[1]]);
            Assert.Equal(2, actual.Graph.Nodes[1].Depth);
            Assert.Single(held.Snapshot());
            Assert.False(actual.Read.IsCompleted);
        }
        finally
        {
            parentRelease.TrySetResult();
            registration.TrySetResult();
            childRelease.TrySetResult();
            await actual.Read.WaitAsync(Poll.Patience);
            await actual.Graph.SettledAsync().WaitAsync(Poll.Patience);
            if (frontier is not null) await frontier.WaitAsync(Poll.Patience);
        }
    }

    [Fact]
    public void Topology_matching_keeps_duplicate_identities_and_sharing_without_creation_order()
    {
        static ReviewNode Node(string name, params ReviewNode[] needs) => new(name, needs, false, false, GitBudget.Review);
        var a = Node("U");
        var b = Node("U");
        var x = Node("X", a);
        var y = Node("Y", b);
        ReviewNode[] reference = [a, b, x, y];
        var first = Node("U");
        var second = Node("U");
        var otherX = Node("X", second);
        var otherY = Node("Y", first);
        var map = MatchTopology([otherY, first, otherX, second], reference, complete: true);
        Assert.NotNull(map);
        Assert.Same(a, map[second]);
        Assert.Same(b, map[first]);
        Assert.Null(MatchTopology([first, otherX, second, Node("Y", second)], reference, complete: true));
        Assert.Null(MatchTopology([a, x, y], reference, complete: true));
        Assert.Null(MatchTopology([a, b, x, y, Node("U")], reference));
        Assert.Null(MatchTopology([first, second, Node("X", first, first)], [a, b, Node("X", a, b)], complete: true));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("budget")]
    [InlineData("program")]
    [InlineData("speculation")]
    [InlineData("needs")]
    public void Topology_matching_rejects_changed_node_contracts(string change)
    {
        var parent = new ReviewNode("E", [], false, false, GitBudget.Review);
        var reference = new ReviewNode("K′", [parent], false, true, GitBudget.Review);
        var changed = new ReviewNode(change == "name" ? "K" : "K′", change == "needs" ? [] : [parent],
            change == "program", change != "speculation", change == "budget" ? GitBudget.Untracked : GitBudget.Review);
        Assert.Null(MatchTopology([parent, changed], [parent, reference], complete: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_hidden_serialized_peer_starts_fail_at_the_signal_bound(bool serialized)
    {
        var reference = new ReviewGraph();
        reference.Run("U", [], () => Task.FromResult(true));
        reference.Run("U", [], () => Task.FromResult(true));
        await reference.SettledAsync().WaitAsync(Poll.Patience);

        var held = new HeldReads();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = new ReviewGraph();
        Task<bool> Hold()
        {
            held.Enqueue((new[] { "diff" }, release));
            return HeldAsync();
        }

        async Task<bool> HeldAsync() { await release.Task; return true; }
        var (_, one) = actual.Run("U", [], Hold);
        async Task ProduceAsync()
        {
            await one;
            if (serialized) await actual.Run("U", [], Hold).Answer;
        }

        var producer = actual.Track(ProduceAsync());
        // Keep the overall read open in the missing case too; only the fixture's bound may end this wait.
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new MovedClock();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frontier = FrontierAsync(actual, held, read.Task, new World(), reference.Nodes, 1,
            () => waiting.TrySetResult(), clock: clock);
        try
        {
            await waiting.Task.WaitAsync(Poll.Patience);
            await clock.Timed.WaitAsync(Poll.Patience);
            Assert.False(frontier.IsCompleted);
            Assert.Single(actual.Nodes);
            Assert.Single(held.Snapshot());
            clock.Advance(Poll.Patience);
            await Assert.ThrowsAsync<TimeoutException>(() => frontier);
        }
        finally
        {
            release.TrySetResult();
            read.TrySetResult();
            await producer.WaitAsync(Poll.Patience);
            await actual.SettledAsync().WaitAsync(Poll.Patience);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_finished_read_rejects_missing_or_unexpected_topology(bool unexpected)
    {
        var reference = new ReviewGraph();
        reference.Run("E", [], () => Task.FromResult(true));
        var actual = new ReviewGraph();
        if (unexpected) actual.Run("K′", [], () => Task.FromResult(true));
        await reference.SettledAsync().WaitAsync(Poll.Patience);
        await actual.SettledAsync().WaitAsync(Poll.Patience);
        var error = await Record.ExceptionAsync(() => FrontierAsync(actual, new HeldReads(), Task.CompletedTask,
            new World(), reference.Nodes, 1));
        Assert.NotNull(error);
        Assert.Contains(unexpected ? "unexpected graph topology" : "missing reference nodes", error.Message);
    }

    [Fact]
    public void Acknowledging_a_snapshot_leaves_a_late_arrival_held_and_signalled()
    {
        var held = new HeldReads();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        held.Enqueue((new[] { "first" }, first));
        var acknowledged = held.Snapshot();
        var changed = held.Changed;
        held.Enqueue((new[] { "late" }, late));
        held.Acknowledge(acknowledged);
        foreach (var (_, release) in acknowledged) release.TrySetResult();
        Assert.True(changed.IsCompletedSuccessfully);
        Assert.True(first.Task.IsCompletedSuccessfully);
        Assert.False(late.Task.IsCompleted);
        Assert.Same(late, Assert.Single(held.Snapshot()).Release);
        held.Acknowledge(held.Snapshot());
        late.TrySetResult();
        Assert.Empty(held.Snapshot());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Observed_starts_keep_the_actual_open_round_even_when_it_violates_the_named_depth(int round)
    {
        var reference = new ReviewGraph();
        var (referenceParent, _) = reference.Run("E", [], () => Task.FromResult(true));
        reference.Run("K′", [referenceParent], () => Task.FromResult(true));
        await reference.SettledAsync().WaitAsync(Poll.Patience);
        var actual = new ReviewGraph();
        var (parent, _) = actual.Run("E", [], () => Task.FromResult(true));
        var held = new HeldReads();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (child, answer) = actual.Run("K′", [parent], async () =>
        {
            held.Enqueue((new[] { "merge-base" }, release));
            await release.Task;
            return true;
        });
        var rounds = new Dictionary<ReviewNode, int>();
        try
        {
            await FrontierAsync(actual, held, answer, new World(), reference.Nodes, round, rounds: rounds);
            Assert.Equal(2, child.Depth);
            Assert.Equal(round, rounds[child]);
            Assert.NotEqual(child.Depth, rounds[child]);
        }
        finally
        {
            release.TrySetResult();
            await answer.WaitAsync(Poll.Patience);
            await actual.SettledAsync().WaitAsync(Poll.Patience);
        }
    }

    [Fact]
    public async Task An_acknowledged_read_finishing_without_another_arrival_wakes_the_frontier()
    {
        var reference = new ReviewGraph();
        reference.Run("E", [], () => Task.FromResult(true));
        var (referenceParent, _) = reference.Run("P", [], () => Task.FromResult(true));
        reference.Run("K", [referenceParent], () => Task.FromResult(true));
        await reference.SettledAsync().WaitAsync(Poll.Patience);
        var held = new HeldReads();
        var actual = new ReviewGraph();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var childStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, answer) = actual.Run("E", [], async () =>
        {
            held.Enqueue((new[] { "first" }, first));
            await first.Task;
            await finish.Task;
            return true;
        });
        var acknowledged = held.Snapshot();
        held.Acknowledge(acknowledged);
        first.TrySetResult();
        var (parent, _) = actual.Run("P", [], () => Task.FromResult(true));
        async Task ProduceAsync()
        {
            await registration.Task;
            var (_, child) = actual.Run("K", [parent], async () =>
            {
                held.Enqueue((new[] { "other" }, other));
                childStarted.TrySetResult();
                await other.Task;
                return true;
            });
            await child;
        }

        var remaining = actual.Track(ProduceAsync());
        var read = Task.WhenAll(answer, remaining);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitingAfterArrival = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frontier = FrontierAsync(actual, held, read, new World(), reference.Nodes, 2, () =>
        {
            waiting.TrySetResult();
            // Enqueue publishes its wake before the stand-in sets childStarted. Observe the held entry itself so an early
            // awakened waiter cannot miss this acknowledgement and leave the test waiting on a later signal that never comes.
            if (held.Snapshot().Any(entry => ReferenceEquals(entry.Release, other))) waitingAfterArrival.TrySetResult();
        });
        try
        {
            await waiting.Task.WaitAsync(Poll.Patience);
            Assert.False(frontier.IsCompleted);
            registration.TrySetResult();
            await childStarted.Task.WaitAsync(Poll.Patience);
            await waitingAfterArrival.Task.WaitAsync(Poll.Patience);
            Assert.False(frontier.IsCompleted);
            Assert.False(answer.IsCompleted);
            var changed = held.Changed;
            finish.TrySetResult();
            var running = await frontier.WaitAsync(Poll.Patience);
            Assert.False(changed.IsCompleted);
            Assert.Same(other, Assert.Single(running).Release);
            Assert.False(read.IsCompleted);
        }
        finally
        {
            first.TrySetResult();
            finish.TrySetResult();
            other.TrySetResult();
            registration.TrySetResult();
            await SettleOwnedAsync(held, actual, read);
        }
    }

    [Fact]
    public async Task A_failed_frontier_assertion_settles_held_reads_and_children_registered_during_teardown()
    {
        var held = new HeldReads();
        var graph = new ReviewGraph();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (parent, answer) = graph.Run("E", [], async () =>
        {
            held.Enqueue((new[] { "parent" }, release));
            await release.Task;
            return true;
        });
        async Task ProduceAsync()
        {
            await answer;
            var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var (_, child) = graph.Run("K′", [parent], async () =>
            {
                held.Enqueue((new[] { "late child" }, late));
                await late.Task;
                return true;
            });
            await child;
        }

        var read = graph.Track(ProduceAsync());
        async Task FailAsync()
        {
            try
            {
                await FrontierAsync(graph, held, read, new World(), [], 1);
            }
            finally
            {
                await SettleOwnedAsync(held, graph, read);
            }
        }

        var error = await Record.ExceptionAsync(FailAsync);
        Assert.NotNull(error);
        Assert.Contains("unexpected graph topology", error.Message);
        Assert.True(read.IsCompletedSuccessfully);
        Assert.Equal(new[] { "E", "K′" }, graph.Nodes.Select(node => node.Name));
        Assert.All(graph.Nodes, node => Assert.True(node.Answered.IsCompletedSuccessfully));
        Assert.Empty(held.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Node_call_accounting_rejects_a_missing_or_second_git_call(bool twice)
    {
        var graph = new ReviewGraph();
        var held = new HeldReads();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (node, answer) = graph.Run("E", [], async () => { await release.Task; return true; });
        try
        {
            Exception? error;
            if (twice)
            {
                held.RecordCall(graph, ["rev-parse", "--abbrev-ref", "HEAD"], GitBudget.Review);
                error = Record.Exception(() => held.RecordCall(graph, ["rev-parse", "--abbrev-ref", "HEAD"], GitBudget.Review));
            }
            else error = Record.Exception(() => held.AssertEveryNodeCalledOnce(graph));
            Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(error);
            Assert.False(node.Answered.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await SettleOwnedAsync(held, graph, answer);
        }
    }

    [Fact]
    public async Task Complete_topology_rejects_swapped_usage_at_equal_waste_but_partial_usage_can_be_undecided()
    {
        static ReviewGraph Pair()
        {
            var graph = new ReviewGraph();
            graph.Run("E", [], () => Task.FromResult(true), speculative: true);
            graph.Run("K", [], () => Task.FromResult(true), speculative: true);
            return graph;
        }

        var reference = Pair();
        var actual = Pair();
        var undecided = Pair();
        await Task.WhenAll(reference.SettledAsync(), actual.SettledAsync(), undecided.SettledAsync()).WaitAsync(Poll.Patience);
        reference.Nodes[0].Use(true);
        reference.Nodes[1].Use(false);
        actual.Nodes[0].Use(false);
        actual.Nodes[1].Use(true);
        Assert.Equal(1, reference.Wasted);
        Assert.Equal(reference.Wasted, actual.Wasted);
        Assert.Null(MatchTopology(actual.Nodes, reference.Nodes, complete: true));
        Assert.NotNull(MatchTopology(undecided.Nodes, reference.Nodes));
        Assert.Null(MatchTopology(undecided.Nodes, reference.Nodes, complete: true));
    }

    [Fact]
    public async Task A_failed_actual_frontier_cancels_and_settles_its_owned_scripted_review()
    {
        var world = new World(RecordHeld: false, Grown: "held", Bases: "both", Untracked: 2);
        var trees = Trees(world);
        var graph = new ReviewGraph();
        var held = new HeldReads();
        using var owned = new CancellationTokenSource();
        var arrived = held.Changed;
        Task<string>? read = null;
        var before = WorkingTree.Runs.Value;
        WorkingTree.Runs.Value = Stand(world, held, graph);
        async Task FailAsync()
        {
            try
            {
                using (GitLaunch.Within(GitBudget.Review))
                {
                    read = OutcomeAsync(() => ReviewGraphReads.ReadAsync(graph, Session, _tree, Recorded, trees,
                        (_, _) => Task.FromResult<string?>(null), owned.Token));
                }
                await arrived.WaitAsync(Poll.Patience);
                Assert.NotEmpty(held.Snapshot());
                Assert.False(read.IsCompleted);
                Assert.False(owned.IsCancellationRequested);
                await FrontierAsync(graph, held, read, world, [], 1);
            }
            finally
            {
                await SettleOwnedAsync(held, graph, read, owned);
            }
        }

        try
        {
            var error = await Record.ExceptionAsync(FailAsync);
            Assert.NotNull(error);
            Assert.Contains("unexpected graph topology", error.Message);
            Assert.True(owned.IsCancellationRequested);
            Assert.NotNull(read);
            Assert.True(read.IsCanceled);
            Assert.NotEmpty(graph.Nodes);
            Assert.All(graph.Nodes, node => Assert.True(node.Answered.IsCompletedSuccessfully));
            Assert.Empty(held.Snapshot());
        }
        finally
        {
            try { await SettleOwnedAsync(held, graph, read, owned); }
            finally { WorkingTree.Runs.Value = before; }
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
