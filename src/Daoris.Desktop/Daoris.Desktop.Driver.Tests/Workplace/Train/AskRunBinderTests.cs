using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A planned ask has its delivery run bound once per repository its plan reaches here, following the saved copy of the shipped <c>ask-delivery</c>.</summary>
public sealed class AskRunBinderTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-askrun-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, TrainPlan> _plans = new(StringComparer.Ordinal);

    private readonly DateTimeOffset _now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Plans(Dictionary<string, TrainPlan> plans) : ITrainPlans
    {
        public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => Task.FromResult(plans.GetValueOrDefault(ask));
    }

    private AskRunBinder Binder() => new(_home, WorkflowPorts.Store(_home), WorkflowPorts.Runs(_home), new Plans(_plans));

    private static TrainPlanPart Part(string id, string repository) =>
        new(id, new HashSet<string>(), new HashSet<string>()) { Repository = repository, Quest = "q" + id };

    private static AskView Ask(string id, int quests = 2) => new(id, "default", "Build it", "Open", "design") { Quests = [.. Enumerable.Range(0, quests).Select(n => $"q{n}")] };

    private static readonly IReadOnlyList<RepoView> Registry =
        [new("engine", true, "checkout-engine"), new("site", true, "checkout-site"), new("away", true, null)];

    private Task<IReadOnlyList<string>> BindAsync(AskRunBinder binder, params AskView[] asks) =>
        binder.BindAsync(DriverConfig.Empty, asks, Registry, [], _now, CancellationToken.None);

    [Fact]
    public async Task A_plan_with_parts_binds_one_run_per_repository_with_a_checkout_here()
    {
        _plans["a1"] = new TrainPlan("a1", 1, [Part("p1", "engine"), Part("p2", "engine"), Part("p3", "site"), Part("p4", "away"), Part("p5", "unknown")]);

        var said = await BindAsync(Binder(), Ask("a1"));

        Assert.Equal(2, said.Count);
        var engine = WorkflowRunBindings.Read(_home, AskRunKey.Of("a1", "engine")!)!;
        Assert.Equal("a1", engine.Ask);
        Assert.Equal("engine", engine.Repository);
        Assert.Equal(WorkflowLevels.Task, engine.Level);
        Assert.True(GraphRun.Names(engine));
        Assert.True(AskRunKey.Is(engine));
        Assert.NotNull(WorkflowRunBindings.Read(_home, AskRunKey.Of("a1", "site")!));
        Assert.False(WorkflowRunBindings.Bound(_home, AskRunKey.Of("a1", "away")!));
    }

    [Fact]
    public async Task The_run_follows_a_copy_of_the_shipped_workflow_saved_once_and_the_binding_is_made_once()
    {
        _plans["a1"] = new TrainPlan("a1", 1, [Part("p1", "engine")]);
        _plans["a2"] = new TrainPlan("a2", 1, [Part("p1", "engine")]);

        await BindAsync(Binder(), Ask("a1"), Ask("a2"));
        var again = await BindAsync(Binder(), Ask("a1"), Ask("a2"));

        Assert.Empty(again);
        var saved = WorkflowPorts.Store(_home).List().Where(file => file.Read.Versions.Any(version => version.From?.Default == AskRunBinder.Shipped)).ToList();
        Assert.Single(saved);
        Assert.Equal(saved[0].Id, WorkflowRunBindings.Read(_home, AskRunKey.Of("a1", "engine")!)!.Workflow);
        Assert.Equal(saved[0].Id, WorkflowRunBindings.Read(_home, AskRunKey.Of("a2", "engine")!)!.Workflow);
    }

    [Fact]
    public async Task An_ask_with_no_plan_or_no_part_or_no_quest_binds_nothing()
    {
        _plans["a2"] = new TrainPlan("a2", 1, []);
        _plans["a3"] = new TrainPlan("a3", 1, [Part("p1", "engine")]);

        var said = await BindAsync(Binder(), Ask("a1"), Ask("a2"), Ask("a3", quests: 0));

        Assert.Empty(said);
        Assert.False(Directory.Exists(WorkflowRunBindings.FolderOf(_home)) && Directory.GetFiles(WorkflowRunBindings.FolderOf(_home)).Length > 0);
    }

    [Fact]
    public async Task A_plan_read_later_binds_then_and_a_revision_that_reaches_a_new_repository_binds_that_one()
    {
        var binder = Binder();
        Assert.Empty(await BindAsync(binder, Ask("a1", quests: 1)));

        _plans["a1"] = new TrainPlan("a1", 1, [Part("p1", "engine")]);
        Assert.Single(await BindAsync(binder, Ask("a1", quests: 1)));

        _plans["a1"] = new TrainPlan("a1", 2, [Part("p1", "engine"), Part("p2", "site")]);
        var said = await BindAsync(binder, Ask("a1", quests: 2));

        Assert.Single(said);
        Assert.NotNull(WorkflowRunBindings.Read(_home, AskRunKey.Of("a1", "site")!));
    }

    [Fact]
    public void A_run_is_keyed_by_its_ask_and_its_repository_and_names_a_file()
    {
        var engine = AskRunKey.Of("a1", "engine");

        Assert.NotEqual(engine, AskRunKey.Of("a1", "site"));
        Assert.NotEqual(engine, AskRunKey.Of("a2", "engine"));
        Assert.Equal(engine, AskRunKey.Of("#a1", "Engine"));
        Assert.True(WorkflowRunBindings.IsRunId(engine));
        Assert.True(AskRunKey.IsBranchUnit(AskRunKey.BranchUnit(engine!)));
        Assert.False(AskRunKey.IsBranchUnit("unit-0123"));
    }
}
