using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1g, the transport wired (D215; the integration design §5.1, §5.2, §17.4), over real git and the home's own store: a unit's
/// <c>integrate</c> node requests through the one door, the transport runs the line's run off the pass and wakes it, the node reads its
/// request's outcome, and <c>line</c> observes the promotion. The verdict port is a stand-in answering green, as Review installs the real one.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationTransportGitTests : LandedFixture, IAsyncLifetime
{
    private const string Pattern = "feature/{quest}-{slug}";
    private const string Unit = "unit-a";
    private const string Declaration = """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""" + "\n";

    private readonly ObservedPort _port = new();
    private readonly List<IntegrationEvent> _told = [];
    private readonly List<string> _integrated = [];
    private readonly List<string> _said = [];
    private readonly Dictionary<string, LandedBranch> _entries = new(StringComparer.Ordinal);
    private IIntegrationVerdicts? _installed;
    private IntegrationTransport? _transport;
    private int _answered;

    public Task InitializeAsync()
    {
        try
        {
            _installed = IntegrationVerdicts.Current;
        }
        catch (InvalidOperationException)
        {
            _installed = null;
        }

        IntegrationVerdicts.Current = new Green();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_transport is not null) await _transport.DisposeAsync();
        if (_installed is not null) IntegrationVerdicts.Current = _installed;
    }

    private sealed class Green : IIntegrationVerdicts
    {
        public Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask) => Task.FromResult(new IntegrationVerdict(VerdictActions.Green) { Said = "a stand-in's green" });
    }

    private string ConfigPath => DaorisHome.Entry(Home, HomeEntry.DriverConfig);

    private DriverConfig Config => DriverConfig.Load(ConfigPath);

    private NodeRecordFiles Files => new(Home, () => true);

    private IntegrationStore Store() => new(Home, () => StoreSettled.Yes);

    private IntegrationTransport Transport(string root) => _transport ??= new IntegrationTransport(Home, new IntegrationTransportWorld
    {
        Config = () => Config,
        Root = (_, _, _) => Task.FromResult<string?>(root),
        Settled = () => StoreSettled.Yes,
        Tidy = IntegrationTidies.Through(new SessionTrees(Home)),
        Told = told =>
        {
            lock (_told) _told.Add(told);
        },
        Answered = () => Interlocked.Increment(ref _answered),
        Said = said =>
        {
            lock (_said) _said.Add(said);
        },
    });

    private NodeRuntime Runtime(string root) => new(Files, _port, () => DateTimeOffset.UtcNow, () => true)
    {
        Integration = NodeRuntime.HomeDoor(Home),
        Lines = Transport(root),
        Integrated = (unit, repository) =>
        {
            lock (_integrated) _integrated.Add($"{unit} {repository}");
        },
    };

    /// <summary>The rule under <c>request: done</c> with its gates declared and the line's declaration accepted, as the choice's door requires.</summary>
    private async Task DoneRuleAsync(string root)
    {
        await CommitAsync(root, LandedGates.File, Declaration, "the declaration");
        var blob = (await GitAsync(root, "rev-parse", $"HEAD:{LandedGates.File}")).Trim();
        DriverConfig.Empty.WithLine("engine", "main").WithLanding("engine", new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true))
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared))
            .WithIntegrationGates("engine", blob)
            .Save(ConfigPath);
    }

    /// <summary>One session's tree with one commit, landed at the quest's done (the automatic landing) or by the person's Accept.</summary>
    private static async Task<(string Branch, string Tip, TreeOpened Tree)> LandedAsync(SessionTrees trees, string root, string quest, string acceptedBy)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{quest}.txt", $"the work of {quest}\n", $"the work of {quest}");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject($"s-{quest}", quest, "Fix the gap"), acceptedBy: acceptedBy);
        Assert.True(landed.Landed, landed.Message);
        return (landed.Branch!, (await GitAsync(root, "rev-parse", landed.Branch!)).Trim(), opened);
    }

    /// <summary>
    /// The branch unit of a landed branch as the inventory gives it, its landing's record as it was last read standing (the tidy removes the
    /// branch once the line holds it); ended where the end judgement found the line holding it.
    /// </summary>
    private WorkUnit UnitOf(SessionTrees trees, string branch, string? ended = null)
    {
        var entry = _entries[branch] = trees.Recorded.Of("engine", branch) ?? _entries[branch];
        var end = ended is null
            ? new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open")
            : new MilestoneEnd(WorkEndState.Ended, EndBy.Merged, "main", ended, DateTimeOffset.UtcNow, "the line holds it");
        return new WorkUnit(Unit, WorkUnitKind.Branch, new WorkOwner("engine", "aurora", "session-branches.json", WorkRecord.DriverWriter),
            new WorkSubject(branch, entry.Tip), end, [])
        {
            Records = new WorkUnitRecords(null, [entry], null),
        };
    }

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, string root, WorkUnit unit) =>
        runtime.PassAsync(new NodeRuntimeLook(Config, true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([unit], new Dictionary<string, string> { ["engine"] = root })),
        }, CancellationToken.None);

    private NodeStatus Status(string node) => Files.Read(Unit)!.Statuses[node];

    private static async Task<bool> HoldsAsync(string root, string tip) =>
        await GitExitAsync(root, "merge-base", "--is-ancestor", tip, "main") == 0;

    private static async Task<int> GitExitAsync(string cwd, params string[] arguments)
    {
        var (code, _, _) = await WorkingTree.GitAsync(cwd, arguments, CancellationToken.None);
        return code;
    }

    private IReadOnlyList<string> Told()
    {
        lock (_told) return [.. _told.Select(told => told.Code)];
    }

    /// <summary>Until the line's run has closed every request and answered; on a timeout, what it told and said.</summary>
    private Task SettledAsync() => Poll.Until(() => Store().Open().Count == 0 && Volatile.Read(ref _answered) > 0, () =>
    {
        lock (_said) return string.Join(", ", Told()) + "\n" + string.Join("\n", _said);
    });

    /// <summary>
    /// The proof's first case (§17.4, R4): a done quest under <c>request: done</c> through Current's branch graph. Its automatic landing is the
    /// request; the node asks the door, the transport runs the line's run off the pass and wakes it, the node is reached on its settled request,
    /// the line holds the tip, the checkout was only fast-forwarded, and <c>line</c> observes it with <c>work.integrated</c> told once.
    /// </summary>
    [Fact]
    public async Task A_done_quest_under_request_done_is_integrated_through_currents_branch_graph()
    {
        var root = await RepositoryAsync("engine");
        await DoneRuleAsync(root);
        var trees = new SessionTrees(Home);
        var (branch, tip, _) = await LandedAsync(trees, root, "q1", AcceptedBy.Auto);
        var before = (await GitAsync(root, "rev-parse", "main")).Trim();
        var runtime = Runtime(root);

        await PassAsync(runtime, root, UnitOf(trees, branch));

        var request = Assert.Single(Store().Open());
        Assert.Equal((branch, tip, IntegrationRoutes.Node), (request.Branch, request.Tip, request.Route));
        await SettledAsync();

        Assert.True(await HoldsAsync(root, tip), "the line holds the frozen tip");
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "main^1")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "main")).Trim(), (await GitAsync(root, "rev-parse", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(root, "status", "--porcelain")).Trim());
        Assert.Equal(IntegrationRequestCloses.Settled, Store().Request(request.Id)!.ClosedWhy);

        await PassAsync(runtime, root, UnitOf(trees, branch));
        Assert.Equal((NodeStates.Reached, NodeSource.Integration), (Status("integrate").State, Status("integrate").By));

        // The look's end judgement finds the line holding the landing's tip (D102), which `line` observes.
        var pass = await PassAsync(runtime, root, UnitOf(trees, branch, ended: tip));
        Assert.Equal(NodeStates.Reached, Status("line").State);
        Assert.Equal([$"{Unit} engine"], _integrated);
        Assert.Contains(pass.Said, said => said.Contains("integrated into its line", StringComparison.Ordinal));

        await PassAsync(runtime, root, UnitOf(trees, branch, ended: tip));
        Assert.Single(_integrated);
        Assert.Equal(
            ["integration.queued", "integration.formed", "integration.replayed", "integration.promoted", "integration.settled", "integration.tidied", "integration.closed"],
            Told().Distinct());
    }

    /// <summary>
    /// The <i>Integrated into the line</i> preset's graph (§17.7), its <i>with no press</i> variant, saved as a version the run is bound to:
    /// <c>claimed</c>, <c>work.requests</c>, <c>handed</c> accepting automatically, <c>integrate</c> under <c>request: done</c>, <c>line</c>.
    /// </summary>
    private const string PresetGraph = """
        {"schemaVersion":2,"entries":["work"],"end":"line",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"requests","kind":"work.requests","version":1,"reach":"observed"},
                  {"id":"landing","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"automatic"}},
                  {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"done","conflicts":"session"}},
                  {"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"requests"},{"id":"b","kind":"after","from":"requests","to":"landing"},
                  {"id":"c","kind":"after","from":"landing","to":"integrate"},{"id":"d","kind":"after","from":"integrate","to":"line"}]}
        """;

    /// <summary>
    /// The proof's second case (§17.7, R4): a done quest whose run follows the preset's graph. Its landing at the done keeps the version's own
    /// step as its provenance, so the run's <c>integrate</c> asks the door on the node's route with the version's <c>request: done</c>, as the
    /// runtime's start asks it, and the transport integrates it: the line holds the tip and the request is settled.
    /// </summary>
    /// <summary>Quest q1's run bound to <paramref name="graph"/> saved as the version <c>integrated</c>, and the process its landing follows.</summary>
    private WorkflowProcess Preset(string graph)
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(Home));
        File.WriteAllText(WorkflowStore.PathOf(Home, "integrated"),
            $$"""{"id":"integrated","name":"Integrated into the line","versions":[{"version":1,"at":"2026-10-31T09:00:00Z","door":"terminal","schema":2,"graph":{{graph}}}]}""");
        var version = Assert.Single(new WorkflowFiles(Home).Load("integrated")!.Read.Versions);
        Assert.True(version.Problem is null, version.Problem);
        Assert.True(WorkflowRunBindings.Bind(Home,
            new WorkflowRunBinding("q1", "engine", "aurora", "integrated", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = version.Digest }));
        var process = WorkflowProcesses.Read(Home, Config, "engine", "aurora", "q1", new AcceptedGates(["unit"]));
        Assert.True(process is { Problem: null, Named: true }, process.Problem);
        return process;
    }

    private static LandingGate Following(WorkflowProcess process) =>
        new(new OpinionGateState(OpinionGateStates.None, "engine"),
            new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" }))
        {
            Workflow = new WorkflowGateState(WorkflowGateStates.Follows, process),
        };

    [Fact]
    public async Task A_done_quest_under_the_integrated_into_the_line_presets_graph_is_integrated()
    {
        var root = await RepositoryAsync("engine");
        await DoneRuleAsync(root);
        var gate = Following(Preset(PresetGraph));
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "q1.txt", "the work of q1\n", "the work of q1");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s-q1", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Auto, gate: gate);
        Assert.True(landed.Landed, landed.Message);
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Version), trees.Recorded.Of("engine", landed.Branch!)!.Integrate);

        var asked = await NodeRuntime.HomeDoor(Home).AskAsync(
            new IntegrationAsk("engine", "aurora", root, landed.Branch!, IntegrationRoutes.Node, IntegrationRequester.NodeStart)
            {
                Unit = "run:q1", Tip = tip, Request = LandingIntegrate.Done, Conflicts = LandingIntegrate.Session,
            }, Config, CancellationToken.None);
        Assert.True(asked.Admitted, asked.Message);
        Transport(root).Send(new IntegrationLineSend("engine", "aurora", "main"));
        await SettledAsync();

        Assert.True(await HoldsAsync(root, tip), "the line holds the frozen tip");
        Assert.Equal((IntegrationRequestCloses.Settled, LandingIntegrate.Done), (Store().Request(asked.Request!.Id)!.ClosedWhy, asked.Request.Policy.Request));
    }

    /// <summary>
    /// C5 (§4.6): no request passes a stopped <c>handed</c>. The run follows the preset's graph under <c>request: owner</c>, and the person
    /// stopped its <c>handed</c>: their Accept, which would be the request, is refused at the landing's gate, so no landing is recorded, the
    /// door has nothing to admit for the branch, and the transport runs nothing.
    /// </summary>
    [Fact]
    public async Task A_stopped_handed_refuses_its_request()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLine("engine", "main").WithLanding("engine", new LandingRule(LandingForm.Branch, Pattern))
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Owner)).Save(ConfigPath);
        var process = Preset(PresetGraph.Replace("\"automatic\"", "\"you\"", StringComparison.Ordinal).Replace("\"done\"", "\"owner\"", StringComparison.Ordinal));
        var run = RunUnits.IdOf(process.Run!);
        var stopped = new NodeRecord(run)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["landing"] = NodeLifecycle.Start(run, "landing") with { State = NodeStates.Stopped, By = NodeCause.Person } },
        };
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "q1.txt", "the work of q1\n", "the work of q1");
        var head = (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim();
        var judged = GraphGate.Judge(new GraphGateFacts(process.Run!, process.Graph!, process.Binding!.Digest!, stopped) { Commit = head });
        Assert.Equal(GraphGateStates.Stopped, judged.State);

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s-q1", "q1", "Fix the gap"), gate: Following(process) with { Graph = judged });

        Assert.False(landed.Landed, landed.Message);
        Assert.Contains("You stopped `landing`", landed.Message);
        Assert.Empty(trees.Recorded.All());
        var asked = await NodeRuntime.HomeDoor(Home).AskAsync(
            new IntegrationAsk("engine", "aurora", root, "feature/q1-fix-the-gap", IntegrationRoutes.Node, IntegrationRequester.NodeStart) { Unit = run, Tip = head },
            Config, CancellationToken.None);
        Assert.Equal(IntegrationRefusals.NoRecord, asked.Refusal);
        var run1 = await Transport(root).RunAsync(new IntegrationLineSend("engine", "aurora", "main"), CancellationToken.None);
        Assert.Equal((false, 0), (run1!.Closed, run1.Events.Count));
        Assert.Empty(Store().Open());
        Assert.False(await HoldsAsync(root, head), "nothing reached the line");
    }

    /// <summary>
    /// C11 (§4.6): under <c>owner</c>, a revision made after the request is offered, not queued. The person's Accept is the request at its frozen
    /// tip; the session commits again after it; the transport integrates the frozen tip alone, the node is reached on it with no second attempt,
    /// nothing is queued for the later commit, and the session's next landing offers it for the person's Accept.
    /// </summary>
    [Fact]
    public async Task A_revision_after_the_request_is_offered_not_queued_under_owner()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLine("engine", "main").WithLanding("engine", new LandingRule(LandingForm.Branch, Pattern))
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Owner, Gates: LandingIntegrate.NoGates)).Save(ConfigPath);
        var trees = new SessionTrees(Home);
        var (branch, tip, tree) = await LandedAsync(trees, root, "q1", AcceptedBy.Person);
        var runtime = Runtime(root);

        await PassAsync(runtime, root, UnitOf(trees, branch));
        var request = Assert.Single(Store().Open());
        Assert.Equal(tip, request.Tip);
        await CommitAsync(tree.Path, "later.txt", "a later revision\n", "a later revision");
        var later = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await SettledAsync();

        Assert.True(await HoldsAsync(root, tip), "the line holds the frozen tip");
        Assert.False(await HoldsAsync(root, later), "the later revision is not integrated");
        await PassAsync(runtime, root, UnitOf(trees, branch));
        Assert.Equal((NodeStates.Reached, 1), (Status("integrate").State, Assert.Single(Files.Read(Unit)!.Attempts).Attempt));
        Assert.Empty(Store().Open());

        // Offered: the session's tree still lands it, at the person's next Accept, and only that press makes its landing.
        var subject = new LandingSubject("s-q1", "q1", "Fix the gap");
        Assert.Null((await trees.PlanAsync(tree.Path, subject)).Problem);
        Assert.Empty(Store().Open());
        var next = await trees.LandAsync(tree.Path, subject);
        Assert.True(next.Landed, next.Message);
        Assert.Equal("a later revision", (await GitAsync(root, "log", "--format=%s", $"main..{next.Branch}")).Trim());
    }
}
