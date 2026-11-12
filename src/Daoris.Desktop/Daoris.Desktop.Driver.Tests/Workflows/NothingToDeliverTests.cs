using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a run's landing leaves to deliver. A session that committed nothing lands nothing (its session branch held <c>empty</c>, no
/// branch made), so every step after its <c>land</c> is passed over as nothing to deliver, a push already started included. A landed
/// branch the line already holds (its pull request completed on the platform with no answer Daoris read) leaves no pull request to open,
/// and its monitor reads it merged.
/// </summary>
public sealed class NothingToDeliverTests
{
    private const string Unit = WireEvaluationTests.Unit;
    private static readonly DateTimeOffset Then = WireEvaluationTests.Then;

    private static readonly NodeDeclaration[] Nodes =
    [
        WireEvaluationTests.Node("landing", NodeKinds.Land),
        WireEvaluationTests.Node("push", NodeKinds.Push),
        new("pull-request", "acme-host/pull-request", 1, NodeReach.Performed),
        new("monitor", NodeKinds.Monitor, 1, NodeReach.Observed) { Outcomes = [PullRequestReading.Merged, PullRequestReading.Closed] },
        new("tidy", NodeKinds.Tidy, 1, NodeReach.Performed),
        new("closed", NodeKinds.Human, 1, NodeReach.Pressed),
    ];

    private static readonly Wire[] Wires =
    [
        WireEvaluationTests.After("c", "landing", "push"),
        WireEvaluationTests.After("d", "push", "pull-request"),
        WireEvaluationTests.After("m", "pull-request", "monitor"),
        new("t", WireKinds.When, new("monitor"), new("tidy")) { Outcome = PullRequestReading.Merged },
        new("x", WireKinds.When, new("monitor"), new("closed")) { Outcome = PullRequestReading.Closed },
    ];

    private static HeldFact Held(string by, string tip) => new(Then, Then, by, tip, "main", "origin/main", "f9", "repo-1");

    private static WorkUnit UnitOf(HeldFact? branchHeld, LandedBranch? landing)
    {
        var grown = new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = Unit, Held = branchHeld };
        LandedBranch[] landings = landing is null ? [] : [landing];
        return Assert.Single(WorkInventory.Units(new InventoryFacts([grown], landings, [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));
    }

    private static LandedBranch Landing(HeldFact? held = null) =>
        new("engine", "default", "feature/q1-fix", "main", "t1", "s-q1", "q1", "Fix", Then)
        {
            Pushed = true, PushedTip = "t1", Units = [Unit], Held = held, GoneAt = held is null ? null : Then.AddHours(1),
        };

    private static WireEvaluationTests.Graph Run(WorkUnit unit) => new(Nodes, Wires)
    {
        Run = WireEvaluationTests.Base with
        {
            Revisions = new Dictionary<string, string?> { [Unit] = "t1" }, Units = new Dictionary<string, WorkUnit> { [Unit] = unit },
        },
    };

    /// <summary>An empty landing: the push, the pull request, its monitor and both its routes are passed over as nothing to deliver, and stay so.</summary>
    [Fact]
    public void An_empty_landing_passes_every_step_after_it_over_as_nothing_to_deliver()
    {
        var g = Run(UnitOf(Held(EndBy.Empty, "f0"), null));

        g.Through("landing");
        g.Evaluate();
        g.Evaluate();

        foreach (var id in new[] { "push", "pull-request", "monitor", "tidy", "closed" })
        {
            var status = g.Statuses[id];
            Assert.Equal((id, NodeStates.Skipped, NodeSource.Path), (id, status.State, status.By));
            Assert.Contains("nothing to deliver: `daoris/s1` committed nothing", status.Words);
        }
    }

    /// <summary>A push begun before its landing read empty (the run the install kept active) is passed over by the landing record.</summary>
    [Fact]
    public void A_push_already_started_on_an_empty_landing_is_passed_over_by_the_landing_record()
    {
        var g = Run(UnitOf(null, null));
        g.Through("landing");
        g.Start(g.Evaluate(), "push");
        Assert.Equal(NodeStates.Active, g.State("push"));

        g.Run = g.Run with { Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(Held(EndBy.Empty, "f0"), null) } };
        var evaluated = g.Evaluate();
        g.Evaluate();

        Assert.Equal((NodeStates.Skipped, NodeSource.Landing), (g.State("push"), g.Statuses["push"].By));
        Assert.Contains(evaluated.Moves, move => move.Node == "push" && move.Detail!.Contains("nothing to deliver", StringComparison.Ordinal));
        Assert.Equal((NodeStates.Skipped, NodeStates.Skipped), (g.State("pull-request"), g.State("monitor")));
    }

