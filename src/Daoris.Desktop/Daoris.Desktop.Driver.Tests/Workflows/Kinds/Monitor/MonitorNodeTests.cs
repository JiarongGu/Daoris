using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The monitor watches the one step wired straight into it, through that step's own state reading, and is reached
/// with the state that step reports as its outcome. A pull request reports <c>merged</c>, a squash merge included, or <c>closed</c>; open or
/// unanswered it is pending and nothing fails it. The save holds a monitor to one watched step that reports a state, and to that step's states
/// as its outcomes; a run routes on them.
/// </summary>
public sealed class MonitorNodeTests
{
    private const string Address = "https://example.invalid/acme/engine/pull/1";

    private static readonly NodeDeclaration Request = new("pull-request", "acme-host/pull-request", 1, NodeReach.Performed);

    private static readonly NodeDeclaration Monitor =
        new("monitor", NodeKinds.Monitor, 1, NodeReach.Observed) { Outcomes = [PullRequestReading.Merged, PullRequestReading.Closed] };

    private static LandedBranch Landing(PullRequestState? state = null, string? address = Address) =>
        new("engine", "default", "feature/q1-fix", "main", "t1", "s-q1", "q1", "Fix", WireEvaluationTests.Then)
        {
            PullRequest = address, Plugin = "acme-host", PullRequestState = state, Squashed = "feed0002", Units = [WireEvaluationTests.Unit],
        };

