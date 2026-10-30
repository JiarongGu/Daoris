using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1o part o2 (the integration design §17.3, §17.6): the <c>work.requests</c> node observed from the quests' records alone. The quests a
/// run's sessions published are found by <c>PublishedBy</c> as <see cref="AskWork"/> joins them; the node is reached once each is done, stays active
/// while one is open or taken, and stays active on a decline, which reaches nothing and fails nothing: the person's pass is what lets the flow go on.
/// </summary>
public sealed class WorkRequestsNodeTests
{
    private const string Run = "run:r";
    private static readonly NodeScope Scope = new("graph-1", "run-1");
    private static readonly NodeDeclaration Node = new("requests", NodeKinds.WorkRequests, 1, NodeReach.Observed);

    private static readonly WorkUnit Self = new(Run, WorkUnitKind.Run, new WorkOwner("engine", "default", null, WorkRecord.DriverWriter),
        new WorkSubject("r", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []);

    private static NodeAcceptance Binding => new(Run, null, Scope.GraphDigest, "requests", 1, 1);

    private static QuestView Quest(string id, string status, string? by = "s1", string to = "tools", string? note = null) =>
        new(id, "engine", to, $"Quest {id}", "Body.", status) { PublishedBy = by, Note = note };

    [Fact]
    public void The_quests_a_runs_sessions_published_are_found_by_the_session_that_published_them()
    {
        QuestView[] every =
        [
            Quest("q1", "Open"), Quest("q2", "Done", by: "s2"), Quest("q3", "Open", by: "peer/s1"), Quest("q4", "Open", by: "other"),
            Quest("q5", "Open", by: null), Quest("q6", "Open", by: "s1", to: "engine"),
        ];

        // s1 and a teammate's record of s2 ("peer/s2", spelled s2 by their machine) publish; q3 names a session this run holds no record of,
        // and q6 is the run's own work.
        var found = WorkRequestsNode.Published(every, ["s1", "peer/s2"], new HashSet<string>(["q6"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal(["q1", "q2"], found.Select(quest => quest.Id));
    }

    /// <summary>
    /// What the run's view draws of each (§17.3): its repository, its state, whether a session of the work waits on it (the work quest that names it in
    /// <c>Awaits</c> while it is not closed) and the close's note; and a helper a session of the work asked beside them, as a request to an agent.
    /// </summary>
    [Fact]
    public void Each_published_quest_is_drawn_with_its_repository_state_wait_and_note_and_a_helper_beside_it_as_a_request_to_an_agent()
    {
        QuestView[] work = [Quest("q7", "Taken", by: null, to: "engine") with { Awaits = "q9" }];
        QuestView[] published = [Quest("q9", "Taken"), Quest("q8", "Done", to: "docs", note: "shipped it"), Quest("q6", "Declined", note: "not here")];
        TracedSession[] helpers =
        [
            new("h1", "engine", "working") { Serves = "s1", Adapter = "codex", Created = Then },
            new("h2", "engine", "failed") { Serves = "s1", Adapter = "gemini", Note = "ran out", Updated = Then },
        ];

        var drawn = WorkRequests.Draw(published, work, helpers);

        Assert.Equal(
            [
                ("quest", "q9", "tools", "taken", true, (string?)null), ("quest", "q8", "docs", "done", false, "shipped it"),
                ("quest", "q6", "tools", "declined", false, "not here"), ("agent", "h1", "codex", "working", false, null), ("agent", "h2", "gemini", "failed", false, "ran out"),
            ],
            drawn.Select(request => (request.Kind, request.Id, request.To, request.State, request.Awaits, request.Words)));
        Assert.All(drawn, request => Assert.Equal("s1", request.Session));

        // Once it is closed its asker is resumed on the answer: the question quest still names it, and nothing waits any more.
        Assert.False(WorkRequests.Draw([Quest("q9", "Done")], [Quest("q7", "Taken", by: null, to: "engine") with { Awaits = "q9" }], [])[0].Awaits);
    }

    private static readonly DateTimeOffset Then = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void It_is_active_while_one_is_open_or_taken_and_names_it()
    {
        var open = WorkRequestsNode.Observe(Self, Node, Binding, [Quest("q1", "Done"), Quest("q2", "Taken")]);

        Assert.Null(open.Refused);
        Assert.Null(open.Outcome);
        Assert.Null(open.Move);
        Assert.Contains("#q2", open.Said);
    }

    [Fact]
    public void A_decline_holds_it_active_never_failed_and_says_whose_it_is_to_pass_over()
    {
        var declined = WorkRequestsNode.Observe(Self, Node, Binding, [Quest("q1", "Done"), Quest("q2", "Declined", note: "not here"), Quest("q3", "Open")]);

        Assert.Null(declined.Refused);
        Assert.Null(declined.Outcome);
        Assert.Null(declined.Move);
        Assert.Contains("#q2", declined.Said);
        Assert.Contains("declined", declined.Said);
        Assert.Null(NodeAuthorities.BuiltIn[NodeKinds.WorkRequests].FailureBy);
    }

    [Fact]
    public void It_is_reached_once_each_is_done_with_the_quests_named_as_its_evidence_through_the_one_table()
    {
        var seen = WorkRequestsNode.Observe(Self, Node, Binding, [Quest("q2", "Done"), Quest("q1", "Done")]);

        Assert.Equal(NodeStates.Reached, seen.Outcome);
        Assert.Equal(NodeSource.Requests, seen.Source);
        Assert.Equal("daoris/work.requests@1", seen.Acceptance!.Evidence!.Schema);
        Assert.Equal("quests#q1,q2", seen.Acceptance.Evidence.Ref);

        var status = new NodeStatus(Run, "requests", NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding, Scope = Scope };
        var moved = NodeLifecycle.Apply(Node, status, Assert.IsType<NodeMove>(seen.Move));
        Assert.Null(moved.Refused);
        Assert.Equal((NodeStates.Reached, NodeSource.Requests), (moved.Status.State, moved.Status.By));
    }

    [Fact]
    public void A_run_whose_sessions_published_nothing_has_nothing_to_wait_on()
    {
        var seen = WorkRequestsNode.Observe(Self, Node, Binding, []);

        Assert.Equal(NodeStates.Reached, seen.Outcome);
        Assert.Equal("quests#none", seen.Acceptance!.Evidence!.Ref);
    }

    [Fact]
    public void Quests_that_were_not_read_wait_and_a_binding_for_another_subject_or_node_is_refused()
    {
        Assert.Null(WorkRequestsNode.Observe(Self, Node, Binding, null).Outcome);
        Assert.Equal(NodeRefusal.NotBound, WorkRequestsNode.Observe(Self, Node, Binding with { Subject = "unit-other" }, []).Refused);
        Assert.Equal(NodeRefusal.NotObserved,
            WorkRequestsNode.Observe(Self, Node with { Kind = NodeKinds.GoAhead }, Binding, []).Refused);
    }

    /// <summary>Through the evaluation: the run's published quests handed in by its unit move the node, and what is wired after it follows.</summary>
    [Fact]
    public void The_evaluation_reaches_the_node_when_each_is_done_and_leaves_it_active_otherwise()
    {
        NodeDeclaration[] nodes = [Node, new("land", NodeKinds.Handed, 1, NodeReach.Observed)];
        Wire[] wires = [new("a", WireKinds.After, new WireEnd("requests"), new WireEnd("land"))];

        WireEvaluated Evaluate(params QuestView[] published)
        {
            var statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["requests"] = new(Run, "requests", NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding, Scope = Scope },
                ["land"] = NodeLifecycle.Start(Run, "land"),
            };
            return WireEvaluation.Evaluate(nodes, wires, statuses, new WireRun(Scope.GraphDigest, Scope.Run)
            {
                Revisions = new Dictionary<string, string?> { [Run] = null },
                Units = new Dictionary<string, WorkUnit> { [Run] = Self },
                Requests = new Dictionary<string, IReadOnlyList<QuestView>> { [Run] = published },
            });
        }

        var waiting = Evaluate(Quest("q1", "Open"));
        Assert.Empty(waiting.Refused);
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (waiting.Statuses["requests"].State, waiting.Statuses["land"].State));

        var held = Evaluate(Quest("q1", "Declined"));
        Assert.Equal(NodeStates.Active, held.Statuses["requests"].State);

        var done = Evaluate(Quest("q1", "Done"));
        Assert.Empty(done.Refused);
        Assert.Equal((NodeStates.Reached, NodeStates.Ready), (done.Statuses["requests"].State, done.Statuses["land"].State));
    }
}
