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
        Told = told =>
        {
            lock (_told) _told.Add(told);
        },
        Answered = () => Interlocked.Increment(ref _answered),
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

    /// <summary>The branch unit of a landed branch as the inventory gives it; ended where the end judgement found the line holding it.</summary>
    private static WorkUnit UnitOf(SessionTrees trees, string branch, string? ended = null)
    {
        var entry = trees.Recorded.Of("engine", branch)!;
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
        await Poll.Until(() => Store().Open().Count == 0 && Volatile.Read(ref _answered) > 0, () => string.Join(", ", Told()));

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
}
