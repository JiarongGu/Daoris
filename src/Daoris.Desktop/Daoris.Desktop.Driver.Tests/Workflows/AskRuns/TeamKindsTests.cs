using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A planned ask's part and its delivery each follow the workflow their place maps to the kind Daoris gives them (D296): a part's run binds the
/// place's <c>team-part</c>, the ask's delivery its <c>ask-delivery</c>; where the place maps none, each binds as it did without the kind.
/// </summary>
public sealed class TeamKindsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-teamkinds-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _line;

    private readonly string _part;

    private readonly string _deliver;

    public TeamKindsTests()
    {
        var store = WorkflowPorts.Store(_home);
        _line = HomeSeed.Save(_home, store, WorkflowDefaults.Of("merge-automatic")!);
        _part = HomeSeed.Save(_home, store, WorkflowDefaults.Of("team-part")!);
        _deliver = HomeSeed.Save(_home, store, WorkflowDefaults.Of("merge-after-accept")!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary><c>engine</c> follows the line's workflow, and maps each team kind its workspace declares where <paramref name="mapped"/>.</summary>
    private DriverConfig Config(bool mapped = true)
    {
        var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowUse(_line, "engine", null, null));
        if (!mapped) return config;
        config = WorkflowSelection.Apply(config, new WorkflowDeclare(TeamKinds.Part, "default", "A part of a planned ask", []));
        config = WorkflowSelection.Apply(config, new WorkflowDeclare(TeamKinds.Delivery, "default", "An ask's delivery", []));
        config = WorkflowSelection.Apply(config, new WorkflowUse(_part, "engine", null, TeamKinds.Part));
        return WorkflowSelection.Apply(config, new WorkflowUse(_deliver, "engine", null, TeamKinds.Delivery));
    }

    /// <summary>The ask's plan gives part <c>p1</c> to quest <c>q1</c>, published under its key.</summary>
    private static readonly QuestView Part = new("q1", "ask #a1", "engine", "Build the part", "", "Open") { Request = "a1/p1/1" };

    /// <summary>A quest of the same ask that no part of its plan names.</summary>
    private static readonly QuestView Other = new("q2", "ask #a1", "engine", "Something else", "", "Open");

    private static readonly TrainPlan Plan = new("a1", 1, [new TrainPlanPart("p1", new HashSet<string>(), new HashSet<string>()) { Quest = "q1", Repository = "engine" }]);

    private sealed class World(AskView ask, TrainPlan? plan, bool answers = true) : IWorkflowStartWorld, ITrainPlans
    {
        public int PlansRead { get; private set; }

        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([Part, Other]);

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(id == ask.Id ? ask : null);

        public ITrainPlans Plans => this;

        public Task<TrainPlan?> OfAsync(string id, CancellationToken ct)
        {
            PlansRead++;
            return answers ? Task.FromResult(id == plan?.Ask ? plan : null) : throw new HttpRequestException("the service did not answer");
        }

        public void Chosen(LandingLine line)
        {
        }
    }

    private static AskView Ask(string? workflow = null) => new("a1", "default", "Build it", "Open", "design")
    {
        Quests = ["q1", "q2"],
        WorkflowChoices = workflow is null ? [] : [new AskWorkflowChoiceView(null, workflow, At)],
    };

    private Task<WorkflowBind> StartAsync(QuestView quest, IWorkflowStartWorld world, DriverConfig config) =>
        WorkflowRunStart.AtStartAsync(new WorkflowStarted(quest, "default"), world, WorkflowPorts.Store(_home), WorkflowPorts.Runs(_home), config, [], () => At, CancellationToken.None);

    [Fact]
    public async Task A_part_of_its_asks_plan_follows_the_workflow_its_place_maps_to_the_part_kind()
    {
        var bound = await StartAsync(Part, new World(Ask(), Plan), Config());

        Assert.Equal(WorkflowBindCame.Bound, bound.Came);
        Assert.Equal((_part, WorkflowLevels.RepositoryKind, TeamKinds.Part), (bound.Binding!.Workflow, bound.Binding.Level, bound.Binding.Kind));
        Assert.Equal("a1", bound.Binding.Ask);
    }

    [Fact]
    public async Task A_quest_of_the_same_ask_that_no_part_names_follows_its_places_ordinary_choice()
    {
        var bound = await StartAsync(Other, new World(Ask(), Plan), Config());

        Assert.Equal((_line, WorkflowLevels.Repository), (bound.Binding!.Workflow, bound.Binding.Level));
    }

    [Fact]
    public async Task An_asks_own_choice_stands_for_its_parts_and_reads_no_plan()
    {
        var world = new World(Ask(workflow: _deliver), Plan);

        var bound = await StartAsync(Part, world, Config());

        Assert.Equal((_deliver, WorkflowLevels.Task), (bound.Binding!.Workflow, bound.Binding.Level));
        Assert.Equal(0, world.PlansRead);
    }

    [Fact]
    public async Task A_part_in_a_place_that_maps_no_part_kind_follows_its_ordinary_choice_and_reads_no_plan()
    {
        var world = new World(Ask(), Plan);

        var bound = await StartAsync(Part, world, Config(mapped: false));

        Assert.Equal((_line, WorkflowLevels.Repository), (bound.Binding!.Workflow, bound.Binding.Level));
        Assert.Equal(0, world.PlansRead);
    }

    [Fact]
    public async Task A_plan_the_service_does_not_answer_holds_the_start_and_binds_nothing()
    {
        var held = await StartAsync(Part, new World(Ask(), Plan, answers: false), Config());

        Assert.Equal((WorkflowBindCame.Failed, true), (held.Came, held.Holds));
        Assert.False(WorkflowRunBindings.Bound(_home, "q1"));
    }

    [Fact]
    public async Task An_asks_delivery_follows_the_workflow_its_place_maps_to_the_delivery_kind_and_saves_no_shipped_copy()
    {
        var binder = new AskRunBinder(_home, WorkflowPorts.Store(_home), WorkflowPorts.Runs(_home), new World(Ask(), Plan), () => At);

        var said = await binder.BindAsync(Config(), [Ask()], [new RepoView("engine", true, "checkout-engine")], [], CancellationToken.None);

        var run = WorkflowRunBindings.Read(_home, AskRunKey.Of("a1", "engine")!)!;
        Assert.Equal((_deliver, WorkflowLevels.RepositoryKind, TeamKinds.Delivery), (run.Workflow, run.Level, run.Kind));
        Assert.Contains($"following `{_deliver}` v1", Assert.Single(said));
        Assert.DoesNotContain(WorkflowPorts.Store(_home).List(), file => file.Read.Versions.Any(version => version.From?.Default == AskRunBinder.Shipped));
    }
}
