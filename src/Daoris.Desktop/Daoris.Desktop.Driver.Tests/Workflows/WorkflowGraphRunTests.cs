using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1e (D190 points 2, 3 and 7; the cutover design §2.2, §2.3, §6, §11.1): a run bound to a graph version is drawn from its record,
/// <c>run:&lt;run&gt;</c>, each node a step where its record's status says it stands, on the subject its binding names, named; an opinion, a look and
/// a plugin's performed step its wires let start said not begun until CUTOVER1d; its branches said to be its graph's; and a look the work's own
/// choice adds drawn beside the graph. Pure, over facts: the reader gathers them (<c>WorkflowPortsTests</c>) and the route answers them
/// (<c>DriverModuleWorkflowRunTests</c>).
/// </summary>
public sealed class WorkflowGraphRunTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private const string Run = "q1";

    private static readonly string Self = RunUnits.IdOf(Run);

    private const string Digest = "gph-0123456789abcdef0123456789abcdef";

    private const string Tip = "1111111111111111111111111111111111111111";

    private const string LandedTip = "2222222222222222222222222222222222222222";

    /// <summary>The claim, the opinion and the look beside it, a plugin's performed deploy, then the landing: every gate wired into it.</summary>
    private const string GatesAndADeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"opinion","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"look","kind":"look","version":1,"reach":"observed","options":{"environment":"local"}},
                  {"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"you"}}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"c>l","kind":"after","from":"claimed","to":"look"},
                  {"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"o>h","kind":"after","from":"opinion","to":"handed"},
                  {"id":"l>h","kind":"after","from":"look","to":"handed"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    /// <summary>A go-ahead before the landing, and the plugin's observed deploy after it.</summary>
    private const string GoAheadAndADeployAfter = """
        {"schemaVersion":2,"entries":["claimed"],"end":"deployed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"},
                  {"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>g","kind":"after","from":"claimed","to":"go"},{"id":"g>h","kind":"after","from":"go","to":"handed"},
                  {"id":"h>d","kind":"after","from":"handed","to":"deployed"}]}
        """;

    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private static NodeGraph Graph(string json)
    {
        var (graph, problems) = NodeGraphs.Read(json);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        return graph;
    }

    private static readonly WorkflowRunBinding Binding = new(Run, "engine", "default", "docs", WorkflowLevels.Repository, T) { Version = 1, Digest = Digest };

    private static QuestView Quest(string id, string status = "Taken") => new(id, "ask:a1", "engine", "The work", "Body.", status);

    private static TracedSession Session(string id, string quest, string state = "working") =>
        new(id, "engine", state) { Quest = quest, Tree = Path.Combine("trees", $"{quest}-tree"), Created = T };

    /// <summary>The run's records: its quests here, its session, its process held as a graph version, and Current's start holds.</summary>
    private static WorkflowRunFacts Records(params QuestView[] quests) =>
        new("engine", new CurrentWorkflow([], [], Digest))
        {
            Quests = quests,
            Sessions = [Session("s1", quests[^1].Id)],
            Process = WorkflowProcess.Current(DriverConfig.Empty, "engine", "default") with { Binding = Binding, Run = Run },
        };

    private static NodeStatus Status(string node, string state, string? subject = null, string? revision = null, int attempt = 1) =>
        new(Self, node, state)
        {
            Attempt = attempt,
            Binding = subject is null ? null : new NodeAcceptance(Self, revision, Digest, node, 1, attempt) { Subject = subject },
            Acceptance = state == NodeStates.Reached ? new NodeAcceptance(Self, revision, Digest, node, 1, attempt) { Subject = subject } : null,
        };

    private static NodeRecord Record(params NodeStatus[] statuses) =>
        new(Self) { Statuses = statuses.ToDictionary(status => status.Node, StringComparer.Ordinal) };

    private static WorkflowRun Derive(WorkflowRunFacts facts, string graph, NodeRecord? record, params GrownBranch[] branches) =>
        WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, Run, Graph(graph), record) { Branches = branches }, "default", "a1");

    private static WorkflowRunStep Step(WorkflowRun run, string id) => run.Steps.Single(step => step.Step.Id == id);

    /// <summary>
    /// A run Daoris has not judged yet draws its graph whole, each node not reached, standing at its entry; the built-ins with a step's kind as
    /// that step (the claim the work, <c>handed</c> the landing), a plugin's node as its own kind, and its wires carried for the drawing.
    /// </summary>
    [Fact]
    public void A_run_not_judged_yet_draws_its_graph_not_reached_standing_at_its_entry()
    {
        var run = Derive(Records(Quest("q1")), GatesAndADeploy, new NodeRecord(Self));

        Assert.Equal(
            [("claimed", WorkflowKinds.Work, NodeKinds.Claimed), ("opinion", WorkflowKinds.Opinion, NodeKinds.Opinion), ("look", WorkflowKinds.Look, NodeKinds.Look),
             ("deploy", "acme.deploy/deploy", "acme.deploy/deploy"), ("handed", WorkflowKinds.Landing, NodeKinds.Handed)],
            run.Steps.Select(step => (step.Step.Id, step.Step.Kind, step.Node)));
        Assert.All(run.Steps, step => Assert.Equal((WorkflowRunStates.NotReached, WorkflowGraphDetails.Blocked), (step.State, step.Detail)));
        Assert.Equal("claimed", run.At);
        Assert.Null(run.Problem);
        var graph = run.Graph!;
        Assert.Equal((false, false, "q1", "daoris/q1-tree"), (graph.Judged, graph.Unread, graph.Quest, graph.Branch));
        Assert.Equal(6, graph.Wires.Count);
        Assert.Contains(new WorkflowGraphWire("deploy", "handed", WireKinds.After), graph.Wires);
        Assert.Equal(("branch", "you", WorkflowParticipation.You, WorkflowPress.Accept),
            ((string?)Step(run, "handed").Step.Settings.Single(setting => setting.Name == "form").Value, (string?)Step(run, "handed").Step.Settings.Single(setting => setting.Name == "accept").Value,
             Step(run, "handed").Step.Participation, Step(run, "handed").Step.Press));
        Assert.Equal(
            [
                "Daoris has not judged its graph yet: its next look does.",
                "Its graph observes quest #q1, its newest work here, and `daoris/q1-tree`, the branch its newest session worked.",
                "Its branches are its graph's, not the landing rule's: a push, a pull request or a deploy happens for this work only where its graph holds one.",
                "Every landing door still holds this run: this build judges its graph and lands nothing for it yet.",
            ],
            WorkflowRunWords.GraphSaid(graph));
    }

    /// <summary>
    /// Each node says its record's state on the subject its binding names: the claim reached on its quest, the landing observed on the branch the
    /// session's records name at the tip it was bound to; the opinion, the look and the plugin's performed deploy its wires let start not begun
    /// (FABRIC-CUTOVER1c's guards), and the run stands at the first of them.
    /// </summary>
    [Fact]
    public void Each_node_says_its_records_state_on_the_subject_it_observes_and_a_gate_not_begun_says_so()
    {
        var record = Record(
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"),
            Status("opinion", NodeStates.Ready, attempt: 0),
            Status("look", NodeStates.Ready, attempt: 0),
            Status("deploy", NodeStates.Ready, attempt: 0),
            Status("handed", NodeStates.Active, "unit-b", Tip));

        var run = Derive(Records(Quest("q1")), GatesAndADeploy, record,
            new GrownBranch("engine", "default", "daoris/q1-tree", "main", "base", null, T) { Unit = "unit-b" });

        Assert.Equal(
            [
                (WorkflowRunStates.Done, WorkflowGraphDetails.Reached, "Reached on quest #q1."),
                (WorkflowRunStates.CannotStart, WorkflowGraphDetails.NotBegun, "Not begun: this build does not ask a graph run's second opinion yet."),
                (WorkflowRunStates.CannotStart, WorkflowGraphDetails.NotBegun, "Not begun: this build does not set up your look on a graph run yet."),
                (WorkflowRunStates.CannotStart, WorkflowGraphDetails.NotBegun, "Not started: this build starts no plugin's step on a graph run yet."),
                (WorkflowRunStates.Working, WorkflowGraphDetails.Active, "Observed on `daoris/q1-tree` at `11111111`."),
            ],
            run.Steps.Select(step => (step.State, step.Detail, WorkflowRunWords.Said(step))));
        Assert.Equal((WorkflowGraphSubjects.Quest, "q1"), (Step(run, "claimed").On, Step(run, "claimed").Quest));
        Assert.Equal((WorkflowGraphSubjects.Branch, "daoris/q1-tree", Tip, "s1"),
            (Step(run, "handed").On, Step(run, "handed").Branch, Step(run, "handed").Commit, Step(run, "handed").Session));
        Assert.Equal("opinion", run.At);
        Assert.True(run.Graph!.Judged);
        Assert.Equal("Its graph observes quest #q1, its newest work here, and `daoris/q1-tree`, the branch its newest session worked.", WorkflowRunWords.GraphSaid(run.Graph)[0]);
    }

    /// <summary>
    /// A go-ahead its wires let start is begun by the runtime and waits on the person, saying its act; once the landing is reached, what follows it
    /// observes that landing, named by the landed branch at the landing's tip as its record keeps it.
    /// </summary>
    [Fact]
    public void A_go_ahead_waits_on_you_and_what_follows_the_landing_observes_the_landing()
    {
        var waiting = Derive(Records(Quest("q1")), GoAheadAndADeployAfter, Record(
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"),
            Status("go", NodeStates.Active, Self)));

        Assert.Equal((WorkflowRunStates.WaitingOnYou, "Waits for your go-ahead: deploy the release"), (Step(waiting, "go").State, WorkflowRunWords.Said(Step(waiting, "go"))));
        Assert.Null(Step(waiting, "go").On);
        Assert.Equal("go", waiting.At);

        var landed = Status("handed", NodeStates.Reached, "unit-b", Tip) with
        {
            Acceptance = new NodeAcceptance(Self, Tip, Digest, "handed", 1, 1) { Subject = "unit-b", Evidence = new NodePort("landing", "landing#1", LandedTip, null) },
        };
        var facts = Records(Quest("q1")) with { Landings = [new LandedBranch("engine", "default", "release/q1", "main", LandedTip, "s1", "q1", null, T) { Units = ["unit-b"] }] };
        var run = Derive(facts, GoAheadAndADeployAfter, Record(
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"),
            Status("go", NodeStates.Reached, Self),
            landed,
            Status("deployed", NodeStates.Active, "unit-b", LandedTip)));

        Assert.Equal("Observed on its landing on `release/q1` at `22222222`.", WorkflowRunWords.Said(Step(run, "deployed")));
        Assert.Equal((WorkflowGraphSubjects.Landing, "release/q1", LandedTip), (Step(run, "deployed").On, Step(run, "deployed").Branch, Step(run, "deployed").Commit));
        Assert.Equal("deployed", run.At);
        Assert.Equal("Reached.", WorkflowRunWords.Said(Step(run, "go")));
    }

    /// <summary>
    /// D190 point 7 (the cutover design §6): a look the chain's choice asks for is D154's hold, drawn beside a graph that holds no look, before its
    /// landing, as a steps run's added look is; a graph holding its own look draws none beside it.
    /// </summary>
    [Fact]
    public void A_look_the_works_choice_adds_is_drawn_beside_a_graph_with_no_look()
    {
        var facts = Records(Quest("q1")) with { Review = new ReviewGateState(ReviewStates.NotShown, new ReviewDecision(ReviewLevels.Chain, "local")) };
        var record = Record(Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"), Status("handed", NodeStates.Active, "unit-b", Tip));

        var run = Derive(facts, ClaimedThenHanded, record);

        Assert.Equal(["claimed", "look", "handed"], run.Steps.Select(step => step.Step.Id));
        var look = Step(run, "look");
        Assert.Equal((ReviewLevels.Chain, (string?)null, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.NotShown), (look.Added, look.Node, look.State, look.Detail));
        Assert.Equal("look", run.At);

        var own = Derive(facts, GatesAndADeploy, record);
        Assert.DoesNotContain(own.Steps, step => step.Added is not null);
    }

    /// <summary>A record that does not read, and a run its record says ended, are each said above its nodes; an ended run stands nowhere.</summary>
    [Fact]
    public void A_record_that_does_not_read_and_a_run_that_ended_are_said()
    {
        var unread = Derive(Records(Quest("q1")), ClaimedThenHanded, null);
        Assert.True(unread.Graph!.Unread);
        Assert.Equal("Its node record does not read, so nothing on it is judged or started until a person mends it.", WorkflowRunWords.GraphSaid(unread.Graph)[0]);

        var ended = Derive(Records(Quest("q1", "Done")), ClaimedThenHanded, Record(
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"), Status("handed", NodeStates.Reached, "unit-b", Tip)) with { Ended = new RunEnd("q1", T) });
        Assert.Null(ended.At);
        Assert.Equal("It ended at quest #q1: nothing of its graph moves again until its chain comes back here.", WorkflowRunWords.GraphSaid(ended.Graph!)[0]);
    }

    /// <summary>The person's skip, refusal and stop of a node keep their words, shown under it in the terminal; a failure says its code.</summary>
    [Fact]
    public void The_terminal_says_a_graph_run_its_lines_its_nodes_and_the_persons_words_under_them()
    {
        var skipped = Status("go", NodeStates.Skipped, Self) with { By = NodeSource.Person, Words = "nobody raises it for this work" };
        var run = Derive(Records(Quest("q1")), GoAheadAndADeployAfter, Record(
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"), skipped,
            Status("handed", NodeStates.Failed, "unit-b", Tip) with { Code = "refused" }));

        var said = WorkflowRunWords.Say([run], null);

        Assert.Contains("engine · `docs` v1 workflow · quest #q1 · ask #a1\n", said);
        Assert.Contains("  Its branches are its graph's, not the landing rule's", said);
        Assert.Contains($"✓ {"The work",-16}  {"done",-19}  Reached on quest #q1.\n", said);
        Assert.Contains($"✓ {"go",-16}  {"skipped",-19}  Passed over for this work, by you.\n", said);
        Assert.Contains("“nobody raises it for this work”", said);
        Assert.Contains($"● {"The landing",-16}  {"failed",-19}  Failed: `refused`.\n", said);
        Assert.Contains("○ deployed by `acme.deploy`  not reached\n", said);
    }
}
