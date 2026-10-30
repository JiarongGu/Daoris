using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1n part n2 (D215's note; the integration design §17.4): a unit whose graph holds an <c>integrate</c> node asks row e's one door
/// for its landed branch, keyed by the unit and the landing's tip, over a stand-in door. It reads the landing's acceptance provenance (a rule
/// gaining <c>integrate</c> over standing landings requests none; an automatic landing under <c>owner</c> waits for the card), is found again
/// by its key after a restart, reconciles before any new start, is asked again when a new landing tip invalidates it, the second waiting for
/// the first's outcome (F41), and waits queued: nothing here runs the integration. The same over real git is
/// <see cref="IntegrateNodeGitTests"/>.
/// </summary>
public sealed class IntegrateNodeTests(ITestOutputHelper output) : IDisposable
{
    private const string Unit = "unit-a";
    private const string Branch = "feature/q1-ship";
    private const string T1 = "1111111111111111111111111111111111111111";
    private const string T2 = "2222222222222222222222222222222222222222";

    private static readonly DateTimeOffset Then = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-integrate-node-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ObservedPort _port = new();

    private readonly Door _door = new();

    private IReadOnlyList<WorkUnit> _units = [BranchUnit(Landed())];

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private NodeRuntime Runtime(Door? door = null) => new(Files, _port, () => Then, () => true) { Integration = door ?? _door };

    private NodeRecord Record(string unit = Unit) => Files.Read(unit)!;

    private NodeStatus Status(string node = "integrate", string unit = Unit) => Record(unit).Statuses[node];

    private static DriverConfig Config(string request = LandingIntegrate.Owner, bool autoAccept = false) =>
        DriverConfig.Empty.WithLine("engine", "main")
            .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}") { AutoAccept = autoAccept })
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: request, Gates: request == LandingIntegrate.Done ? LandingIntegrate.Declared : null));

    private static LandedBranch Landed(string tip = T1, string by = AcceptedBy.Person, string? request = LandingIntegrate.Owner) =>
        new("engine", "default", Branch, "main", tip, "s1", "q1", null, Then)
        {
            Units = [Unit], AcceptedBy = by, Integrate = request is null ? null : new LandedIntegrate(request, LandedIntegrate.Rule),
        };

    private static WorkUnit BranchUnit(LandedBranch landing, string? revision = null, string id = Unit) => new(id, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject(Branch, revision ?? landing.Tip),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Records = new WorkUnitRecords(null, [landing], null),
    };

    private NodeRuntimeLook Look(DriverConfig config, bool whole = true) => new(config, whole)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" })),
    };

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, DriverConfig config, bool whole = true) => runtime.PassAsync(Look(config, whole), CancellationToken.None);

    /// <summary>The ask a unit's node makes, answered as the door answers: admitted into a request kept by the key it froze.</summary>
    private sealed class Door : IIntegrationDoor
    {
        public Dictionary<(string Unit, string Tip), IntegrationRequest> Requests { get; } = [];

        public List<IntegrationAsk> Asked { get; } = [];

        public int Found { get; private set; }

        public List<string> Dropped { get; } = [];

        /// <summary>Set, every call throws as a door that is not there does.</summary>
        public bool Down { get; set; }

        /// <summary>Set, the request is kept and then the answer is lost: a crash after the door's write.</summary>
        public bool LoseAnswer { get; set; }

        public HashSet<string> Promoted { get; } = [];

        public Func<IntegrationAsk, IntegrationAdmission?>? Refuses { get; set; }

        public Task<IntegrationRequest?> FindAsync(string unit, string tip, CancellationToken ct)
        {
            if (Down) throw new IOException("the store is not there");
            Found++;
            return Task.FromResult(Requests.GetValueOrDefault((unit, tip)));
        }

        public Task<IntegrationAdmission> AskAsync(IntegrationAsk ask, DriverConfig config, CancellationToken ct)
        {
            if (Down) throw new IOException("the store is not there");
            Asked.Add(ask);
            if (Refuses?.Invoke(ask) is { } refused) return Task.FromResult(refused);
            if (Requests.Values.Any(open => open.ClosedAt is null && open.Branch == ask.Branch))
            {
                return Task.FromResult(new IntegrationAdmission(false, "a request is already open") { Refusal = IntegrationRefusals.Open });
            }

            var request = new IntegrationRequest($"request-{Requests.Count + 1}", ask.Repository, "default", ask.Branch, ask.Tip!, IntegrationRoutes.Node, "main",
                ask.Requester, new LandingIntegrate().Resolved(), Then) { Unit = ask.Unit };
            Requests[(ask.Unit!, ask.Tip!)] = request;
            if (LoseAnswer) throw new IOException("the answer was lost");
            return Task.FromResult(new IntegrationAdmission(true, "requested") { Request = request });
        }

        public Task<bool> PromotedAsync(IntegrationRequest request, CancellationToken ct) => Task.FromResult(Promoted.Contains(request.Id));

        public Task<bool> DropAsync(IntegrationRequest request, CancellationToken ct)
        {
            Dropped.Add(request.Id);
            Close(request.Id, IntegrationRequestCloses.Dropped);
            return Task.FromResult(true);
        }

        /// <summary>The line's run ended the request, as row g's will: settled, handed back or dropped.</summary>
        public void Close(string id, string why)
        {
            var key = Requests.Single(each => each.Value.Id == id).Key;
            Requests[key] = Requests[key] with { ClosedAt = Then, ClosedWhy = why };
        }
    }

    /// <summary>
    /// The start asks the one door for the landed branch at the tip <c>handed</c> recorded, by the unit and that tip, as the node route; the
    /// attempt is kept with its key before the answer; the request waits queued, its node active, and no later pass asks again.
    /// </summary>
    [Fact]
    public async Task A_units_integrate_asks_the_door_for_its_landed_branch_by_its_key_and_waits_queued()
    {
        var runtime = Runtime();
        var pass = await PassAsync(runtime, Config());

        Assert.True(pass.Armed);
        Assert.Equal(NodeStates.Reached, Status("handed").State);
        var ask = Assert.Single(_door.Asked);
        Assert.Equal((Unit, T1, Branch, IntegrationRoutes.Node, IntegrationRequester.NodeStart), (ask.Unit, ask.Tip, ask.Branch, ask.Route, ask.Requester));
        Assert.Equal("checkout", ask.Root);
        Assert.Contains(pass.Performed, done => (done.Node, done.What) == ("integrate", PerformedWhat.Sent));

        var status = Status();
        Assert.Equal((NodeStates.Active, 1, 1), (status.State, status.Attempt, status.Unreconciled));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((IntegrateNodes.Sender, AttemptStates.Settled, T1, IntegrateNodes.Key(Unit, T1)), (attempt.Plugin, attempt.State, attempt.Call.SubjectRevision, attempt.Call.IdempotencyKey));

        // Nothing runs it: asked once, and the line's node waits behind it, however many passes.
        await PassAsync(runtime, Config());
        await PassAsync(runtime, Config(), whole: false);
        Assert.Single(_door.Asked);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.NotEqual(NodeStates.Reached, Status("line").State);
        Assert.Empty(_port.Sent);
    }

    /// <summary>
    /// The inventory names an unproven landed branch with no revision (only a proven end carries a tip), and a graph is judged on a revision, so
    /// the runtime reads the landing's recorded tip as the branch unit's own where its graph holds <c>integrate</c>; else the node never began.
    /// </summary>
    [Fact]
    public async Task A_landed_branch_the_inventory_names_with_no_revision_is_judged_on_its_landings_tip()
    {
        _units = [BranchUnit(Landed()) with { Subject = new WorkSubject(Branch, null) }];

        var pass = await PassAsync(Runtime(), Config());

        Assert.DoesNotContain(pass.Said, line => line.Contains("not judged", StringComparison.Ordinal));
        Assert.Equal(T1, Assert.Single(_door.Asked).Tip);
    }

    /// <summary>A runtime nobody gave a door starts none and waits ready, as an install with no row g does.</summary>
    [Fact]
    public async Task A_runtime_with_no_door_leaves_the_node_ready_and_writes_no_attempt()
    {
        await new NodeRuntime(Files, _port, () => Then, () => true).PassAsync(Look(Config()), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Status("handed").State);
        Assert.Equal(NodeStates.Ready, Status().State);
        Assert.Empty(Record().Attempts);
    }

    /// <summary>
    /// The node is read from the integration record, never copied: its request settled, it is reached with the request as its evidence; handed
    /// back or dropped, it fails with that word; a word this build does not read leaves it as it is. Each lifts the attempt's gate.
    /// </summary>
    [Theory]
    [InlineData(IntegrationRequestCloses.Settled, NodeStates.Reached, null)]
    [InlineData(IntegrationRequestCloses.HandedBack, NodeStates.Failed, IntegrationRequestCloses.HandedBack)]
    [InlineData(IntegrationRequestCloses.Dropped, NodeStates.Failed, IntegrationRequestCloses.Dropped)]
    [InlineData("superseded-by-a-newer-build", NodeStates.Active, null)]
    public async Task The_node_follows_its_requests_closing_through_the_record(string closed, string state, string? code)
    {
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        _door.Close("request-1", closed);

        await PassAsync(runtime, Config(), whole: false);

        var status = Status();
        Assert.Equal((state, code), (status.State, status.Code));
        if (state == NodeStates.Reached)
        {
            Assert.Equal((NodeSource.Integration, "integration:request-1", null), (status.By, status.Acceptance!.Evidence!.Ref, status.Unreconciled));
        }
        else if (state == NodeStates.Failed)
        {
            Assert.Null(status.Unreconciled);
        }

        Assert.Single(_door.Asked);
    }

    /// <summary>A door that is not there leaves the attempt and its node unknown, asked after at each pass and never asked to make a second.</summary>
    [Fact]
    public async Task An_unanswered_door_leaves_the_node_unknown_and_the_next_pass_asks_after_its_key()
    {
        var runtime = Runtime();
        _door.Down = true;
        var pass = await PassAsync(runtime, Config());

        Assert.Equal((NodeStates.Unknown, AttemptStates.Unknown), (Status().State, Assert.Single(Record().Attempts).State));
        Assert.Contains(pass.Performed, done => (done.Node, done.What) == ("integrate", PerformedWhat.Unknown));
        Assert.Empty(_door.Asked);

        _door.Down = false;
        await PassAsync(runtime, Config(), whole: false);

        // Nothing was made for the key, so the door never took it: judged again, a fresh attempt, and now asked once.
        Assert.Equal(2, Status().Attempt);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Single(_door.Asked);
        Assert.Equal(["not-started", AttemptStates.Settled], [Record().Attempts[0].Outcome, Record().Attempts[0].State]);
    }

    /// <summary>
    /// A restart finds the request its key made: the door kept it and the answer was lost, so a runtime started again asks what became of the
    /// key before any new start, finds it, and the node is active on the same attempt with no second request.
    /// </summary>
    [Fact]
    public async Task A_restart_finds_its_request_by_its_key_and_makes_no_second()
    {
        _door.LoseAnswer = true;
        await PassAsync(Runtime(), Config());
        Assert.Equal((NodeStates.Unknown, 1), (Status().State, Status().Attempt));
        Assert.Single(_door.Asked);

        _door.LoseAnswer = false;
        var restarted = Runtime();
        await PassAsync(restarted, Config());

        Assert.Equal((NodeStates.Active, 1, AttemptStates.Settled), (Status().State, Status().Attempt, Assert.Single(Record().Attempts).State));
        Assert.Single(_door.Asked);
        Assert.Single(_door.Requests);
    }

    /// <summary>
    /// F41: a new landing tip invalidates the node, its attempt kept unreconciled, so no new request is asked while the first's outcome is
    /// not in; once the first is settled, the second is asked for the new tip. The run's view shows the first's state meanwhile.
    /// </summary>
    [Fact]
    public async Task A_new_landing_tip_invalidates_the_node_and_the_second_request_waits_for_the_firsts_outcome()
    {
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        Assert.Equal(T1, Assert.Single(_door.Asked).Tip);

        var advance = new LandedAdvance(T1, T2, Then.AddHours(1), "s2") { AcceptedBy = AcceptedBy.Person, Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule) };
        _units = [BranchUnit(Landed(T2) with { Advances = [advance] }, T2)];
        await PassAsync(runtime, Config());

        // The first request is still open: its node was judged again on the new tip, and holds the attempt it may still own.
        var held = Status();
        Assert.NotEqual(NodeStates.Active, held.State);
        Assert.Equal(1, held.Unreconciled);
        Assert.Single(_door.Asked);
        Assert.Empty(_port.Sent);

        // Its outcome is in: the gate lifts, and the second revision is requested.
        _door.Close("request-1", IntegrationRequestCloses.Settled);
        await PassAsync(runtime, Config());

        Assert.Equal([T1, T2], _door.Asked.Select(ask => ask.Tip));
        Assert.Equal((NodeStates.Active, 2, 2), (Status().State, Status().Attempt, Status().Unreconciled));
        Assert.Equal([IntegrateNodes.Key(Unit, T1), IntegrateNodes.Key(Unit, T2)], Record().Attempts.Select(attempt => attempt.Call.IdempotencyKey));
    }

    /// <summary>
    /// INTEGRATE1n part n3: a wake that names no unit still judges the one owing only an <c>integrate</c> start, a node held ready (here for
    /// the owner's card), so a start the look left owing is judged at the wake, never left to the look's deadline.
    /// </summary>
    [Fact]
    public async Task A_wake_judges_a_unit_owing_only_an_integrate_start_without_waiting_for_the_look()
    {
        _units = [BranchUnit(Landed(by: AcceptedBy.Auto, request: LandingIntegrate.Owner))];
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        Assert.Equal(NodeStates.Ready, Status().State);

        var wake = await PassAsync(runtime, Config(), whole: false);
        Assert.Equal([Unit], wake.Evaluated);

        _units = [BranchUnit(Landed())];
        await PassAsync(runtime, Config());
        Assert.Equal(NodeStates.Active, Status().State);
    }

    /// <summary>
    /// F40: the start reads the landing's provenance. A rule gaining <c>integrate</c> over a standing landing (a record that says neither)
    /// requests none of them; an automatic landing under <c>owner</c> waits for the owner's card; each leaves the node ready and writes nothing.
    /// An automatic landing under <c>done</c>, and a person's Accept under either, is the request.
    /// </summary>
    [Theory]
    [InlineData(AcceptedBy.Person, null, LandingIntegrate.Owner, false, false)]
    [InlineData(AcceptedBy.Auto, LandingIntegrate.Owner, LandingIntegrate.Owner, false, true)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Steward, LandingIntegrate.Owner, false, false)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Owner, LandingIntegrate.Owner, true, false)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Done, LandingIntegrate.Done, true, true)]
    [InlineData(AcceptedBy.Auto, LandingIntegrate.Done, LandingIntegrate.Done, true, true)]
    public async Task The_start_reads_the_landings_acceptance_provenance(string by, string? stood, string rule, bool asks, bool autoAccept)
    {
        _units = [BranchUnit(Landed(by: by, request: stood))];

        var pass = await PassAsync(Runtime(), Config(rule, autoAccept));

        Assert.Equal(NodeStates.Reached, Status("handed").State);
        Assert.Equal(asks, _door.Asked.Count == 1);
        Assert.Equal(asks ? NodeStates.Active : NodeStates.Ready, Status().State);
        Assert.Equal(asks ? 1 : 0, Record().Attempts.Count);
        if (!asks) Assert.Contains(pass.Said, line => line.Contains("its integration waits", StringComparison.Ordinal));
    }

    /// <summary>A refused ask fails the node with the door's code and words, said once; a door that cannot read git is unknown, never failed.</summary>
    [Fact]
    public async Task A_refused_ask_fails_the_node_with_the_doors_code_and_an_unread_one_is_unknown()
    {
        _door.Refuses = _ => new IntegrationAdmission(false, "the branch moved since its landing") { Refusal = IntegrationRefusals.Moved };
        var pass = await PassAsync(Runtime(), Config());

        var status = Status();
        Assert.Equal((NodeStates.Failed, IntegrationRefusals.Moved, null), (status.State, status.Code, status.Unreconciled));
        Assert.Contains(pass.Said, line => line.Contains("(moved): the branch moved since its landing", StringComparison.Ordinal));
        Assert.Single(_door.Asked);

        // Unread: git could not say. The node is unknown and asked after again, as an unanswered door's is.
        Directory.Delete(_home, recursive: true);
        _door.Refuses = _ => new IntegrationAdmission(false, "git could not read the branch") { Refusal = IntegrationRefusals.Unread };
        await PassAsync(Runtime(), Config());
        Assert.Equal(NodeStates.Unknown, Status().State);
    }

    /// <summary>A repository whose rule carries no <c>integrate</c> arms nothing, writes no record and asks no door.</summary>
    [Fact]
    public async Task A_repository_whose_rule_carries_no_integrate_is_never_governed()
    {
        var plain = DriverConfig.Empty.WithLine("engine", "main").WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var pass = await PassAsync(Runtime(), plain);

        Assert.False(pass.Armed);
        Assert.Empty(_door.Asked);
        Assert.False(Directory.Exists(_home));
    }

    /// <summary>
    /// F51: only the integrating repository's branch units are governed; another repository's units, and its quests, cost the pass no record.
    /// </summary>
    [Fact]
    public void Only_the_integrating_repositorys_branch_units_are_governed()
    {
        var other = BranchUnit(Landed(), id: "unit-b") with { Owner = new WorkOwner("shop", "default", "session-branches.json", WorkRecord.DriverWriter) };
        var read = new NodeUnitsRead([BranchUnit(Landed()), other], new Dictionary<string, string> { ["engine"] = "checkout" });

        var governed = NodeRuntime.Govern(Config(), read, []);

        Assert.Equal([Unit], governed.Select(each => each.Unit.Id));
        Assert.Contains(governed[0].Graph.Nodes, node => node.Kind == NodeKinds.Integrate);
    }

    /// <summary>
    /// F51, measured: the arming and the governing over a thousand branch units of forty repositories, one of which carries <c>integrate</c>.
    /// Only that repository's units are governed, whatever the rest hold, and the cost is printed. The graph of a repository is judged once for
    /// all its units, so the others cost a lookup each.
    /// </summary>
    [Fact]
    public void The_arming_and_the_governing_are_measured_over_a_thousand_units_of_forty_repositories()
    {
        var config = Config();
        for (var repository = 0; repository < 40; repository++)
        {
            config = config.WithLanding($"repo-{repository}", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        }

        var units = Enumerable.Range(0, 1000).Select(index =>
        {
            var integrating = index % 40 == 0;
            var unit = BranchUnit(Landed(), id: $"unit-{index:D4}");
            return integrating ? unit : unit with { Owner = new WorkOwner($"repo-{index % 40}", "default", "session-branches.json", WorkRecord.DriverWriter) };
        }).ToList();
        var read = new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = "checkout" });

        var clock = Stopwatch.StartNew();
        var armed = Enumerable.Range(0, 1000).All(_ => NodeRuntime.Armed(config));
        var armedIn = clock.Elapsed;
        clock.Restart();
        var governed = NodeRuntime.Govern(config, read, []);
        var governedIn = clock.Elapsed;

        Assert.True(armed);
        Assert.Equal(25, governed.Count);
        Assert.All(governed, each => Assert.Equal("engine", each.Unit.Owner.Repository));
        output.WriteLine($"arming: {armedIn.TotalMilliseconds / 1000:F4} ms a call over 41 rules; governing: {governedIn.TotalMilliseconds:F1} ms for 1000 units, 25 governed");
    }

    /// <summary>
    /// A schema-1 run's landed branch (its quest bound to a steps version) is no graph run's: Current's graph governs the branch, which asks
    /// the door as any landed branch does, and the quest, run by that version, is never governed beside it.
    /// </summary>
    [Fact]
    public async Task A_schema_1_runs_landed_branch_integrates_through_Currents_graph()
    {
        var quest = new WorkUnit("quest:q1", WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject("#q1", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [], new QuestView("q1", "", "engine", "", "", "Taken")),
        };
        _units = [quest, BranchUnit(Landed())];
        var steps = new DecodedVersion(1, "wfv-steps", null, null, null);

        await Runtime().PassAsync(Look(Config()) with { Bound = (unit, _) => unit.Kind == WorkUnitKind.Quest ? steps : null }, CancellationToken.None);

        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Equal(Unit, Assert.Single(_door.Asked).Unit);
        Assert.Empty(Files.Read("quest:q1")!.Statuses);
    }

    /// <summary>
    /// The person's stop before promotion drops the request as <c>--drop</c> does; once the record says promoted it is refused and the node
    /// reads its record's reach (F50), through the run's own door and the pass that applies a word said earlier.
    /// </summary>
    [Fact]
    public void A_stop_is_refused_once_the_record_says_promoted()
    {
        var node = new NodeDeclaration("integrate", NodeKinds.Integrate, 1, NodeReach.Waited);
        var active = NodeLifecycle.Start(Unit, "integrate") with { State = NodeStates.Active, Attempt = 1 };

        Assert.Equal([NodeWordActs.Stop], RunNodeWords.Acts(node, active));
        Assert.Empty(RunNodeWords.Acts(node, active, promoted: true));
        Assert.Equal([NodeWordActs.Stop], RunNodeWords.Acts(node, active with { State = NodeStates.Invalidated }, promoted: false));

        var record = new NodeRecord("run:q7") { Pending = [new NodeWord("integrate", NodeWordActs.Stop, null, Then)] };
        var declared = new Dictionary<string, NodeDeclaration> { ["integrate"] = node };
        var scope = new NodeScope("digest", "run");
        var refused = RunNodeWords.Apply(record, declared, scope, Then, new HashSet<string> { "integrate" });
        Assert.Equal([RunNodeWords.Promoted], refused.Applied.Select(each => each.Refused));
        Assert.Empty(refused.Next!.Pending);
    }

    /// <summary>
    /// What the record says promoted is read from the door's member: an open request whose member promoted is counted, one still queued, one
    /// closed and one the door could not be asked about are not, so a stop is refused on the record and never on a guess.
    /// </summary>
    [Fact]
    public async Task The_promoted_nodes_are_those_whose_open_request_the_door_says_promoted()
    {
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        var record = Record();
        Assert.Empty(await IntegrateNodes.PromotedAsync(_door, record, CancellationToken.None));

        _door.Promoted.Add("request-1");
        Assert.Equal(["integrate"], await IntegrateNodes.PromotedAsync(_door, record, CancellationToken.None));

        _door.Close("request-1", IntegrationRequestCloses.Settled);
        Assert.Empty(await IntegrateNodes.PromotedAsync(_door, record, CancellationToken.None));

        _door.Down = true;
        Assert.Empty(await IntegrateNodes.PromotedAsync(_door, record, CancellationToken.None));
    }

    /// <summary>
    /// A stopped node whose request is still queued drops it before promotion, as the person's stop does; the record then says dropped and the
    /// attempt's gate lifts, with nothing asked of the door again for the same tip.
    /// </summary>
    [Fact]
    public async Task A_stopped_node_drops_its_queued_request_and_lifts_its_gate()
    {
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        var (refused, _) = Files.Edit<bool>(Unit, record =>
        {
            var status = record.Statuses["integrate"];
            var stopped = NodeLifecycle.Apply(new NodeDeclaration("integrate", NodeKinds.Integrate, 1, NodeReach.Waited), status, new NodeMove(NodeStates.Stopped, NodeCause.Person));
            return (WorkPerformer.Kept(record, stopped, "person-stop", Then), true);
        });
        Assert.Null(refused);
        Assert.Equal(1, Status().Unreconciled);

        await PassAsync(runtime, Config(), whole: false);

        Assert.Equal(["request-1"], _door.Dropped);
        Assert.Equal((NodeStates.Stopped, null), (Status().State, Status().Unreconciled));
        Assert.Single(_door.Asked);
    }
}
