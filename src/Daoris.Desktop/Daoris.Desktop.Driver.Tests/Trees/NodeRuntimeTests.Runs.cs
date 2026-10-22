using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1c (D190 points 2 and 3; the cutover design §2, §3): a run bound to a graph version governed by the runtime on its own record,
/// <c>run:&lt;run&gt;</c>, each node observing the subject its kind names; Current kept off its quests and branches; its go-ahead begun on its own
/// answers; no gate node begun and no performed node started until the doors read the run (CUTOVER1d). Each seen failing first.
/// </summary>
public sealed partial class NodeRuntimeTests
{
    private const string RunId = "q7";

    private static readonly string RunUnit = RunUnits.IdOf(RunId);

    private const string Tip1 = "1111111111111111111111111111111111111111";

    private const string Tip2 = "2222222222222222222222222222222222222222";

    private static readonly DateTimeOffset Opened = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A plugin's observed step: the deploy its plugin reports.</summary>
    private static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    /// <summary>The least a named version's graph holds: <c>claimed</c>, then <c>handed</c>.</summary>
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    /// <summary>The plugin's observed deploy after the landing: a node a wire path leads to from <c>handed</c>.</summary>
    private const string DeployedAfterHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"deployed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"},
                  {"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>d","kind":"after","from":"handed","to":"deployed"}]}
        """;

    /// <summary>A go-ahead between the claim and the landing, naming its act.</summary>
    private const string GoAheadBeforeHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>g","kind":"after","from":"claimed","to":"go"},{"id":"g>h","kind":"after","from":"go","to":"handed"}]}
        """;

    /// <summary>The opinion and the look beside it before the landing, and the plugin's performed deploy after the claim.</summary>
    private const string GatesAndADeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"opinion","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"look","kind":"look","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"c>l","kind":"after","from":"claimed","to":"look"},
                  {"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"o>h","kind":"after","from":"opinion","to":"handed"},
                  {"id":"l>h","kind":"after","from":"look","to":"handed"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    /// <summary>Current's landing rule in the engine, whose cycle observes the plugin's deploy on each branch it governs, and its work cycle.</summary>
    private const string CurrentCycles = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
            "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } } },
          "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" }], "wires": [] } } } }
        """;

    /// <summary>Current's landing rule in two repositories.</summary>
    private const string TwoLandingCycles = """
        { "landings": {
            "engine": { "form": "merge", "cycle": { "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
              "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } },
            "other": { "form": "merge", "cycle": { "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
              "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } } } }
        """;

    /// <summary>Each branch's tip as the pass reads it, by branch.</summary>
    private readonly Dictionary<string, string> _branchTips = new(StringComparer.Ordinal);

    /// <summary>Why the units' attribution was not read, as the service's outage says it; none where it was read.</summary>
    private IReadOnlyList<string> _unread = [];

    private int _gatesRead;

    /// <summary>A look over the run's home: the units the test sets, the branches' tips, and the gates counted.</summary>
    private NodeRuntimeLook RunLook(string config = "{}", bool whole = true) => Look(config, whole) with
    {
        Units = _ =>
        {
            Interlocked.Increment(ref _unitsRead);
            return Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout", ["other"] = "elsewhere" })
            {
                Unread = _unread,
            });
        },
        Steps = () => [Step, Build, Deployed],
        Tips = (_, branches, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
            branches.Where(_branchTips.ContainsKey).ToDictionary(branch => branch, branch => _branchTips[branch], StringComparer.Ordinal)),
        Gates = (_, _, _) =>
        {
            Interlocked.Increment(ref _gatesRead);
            return Task.FromResult<(OpinionGateState?, ReviewGateState?)>((null, null));
        },
    };

    /// <summary>Run <c>q7</c> bound to v1 of a workflow holding <paramref name="graph"/>.</summary>
    private void BoundTo(string graph, string? ask = null) => RunHome.Bind(_home, RunId, RunHome.SaveGraph(_home, graph), _now, ask: ask);

    private Task<NodeRuntimePass> RunPassAsync(NodeRuntime runtime, string config = "{}", bool whole = true) =>
        runtime.PassAsync(RunLook(config, whole), CancellationToken.None);

    private NodeStatus RunStatus(string node, string unit = "") => Files.Read(unit.Length == 0 ? RunUnit : unit)!.Statuses[node];

    private bool RecordKept(string unit) => File.Exists(NodeRecordFiles.PathOf(_home, unit));

    /// <summary>
    /// A saved graph version no run binds arms nothing, and a home with no runs folder reads no binding (§3); a run bound to a steps version is
    /// D157's (§1), so it arms nothing either and no run record is written. Each binding is read once, however many looks pass.
    /// </summary>
    [Fact]
    public async Task An_unbound_graph_version_or_a_steps_run_arms_nothing_and_a_binding_is_read_once()
    {
        RunHome.SaveGraph(_home, ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId))];
        var runtime = Runtime();

        var unbound = await RunPassAsync(runtime);
        Assert.False(unbound.Armed);
        Assert.Equal((0, 0), (_unitsRead, runtime.BindingsRead));
        Assert.False(Directory.Exists(WorkflowRunBindings.FolderOf(_home)));

        RunHome.Bind(_home, RunId, RunHome.SaveSteps(_home), _now, workflow: "plain");
        foreach (var whole in new[] { true, false, true })
        {
            Assert.False((await RunPassAsync(runtime, whole: whole)).Armed);
        }

        Assert.Equal((0, 1), (_unitsRead, runtime.BindingsRead));
        Assert.False(RecordKept(RunUnit));
    }

    /// <summary>
    /// A run bound to a graph version arms the runtime though its graph names no plugin's node (§3), and is governed on its own record:
    /// <c>claimed</c> observes its quest, <c>handed</c> its session's branch at the tip read, each binding naming its subject, and neither
    /// subject's record is written. Current's cycles govern another quest and branch of the repository, and never the run's (§2.3). The run's
    /// binding is read once across the passes.
    /// </summary>
    [Fact]
    public async Task A_bound_graph_run_arms_and_moves_claimed_and_handed_on_its_own_record_its_subjects_untouched()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        // Another quest and its branch, its work ended at its tip, which the landing rule's cycle evaluates once a branch's end is proved (§1).
        IReadOnlyList<WorkUnit> Others() =>
            [RunHome.QuestUnit(RunHome.Quest("q9")), RunHome.Branch("unit-b9", "daoris/s9", Opened, ["q9"]) with { Subject = new WorkSubject("daoris/s9", Tip2) }];
        _branchTips["daoris/s1"] = Tip1;
        _branchTips["daoris/s9"] = Tip2;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId)), b1, .. Others()];
        var runtime = Runtime();

        var taken = await RunPassAsync(runtime, CurrentCycles);
        Assert.True(taken.Armed);
        Assert.Equal(["quest:q9", RunUnit, "unit-b9"], taken.Evaluated.Order(StringComparer.Ordinal));
        var claimed = RunStatus("claimed");
        Assert.Equal((NodeStates.Active, "quest:q7"), (claimed.State, claimed.Binding!.Subject));
        Assert.Equal(["quest:q9"], _port.Sent.Select(call => call.Unit));

        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1, .. Others()];
        await RunPassAsync(runtime, CurrentCycles);
        var handed = RunStatus("handed");
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("claimed").State, handed.State));
        Assert.Equal(("unit-b1", Tip1), (handed.Binding!.Subject, handed.Binding.SubjectRevision));

        var landed = b1 with { Records = b1.Records with { Landings = [RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId)] } };
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), landed, .. Others()];
        await RunPassAsync(runtime, CurrentCycles);
        handed = RunStatus("handed");
        Assert.Equal((NodeStates.Reached, "unit-b1", Tip1), (handed.State, handed.Acceptance!.Subject, handed.Acceptance.SubjectRevision));

        Assert.Equal((false, false, true, true), (RecordKept("quest:q7"), RecordKept("unit-b1"), RecordKept("quest:q9"), RecordKept("unit-b9")));
        Assert.Equal(1, runtime.BindingsRead);
    }

    /// <summary>
    /// A record Current's work cycle wrote on the run's quest before the run was bound is left as it stands (§2.3): nothing governs it now, so its
    /// attempt is not asked after or sent again, and the look says so.
    /// </summary>
    [Fact]
    public async Task A_record_Current_wrote_on_a_runs_quest_before_it_was_bound_is_left_as_it_stands_and_said()
    {
        BoundTo(ClaimedThenHanded);
        const string Quest = "quest:q7";
        var call = new WorkPerformCall(Quest, Plugin + "/deploy", 1, "i-before", "k-before", "", [], _now.AddMinutes(15));
        Files.Write(new NodeRecord(Quest)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = new(Quest, "deploy", NodeStates.Active) { Attempt = 1, Tries = 1, Unreconciled = 1 } },
            Attempts = [new NodeAttempt("deploy", 1, Plugin, call, AttemptStates.Sent) { RecordedAt = _now, Generation = 1, Deliveries = 1 }],
        });
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId))];

        var pass = await RunPassAsync(Runtime(), CurrentCycles);

        Assert.Single(pass.Said, line => line.StartsWith($"work  {Quest}:", StringComparison.Ordinal) && line.Contains("left as it stands", StringComparison.Ordinal));
        Assert.Equal([RunUnit], pass.Evaluated);
        Assert.Equal((0, 0), (_port.Asked.Count, _port.Sent.Count));
        Assert.Equal(AttemptStates.Sent, Assert.Single(Files.Read(Quest)!.Attempts).State);
    }

    /// <summary>
    /// The chain's next step here opens a session on a branch of its own at the same commit: <c>claimed</c> moves to its quest and <c>handed</c>
    /// lapses off the old branch (§2.4.4), and is begun on the new one once that step is done. A look's set-up step published after it, its
    /// session's branch newer still, is never the run's subject (§2.2).
    /// </summary>
    [Fact]
    public async Task The_chains_next_session_lapses_handed_and_a_set_up_quest_is_never_the_runs_subject()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        var b2 = RunHome.Branch("unit-b2", "daoris/s2", Opened.AddHours(1), ["q8"]);
        var b3 = RunHome.Branch("unit-b3", "daoris/s3", Opened.AddHours(2), ["q6"]);
        foreach (var branch in new[] { "daoris/s1", "daoris/s2", "daoris/s3" }) _branchTips[branch] = Tip1;
        var q7 = RunHome.Quest(RunId, "Done");
        _units = [RunHome.QuestUnit(q7), b1];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active, "unit-b1"), (RunStatus("claimed").State, RunStatus("handed").State, RunStatus("handed").Binding!.Subject));

        IReadOnlyList<WorkUnit> Grown(string status) =>
        [
            RunHome.QuestUnit(q7), RunHome.QuestUnit(RunHome.Quest("q8", status, parent: RunId)),
            RunHome.QuestUnit(RunHome.Quest("q6", "Taken", parent: "q8", setUpIn: "dev")), b1, b2, b3,
        ];

        _units = Grown("Taken");
        await RunPassAsync(runtime);
        var claimed = RunStatus("claimed");
        Assert.Equal((NodeStates.Active, "quest:q8"), (claimed.State, claimed.Binding!.Subject));
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);

        _units = Grown("Done");
        await RunPassAsync(runtime);
        var handed = RunStatus("handed");
        Assert.Equal((NodeStates.Active, "unit-b2", Tip1), (handed.State, handed.Binding!.Subject, handed.Binding.SubjectRevision));
    }

    /// <summary>
    /// A node after <c>handed</c> binds the landing's tip from its record, here a squash's commit, and its plugin is told the landed branch at
    /// that tip under the run's branch unit (§2.4.1). The session's branch tidied after its landing, its tip no longer read, leaves
    /// <c>handed</c> reached and the node after it bound where it was.
    /// </summary>
    [Fact]
    public async Task The_branch_tidied_after_its_landing_leaves_handed_reached_and_a_node_after_it_told_on_the_landings_tip()
    {
        BoundTo(DeployedAfterHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        _branchTips["daoris/s1"] = Tip1;
        var quest = RunHome.QuestUnit(RunHome.Quest(RunId, "Done"));
        _units = [quest, b1];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("handed").State);

        var landed = b1 with
        {
            Records = b1.Records with { Landings = [RunHome.Landing("unit-b1", "feature/q7", Tip2, _now, RunId, squashed: Tip1)] },
        };
        _units = [quest, landed];
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Reached, RunStatus("handed").State);
        var deployed = RunStatus("deployed");
        Assert.Equal((NodeStates.Active, "unit-b1", Tip2), (deployed.State, deployed.Binding!.Subject, deployed.Binding.SubjectRevision));
        var told = Assert.Single(_port.Told[^1][Plugin]);
        Assert.Equal(("unit-b1", "feature/q7", Tip2), (told.Unit, told.Branch, told.Tip));
        Assert.Equal(["deployed"], told.Awaiting);

        _branchTips.Remove("daoris/s1");
        _units = [quest, landed with { Records = landed.Records with { Branch = landed.Records.Branch! with { GoneAt = _now } } }];
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Reached, RunStatus("handed").State);
        deployed = RunStatus("deployed");
        Assert.Equal((NodeStates.Active, Tip2), (deployed.State, deployed.Binding!.SubjectRevision));
    }

    /// <summary>
    /// A run's go-ahead is begun on the run, though a go-ahead is pressed (§3), and is answered only by a go-ahead on the run's ask that a session or
    /// a quest of the run asked (§2.4.2): another run's yes to the same act is not taken.
    /// </summary>
    [Fact]
    public async Task Another_runs_go_ahead_is_not_taken_and_the_runs_own_answers_it()
    {
        BoundTo(GoAheadBeforeHanded, ask: "a1");
        var quest = RunHome.QuestUnit(RunHome.Quest(RunId, "Done"));
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        _branchTips["daoris/s1"] = Tip1;
        var yes = new GoAheadAnswerView(true, null, _now);
        var theirs = new GoAheadView(1, "write", "production", "deploy the release", [new GoAheadRequestView("s9", "q9", _now, "to ship theirs")]) { Answer = yes };
        WorkUnit Ask(params GoAheadView[] goAheads) =>
            Assert.Single(AskUnits.Read([new AskView("a1", "default", "Ship it.", "Published", "repository") { Quests = [RunId, "q9"], GoAheads = goAheads }]));

        _units = [quest, b1, Ask(theirs)];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        var go = RunStatus("go");
        Assert.Equal((NodeStates.Active, (string?)null), (go.State, go.Binding!.Subject));
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);

        var ours = new GoAheadView(2, "write", "production", "deploy the release", [new GoAheadRequestView("s2", RunId, _now, "to ship ours")]) { Answer = yes };
        _units = [quest, b1, Ask(theirs, ours)];
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("go").State, RunStatus("handed").State));
    }

    /// <summary>
    /// Without the service the units read are the branches alone, with no links (§2.3): no branch of a repository holding an open graph run is
    /// governed by Current, said once a pass, and the run's own nodes keep their bindings; another repository's cycle goes on.
    /// </summary>
    [Fact]
    public async Task The_service_unread_leaves_a_runs_branch_governed_by_nobody_said_and_another_repositorys_cycle_going_on()
    {
        BoundTo(ClaimedThenHanded);
        _branchTips["daoris/s1"] = Tip1;
        _branchTips["daoris/s5"] = Tip2;
        // Both branches' work ended at their tips, so a landing rule's cycle would evaluate either.
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, []) with { Subject = new WorkSubject("daoris/s1", Tip1) };
        var b5 = RunHome.Branch("unit-b5", "daoris/s5", Opened, []) with
        {
            Owner = new WorkOwner("other", "default", SessionBranches.FileName, WorkRecord.DriverWriter), Subject = new WorkSubject("daoris/s5", Tip2),
        };
        _units = [b1, b5];
        _unread = ["the service did not answer, so its quests and asks were not read: offline"];
        var runtime = Runtime();

        var pass = await RunPassAsync(runtime, TwoLandingCycles);

        Assert.Contains("unit-b5", pass.Evaluated);
        Assert.True(RecordKept("unit-b5"));
        Assert.DoesNotContain("unit-b1", pass.Evaluated);
        Assert.False(RecordKept("unit-b1"));
        Assert.Single(pass.Said, line => line.Contains("`engine`", StringComparison.Ordinal) && line.Contains("offline", StringComparison.Ordinal));
        Assert.NotEqual(NodeStates.Active, Files.Read(RunUnit)!.Statuses.GetValueOrDefault("claimed")?.State);
    }

    /// <summary>
    /// A branch whose sessions served quests of two runs, one bound to a graph version, is governed by neither: Current's cycle leaves it, the
    /// run's <c>handed</c> finds no subject there and is not begun, and the pass says so (§2.3).
    /// </summary>
    [Fact]
    public async Task A_branch_two_runs_produced_is_held_and_said()
    {
        BoundTo(ClaimedThenHanded);
        _branchTips["daoris/s1"] = Tip1;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.QuestUnit(RunHome.Quest("q9")),
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId, "q9"]) with { Subject = new WorkSubject("daoris/s1", Tip1) },
        ];
        var runtime = Runtime();

        var pass = await RunPassAsync(runtime, CurrentCycles);

        Assert.Equal((NodeStates.Reached, NodeStates.Ready), (RunStatus("claimed").State, RunStatus("handed").State));
        Assert.DoesNotContain("unit-b1", pass.Evaluated);
        Assert.False(RecordKept("unit-b1"));
        Assert.Single(pass.Said, line => line.Contains("unit-b1", StringComparison.Ordinal) && line.Contains("#q9", StringComparison.Ordinal));
    }

    /// <summary>
    /// A run ends on its record once its newest quest here names no step to come and its graph's end is settled (§3), and then arms nothing. Its
    /// chain coming back to the repository (A → B → A) arms it again: by the start's ask (<see cref="NodeRuntime.Return"/>), and at the first look
    /// of a process started again; its end lapses with <c>claimed</c>, which moves to the new quest.
    /// </summary>
    [Fact]
    public async Task An_ended_run_is_armed_again_by_its_chains_return_and_at_the_first_look_after_a_restart()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        _branchTips["daoris/s1"] = Tip1;
        var q7 = RunHome.Quest(RunId, "Done");
        _units = [RunHome.QuestUnit(q7), b1];
        var runtime = Runtime();
        Assert.False(runtime.Return(RunId));

        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Reached, RunStatus("handed").State);
        Assert.Equal(RunId, Files.Read(RunUnit)!.Ended?.Quest);
        Assert.False((await RunPassAsync(runtime)).Armed);

        // The chain went to another repository and came back here: a quest of the ended run, its start not heard yet.
        _units = [RunHome.QuestUnit(q7), RunHome.QuestUnit(RunHome.Quest("q8", "Done", parent: RunId, to: "other")), RunHome.QuestUnit(RunHome.Quest("q9", parent: "q8")), b1];
        Assert.False((await RunPassAsync(runtime)).Armed);

        // A process started again judges every bound run once at its first look.
        var again = Runtime();
        var restarted = await RunPassAsync(again);
        Assert.Contains(RunUnit, restarted.Evaluated);
        Assert.Equal((NodeStates.Active, "quest:q9"), (RunStatus("claimed").State, RunStatus("claimed").Binding!.Subject));
        Assert.Null(Files.Read(RunUnit)!.Ended);

        // The first process hears the start's ask: a wake arms it and judges the run on what is there now.
        Assert.False(runtime.Return("q8"));
        Assert.True(runtime.Return(RunId));
        var returned = await RunPassAsync(runtime, whole: false);
        Assert.True(returned.Armed);
        Assert.Contains(RunUnit, returned.Evaluated);
    }

    /// <summary>
    /// A run whose graph version stops reading (its workflow's file removed) is held whole (§3, D157 §5.2): it arms nothing by itself, its record
    /// is kept as it stands with nothing on it judged, Current still governs none of its quests, and a look armed by something else says so.
    /// </summary>
    [Fact]
    public async Task A_run_whose_version_stops_reading_is_held_whole_and_said()
    {
        BoundTo(ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId))];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        var kept = NodeRecordFiles.ToJson(Files.Read(RunUnit)!);

        File.Delete(WorkflowStore.PathOf(_home, "docs"));
        Assert.False((await RunPassAsync(runtime)).Armed);
        var held = await RunPassAsync(runtime, CurrentCycles);

        Assert.Empty(held.Evaluated);
        Assert.Single(held.Said, line => line.StartsWith($"work  {RunUnit}:", StringComparison.Ordinal) && line.Contains("held whole", StringComparison.Ordinal));
        Assert.Equal(kept, NodeRecordFiles.ToJson(Files.Read(RunUnit)!));
        Assert.Empty(_port.Sent);
    }

    /// <summary>The gates each run's branch is read at, by branch unit and tip, and what they answer (FABRIC-CUTOVER1d).</summary>
    private readonly List<(string Unit, string? Tip)> _gatesAt = [];

    private readonly Dictionary<string, (OpinionGateState? Opinion, ReviewGateState? Review)> _gatesOf = new(StringComparer.Ordinal);

    /// <summary>A look whose gates answer from <see cref="_gatesOf"/> by the tip asked, counting each read.</summary>
    private NodeRuntimeLook GatesLook() => RunLook() with
    {
        Gates = (unit, tip, _) =>
        {
            Interlocked.Increment(ref _gatesRead);
            _gatesAt.Add((unit.Id, tip));
            return Task.FromResult(tip is null ? default : _gatesOf.GetValueOrDefault(tip));
        },
    };

    /// <summary>The engine's second opinion settled, and its look reviewed, each at <paramref name="tip"/>.</summary>
    private static (OpinionGateState?, ReviewGateState?) Answered(string tip) =>
    (
        new OpinionGateState(OpinionGateStates.Settled, RunHome.Repository) { Tip = tip },
        new ReviewGateState(ReviewStates.Reviewed, new ReviewDecision(ReviewLevels.Repository, "preview") { Repository = RunHome.Repository })
        {
            Tip = tip, Verdict = new QuestReviewVerdictView(ReviewVerdicts.Reviewed),
        });

    /// <summary>
    /// FABRIC-CUTOVER1d (the cutover design §2.2, §3): the doors read a run's record, so its opinion and look are begun on its branch at the tip
    /// the pass read, their gates read for that branch at that tip (never at a tree's <c>HEAD</c>), and each reached on its gate's answer there;
    /// its plugin's performed node is started on the branch's unit at that tip, and awaited under it. A commit on the branch moves them all: the
    /// gates are read again at the new tip.
    /// </summary>
    [Fact]
    public async Task A_runs_gate_nodes_are_begun_their_gates_read_at_its_branch_tip_and_its_performed_node_started()
    {
        BoundTo(GatesAndADeploy);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();

        await runtime.PassAsync(GatesLook(), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, RunStatus("claimed").State);
        Assert.Equal((NodeStates.Active, NodeStates.Active), (RunStatus("opinion").State, RunStatus("look").State));
        Assert.Equal(("unit-b1", Tip1), (RunStatus("opinion").Binding!.Subject, RunStatus("opinion").Binding!.SubjectRevision));
        var sent = Assert.Single(_port.Sent);
        Assert.Equal(("unit-b1", Plugin + "/deploy", Tip1), (sent.Unit, sent.Step, sent.SubjectRevision));
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);
        Assert.Contains(("unit-b1", (string?)Tip1), _gatesAt);
        Assert.DoesNotContain(_gatesAt, read => read.Tip is null);

        _gatesOf[Tip1] = Answered(Tip1);
        await runtime.PassAsync(GatesLook(), CancellationToken.None);
        Assert.Equal((NodeStates.Reached, NodeStates.Reached), (RunStatus("opinion").State, RunStatus("look").State));

        // A commit on the branch: the gates are read again at the new tip, and the opinion waits on it there.
        _branchTips["daoris/s1"] = Tip2;
        _gatesAt.Clear();
        await runtime.PassAsync(GatesLook(), CancellationToken.None);
        Assert.Equal((NodeStates.Active, Tip2), (RunStatus("opinion").State, RunStatus("opinion").Binding!.SubjectRevision));
        Assert.Contains(("unit-b1", (string?)Tip2), _gatesAt);
    }

    /// <summary>
    /// FABRIC-CUTOVER1d: a run's ready performed node is owed a start at a wake as at the look, and the awaited set tells its plugin of it under
    /// the run's branch once it is started (§2.4.1).
    /// </summary>
    [Fact]
    public async Task A_runs_performed_node_is_told_under_its_branch_once_started()
    {
        BoundTo(GatesAndADeploy);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();

        await runtime.PassAsync(GatesLook(), CancellationToken.None);

        var told = Assert.Single(_port.Told[^1][Plugin]);
        Assert.Equal(("unit-b1", "daoris/s1", Tip1), (told.Unit, told.Branch, told.Tip));
        Assert.Equal(["deploy"], told.Awaiting);
    }

    /// <summary>
    /// A performed attempt a run's record keeps was sent for the subject its node observed then (§2.4.1): its reconcile names that subject's unit,
    /// never <c>run:</c>, and its completion, heard after the run's branch moved to the chain's next session, reaches the run's record, where it
    /// settles the attempt as a stale one's.
    /// </summary>
    [Fact]
    public async Task A_completion_naming_a_subject_the_run_has_left_reaches_its_record_and_its_reconcile_names_that_subject()
    {
        const string Graph = """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                      {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
            """;
        var digest = RunHome.SaveGraph(_home, Graph);
        RunHome.Bind(_home, RunId, digest, _now);
        var bound = new NodeAcceptance(RunUnit, Tip1, digest, "deploy", 1, 1) { Subject = "unit-b1" };
        var call = new WorkPerformCall("unit-b1", Plugin + "/deploy", 1, "i-left", "k-left", Tip1, [], _now.AddMinutes(15));
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["deploy"] = new(RunUnit, "deploy", NodeStates.Active) { Attempt = 1, Tries = 1, Unreconciled = 1, Binding = bound, Scope = new NodeScope(digest, RunId) },
            },
            Attempts = [new NodeAttempt("deploy", 1, Plugin, call, AttemptStates.Sent) { RecordedAt = _now, Generation = 1, Deliveries = 1 }],
        });
        _branchTips["daoris/s1"] = Tip1;
        _branchTips["daoris/s2"] = Tip2;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.QuestUnit(RunHome.Quest("q8", parent: RunId)),
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]), RunHome.Branch("unit-b2", "daoris/s2", Opened.AddHours(1), ["q8"]),
        ];
        _port.Answer = _ => new(ReconcileStates.Running);
        var runtime = Runtime();

        await RunPassAsync(runtime);
        Assert.Equal(("unit-b1", "i-left"), (Assert.Single(_port.Asked).Unit, _port.Asked[0].Invocation));
        Assert.NotEqual(NodeStates.Active, RunStatus("deploy").State);

        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached), _now, "e-left", "engine", "daoris/s1", Tip1, Invocation: "i-left")));
        var heard = await RunPassAsync(runtime, whole: false);

        Assert.Contains(heard.Performed, done => (done.Unit, done.Node, done.What) == (RunUnit, "deploy", PerformedWhat.Stale));
        var settled = Assert.Single(Files.Read(RunUnit)!.Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (settled.State, settled.Outcome));
        Assert.Null(RunStatus("deploy").Unreconciled);
        Assert.Empty(_port.Sent);
    }
}
