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

    /// <summary>The binding Daoris evaluated when the node started: the unit's live tip, as the reader hands it in.</summary>
    private static NodeAcceptance Binding(WorkUnit unit, string kind, string? tip = "t1") => new(unit.Id, tip, Graph, kind, 1, 1);

    private static NodeStatus Observing(WorkUnit unit, string kind, string? tip = "t1") =>
        new(unit.Id, kind, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding(unit, kind, tip) };

    /// <summary>The observation applied as any node's report is: through the one table, by its authority.</summary>
    private static NodeMoved Applied(WorkUnit unit, string kind, NodeGitReads? git = null, string? tip = "t1")
    {
        var observation = BuiltInNodes.Observe(unit, Node(kind), Graph, 1, git);
        Assert.Null(observation.Refused);
        Assert.NotNull(observation.Move);
        return NodeLifecycle.Apply(Node(kind), Observing(unit, kind, tip), observation.Move);
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
            var done = QuestUnit(Quest("Done", held));
            var moved = Applied(done, NodeKinds.Claimed, tip: done.Subject.Revision);
            Assert.Equal((NodeStates.Reached, NodeSource.Ledger), (moved.Status.State, moved.Status.By));
            Assert.Equal(("quest:q1", "quests#q1"), (moved.Status.Current?.Unit, moved.Status.Current?.Evidence?.Ref));
        }

        Assert.Null(BuiltInNodes.Observe(QuestUnit(Quest("Taken")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Null(BuiltInNodes.Observe(QuestUnit(Quest("Declined")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Equal(NodeStates.Unknown, Applied(QuestUnit(Quest("Paused")), NodeKinds.Claimed).Status.State);
    }

    /// <summary>
    /// FABRIC-CUTOVER1a (the cutover design §2.2): on a run's subject, <c>claimed</c> reaches on its quest's done only where that quest's steps
    /// name none still to come in its repository, as the close and the opinion's gate read a chain; a look's set-up step is no step of the
    /// work. On a quest unit of Current's work cycle it reads as today.
    /// </summary>
    [Fact]
    public void Claimed_on_a_runs_subject_waits_for_its_chains_last_step_here()
    {
        WorkUnit QuestUnit(params QuestStepView[] then) =>
            Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done") { Updated = Then, Then = then }], []));
        NodeAcceptance OnRun(WorkUnit quest) => new("run:r", quest.Subject.Revision, Graph, NodeKinds.Claimed, 1, 1) { Subject = quest.Id };

        var more = QuestUnit(new QuestStepView("engine", "Next", "More of it."));
        var waits = BuiltInNodes.Observe(more, Node(NodeKinds.Claimed), OnRun(more));
        Assert.Equal(((string?)null, (string?)null, (NodeMove?)null), (waits.Refused, waits.Outcome, waits.Move));
        Assert.Contains("engine", waits.Said, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Reached, BuiltInNodes.Observe(more, Node(NodeKinds.Claimed), Graph, 1).Outcome);

        foreach (var last in new[]
        {
            QuestUnit(),
            QuestUnit(new QuestStepView("game", "Then there", "Elsewhere.")),
            QuestUnit(new QuestStepView("engine", "Look", "At it.") { SetUpIn = "preview" }),
        })
        {
            var seen = BuiltInNodes.Observe(last, Node(NodeKinds.Claimed), OnRun(last));
            Assert.Equal((NodeStates.Reached, "quest:q1"), (seen.Outcome, seen.Acceptance?.Subject));
        }
    }

    /// <summary>
    /// FABRIC-CUTOVER1a (the cutover design §2.4.4, §11.2): on a run's subject <c>handed</c> is its session's work handed on, reached where the
    /// session branch's landing holds the tip it is bound to, by its record: made at it, or squashing it. Its acceptance keeps that tip, the one
    /// its binding lapses on, so the one table takes the reach; its evidence names the landing's tip, which what follows binds. A landing
    /// squashing another tip is not this work's; a branch unit of Current's landing cycle reads as today, on the landing's tip.
    /// </summary>
    [Fact]
    public void Handed_on_a_runs_subject_is_reached_by_its_landing_squashing_the_bound_tip()
    {
        var squash = Landed() with { Tip = "s1", Squashed = "t1" };
        var unit = Unit(Grown(), squash);
        var bound = new NodeAcceptance("run:r", "t1", Graph, NodeKinds.Handed, 1, 1) { Subject = unit.Id };

        var seen = BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), bound);
        Assert.Equal((NodeStates.Reached, bound.Id, "s1"), (seen.Outcome, seen.Acceptance?.Id, seen.Acceptance?.Evidence?.Revision));
        var moved = NodeLifecycle.Apply(Node(NodeKinds.Handed),
            new NodeStatus("run:r", NodeKinds.Handed, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = bound }, seen.Move!);
        Assert.Equal(((string?)null, NodeStates.Reached), (moved.Refused, moved.Status.State));

        foreach (var other in new[] { squash with { Squashed = "t0" }, squash with { Squashed = null } })
        {
            Assert.Null(BuiltInNodes.Observe(Unit(Grown(), other), Node(NodeKinds.Handed), bound).Move);
        }

        Assert.Equal(("s1", "s1"), (BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), Graph, 1).Acceptance?.SubjectRevision,
            BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), Graph, 1).Acceptance?.Evidence?.Revision));
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

    /// <summary>
    /// FABRIC-NODE1c, defect 4: an observation takes the binding Daoris evaluated, its inputs and receipts with it. A QA wired before
    /// <c>handed</c> is in <c>handed</c>'s acceptance, so invalidating the QA reaches it. A binding of another unit, node or version is no
    /// observation, and evidence at another tip than the binding's is not this binding's reach.
    /// </summary>
    [Fact]
    public void An_observation_takes_its_evaluated_binding_so_an_upstream_invalidation_reaches_it()
    {
        var unit = Unit(Grown(Held()), Landed(held: Held()));
        var qa = new NodeAcceptance(unit.Id, "t1", Graph, "qa", 1, 1);
        var binding = Binding(unit, NodeKinds.Handed) with { PrerequisiteReceipts = [qa.Id] };

        var observed = BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), binding);
        Assert.Equal(new[] { qa.Id }, observed.Acceptance?.PrerequisiteReceipts);
        var moved = NodeLifecycle.Apply(Node(NodeKinds.Handed), Observing(unit, NodeKinds.Handed) with { Binding = binding }, observed.Move!);
        Assert.Equal(((string?)null, NodeStates.Reached, binding.Id), (moved.Refused, moved.Status.State, moved.Status.Current?.Id));
        Assert.Contains(NodeAcceptances.Invalidate([qa, moved.Status.Current!], each => each.Id == qa.Id ? NodeLapse.Revision : null),
            each => each.Acceptance == binding.Id && each.Upstream == qa.Id);

        var newer = binding with { SubjectRevision = "t2" };
        var stale = NodeLifecycle.Apply(Node(NodeKinds.Handed), Observing(unit, NodeKinds.Handed) with { Binding = newer },
            BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), newer).Move!);
        Assert.Equal(NodeRefusal.NotBound, stale.Refused);

        Assert.Equal(NodeRefusal.NotBound, BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), binding with { Unit = "unit-b" }).Refused);
        Assert.Equal(NodeRefusal.NotBound, BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), binding with { Node = NodeKinds.Line }).Refused);
        Assert.Equal(NodeRefusal.NotBound, BuiltInNodes.Observe(unit, Node(NodeKinds.Handed), binding with { NodeVersion = 2 }).Refused);
    }
}
