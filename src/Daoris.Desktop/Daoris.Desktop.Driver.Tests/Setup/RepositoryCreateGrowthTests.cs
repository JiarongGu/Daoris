using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The growth switch on <c>repository create</c>: the plan, which the apply, the terminal and the window's view all
/// judge by, reads the workspace's value. <c>off</c> refuses in the route's words with nothing written, in every door; <c>asked</c> and <c>mode</c>
/// are carried on the plan for the card and the pipe to read; a repository's own value governs a room alone, never a department opened in its workspace.
/// </summary>
public sealed class RepositoryCreateGrowthTests : IDisposable
{
    private static readonly CreateAuthor Fixture = new("Fixture", "fixture@example.test");

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "repository-create-growth", Guid.NewGuid().ToString("N")[..8]);
    private readonly IDisposable _chosen = FlowRepository.Use(MemoryRepositories.New().Port);

    public RepositoryCreateGrowthTests() => Directory.CreateDirectory(Work());

    public void Dispose()
    {
        _chosen.Dispose();
        GitSeed.Remove(_scratch);
    }

    private string Work(params string[] parts) => Path.Combine([_scratch, "aurora", .. parts]);

    private static CreateAsk Ask() => new("ledger", "keeps the books") { Workspace = "aurora" };

    private static CreateDoors Doors(string home, string folder, DriverConfig config) => new(new World(folder), null!, home, true, () => config, _ => { }, new DateOnly(2026, 11, 12));

    private Task<CreatePlanned> PlanAsync(DriverConfig config) => RepositoryCreatePlans.PlanAsync(Doors(Path.Combine(_scratch, "home"), Work(), config), Ask());

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    [Fact]
    public async Task A_workspace_at_mode_plans_as_it_did_and_says_so()
    {
        var plan = await PlanAsync(DriverConfig.Empty);

        Assert.True(plan.Applies, string.Join("; ", plan.Refusals.Select(each => each.Sentence)));
        Assert.Equal("mode", plan.Growth);
    }

    [Fact]
    public async Task A_workspace_set_to_asked_plans_as_it_did_and_carries_the_value_for_the_card()
    {
        var plan = await PlanAsync(Config("""{"workspaceGrowth":{"aurora":"asked"}}"""));

        Assert.True(plan.Applies, string.Join("; ", plan.Refusals.Select(each => each.Sentence)));
        Assert.Equal("asked", plan.Growth);
        Assert.Equal("asked", JsonSerializer.SerializeToElement(CreatePlanWire.Of(plan, new DateOnly(2026, 11, 12)), new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("growth").GetString());
    }

    [Fact]
    public async Task A_workspace_set_to_off_refuses_the_plan_in_the_routes_words_and_names_the_row_that_turns_it_on()
    {
        var plan = await PlanAsync(Config("""{"workspaceGrowth":{"Aurora":"off"}}"""));

        Assert.False(plan.Applies);
        var refusal = Assert.Single(plan.Refusals);
        Assert.Equal(("growth-off", "Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms)."), (refusal.Code, refusal.Sentence));
        Assert.Equal("off", plan.Growth);
    }

    [Fact]
    public async Task A_repositorys_own_value_is_no_part_of_opening_a_department_in_its_workspace()
    {
        var plan = await PlanAsync(Config("""{"growth":{"ledger":"off","atlas":"off"}}"""));

        Assert.True(plan.Applies, string.Join("; ", plan.Refusals.Select(each => each.Sentence)));
        Assert.Equal("mode", plan.Growth);
    }

    [Fact]
    public async Task An_apply_of_a_plan_whose_workspace_turned_off_since_is_refused_with_nothing_made()
    {
        var home = Path.Combine(_scratch, "home");
        var open = await PlanAsync(DriverConfig.Empty);
        var off = Config("""{"workspaceGrowth":{"aurora":"off"}}""");

        var result = await RepositoryCreates.ApplyAsync(Doors(home, Work(), off), Ask(), open.Digest);

        Assert.False(result.Done);
        Assert.Contains("Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).", result.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Work("ledger")));
    }

    [Fact]
    public async Task The_terminals_apply_refuses_an_off_workspace_in_the_same_words_and_makes_nothing()
    {
        var output = new StringWriter { NewLine = "\n" };

        var code = await RepositoryCreateCommand.ApplyAsync(Ask(), null, Doors(Path.Combine(_scratch, "home"), Work(), Config("""{"workspaceGrowth":{"aurora":"off"}}""")), output, default);

        Assert.NotEqual(0, code);
        Assert.Contains("Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).", output.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Work("ledger")));
    }

    /// <summary>Nothing but the registry's one row, its author and its line: the plan reads no service.</summary>
    private sealed class World(string folder) : ICreateWorld
    {
        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RepoView>>([new RepoView("atlas", false, Path.Combine(folder, "atlas"), "aurora")]);

        public bool LocalService => true;

        public DateTime CreatedAt(string root) => DateTime.UnixEpoch;

        public Task<string?> LineOfAsync(RepoView row, DriverConfig config, CancellationToken ct) => Task.FromResult<string?>("main");

        public Task<CreateAuthor?> AuthorOfAsync(RepoView row, CancellationToken ct) => Task.FromResult<CreateAuthor?>(Fixture);

        public Task<CreateAuthor?> GitOwnAuthorAsync(string directory, CancellationToken ct) => Task.FromResult<CreateAuthor?>(null);

        public string? RemoteStepOf(DriverConfig config, string repository, string workspace) => null;

        public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct) => Task.FromResult((true, ""));
    }
}
