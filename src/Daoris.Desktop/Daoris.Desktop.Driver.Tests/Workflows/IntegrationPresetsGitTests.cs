using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1q's second proof (the integration design §17.7, §18): the <i>Integrated into the line</i> preset as <c>workflow new --from</c>
/// saves it, bound to a done quest's run, taken through the runtime's own pass over real git and the home's own store. The landing is the
/// request, the transport integrates the branch into the line, and the run's <c>integrate</c> and <c>line</c> nodes are reached.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationPresetsGitTests : LandedFixture, IAsyncLifetime
{
    private const string Pattern = "feature/{quest}-{slug}";
    private const string Declaration = """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""" + "\n";

    private readonly ObservedPort _port = new();
    private readonly List<string> _said = [];
    private IntegrationTransport? _transport;
    private int _answered;

    public IntegrationPresetsGitTests() => IntegrationVerdicts.Within = new Green();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_transport is not null) await _transport.DisposeAsync();
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
        Told = _ => { },
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
    };

    /// <summary>
    /// The preset's graph saved into the home, then read back as the version the run binds; the terminal's own verb saving the preset is held by
    /// <see cref="WorkflowGraphPresetsTests"/>.
    /// </summary>
    private Task<WorkflowProcess> SavedPresetAsync(string preset)
    {
        var graph = WorkflowPresets.GraphOf(preset)!.GraphJson;
        StoredSteps.AddGraph(WorkflowPorts.Store(Home), "made", "Integrated", "2026-10-31T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(graph)!);
        var version = Assert.Single(new WorkflowFiles(Home).Load("made")!.Read.Versions);
        Assert.True(version.Problem is null, version.Problem);
        Assert.True(WorkflowRunBindings.Bind(Home,
            new WorkflowRunBinding("q1", "engine", "aurora", "made", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = version.Digest }));
        var process = WorkflowProcesses.Read(Home, Config, "engine", "aurora", "q1", new AcceptedGates(["unit"]));
        Assert.True(process is { Problem: null, Named: true }, process.Problem);
        return Task.FromResult(process);
    }

    private static LandingGate Following(WorkflowProcess process) =>
        new(new OpinionGateState(OpinionGateStates.None, "engine"),
            new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" }))
        {
            Workflow = new WorkflowGateState(WorkflowGateStates.Follows, process),
        };

    private static async Task<bool> HoldsAsync(string root, string tip)
    {
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, "main"], CancellationToken.None);
        return code == 0;
    }

    [Fact]
    public async Task A_done_quest_is_integrated_into_the_line_by_the_saved_preset_run_end_to_end()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, LandedGates.File, Declaration, "the declaration");
        var blob = (await GitAsync(root, "rev-parse", $"HEAD:{LandedGates.File}")).Trim();
        DriverConfig.Empty.WithLine("engine", "main")
            .WithLinePolicy("engine", new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared))
            .WithIntegrationGates("engine", blob)
            .Save(ConfigPath);
        var process = await SavedPresetAsync("integrated-no-press");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "q1.txt", "the work of q1\n", "the work of q1");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s-q1", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Auto, gate: Following(process));
        Assert.True(landed.Landed, landed.Message);
        var branch = landed.Branch!;
        var tip = (await GitAsync(root, "rev-parse", branch)).Trim();

        var quest = new WorkUnit("quest:q1", WorkUnitKind.Quest, new WorkOwner("engine", "aurora", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject("#q1", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [], new QuestView("q1", "", "engine", "Fix the gap", "", "Done")),
        };
        var recorded = trees.Recorded.Of("engine", branch)!;
        WorkUnit Branch(string? ended = null)
        {
            // The tidy removes the branch once the line holds it, so the record is the one last read standing.
            var entry = recorded = trees.Recorded.Of("engine", branch) ?? recorded;
            var end = ended is null
                ? new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open")
                : new MilestoneEnd(WorkEndState.Ended, EndBy.Merged, "main", ended, DateTimeOffset.UtcNow, "the line holds it");
            return new WorkUnit("branch-a", WorkUnitKind.Branch, new WorkOwner("engine", "aurora", "session-branches.json", WorkRecord.DriverWriter),
                new WorkSubject(branch, entry.Tip), end, [])
            {
                Links = [new WorkLink(WorkRelation.ProducedBy, "quest:q1")],
                Records = new WorkUnitRecords(null, [entry], null),
            };
        }

        var runtime = Runtime(root);
        Task<NodeRuntimePass> Pass(WorkUnit unit) => runtime.PassAsync(new NodeRuntimeLook(Config, true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([quest, unit], new Dictionary<string, string> { ["engine"] = root })),
            Bound = NodeRuntime.BoundIn(Home),
            Tips = NodeTips.ReadAsync,
        }, CancellationToken.None);

        var first = await Pass(Branch());
        var seen = Files.Read(RunUnits.IdOf("q1"));
        Assert.True(Store().Open().Count == 1, string.Join("\n", first.Said) + "\n" + string.Join(",", seen?.Statuses.Select(s => s.Key + "=" + s.Value.State) ?? ["no record"]));
        var request = Assert.Single(Store().Open());
        Assert.Equal((branch, tip, IntegrationRoutes.Node, RunUnits.IdOf("q1")), (request.Branch, request.Tip, request.Route, request.Unit));
        await Poll.Until(() => Store().Open().Count == 0 && Volatile.Read(ref _answered) > 0, () => string.Join("\n", _said));

        Assert.True(await HoldsAsync(root, tip), "the line holds the frozen tip");
        await Pass(Branch());
        var run = Files.Read(RunUnits.IdOf("q1"))!;
        Assert.Equal(NodeStates.Reached, run.Statuses["integrate"].State);
        await Pass(Branch(ended: tip));
        Assert.Equal(NodeStates.Reached, Files.Read(RunUnits.IdOf("q1"))!.Statuses["line"].State);
    }
}