    /// <summary>
    /// A landed branch the line holds, its pull request completed on the platform where no plugin answered for it: the request is not opened,
    /// the monitor reads it merged, and the run goes on to its tidy.
    /// </summary>
    [Fact]
    public void A_landing_the_line_holds_closes_its_pull_request_step_and_the_monitor_reads_it_merged()
    {
        var g = Run(UnitOf(null, Landing()));
        g.Through("landing");
        g.Through("push");

        g.Run = g.Run with { Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(null, Landing(Held(LandedKind.OnLine, "t1"))) } };
        g.Evaluate();

        var request = g.Statuses["pull-request"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path), (request.State, request.By));
        Assert.Contains("`feature/q1-fix` is on `origin/main` already", request.Words);

        g.Start(g.Evaluate(), "monitor");
        var routed = g.Evaluate();
        Assert.Equal((NodeStates.Reached, PullRequestReading.Merged), (g.State("monitor"), g.Statuses["monitor"].Acceptance?.Outcome));
        Assert.Equal(NodeStates.Ready, g.State("tidy"));
        Assert.Equal(["closed"], routed.NotTaken);
    }

    /// <summary>
    /// A pull-request step that failed (no token, not transient) and whose request is then opened and merged another way: the line's hold
    /// passes the failed step over as nothing to deliver, so the run reaches its monitor, which reads it merged; held no longer, it stays failed.
    /// </summary>
    [Fact]
    public void A_failed_pull_request_step_whose_branch_the_line_holds_is_passed_over_and_the_run_goes_on()
    {
        var g = Run(UnitOf(null, Landing()));
        g.Through("landing");
        g.Through("push");
        g.Start(g.Evaluate(), "pull-request");
        g.Fail("pull-request");

        g.Evaluate();
        Assert.Equal(NodeStates.Failed, g.State("pull-request"));

        g.Run = g.Run with { Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(null, Landing(Held(LandedKind.OnLine, "t1"))) } };
        g.Evaluate();

        var request = g.Statuses["pull-request"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path, (string?)null), (request.State, request.By, request.Code));
        Assert.Contains("`feature/q1-fix` is on `origin/main` already", request.Words);

        g.Start(g.Evaluate(), "monitor");
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, PullRequestReading.Merged), (g.State("monitor"), g.Statuses["monitor"].Acceptance?.Outcome));
        Assert.Equal(NodeStates.Ready, g.State("tidy"));
    }

    /// <summary>The line's hold reads merged only where the plugin gave no answer: its own answer stands.</summary>
    [Fact]
    public void A_plugins_answer_stands_over_the_lines_hold()
    {
        var held = Landing(Held(LandedKind.OnLine, "t1"));
        Assert.Equal(PullRequestReading.Merged, PullRequestReading.Of(held).State);
        Assert.True(PullRequestReading.IsMerged(held));

        var abandoned = held with { PullRequest = "https://example.invalid/pr/1", PullRequestState = new PullRequestState(PullRequestStates.Abandoned) };
        Assert.Equal(PullRequestReading.Closed, PullRequestReading.Of(abandoned).State);
        Assert.False(PullRequestReading.IsMerged(abandoned));
        Assert.Null(PullRequestReading.Of(Landing()).State);
    }
}
