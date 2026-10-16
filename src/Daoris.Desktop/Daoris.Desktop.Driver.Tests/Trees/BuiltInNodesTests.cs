using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-NODE1 (the fabric events design §E4, §C2, §C7): the built-in nodes are observed from the records UNIT1a's adapters already
/// read, never a second mechanism. Each observation is its authority's report, applied through the same one table as any node; what
/// the records cannot establish is unknown, and what they do not hold yet is no outcome, never a failure.
/// </summary>
public sealed class BuiltInNodesTests
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private const string Graph = "graph-1";

    private static HeldFact Held(string tip = "t1") => new(Then, Then, EndBy.Landed, tip, "main", "feature/x", "m1", "repo-id");

    private static GrownBranch Grown(HeldFact? held = null) =>
        new("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a", Held = held };

    private static LandedBranch Landed(bool pushed = false, PullRequestState? pr = null, PullRequestAskFailed? askFailed = null, HeldFact? held = null) =>
        new("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then)
        {
            Units = ["unit-a"], Pushed = pushed, PushedTip = pushed ? "t1" : null, Plugin = "acme-pr", PullRequestState = pr,
            PullRequestAskFailed = askFailed, Held = held,
        };

    private static WorkUnit Unit(GrownBranch? branch = null, params LandedBranch[] landings) =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            branch is null ? [] : [branch], landings, [], [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));

    private static NodeDeclaration Node(string kind) => new(kind, kind, 1, NodeReach.Observed);

    private static NodeStatus Observing(WorkUnit unit, string kind) => new(unit.Id, kind, NodeStates.Active) { Attempt = 1, Tries = 1 };

    /// <summary>The observation applied as any node's report is: through the one table, by its authority.</summary>
    private static NodeMoved Applied(WorkUnit unit, string kind, NodeGitReads? git = null)
    {
        var observation = BuiltInNodes.Observe(unit, Node(kind), Graph, 1, git);
        Assert.Null(observation.Refused);
        Assert.NotNull(observation.Move);
        return NodeLifecycle.Apply(Node(kind), Observing(unit, kind), observation.Move);
    }

    /// <summary>
    /// From the records alone: the landing is <c>handed</c>, the kept end fact is <c>line</c>, a pull request's completed answer that
    /// passes D148 point 4's checks is <c>pull-request</c>, each reached by its own authority with an acceptance bound to the commit
    /// its evidence names and pointing at its record, never a path.
    /// </summary>
    [Fact]
    public void A_branch_units_built_ins_are_reached_from_its_records_alone()
    {
        var completed = new PullRequestState(PullRequestStates.Completed) { MergeCommit = "m9", SourceCommit = "t1", Target = "main", Plugin = "acme-pr" };
        var unit = Unit(Grown(Held()), Landed(pushed: true, pr: completed, held: Held()));

        foreach (var (kind, source, evidence) in new[]
        {
            (NodeKinds.Handed, NodeSource.Git, "landings.json#feature/x"),
            (NodeKinds.Line, NodeSource.Git, "landings.json#feature/x"),
            (NodeKinds.PullRequest, NodeSource.Plugin, "landings.json#feature/x"),
        })
        {
            var moved = Applied(unit, kind);

            Assert.Equal((kind, (string?)null, NodeStates.Reached, source), (kind, moved.Refused, moved.Status.State, moved.Status.By));
            var accepted = moved.Status.Current!;
            Assert.Equal(("unit-a", "t1", Graph, kind, 1, 1), (accepted.Unit, accepted.SubjectRevision, accepted.GraphDigest, accepted.Node, accepted.NodeVersion, accepted.Attempt));
            Assert.Equal((evidence, "t1", accepted.Id), (accepted.Evidence?.Ref, accepted.Evidence?.Revision, accepted.Evidence?.Acceptance));
            Assert.DoesNotContain("engine", accepted.Evidence!.Ref, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <c>pushed</c> needs git's remote-tracking ref as well as the plugin's answer: unread, it is unknown; read at the pushed tip it is
    /// reached; elsewhere it is not yet. The records keep no failed push, so it never fails here.
    /// </summary>
    [Fact]
    public void Pushed_needs_the_plugins_answer_and_gits_ref()
    {
        var unit = Unit(Grown(), Landed(pushed: true));

        Assert.Equal(NodeStates.Unknown, Applied(unit, NodeKinds.Pushed).Status.State);
        Assert.Equal(NodeStates.Reached, Applied(unit, NodeKinds.Pushed, new NodeGitReads(RemoteTip: _ => "t1")).Status.State);
        var elsewhere = BuiltInNodes.Observe(unit, Node(NodeKinds.Pushed), Graph, 1, new NodeGitReads(RemoteTip: _ => "t0"));
        Assert.Null(elsewhere.Outcome);
        Assert.Null(BuiltInNodes.Observe(Unit(Grown(), Landed()), Node(NodeKinds.Pushed), Graph, 1).Outcome);
    }

    /// <summary>
    /// A pull request abandoned is its failure, with its code; an ask that failed, or a state the platform did not give, is unknown; a
    /// completed answer whose source is not the recorded tip waits on git's ancestry, and one failing D148's checks is no event.
    /// </summary>
    [Fact]
    public void A_pull_requests_answers_are_its_outcomes_by_its_authority()
    {
        var abandoned = Applied(Unit(Grown(), Landed(pr: new PullRequestState(PullRequestStates.Abandoned))), NodeKinds.PullRequest);
        Assert.Equal((NodeStates.Failed, PullRequestStates.Abandoned), (abandoned.Status.State, abandoned.Status.Code));

        var askFailed = new PullRequestAskFailed("timeout", "acme-pr", Then);
        Assert.Equal(NodeStates.Unknown, Applied(Unit(Grown(), Landed(pr: new PullRequestState(PullRequestStates.Open), askFailed: askFailed)), NodeKinds.PullRequest).Status.State);
        Assert.Equal(NodeStates.Unknown, Applied(Unit(Grown(), Landed(pr: new PullRequestState(PullRequestStates.Unknown))), NodeKinds.PullRequest).Status.State);
        Assert.Null(BuiltInNodes.Observe(Unit(Grown(), Landed(pr: new PullRequestState(PullRequestStates.Open))), Node(NodeKinds.PullRequest), Graph, 1).Outcome);

        var ahead = new PullRequestState(PullRequestStates.Completed) { MergeCommit = "m9", SourceCommit = "t2", Target = "main" };
        var unit = Unit(Grown(), Landed(pr: ahead));
        Assert.Equal(NodeStates.Unknown, Applied(unit, NodeKinds.PullRequest).Status.State);
        Assert.Equal(NodeStates.Reached, Applied(unit, NodeKinds.PullRequest, new NodeGitReads(Holds: (source, tip) => source == "t2" && tip == "t1")).Status.State);

        var elsewhere = new PullRequestState(PullRequestStates.Completed) { MergeCommit = "m9", SourceCommit = "t1", Target = "release" };
        Assert.Null(BuiltInNodes.Observe(Unit(Grown(), Landed(pr: elsewhere)), Node(NodeKinds.PullRequest), Graph, 1).Outcome);
    }

    /// <summary>Not yet is pending, never failed: a branch nothing handed on or held is observed with no outcome, and stays active.</summary>
    [Fact]
    public void Not_yet_is_no_outcome_and_an_unknown_owner_is_unknown()
    {
        var unit = Unit(Grown());
        foreach (var kind in new[] { NodeKinds.Handed, NodeKinds.Line, NodeKinds.PullRequest })
        {
            var observed = BuiltInNodes.Observe(unit, Node(kind), Graph, 1);
            Assert.Equal((kind, (string?)null, (NodeMove?)null), (kind, observed.Outcome, observed.Move));
            Assert.False(string.IsNullOrWhiteSpace(observed.Said));
        }

        var merged = Applied(Unit(Grown(Held())), NodeKinds.Handed);
        Assert.Equal((NodeStates.Reached, "session-branches.json#daoris/s1"), (merged.Status.State, merged.Status.Current?.Evidence?.Ref));

        var unowned = Assert.Single(WorkInventory.Units(new InventoryFacts(
            [], [], [new SessionTree(Path.GetFullPath(Path.Combine("node-trees", "stray")), "default", "engine", "feature/stray")], [], [],
            new HashSet<string>(), _ => false, Then)));
        Assert.Equal(NodeStates.Unknown, Applied(unowned, NodeKinds.Line).Status.State);
    }

    /// <summary><c>claimed</c> is the agent's done on the quest's record, held or not: a claim, by the ledger, and never anything else's evidence.</summary>
    [Fact]
    public void Claimed_is_the_quests_done_read_from_its_record()
    {
        QuestView Quest(string status, bool held = false) => new("q1", "game", "engine", "The work", "Body.", status) { Updated = Then, Held = held };
        WorkUnit QuestUnit(QuestView quest) => Assert.Single(QuestUnits.Read([quest], []));

        foreach (var held in new[] { false, true })
        {
            var moved = Applied(QuestUnit(Quest("Done", held)), NodeKinds.Claimed);
            Assert.Equal((NodeStates.Reached, NodeSource.Ledger), (moved.Status.State, moved.Status.By));
            Assert.Equal(("quest:q1", "quests#q1"), (moved.Status.Current?.Unit, moved.Status.Current?.Evidence?.Ref));
        }

        Assert.Null(BuiltInNodes.Observe(QuestUnit(Quest("Taken")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Null(BuiltInNodes.Observe(QuestUnit(Quest("Declined")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Equal(NodeStates.Unknown, Applied(QuestUnit(Quest("Paused")), NodeKinds.Claimed).Status.State);
    }

    /// <summary>An observer reads only its own unit's kind, and only the observed kinds: a pressed, performed or waited node is its row's.</summary>
    [Fact]
    public void Only_an_observed_built_in_on_its_own_kind_of_unit_is_observed()
    {
        var quest = Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done")], []));
        var branch = Unit(Grown(Held()));

        Assert.Equal(NodeRefusal.NotThisUnit, BuiltInNodes.Observe(quest, Node(NodeKinds.Line), Graph, 1).Refused);
        Assert.Equal(NodeRefusal.NotThisUnit, BuiltInNodes.Observe(branch, Node(NodeKinds.Claimed), Graph, 1).Refused);
        foreach (var kind in new[] { NodeKinds.Human, NodeKinds.QuestClose, NodeKinds.WaitUnit, NodeKinds.All, "acme.deploy/deploy" })
        {
            var refused = BuiltInNodes.Observe(branch, new NodeDeclaration("n", kind, 1, NodeReach.Performed), Graph, 1);
            Assert.Equal((kind, NodeRefusal.NotObserved, (NodeMove?)null), (kind, refused.Refused, refused.Move));
        }
    }
}
