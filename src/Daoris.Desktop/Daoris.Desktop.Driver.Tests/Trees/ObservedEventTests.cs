using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1i (the fabric events design §C4, §E2, §E10): an observed plugin node reached by its believed event, over a stand-in port and
/// a stand-in read of the tips. The event is bound to the frame actually sent to its process and to one observed node of its unit's graph,
/// believed by the five rules with its tip read fresh, accepted on the node's binding and written before its wire fires; a replay mints
/// nothing, an older or conflicting word is late, and the person's stop or skip stands.
/// </summary>
public sealed class ObservedEventTests : IDisposable
{
    private const string Plugin = "acme.deploy";
    private const string A = "engine~feature-a";
    private const string B = "engine~feature-b";
    private const string C0 = "0000000000000000000000000000000000000000";
    private const string C1 = "1111111111111111111111111111111111111111";
    private const string C2 = "2222222222222222222222222222222222222222";

    /// <summary>The plugin's observed step, and a performed one a wire starts after it.</summary>
    private static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    private static readonly NodeDeclaration Notify = new(Plugin + "/notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    /// <summary>A repository's branches under a landing rule whose cycle observes the deploy and then notifies.</summary>
    private const string Observing = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "notify",
            "nodes": [
              { "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" },
              { "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" } ],
            "wires": [{ "id": "w1", "kind": "after", "from": "deployed", "to": "notify" }] } } } }
        """;

    /// <summary>Observed work alone: the deploy and nothing performed.</summary>
    private const string ObservedOnly = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
            "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } } } }
        """;

    /// <summary>Two observed nodes of the one step: an event names neither.</summary>
    private const string Twice = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["again"], "end": "deployed",
            "nodes": [
              { "id": "again", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" },
              { "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" } ],
            "wires": [{ "id": "w1", "kind": "after", "from": "again", "to": "deployed" }] } } } }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-observed-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ObservedPort _port = new();

    private readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal) { ["feature/a"] = C1, ["feature/b"] = C1 };

    private readonly List<(string Root, int Branches)> _tipReads = [];

    private DateTimeOffset _now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private IReadOnlyList<WorkUnit> _units = [BranchUnit(A, "feature/a")];

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private NodeRuntime Runtime() => new(Files, _port, () => _now, () => true);

    /// <summary>A branch unit as the inventory gives it: its revision the end fact's (<paramref name="revision"/>), which may lag the branch.</summary>
    private static WorkUnit BranchUnit(string id, string branch, string revision = C1) => new(id, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject(branch, revision),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    private NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" })),
        Steps = () => [Deployed, Notify],
        Tips = (root, branches, _) =>
        {
            _tipReads.Add((root, branches.Count));
            return Task.FromResult<IReadOnlyDictionary<string, string>>(branches.Where(_tips.ContainsKey).ToDictionary(branch => branch, branch => _tips[branch]));
        },
    };

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, string config = Observing, bool whole = true) =>
        runtime.PassAsync(Look(config, whole), CancellationToken.None);

    private NodeRecord Record(string unit = A) => Files.Read(unit)!;

    private WorkStepHeard Event(string state, string id, DateTimeOffset at, string commit = C1, string branch = "feature/a", string step = "deployed") =>
        new(Plugin, _port.Generation!.Value, new WorkStepFrame(step, new WorkOutcome(state, Code: state == WorkStates.Failed ? "broken" : null,
            Evidence: state == WorkStates.Reached ? new WorkEvidence(null, "https://deploy.example/" + id, "staging", "deployed") : null), at, id, "engine", branch, commit));

    /// <summary>The person's stop or skip, as their door makes it: one locked edit through the one table, on the status as it stands then.</summary>
    private string Press(string unit, string state)
    {
        var (refused, after) = Files.Edit<string>(unit, record =>
        {
            var status = WorkPerformer.StatusOf(record, "deployed");
            var move = state == NodeStates.Stopped
                ? new NodeMove(NodeStates.Stopped, NodeCause.Person)
                : new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "not this time", Scope = status.Scope };
            var moved = NodeLifecycle.Apply(Deployed with { Id = "deployed" }, status, move);
            return (moved.Moved ? WorkPerformer.Kept(record, moved, "person-" + state, _now) : null, moved.Status.State);
        });
        Assert.Null(refused);
        return after!;
    }

    [Fact]
    public async Task An_observed_node_is_begun_awaited_and_reached_by_its_believed_event_before_its_wire_fires()
    {
        var runtime = Runtime();
        await PassAsync(runtime);

        // Begun (ready to active, its start written), then published under its plugin's step, the frame carrying the host's unit.
        Assert.Equal(NodeStates.Active, Record().Statuses["deployed"].State);
        var told = Assert.Single(_port.SentTo(Plugin, 1)!.Units);
        Assert.Equal(["deployed"], told.Awaiting);
        Assert.Equal((A, C1), (told.Unit, told.Tip));
        Assert.Empty(_port.Sent);

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, step: Plugin + "/deployed"));
        var pass = await PassAsync(runtime, whole: false);

        var deployed = Record().Statuses["deployed"];
        Assert.Equal((NodeStates.Reached, NodeSource.Plugin), (deployed.State, deployed.By));
        Assert.Equal((C1, 1, deployed.Binding!.GraphDigest), (deployed.Acceptance!.SubjectRevision, deployed.Acceptance.Attempt, deployed.Acceptance.GraphDigest));
        var kept = Assert.Single(Record().Observations);
        Assert.Equal((Plugin, "deployed", C1, "e1", WorkStates.Reached, "deployed"), (kept.Plugin, kept.Step, kept.Commit, kept.Id, kept.State, kept.Node));
        Assert.Contains(pass.Performed, done => (done.Node, done.What, done.Code) == ("deployed", PerformedWhat.Applied, WorkStates.Reached));

        // Its wire fired once the acceptance was written: the notify sent on the same revision, and the tips read once a pass (FABRIC-PERFORM1m:
        // the first pass's to bind the node, the second's for the event's belief and the binding alike).
        Assert.Equal(C1, Assert.Single(_port.Sent).SubjectRevision);
        Assert.Equal([("checkout", 1), ("checkout", 1)], _tipReads);
        Assert.Empty(_port.Refusals);
    }

    [Fact]
    public async Task A_unit_desired_since_whose_watch_send_failed_is_not_watched_and_its_event_moves_nothing()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        _units = [BranchUnit(A, "feature/a"), BranchUnit(B, "feature/b")];
        _port.Fails = units => units.Any(unit => unit.Branch == "feature/b");
        await PassAsync(runtime);

        // Desired and begun, but the process was never told of it: the frame it was last sent names the first unit alone.
        Assert.Equal(NodeStates.Active, Record(B).Statuses["deployed"].State);
        Assert.Equal(2, _port.Told[^1][Plugin].Count);
        Assert.Equal(["feature/a"], _port.SentTo(Plugin, 1)!.Units.Select(unit => unit.Branch));
        var before = NodeRecordFiles.ToJson(Record(B));

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, branch: "feature/b"));
        var pass = await PassAsync(runtime, whole: false);

        Assert.Contains(pass.Performed, done => (done.What, done.Code) == (PerformedWhat.NotTaken, WorkRefusals.NotWatched));
        Assert.Equal((Plugin, WorkRefusals.NotWatched), Assert.Single(_port.Refusals));
        Assert.Equal(before, NodeRecordFiles.ToJson(Record(B)));
        Assert.Empty(_port.Sent);
    }

    [Fact]
    public async Task Another_units_event_from_the_current_process_is_a_counted_refusal_and_no_record_changes()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        var before = NodeRecordFiles.ToJson(Record());

        // A real repository, a real branch at its tip and the authorised plugin's current process: still no unit that process was told of.
        _tips["feature/z"] = C1;
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, branch: "feature/z"));
        await PassAsync(runtime, whole: false);

        Assert.Equal((Plugin, WorkRefusals.NotWatched), Assert.Single(_port.Refusals));
        Assert.Equal(before, NodeRecordFiles.ToJson(Record()));
        Assert.Single(Files.All().Records);
        Assert.Empty(_port.Sent);
    }

    [Fact]
    public async Task A_ref_moved_after_publication_leaves_the_event_stale_and_an_unread_tip_establishes_nothing()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        Assert.Equal(C1, Assert.Single(_port.SentTo(Plugin, 1)!.Units).Tip);

        // The branch moved after the frame was told and before the event was heard; the wake judges with the unit cached at C1. The new tip's
        // event, heard in the same pass, reaches no node bound to the old one either: the node rebinds after the hearing (FABRIC-PERFORM1m).
        _tips["feature/a"] = C2;
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e2", _now, commit: C2));
        await PassAsync(runtime, whole: false);

        // A tip not read establishes nothing.
        _tips.Remove("feature/a");
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e3", _now));
        await PassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Active, Record().Statuses["deployed"].State);
        Assert.Empty(Record().Observations);
        Assert.Empty(_port.Sent);
        Assert.Equal([WorkRefusals.Stale, WorkRefusals.Stale, WorkRefusals.Stale], _port.Refusals.Select(refusal => refusal.Code));
    }

    [Fact]
    public async Task A_lagging_units_observed_node_rebinds_to_the_tip_read_at_the_next_pass_and_is_reached_there()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        Assert.Equal(C1, Record().Statuses["deployed"].Binding!.SubjectRevision);

        // FABRIC-PERFORM1m: the branch moved and the unit's revision, the end fact's, still lags at C1. The next pass, a wake, reads the tip and
        // rebinds the node to it: the old binding lapses under RUN1's rules, and the person is told nothing, no work is sent, nothing kept.
        _tips["feature/a"] = C2;
        var rebound = await PassAsync(runtime, whole: false);
        var deployed = Record().Statuses["deployed"];
        Assert.Equal((NodeStates.Active, C2), (deployed.State, deployed.Binding!.SubjectRevision));
        Assert.Equal([A], rebound.Evaluated);
        Assert.Empty(rebound.Said);
        Assert.False(rebound.AwaitedChanged);
        Assert.Empty(_port.Sent);
        Assert.Empty(Record().Observations);

        // The new tip's event is reached on that binding, and its wire fires once.
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, commit: C2));
        await PassAsync(runtime, whole: false);
        var reached = Record();
        Assert.Equal((NodeStates.Reached, C2), (reached.Statuses["deployed"].State, reached.Statuses["deployed"].Acceptance!.SubjectRevision));
        Assert.Equal(C2, Assert.Single(reached.Observations).Commit);
        Assert.Empty(_port.Refusals);

        // A performed node is bound as before, to the unit's revision: only the observed node reads the tip.
        Assert.Equal(C1, Assert.Single(_port.Sent).SubjectRevision);

        // The end fact catching up reaches nothing again: the observed node keeps its acceptance and its event, its wire fires no more.
        _units = [BranchUnit(A, "feature/a", C2)];
        await PassAsync(runtime);
        Assert.Equal(NodeRecordFiles.StatusText(reached.Statuses["deployed"]), NodeRecordFiles.StatusText(Record().Statuses["deployed"]));
        Assert.Equal(reached.Observations, Record().Observations);
        Assert.Equal(reached.Fired, Record().Fired);

        // One read of the checkout's tips a pass, whatever binds or hears.
        Assert.Equal([("checkout", 1), ("checkout", 1), ("checkout", 1), ("checkout", 1)], _tipReads);
    }

    [Fact]
    public async Task An_event_at_the_old_tip_after_the_rebind_is_stale_and_moves_nothing()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        _tips["feature/a"] = C2;
        await PassAsync(runtime, whole: false);
        Assert.Equal(C2, Record().Statuses["deployed"].Binding!.SubjectRevision);
        var before = NodeRecordFiles.ToJson(Record());

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, commit: C1));
        var pass = await PassAsync(runtime, whole: false);

        Assert.Contains(pass.Performed, done => (done.Node, done.What, done.Code) == ("deployed", PerformedWhat.NotTaken, WorkRefusals.Stale));
        Assert.Equal((Plugin, WorkRefusals.Stale), Assert.Single(_port.Refusals));
        Assert.Equal(before, NodeRecordFiles.ToJson(Record()));
        Assert.Empty(_port.Sent);
    }

    [Fact]
    public async Task An_unread_tip_binds_nothing_new_and_keeps_the_node_as_it_stands()
    {
        _units = [BranchUnit(A, "feature/a"), BranchUnit(B, "feature/b")];
        _tips.Remove("feature/b");
        var runtime = Runtime();
        await PassAsync(runtime);

        // FABRIC-PERFORM1m: B's tip was not read, so its node is left ready, bound to nothing on a guess, and never published.
        Assert.Equal(NodeStates.Active, Record().Statuses["deployed"].State);
        Assert.Equal(NodeStates.Ready, Record(B).Statuses["deployed"].State);
        Assert.Equal(["feature/a"], _port.SentTo(Plugin, 1)!.Units.Select(unit => unit.Branch));

        // A rebound to its moved tip, then that tip unread at the look's deadline: its node stays as it stands, never lapsed back to the
        // unit's lagging revision, and its record is not written.
        _tips["feature/a"] = C2;
        await PassAsync(runtime, whole: false);
        var before = NodeRecordFiles.ToJson(Record());
        Assert.Equal(C2, Record().Statuses["deployed"].Binding!.SubjectRevision);
        _tips.Remove("feature/a");
        var unread = await PassAsync(runtime);
        Assert.Equal(before, NodeRecordFiles.ToJson(Record()));
        Assert.Empty(unread.Said);
        Assert.Empty(_port.Sent);

        // B's tip read: its node is begun on it at the next pass.
        _tips["feature/b"] = C2;
        await PassAsync(runtime, whole: false);
        var b = Record(B).Statuses["deployed"];
        Assert.Equal((NodeStates.Active, C2), (b.State, b.Binding!.SubjectRevision));
    }

    [Fact]
    public async Task An_older_or_tied_event_for_a_step_and_commit_is_late_and_another_commits_is_ordered_apart_yet_stale()
    {
        var runtime = Runtime();
        await PassAsync(runtime);

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e2", _now));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e3", _now));
        _port.Heard.Enqueue(Event(WorkStates.Failed, "e1", _now - TimeSpan.FromMinutes(5)));
        // Another commit's older event is not late behind this one's: its order is its own, and the fourth rule still judges it.
        _port.Heard.Enqueue(Event(WorkStates.Failed, "e0", _now - TimeSpan.FromMinutes(10), commit: C0));
        await PassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Reached, Record().Statuses["deployed"].State);
        Assert.Equal([WorkRefusals.Late, WorkRefusals.Late, WorkRefusals.Stale], _port.Refusals.Select(refusal => refusal.Code));
        Assert.Equal("e2", Assert.Single(Record().Observations).Id);
        Assert.Single(_port.Sent);
    }

    [Fact]
    public async Task An_event_before_the_watch_moves_nothing_and_the_same_fact_replayed_after_a_resync_reaches_despite_its_time()
    {
        var runtime = Runtime();
        var old = _now - TimeSpan.FromHours(2);
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", old));
        await PassAsync(runtime);

        // Heard before anything was published: not watched, and nothing kept that would make its replay a duplicate.
        Assert.Equal(NodeStates.Active, Record().Statuses["deployed"].State);
        Assert.Equal((Plugin, WorkRefusals.NotWatched), Assert.Single(_port.Refusals));
        Assert.Empty(Record().Observations);

        // The process restarted and was told the set again: the plugin sends what it knows, the same fact at its own time.
        _port.Restart();
        await PassAsync(runtime, whole: false);
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", old));
        await PassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Reached, Record().Statuses["deployed"].State);
        Assert.Single(_port.Sent);
    }

    [Fact]
    public async Task Two_events_for_one_node_neither_overwrite_its_evidence_nor_fire_its_wire_again_across_restarts()
    {
        var runtime = Runtime();
        await PassAsync(runtime);
        var at = _now;
        _port.Heard.Enqueue(Event(WorkStates.Unknown, "e0", at - TimeSpan.FromMinutes(1)));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", at));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", at));
        // Its id reused for another outcome; a conflicting outcome, newer; the same outcome again under another id.
        _port.Heard.Enqueue(Event(WorkStates.Failed, "e1", at));
        _port.Heard.Enqueue(Event(WorkStates.Failed, "e9", at + TimeSpan.FromSeconds(30)));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e8", at + TimeSpan.FromSeconds(40)));
        var pass = await PassAsync(runtime, whole: false);

        Assert.Equal([PerformedWhat.Noted, PerformedWhat.Applied, PerformedWhat.Replay, PerformedWhat.NotTaken, PerformedWhat.NotTaken, PerformedWhat.Replay],
            pass.Performed.Take(6).Select(done => done.What));
        Assert.Equal([WorkRefusals.Late, WorkRefusals.Late], _port.Refusals.Select(refusal => refusal.Code));
        var reached = Record();
        Assert.Equal(NodeStates.Reached, reached.Statuses["deployed"].State);
        Assert.Equal("e1", Assert.Single(reached.Observations).Id);
        Assert.Single(_port.Sent);

        // Replayed across a plugin restart and a loop restart: nothing minted, no trigger fired again, the evidence as it was.
        _port.Restart();
        var again = Runtime();
        await PassAsync(again);
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", at));
        _port.Heard.Enqueue(Event(WorkStates.Failed, "e7", at + TimeSpan.FromMinutes(1)));
        await PassAsync(again, whole: false);

        Assert.Equal(NodeRecordFiles.StatusText(reached.Statuses["deployed"]), NodeRecordFiles.StatusText(Record().Statuses["deployed"]));
        Assert.Equal(reached.Fired, Record().Fired);
        Assert.Equal(reached.Observations, Record().Observations);
        Assert.Single(_port.Sent);
    }

    [Theory]
    [InlineData(NodeStates.Stopped)]
    [InlineData(NodeStates.Skipped)]
    public async Task The_persons_stop_or_skip_stands_against_evidence_in_either_order(string press)
    {
        _units = [BranchUnit(A, "feature/a"), BranchUnit(B, "feature/b")];
        var runtime = Runtime();
        await PassAsync(runtime);

        // The press first: the event finds no node awaiting it, and evidence never brings the node back. (A skip satisfies its `after` wire,
        // §E2, so the notify a skip starts is the person's, made before the event is heard.)
        Assert.Equal(press, Press(A, press));
        await PassAsync(runtime);
        var pressed = _port.Sent.Count(call => call.Unit == A);
        Assert.Equal(press == NodeStates.Skipped ? 1 : 0, pressed);
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now));
        await PassAsync(runtime, whole: false);
        Assert.Equal(press, Record().Statuses["deployed"].State);
        Assert.Empty(Record().Observations);
        Assert.Equal((Plugin, WorkRefusals.NotWatched), Assert.Single(_port.Refusals));
        Assert.Equal(pressed, _port.Sent.Count(call => call.Unit == A));

        // The event first: the press is judged on the status as it then stands (a reached node is neither stopped nor skipped), and a replay
        // after it moves nothing more.
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e2", _now, branch: "feature/b"));
        await PassAsync(runtime, whole: false);
        var after = Press(B, press);
        var sent = _port.Sent.Count;
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e2", _now, branch: "feature/b"));
        await PassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Reached, after);
        Assert.Equal(after, Record(B).Statuses["deployed"].State);
        Assert.Equal(sent, _port.Sent.Count);
        Assert.Single(_port.Sent, call => call.Unit == B);
    }

    [Fact]
    public async Task Observed_only_work_arms_the_runtime_and_a_lease_lost_or_a_disarm_tells_and_writes_nothing()
    {
        Assert.True(NodeRuntime.Armed(DriverConfig.Parse(ObservedOnly)));
        Assert.False(NodeRuntime.Armed(DriverConfig.Parse("{}")));

        // Without the lease: nothing told, written or started.
        var unheld = new NodeRuntime(Files, _port, () => _now, () => false);
        var lost = await unheld.PassAsync(Look(ObservedOnly), CancellationToken.None);
        Assert.Empty(lost.Performed);
        Assert.Empty(_port.Told);
        Assert.False(Directory.Exists(NodeRecordFiles.FolderOf(_home)));

        // Armed by observed work alone: begun, then published.
        var runtime = Runtime();
        await PassAsync(runtime, ObservedOnly);
        Assert.Equal(NodeStates.Active, Record().Statuses["deployed"].State);
        Assert.Equal(["deployed"], Assert.Single(_port.Told[^1][Plugin]).Awaiting);

        // Disarmed before the plan's supervision: the set withdrawn, and the pass after reads, tells and writes nothing.
        Assert.True(await runtime.WithdrawAsync(DriverConfig.Parse("{}"), CancellationToken.None));
        Assert.Empty(_port.Told[^1]);
        var told = _port.Told.Count;
        var read = _tipReads.Count;
        var idle = await PassAsync(runtime, "{}");
        Assert.False(idle.Armed);
        Assert.Equal(told, _port.Told.Count);
        Assert.Equal(read, _tipReads.Count);
    }

    [Fact]
    public async Task A_graph_with_two_observed_nodes_of_the_events_step_takes_neither()
    {
        var runtime = Runtime();
        await PassAsync(runtime, Twice);
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now));
        await PassAsync(runtime, Twice, whole: false);

        Assert.Equal((Plugin, WorkRefusals.NotNamed), Assert.Single(_port.Refusals));
        Assert.DoesNotContain(Record().Statuses.Values, status => status.State == NodeStates.Reached);
    }

    [Fact]
    public void A_records_events_read_back_and_a_record_written_before_them_reads_with_none()
    {
        var record = new NodeRecord(A) { Observations = [new ObservedEvent(Plugin, "repository-id", "deployed", C1, "e1", WorkStates.Reached, _now, "deployed")] };
        Assert.Equal(record.Observations, NodeRecordFiles.Parse(NodeRecordFiles.ToJson(record))!.Observations);

        var older = NodeRecordFiles.ToJson(new NodeRecord(A));
        Assert.DoesNotContain("observations", older, StringComparison.Ordinal);
        Assert.Empty(NodeRecordFiles.Parse(older)!.Observations);
    }
}