    private static WorkUnit Unit(LandedBranch landing)
    {
        var grown = new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, WireEvaluationTests.Then) { Unit = WireEvaluationTests.Unit };
        return Assert.Single(WorkInventory.Units(new InventoryFacts([grown], [landing], [], [], new HashSet<string>(), _ => false,
            WireEvaluationTests.Then.AddDays(1))));
    }

    /// <summary>The monitor's reading on <paramref name="landing"/>, its one watched step the pull request's.</summary>
    private static NodeObservation Observed(LandedBranch landing, params NodeDeclaration[] follows)
    {
        var unit = Unit(landing);
        var reading = new NodeReading(unit, Monitor, new NodeAcceptance(unit.Id, "t1", "graph-1", Monitor.Id, 1, 1)) { Follows = follows };
        return Assert.IsAssignableFrom<IObserves>(NodeKit.Of(NodeKinds.Monitor)).Observe(reading);
    }

    private static PullRequestState Completed(string? how = null) => new(PullRequestStates.Completed) { How = how };

    [Fact]
    public void It_is_pending_while_the_request_is_unopened_open_or_unanswered_and_nothing_fails_it()
    {
        Assert.Null(Observed(Landing(address: null), Request).Outcome);
        Assert.Null(Observed(Landing(), Request).Outcome);
        Assert.Null(Observed(Landing(new PullRequestState(PullRequestStates.Open)), Request).Outcome);
        Assert.Null(NodeKit.Row(NodeKinds.Monitor)!.FailureBy);
        Assert.False(NodeKit.Row(NodeKinds.Monitor)!.PersonSkips);
        Assert.Equal(NodeSource.Watched, NodeKit.Row(NodeKinds.Monitor)!.ReachBy);
    }

    [Fact]
    public void It_is_reached_with_the_watched_steps_state_a_squash_read_as_merged()
    {
        var squashed = Observed(Landing(Completed(MergeHow.Squash)), Request);
        Assert.Equal((NodeStates.Reached, NodeSource.Watched, PullRequestReading.Merged), (squashed.Outcome, squashed.Source, squashed.Acceptance!.Outcome));
        Assert.Contains("squashed", squashed.Said, StringComparison.Ordinal);
        Assert.Equal("t1", squashed.Acceptance.SubjectRevision);

        var closed = Observed(Landing(new PullRequestState(PullRequestStates.Abandoned)), Request);
        Assert.Equal((NodeStates.Reached, PullRequestReading.Closed), (closed.Outcome, closed.Acceptance!.Outcome));
        Assert.Contains("without merging", closed.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void It_reads_nothing_where_it_follows_no_one_step_that_reports_a_state()
    {
        var push = new NodeDeclaration("push", NodeKinds.Push, 1, NodeReach.Performed);
        Assert.Null(Observed(Landing(Completed()), push).Outcome);
        Assert.Null(Observed(Landing(Completed())).Outcome);
        Assert.Null(Observed(Landing(Completed()), Request, Request with { Id = "again" }).Outcome);
        Assert.Equal([PullRequestReading.Merged, PullRequestReading.Closed], Assert.IsAssignableFrom<IWatchable>(NodeKit.Of(Request.Kind)).States);
    }

    internal static string Graph(string monitor, string wires, string tail = "") => $$$"""
        {"schemaVersion":2,"entries":["work"],"end":"tidy","nodes":[
          {"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
          {"id":"push","kind":"push","version":1,"reach":"performed"},
          {"id":"pull-request","kind":"acme-host/pull-request","version":1,"reach":"performed"},
          {{{monitor}}},
          {"id":"tidy","kind":"tidy","version":1,"reach":"performed"},
          {"id":"closed","kind":"human","version":1,"reach":"pressed"}{{{tail}}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"push"},
                  {"id":"c","kind":"after","from":"push","to":"pull-request"},{{{wires}}}]}
        """;

    internal const string Watching = """{"id":"monitor","kind":"monitor","version":1,"reach":"observed","outcomes":["merged","closed"]}""";

    internal const string Routes = """
        {"id":"t","kind":"when","from":"monitor","to":"tidy","outcome":"merged"},{"id":"x","kind":"when","from":"monitor","to":"closed","outcome":"closed"}
        """;

    private static IEnumerable<string?> Refused(string json) =>
        NodeGraphs.Read(json, onSave: true).Problems.Where(problem => problem.Node == "monitor").Select(problem => problem.Code);

    [Fact]
    public void The_save_takes_a_monitor_on_the_one_step_it_watches_with_that_steps_states()
    {
        var (graph, problems) = NodeGraphs.Read(Graph(Watching, """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""" + Routes), onSave: true);

        Assert.Empty(problems);
        Assert.Equal([PullRequestReading.Merged, PullRequestReading.Closed], graph!.Nodes.Single(node => node.Id == "monitor").Outcomes);
    }

    [Fact]
    public void The_save_refuses_a_monitor_on_no_step_two_steps_a_step_with_no_state_or_outcomes_not_its_states()
    {
        Assert.Equal([GraphRefusal.MonitorWatch], Refused(Graph(Watching, """{"id":"m","kind":"after","from":"push","to":"monitor"},""" + Routes)));
        Assert.Equal([GraphRefusal.MonitorWatch], Refused(Graph(Watching,
            """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},{"id":"n","kind":"after","from":"push","to":"monitor"},""" + Routes)));
        Assert.Contains(GraphRefusal.MonitorWatch, Refused(Graph(Watching, """{"id":"m","kind":"trigger","from":"pull-request","to":"monitor"},""" + Routes)));
        var reversed = Watching.Replace("""["merged","closed"]""", """["closed","merged"]""", StringComparison.Ordinal);
        Assert.Equal([GraphRefusal.MonitorWatch], Refused(Graph(reversed, """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""" + Routes)));

        // A monitor with no outcomes is no monitor's declaration, and no `when` wire follows it.
        var bare = """{"id":"monitor","kind":"monitor","version":1,"reach":"observed"}""";
        Assert.Contains(GraphRefusal.Declaration, Refused(Graph(bare, """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""" + Routes)));
        Assert.Equal("outcomes", NodeDeclarations.Problem(new NodeDeclaration("w", NodeKinds.Monitor, 1, NodeReach.Observed)));
        Assert.Equal("outcomes", NodeDeclarations.Problem(new NodeDeclaration("t", NodeKinds.Tidy, 1, NodeReach.Performed) { Outcomes = ["merged", "closed"] }));
    }

    /// <summary>A run over the shipped routes: a request merged reaches the tidy, one closed the person's task, and the other route is not taken.</summary>
    [Theory]
    [InlineData(PullRequestStates.Completed, "tidy", "closed")]
    [InlineData(PullRequestStates.Abandoned, "closed", "tidy")]
    public void A_run_routes_on_what_the_monitor_saw(string answered, string reached, string untaken)
    {
        NodeDeclaration[] nodes =
        [
            Request, Monitor, new("tidy", NodeKinds.Tidy, 1, NodeReach.Performed), new("closed", NodeKinds.Human, 1, NodeReach.Pressed),
        ];
        Wire[] wires =
        [
            WireEvaluationTests.After("m", Request.Id, Monitor.Id),
            new("t", WireKinds.When, new(Monitor.Id), new("tidy")) { Outcome = PullRequestReading.Merged },
            new("x", WireKinds.When, new(Monitor.Id), new("closed")) { Outcome = PullRequestReading.Closed },
        ];
        var g = new WireEvaluationTests.Graph(nodes, wires)
        {
            Run = WireEvaluationTests.Base with
            {
                Revisions = new Dictionary<string, string?> { [WireEvaluationTests.Unit] = "t1" },
                Units = new Dictionary<string, WorkUnit> { [WireEvaluationTests.Unit] = Unit(Landing(new PullRequestState(answered))) },
            },
        };

        g.Through(Request.Id);
        g.Start(g.Evaluate(), Monitor.Id);
        var routed = g.Evaluate();

        Assert.Equal(NodeStates.Reached, g.State(Monitor.Id));
        Assert.Equal((NodeStates.Ready, NodeStates.Blocked), (g.State(reached), g.State(untaken)));
        Assert.Equal([untaken], routed.NotTaken);
    }
}
