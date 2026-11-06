using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D245's steps past a pull request: the <c>monitor</c> reads the request's answer on the landing record, merged or closed, a squash read as
/// merged; the <c>tidy</c> reads what its door ended there; the save places each after what it rests on; the tidy's record reads back whole.
/// </summary>
public sealed class CompletionNodesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "completion-nodes", Guid.NewGuid().ToString("N")[..8]);

    public CompletionNodesTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static LandedBranch Landing(PullRequestState? state = null, string? address = "https://example.invalid/acme/engine/pull/1") =>
        new("engine", "default", "feature/q1-fix", "main", "c0ffee00", "s-q1", "q1", "Fix", DateTimeOffset.UtcNow)
        {
            PullRequest = address, Plugin = "acme-host", PullRequestState = state, Squashed = "feed0002", Units = ["u1", "u2"],
        };

    [Fact]
    public void The_monitor_waits_while_the_request_is_unopened_open_or_unanswered()
    {
        Assert.False(CompletionNodes.Monitor(null).Reached);
        Assert.False(CompletionNodes.Monitor(Landing(address: null)).Reached);
        Assert.False(CompletionNodes.Monitor(Landing()).Reached);
        Assert.False(CompletionNodes.Monitor(Landing(new PullRequestState(PullRequestStates.Open))).Reached);
    }

    [Fact]
    public void The_monitor_reads_a_squash_merge_as_merged_and_a_closed_request_as_its_end()
    {
        var squashed = CompletionNodes.Monitor(Landing(new PullRequestState(PullRequestStates.Completed) { How = MergeHow.Squash }));
        Assert.True(squashed.Reached);
        Assert.Contains("squashed", squashed.Said, StringComparison.Ordinal);
        Assert.Equal("c0ffee00", squashed.Landing!.Tip);

        var closed = CompletionNodes.Monitor(Landing(new PullRequestState(PullRequestStates.Abandoned)));
        Assert.True(closed.Reached);
        Assert.Contains("without merging", closed.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tidy_is_reached_only_where_its_record_holds_the_landings_tip()
    {
        Assert.False(CompletionNodes.Tidy(Landing()).Reached);
        Assert.False(CompletionNodes.Tidy(Landing() with { Tidied = new ChainTidied("0ld0ld00", DateTimeOffset.UtcNow) }).Reached);
        var tidied = CompletionNodes.Tidy(Landing() with
        {
            Tidied = new ChainTidied("c0ffee00", DateTimeOffset.UtcNow) { Parts = [new("work/one", "feed0001") { KeptOn = "refs/daoris/discarded/work/one" }] },
        });
        Assert.True(tidied.Reached);
        Assert.StartsWith("1 part(s)", tidied.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void The_monitor_and_the_tidy_are_built_in_kinds_neither_skipped_nor_failed_by_the_person()
    {
        Assert.Contains(NodeKinds.Monitor, NodeKinds.BuiltIn);
        Assert.Contains(NodeKinds.Tidy, NodeKinds.BuiltIn);
        Assert.False(NodeAuthorities.BuiltIn[NodeKinds.Monitor].PersonSkips);
        Assert.False(NodeAuthorities.BuiltIn[NodeKinds.Tidy].PersonSkips);
        Assert.Null(NodeAuthorities.BuiltIn[NodeKinds.Monitor].FailureBy);
        Assert.Equal(NodeSource.Landing, NodeAuthorities.BuiltIn[NodeKinds.Tidy].FailureBy);
        Assert.Equal([NodeKinds.Land, NodeKinds.Push, PullRequestSteps.Id, NodeKinds.Monitor, NodeKinds.Tidy], CompletionNodes.Set);
        Assert.All(new[] { NodeKinds.Land, NodeKinds.Push, NodeKinds.Monitor, NodeKinds.Tidy }, kind => Assert.True(CompletionNodes.RunBegins(kind)));
    }

    private static string Graph(string tail, string wires) => $$$"""
        {"schemaVersion":2,"entries":["work"],"end":"tidy","nodes":[
          {"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
          {"id":"push","kind":"push","version":1,"reach":"performed"},
          {"id":"pull-request","kind":"acme-host/pull-request","version":1,"reach":"performed"},
          {{{tail}}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"push"},
                  {"id":"c","kind":"after","from":"push","to":"pull-request"},{{{wires}}}]}
        """;

    private const string Tail = """
        {"id":"monitor","kind":"monitor","version":1,"reach":"observed"},
        {"id":"tidy","kind":"tidy","version":1,"reach":"performed"}
        """;

    [Fact]
    public void The_save_takes_the_whole_set_in_its_order()
    {
        var (graph, problems) = NodeGraphs.Read(Graph(Tail, """
            {"id":"m","kind":"after","from":"pull-request","to":"monitor"},{"id":"t","kind":"after","from":"monitor","to":"tidy"}
            """), onSave: true);

        Assert.Empty(problems);
        Assert.Equal([NodeKinds.Claimed, NodeKinds.Land, NodeKinds.Push, "acme-host/pull-request", NodeKinds.Monitor, NodeKinds.Tidy],
            graph!.Nodes.Select(node => node.Kind));
    }

    [Fact]
    public void The_save_refuses_a_monitor_no_pull_request_reaches_and_a_tidy_no_monitor_reaches()
    {
        // A graph with such a monitor or tidy is no graph: its read names each, whether or not it is being saved.
        var early = NodeGraphs.Read(Graph(Tail, """
                {"id":"m","kind":"after","from":"push","to":"monitor"},{"id":"t","kind":"after","from":"push","to":"tidy"}
                """), onSave: false);

        Assert.Null(early.Graph);
        Assert.Equal(["monitor", "tidy"], early.Problems.Where(problem => problem.Code == "land-tail").Select(problem => problem.Node));
        Assert.NotEmpty(NodeGraphs.Read(Graph(Tail, """
            {"id":"m","kind":"after","from":"push","to":"monitor"},{"id":"t","kind":"after","from":"monitor","to":"tidy"}
            """), onSave: true).Problems);
    }

    [Fact]
    public void The_tidys_record_reads_back_whole_and_names_a_part_only_at_the_tip_it_ended()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landing());
        var tidied = new ChainTidied("c0ffee00", DateTimeOffset.Parse("2026-11-06T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture))
        {
            Parts =
            [
                new("work/one", "feed0001") { KeptOn = "refs/daoris/discarded/work/one", Act = "act-1" },
                new("work/two", "feed0002") { KeptOn = "refs/daoris/discarded/work/two", Kept = "it is checked out at a tree" },
            ],
        };

        landings.Tidied("engine", "feature/q1-fix", tidied);

        var read = new LandedBranches(_home).Of("engine", "feature/q1-fix")!;
        Assert.Equal(tidied, read.Tidied);
        Assert.Equal("act-1", ChainTidies.Of([read], "ENGINE", "work/one", "feed0001")!.Value.Part.Act);
        Assert.Null(ChainTidies.Of([read], "engine", "work/one", "feed0009"));
        Assert.Null(ChainTidies.Of([read], "engine", "work/three", "feed0001"));
    }
}
