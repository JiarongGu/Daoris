using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the tests of the built-in nodes observed from the records share: a branch unit and its
/// landing as the records keep them, a node's binding, and an observation applied through the one table as any node's report is.
/// </summary>
internal static class ObservedFixture
{
    public static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    public const string Graph = "graph-1";

    public static HeldFact Held(string tip = "t1") => new(Then, Then, EndBy.Landed, tip, "main", "feature/x", "m1", "repo-id");

    public static GrownBranch Grown(HeldFact? held = null) =>
        new("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a", Held = held };

    public static LandedBranch Landed(bool pushed = false, PullRequestState? pr = null, PullRequestAskFailed? askFailed = null, HeldFact? held = null) =>
        new("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then)
        {
            Units = ["unit-a"], Pushed = pushed, PushedTip = pushed ? "t1" : null, Plugin = "acme-pr", PullRequestState = pr,
            PullRequestAskFailed = askFailed, Held = held,
        };

    public static WorkUnit Unit(GrownBranch? branch = null, params LandedBranch[] landings) =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            branch is null ? [] : [branch], landings, [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));

    public static NodeDeclaration Node(string kind) => new(kind, kind, 1, NodeReach.Observed);

    /// <summary>The binding Daoris evaluated when the node started: the unit's live tip, as the reader hands it in.</summary>
    public static NodeAcceptance Binding(WorkUnit unit, string kind, string? tip = "t1") => new(unit.Id, tip, Graph, kind, 1, 1);

    public static NodeStatus Observing(WorkUnit unit, string kind, string? tip = "t1") =>
        new(unit.Id, kind, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding(unit, kind, tip) };

    /// <summary>The observation applied as any node's report is: through the one table, by its authority.</summary>
    public static NodeMoved Applied(WorkUnit unit, string kind, NodeGitReads? git = null, string? tip = "t1")
    {
        var observation = NodeObserving.Observe(unit, Node(kind), Graph, 1, git);
        Assert.Null(observation.Refused);
        Assert.NotNull(observation.Move);
        return NodeLifecycle.Apply(Node(kind), Observing(unit, kind, tip), observation.Move);
    }
}
