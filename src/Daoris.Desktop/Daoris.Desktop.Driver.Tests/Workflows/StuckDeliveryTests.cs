using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The delivery steps that settle on their own: a <c>push</c> active with its attempt unreconciled when the line comes to hold the branch
/// is passed over as nothing to deliver; one the remote does not hold is pushed again; and a pull request's watch reaches at the commit its
/// own binding holds, however the landing's tip reads, so a request passed over as delivered never leaves it waiting.
/// </summary>
public sealed class StuckDeliveryTests
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
    ];

    private static readonly Wire[] Wires =
    [
        WireEvaluationTests.After("c", "landing", "push"),
        WireEvaluationTests.After("d", "push", "pull-request"),
        WireEvaluationTests.After("m", "pull-request", "monitor"),
        new("t", WireKinds.When, new("monitor"), new("tidy")) { Outcome = PullRequestReading.Merged },
    ];

    private static readonly HeldFact OnLine = new(Then, Then, LandedKind.OnLine, "t1", "dev", "origin/dev", "f9", "engine-1");

    private static LandedBranch Landing(HeldFact? held = null, string tip = "t1") =>
        new("engine", "default", "daoris/s-0001", "dev", tip, "s-0001", "q1", "Fix", Then)
        {
            Pushed = true, PushedTip = tip, Units = [Unit], Held = held, GoneAt = held is null ? null : Then.AddHours(1),
        };

    private static WorkUnit UnitOf(LandedBranch landing) =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s-0001", "dev", "f0", null, Then) { Unit = Unit }], [landing], [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));

    private static WireEvaluationTests.Graph Run(LandedBranch landing) => new(Nodes, Wires)
    {
        Run = WireEvaluationTests.Base with
        {
            Revisions = new Dictionary<string, string?> { [Unit] = "t1" }, Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(landing) },
        },
    };

    [Fact]
    public void A_push_active_and_unreconciled_is_passed_over_once_the_line_holds_the_branch()
    {
        var g = Run(Landing());
        g.Through("landing");
        g.Start(g.Evaluate(), "push");
        Assert.Equal((NodeStates.Active, 1), (g.State("push"), g.Statuses["push"].Unreconciled));

        g.Run = g.Run with { Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(Landing(OnLine)) } };
        g.Evaluate();
        g.Evaluate();

        var push = g.Statuses["push"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path, (int?)null), (push.State, push.By, push.Unreconciled));
        Assert.Contains("`daoris/s-0001` is on `origin/dev` already", push.Words);
        Assert.Equal(NodeStates.Skipped, g.State("pull-request"));
    }

    [Fact]
    public void A_push_the_line_does_not_hold_is_never_passed_over()
    {
        var g = Run(Landing());
        g.Through("landing");
        g.Start(g.Evaluate(), "push");

        g.Evaluate();

        Assert.Contains(g.State("push"), new[] { NodeStates.Active, NodeStates.Unknown });
    }

    [Fact]
    public void A_push_whose_remote_does_not_hold_the_pushed_commit_says_so_and_is_pushed_again()
    {
        var landing = Landing();
        var observed = NodeObserving.Observe(UnitOf(landing), new NodeDeclaration("push", NodeKinds.Push, 1, NodeReach.Performed), "graph-1", 1,
            new NodeGitReads(RemoteTip: _ => "t0"));
        var active = new NodeStatus(Unit, "push", NodeStates.Active) { Attempt = 1, Unreconciled = 1, Holds = observed.Holds };

        Assert.Equal(PushReading.NotOnRemote, observed.Holds);
        Assert.True(PushAttempts.Due(landing, active));
        Assert.False(PushAttempts.Due(landing, active with { Holds = null }));
        Assert.True(PushAttempts.Due(landing with { Pushed = false, PushedTip = null }, active with { Holds = null }));
    }

    [Fact]
    public void A_watch_reaches_merged_at_its_own_binding_where_the_landings_tip_reads_otherwise()
    {
        var landing = Landing(OnLine, tip: "t2") with
        {
            PullRequest = "https://example.invalid/pr/1", PullRequestState = new PullRequestState(PullRequestStates.Completed) { Target = "dev", How = MergeHow.Squash },
        };
        var g = Run(landing);
        g.Through("landing");
        g.Evaluate();
        Assert.Equal(NodeStates.Skipped, g.State("pull-request"));

        g.Start(g.Evaluate(), "monitor");
        g.Evaluate();

        Assert.Equal((NodeStates.Reached, PullRequestReading.Merged), (g.State("monitor"), g.Statuses["monitor"].Acceptance?.Outcome));
        Assert.Equal(NodeStates.Ready, g.State("tidy"));
    }
}
