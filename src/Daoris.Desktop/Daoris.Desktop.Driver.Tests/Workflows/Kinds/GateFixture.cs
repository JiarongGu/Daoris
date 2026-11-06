using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the tests of D157's gates as observed nodes (<c>look</c>, <c>go-ahead</c>, <c>opinion</c>) share: a branch unit, its binding and the gates' reads.</summary>
internal static class GateFixture
{
    public const string Unit = "unit-a";
    public const string Tip = "tip-1";
    public const string Act = "deploy the release to production";

    public static readonly DateTimeOffset Then = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public static readonly NodeScope Scope = new("graph-1", "run-1");

    public static NodeDeclaration Node(string kind) => new(kind, kind, 1, NodeKit.Row(kind)!.Reach)
    {
        Options = kind == NodeKinds.GoAhead ? new Dictionary<string, string> { ["act"] = WorkflowSteps.Quoted(Act) } : new Dictionary<string, string>(),
    };

    public static NodeAcceptance Binding(string kind) => new(Unit, Tip, Scope.GraphDigest, kind, 1, 1);

    public static NodeStatus Active(string kind) =>
        new(Unit, kind, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding(kind), Scope = Scope };

    public static LandedBranch Landed(string tip = Tip, LandingReview? review = null) =>
        new("engine", "default", "feature/q1-ship", "main", tip, "s1", "q1", null, Then) { Units = [Unit], Review = review };

    public static WorkUnit Branch(params LandedBranch[] landings) => new(Unit, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject("daoris/s1", Tip),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Records = new WorkUnitRecords(null, landings, null),
    };

    public static ReviewGateState Gate(string state, QuestReviewVerdictView? verdict = null, string? tip = Tip, string? skipped = null, string repository = "engine") =>
        new(state, new ReviewDecision(skipped is null ? ReviewLevels.Repository : ReviewLevels.Skip, skipped is null ? "preview" : null)
        {
            Repository = repository,
            Skip = skipped is null ? null : new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = skipped, At = Then },
        })
        {
            Tip = tip, Verdict = verdict,
        };

    public static GoAheadView Asked(string act, bool? approved, string? words = null, int number = 1) =>
        new(number, "write", "production", act, [new GoAheadRequestView("s1", "q1", Then, "to ship")])
        {
            Answer = approved is { } yes ? new GoAheadAnswerView(yes, words, Then) : null,
        };

    /// <summary>The observation applied as the evaluation applies it: through the one table, a skip scoped to its attempt's run.</summary>
    public static NodeMoved Applied(NodeDeclaration node, NodeObservation seen)
    {
        Assert.Null(seen.Refused);
        var move = Assert.IsType<NodeMove>(seen.Move);
        return NodeLifecycle.Apply(node, Active(node.Kind), move.To == NodeStates.Skipped ? move with { Scope = Scope } : move);
    }
}
