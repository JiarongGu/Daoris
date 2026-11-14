using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound to a graph holding an <c>integrate</c> node asks the one
/// door for its landed branch, keyed by the run's unit and the landing's tip, over a stand-in door. It reads the landing's acceptance
/// provenance (a landing whose record says neither requests nothing; an automatic landing under <c>owner</c> waits for the card), is found
/// again by its key after a restart, reconciles before any new start, is asked again when a new landing tip invalidates it, the second waiting
/// for the first's outcome, and waits queued: nothing here runs the integration. The same over real git is <see cref="IntegrateNodeGitTests"/>.
/// </summary>
public sealed class IntegrateNodeTests : IDisposable
{
    private const string RunId = "q7";
    private const string Unit = "unit-a";
    private const string Session = "daoris/s1";
    private const string Branch = "feature/q1-ship";
    private const string T1 = "1111111111111111111111111111111111111111";
    private const string T2 = "2222222222222222222222222222222222222222";

    private static readonly string RunUnit = RunUnits.IdOf(RunId);

    private static readonly DateTimeOffset Then = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-integrate-node-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ObservedPort _port = new();

    private readonly Door _door = new();

    private readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal) { [Session] = T1 };

    private IReadOnlyList<WorkUnit> _units = Run(Landed());

    /// <summary>The <c>request</c> the bound version's <c>integrate</c> asks with, read when the run is first bound.</summary>
    private string _request = LandingIntegrate.Owner;

    private bool _bound;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The landing, then the unit's <c>integrate</c> asking with <paramref name="request"/>, then the line.</summary>
    private static string Graph(string request) => $$$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"line",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"integrate"}},
                  {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}"}},
                  {"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>i","kind":"after","from":"handed","to":"integrate"},
                  {"id":"i>l","kind":"after","from":"integrate","to":"line"}]}
        """;

    private NodeRecordFiles Files => new(_home, () => true);

    private NodeRuntime Runtime(Door? door = null) => new(Files, _port, () => Then, () => true) { Doors = NodeDoors.None.With(NodeKinds.Integrate, door ?? _door), Plans = _plans };

    private ITrainPlans? _plans;

    private sealed class Plans(TrainPlan? plan) : ITrainPlans
    {
        public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => Task.FromResult(plan);
    }

    /// <summary>The run's quest as a worker's part of ask <c>a1</c>: published under <c>a1/p1/1</c>, and the plan says which quest is part <c>p1</c>'s.</summary>
    private void AsPart(string? planned)
    {
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done") with { Request = "a1/p1/1" }), RunHome.Branch(Unit, Session, Then.AddHours(-1), [RunId], Landed())];
        _plans = new Plans(new TrainPlan("a1", 1, [new TrainPlanPart("p1", new HashSet<string>(), new HashSet<string>()) { Quest = planned }]));
    }

    private NodeRecord Record(string unit = "") => Files.Read(unit.Length == 0 ? RunUnit : unit)!;

    private NodeStatus Status(string node = "integrate", string unit = "") => Record(unit).Statuses[node];

    private static DriverConfig Config(string request = LandingIntegrate.Owner) =>
        DriverConfig.Empty.WithLine("engine", "main")
            .WithLinePolicy("engine", new LandingIntegrate(Gates: request == LandingIntegrate.Done ? LandingIntegrate.Declared : null));

    private static LandedBranch Landed(string tip = T1, string by = AcceptedBy.Person, string? request = LandingIntegrate.Owner) =>
        RunHome.Landing(Unit, Branch, tip, Then, RunId) with
        {
            AcceptedBy = by, Integrate = request is null ? null : new LandedIntegrate(request, LandedIntegrate.Version),
        };

    /// <summary>Run <c>q7</c>'s quest, done, and the branch unit its session worked, carrying <paramref name="landing"/>.</summary>
    private static IReadOnlyList<WorkUnit> Run(LandedBranch landing) =>
        [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch(Unit, Session, Then.AddHours(-1), [RunId], landing)];

    private NodeRuntimeLook Look(DriverConfig config, bool whole = true)
    {
        if (!_bound)
        {
            RunHome.Bind(_home, RunId, RunHome.SaveGraph(_home, Graph(_request)), Then);
            _bound = true;
        }

        return new(config, whole)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" })),
            Tips = (_, branches, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
                branches.Where(_tips.ContainsKey).ToDictionary(branch => branch, branch => _tips[branch], StringComparer.Ordinal)),
        };
    }

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

        public List<StepAuthority?> Authorities { get; } = [];

        public Task<IntegrationAdmission> AskAsync(IntegrationAsk ask, DriverConfig config, CancellationToken ct)
        {
            if (Down) throw new IOException("the store is not there");
            Asked.Add(ask);
            Authorities.Add(StepAuthority.Current);
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

        /// <summary>The line's run ended the request, as it does: settled, handed back or dropped.</summary>
        public void Close(string id, string why)
        {
            var key = Requests.Single(each => each.Value.Id == id).Key;
            Requests[key] = Requests[key] with { ClosedAt = Then, ClosedWhy = why };
        }
    }

    /// <summary>
    /// The start asks the one door for the landed branch at the tip <c>handed</c> recorded, by the run's unit and that tip, as the node route;
    /// the attempt is kept with its key before the answer; the request waits queued, its node active, and no later pass asks again.
    /// </summary>
    [Fact]
    public async Task A_units_integrate_asks_the_door_for_its_landed_branch_by_its_key_and_waits_queued()
    {
        var runtime = Runtime();
        var pass = await PassAsync(runtime, Config());

        Assert.True(pass.Armed);
        Assert.Equal(NodeStates.Reached, Status("handed").State);
        var ask = Assert.Single(_door.Asked);
        Assert.Equal((RunUnit, T1, Branch, IntegrationRoutes.Node, IntegrationRequester.NodeStart), (ask.Unit, ask.Tip, ask.Branch, ask.Route, ask.Requester));
        Assert.Equal("checkout", ask.Root);
        Assert.Contains(pass.Performed, done => (done.Node, done.What) == ("integrate", PerformedWhat.Sent));

        var status = Status();
        Assert.Equal((NodeStates.Active, 1, 1), (status.State, status.Attempt, status.Unreconciled));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((IntegrateNodes.Sender, AttemptStates.Settled, T1, IntegrateNodes.Key(RunUnit, T1)), (attempt.Plugin, attempt.State, attempt.Call.SubjectRevision, attempt.Call.IdempotencyKey));
        Assert.Equal(new StepAuthority(RunId, "integrate", 1, attempt.Call.Invocation), Assert.Single(_door.Authorities));

        // Nothing runs it: asked once, and the line's node waits behind it, however many passes.
        await PassAsync(runtime, Config());
        await PassAsync(runtime, Config(), whole: false);
        Assert.Single(_door.Asked);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.NotEqual(NodeStates.Reached, Status("line").State);
        Assert.Empty(_port.Sent);
    }

    /// <summary>A part's quest carries its ask and part in its publish key, and the plan names that quest for the part: its request is tagged.</summary>
    [Fact]
    public async Task A_parts_integrate_names_its_ask_and_part_so_its_request_is_the_trains()
    {
        AsPart(planned: RunId);

        await PassAsync(Runtime(), Config());

        Assert.Equal(new TrainRef("a1", "p1"), Assert.Single(_door.Asked).Train);
    }

    /// <summary>A key alone names nothing: a plan that gives the part to another quest, or none read, leaves the request an ordinary landing's.</summary>
    [Theory]
    [InlineData("another-quest")]
    [InlineData(null)]
    public async Task A_request_is_tagged_only_where_the_plan_gives_the_part_to_this_quest(string? planned)
    {
        AsPart(planned);

        await PassAsync(Runtime(), Config());

        Assert.Null(Assert.Single(_door.Asked).Train);
    }

    /// <summary>A runtime nobody gave a door starts none and waits ready, as an install with no door does.</summary>
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

        // Nothing is made for the key, so the door never took it: judged again, a fresh attempt, and asked once.
        Assert.Equal(2, Status().Attempt);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Single(_door.Asked);
        Assert.Equal(["not-started", AttemptStates.Settled], [Record().Attempts[0].Outcome, Record().Attempts[0].State]);
    }

    /// <summary>
    /// A restart finds the request its key made: the door kept it and the answer is lost, so a runtime started again asks what became of the
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
    /// A new landing tip invalidates the node, its attempt kept unreconciled, so no new request is asked while the first's outcome is
    /// not in; once the first is settled, the second is asked for the new tip. The run's view shows the first's state meanwhile.
    /// </summary>
    [Fact]
    public async Task A_new_landing_tip_invalidates_the_node_and_the_second_request_waits_for_the_firsts_outcome()
    {
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        Assert.Equal(T1, Assert.Single(_door.Asked).Tip);

        var advance = new LandedAdvance(T1, T2, Then.AddHours(1), "s2")
        {
            AcceptedBy = AcceptedBy.Person, Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Version),
        };
        _tips[Session] = T2;
        _units = Run(Landed(T2) with { Advances = [advance] });
        await PassAsync(runtime, Config());

        // The first request is still open: its node is judged again on the new tip, and holds the attempt it may still own.
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
        Assert.Equal([IntegrateNodes.Key(RunUnit, T1), IntegrateNodes.Key(RunUnit, T2)], Record().Attempts.Select(attempt => attempt.Call.IdempotencyKey));
    }

    /// <summary>
    /// A wake that names no unit still judges the one owing only an <c>integrate</c> start, a node held ready (here for
    /// the owner's card), so a start the look left owing is judged at the wake, never left to the look's deadline.
    /// </summary>
    [Fact]
    public async Task A_wake_judges_a_unit_owing_only_an_integrate_start_without_waiting_for_the_look()
    {
        _units = Run(Landed(by: AcceptedBy.Auto, request: LandingIntegrate.Owner));
        var runtime = Runtime();
        await PassAsync(runtime, Config());
        Assert.Equal(NodeStates.Ready, Status().State);

        var wake = await PassAsync(runtime, Config(), whole: false);
        Assert.Equal([RunUnit], wake.Evaluated);

        _units = Run(Landed());
        await PassAsync(runtime, Config());
        Assert.Equal(NodeStates.Active, Status().State);
    }

    /// <summary>
    /// The start reads the landing's provenance under the bound version's <c>integrate</c>. A landing whose record says neither requests
    /// nothing; an automatic landing under <c>owner</c> waits for the owner's card, one under <c>steward</c> for the act; each leaves the node
    /// ready and writes nothing. An automatic landing under <c>done</c>, and a person's Accept under either, is the request.
    /// </summary>
    [Theory]
    [InlineData(AcceptedBy.Person, null, LandingIntegrate.Owner, false)]
    [InlineData(AcceptedBy.Auto, LandingIntegrate.Owner, LandingIntegrate.Owner, false)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Steward, LandingIntegrate.Owner, false)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Owner, LandingIntegrate.Owner, true)]
    [InlineData(AcceptedBy.Person, LandingIntegrate.Done, LandingIntegrate.Done, true)]
    [InlineData(AcceptedBy.Auto, LandingIntegrate.Done, LandingIntegrate.Done, true)]
    public async Task The_start_reads_the_landings_acceptance_provenance(string by, string? stood, string request, bool asks)
    {
        _request = request;
        _units = Run(Landed(by: by, request: stood));

        var pass = await PassAsync(Runtime(), Config(request));

        Assert.Equal(NodeStates.Reached, Status("handed").State);
        Assert.Equal(asks, _door.Asked.Count == 1);
        Assert.Equal(asks ? NodeStates.Active : NodeStates.Ready, Status().State);
        Assert.Equal(asks ? 1 : 0, Record().Attempts.Count);
        if (asks) Assert.Equal(request, Assert.Single(_door.Asked).Request);
        else Assert.Contains(pass.Said, line => line.Contains("its integration waits", StringComparison.Ordinal));
    }

    /// <summary>
    /// An automatic landing under <c>owner</c> waits for the owner's card and is named as one, its unit, branch and tip; once
    /// the card makes the landing's request, the node follows that request as its own, asks the door nothing more, and the wait is gone.
    /// </summary>
    [Fact]
    public async Task A_landing_waiting_for_the_owners_card_is_named_until_the_card_makes_its_request_which_the_node_then_follows()
    {
        _units = Run(Landed(by: AcceptedBy.Auto, request: LandingIntegrate.Owner));
        var runtime = Runtime();

        await PassAsync(runtime, Config());

        var wait = Assert.Single(runtime.CardWaits());
        Assert.Equal((RunUnit, "engine", Branch, T1, IntegrationRefusals.AwaitsOwner, (DateTimeOffset?)Then), (wait.Unit, wait.Repository, wait.Branch, wait.Tip, wait.Code, wait.Since));
        Assert.Equal(NodeStates.Ready, Status().State);

        // The card: the recorded-landing door keeps the request under the landing's unit and tip.
        _door.Requests[(RunUnit, T1)] = new IntegrationRequest("request-card", "engine", "default", Branch, T1, IntegrationRoutes.Recorded, "main",
            IntegrationRequester.Card, new LandingIntegrate().Resolved(), Then) { Unit = RunUnit };
        await PassAsync(runtime, Config());

        Assert.Empty(runtime.CardWaits());
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Empty(_door.Asked);
    }

    /// <summary>
    /// A landing whose record says neither is no request: it waits for the owner's card, unless the place's workflow's <c>integrate</c> says
    /// <c>request: done</c>, which leaves it to Daoris's sweep, no wait of the owner's.
    /// </summary>
    [Fact]
    public async Task A_landing_that_says_neither_waits_for_the_card_unless_the_places_workflow_leaves_it_to_the_sweep()
    {
        _units = Run(Landed(request: null));
        var runtime = Runtime();

        await PassAsync(runtime, IntegratingWorkflow.Follow(_home, Config(LandingIntegrate.Done), LandingIntegrate.Done));
        Assert.Empty(runtime.CardWaits());
        Assert.Equal(NodeStates.Ready, Status().State);

        await PassAsync(runtime, Config());
        Assert.Equal(IntegrationRefusals.NoRequest, Assert.Single(runtime.CardWaits()).Code);
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
        Directory.Delete(NodeRecordFiles.FolderOf(_home), recursive: true);
        _door.Refuses = _ => new IntegrationAdmission(false, "git could not read the branch") { Refusal = IntegrationRefusals.Unread };
        await PassAsync(Runtime(), Config());
        Assert.Equal(NodeStates.Unknown, Status().State);
    }

    /// <summary>
    /// The person's stop before promotion drops the request as <c>--drop</c> does; once the record says promoted it is refused and the node
    /// reads its record's reach, through the run's own door and the pass that applies a word said earlier.
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
        var (refused, _) = Files.Edit<bool>(RunUnit, record =>
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
