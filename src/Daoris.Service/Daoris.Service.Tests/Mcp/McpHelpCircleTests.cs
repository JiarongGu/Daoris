using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// ASKSCOPE1c (D158's ASKSCOPE1 note point 5): an Ask Daoris conversation in a workspace's room is handed a connector bound to
/// that workspace by its record, never by its arguments. Its search, its reads, the registry and the quests answer from that
/// workspace alone, and a call naming another workspace, or `all`, is refused and says so. A connector bound to none, as
/// <c>default</c>'s room's is, answers as before.
/// </summary>
public sealed class McpHelpCircleTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    private SqliteConnection _connection = null!;
    private KnowledgeService _service = null!;
    private QuestStore _quests = null!;

    private sealed class Entries(params KnowledgeEntry[] entries) : IKnowledgeSource
    {
        public string Name => "fixture";

        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }

    private static KnowledgeEntry Decision(string repository) => new(
        repository, EntryKind.Decision, Provenance.Local, "D1 — chunk hydration", "Cap hydration work per frame.", "docs/DECISIONS.md", "D1");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        // Two workspaces on one machine, each with the same lesson, so a boundary that leaks shows in every answer.
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new Entries(Decision("engine"), Decision("ledger")),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        foreach (var (name, workspace) in new[] { ("engine", "alpha"), ("ledger", "beta") })
        {
            await _service.RegisterAsync(
                new Registration(name, Adopted: true, $"{name}.", ["x"], ["y"], [], Entries: 0, Workspace: workspace), Now);
        }

        await _service.RefreshAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>A connector in Ask Daoris's room, which is no repository, bound to <paramref name="bound"/> or to none.</summary>
    private WorkspaceScope Circle(string? bound) =>
        new(_service, new AmbientWorkspace(Path.Combine(Path.GetTempPath(), "daoris-help-alpha")) { Bound = bound });

    private KnowledgeSearchTools Search(string? bound) => new(_service, Circle(bound));

    private QuestTools Quests(string? bound) => new(_quests, new QuestExchange(_service, _quests), Circle(bound));

    [Fact]
    public async Task A_bound_conversation_s_search_answers_from_its_workspace_and_refuses_another_or_all()
    {
        var tools = Search("alpha");

        var own = await tools.SearchAsync("hydration");
        Assert.Contains("1 result(s) for \"hydration\" in workspace `alpha`", own);
        Assert.Contains("`engine`", own);
        Assert.DoesNotContain("`ledger`", own);
        Assert.Contains("1 result(s)", await tools.SearchAsync("hydration", workspace: " ALPHA "));

        foreach (var other in new[] { "beta", "all", "ALL" })
        {
            var refused = await tools.SearchAsync("hydration", workspace: other);
            Assert.StartsWith("Refused: this conversation is workspace `alpha`'s", refused);
            Assert.DoesNotContain("`ledger`", refused);
        }

        Assert.Contains(
            "never from every workspace", await tools.SearchAsync("hydration", workspace: "all"));
        Assert.Contains("never from workspace `beta`", await tools.SearchAsync("hydration", workspace: "beta"));
    }

    [Fact]
    public async Task A_bound_conversation_reads_its_own_workspace_s_entries_and_refuses_another_s()
    {
        var tools = Search("alpha");

        Assert.Contains("Cap hydration work per frame.", await tools.GetAsync("engine:docs/DECISIONS.md#D1"));

        var refused = await tools.GetAsync("ledger:docs/DECISIONS.md#D1");
        Assert.StartsWith("Refused: `ledger:docs/DECISIONS.md#D1` is not workspace `alpha`'s", refused);
        Assert.DoesNotContain("Cap hydration work per frame.", refused);
    }

    [Fact]
    public async Task A_bound_conversation_s_registry_and_repositories_are_its_workspace_s()
    {
        var tools = Search("alpha");

        var registry = await tools.RegistryAsync();
        Assert.Contains("The family in workspace `alpha`", registry);
        Assert.Contains("`engine`", registry);
        Assert.DoesNotContain("ledger", registry);
        Assert.StartsWith("Refused:", await tools.RegistryAsync("beta"));
        Assert.StartsWith("Refused:", await tools.RegistryAsync("all"));

        var repositories = await tools.RepositoriesAsync();
        Assert.Contains("| engine | alpha |", repositories);
        Assert.DoesNotContain("ledger", repositories);
        Assert.StartsWith("Refused:", await tools.RepositoriesAsync("all"));
        Assert.StartsWith("Refused:", await tools.ConvergenceAsync(workspace: "beta"));
    }

    [Fact]
    public async Task A_bound_conversation_s_quests_are_its_workspace_s()
    {
        var tools = Quests("alpha");

        Assert.Equal("No open quests in workspace `alpha`.", await tools.ListQuestsAsync());
        Assert.StartsWith("Refused:", await tools.ListQuestsAsync(workspace: "beta"));
        Assert.StartsWith("Refused:", await tools.ListQuestsAsync(workspace: "all"));
    }

    /// <summary><c>default</c>'s room is the whole machine (ASKSCOPE1b): its connector is bound to none, and answers as before.</summary>
    [Fact]
    public async Task A_connector_bound_to_none_answers_as_before()
    {
        var tools = Search(bound: null);

        Assert.Contains("2 result(s) for \"hydration\" across every workspace on this machine", await tools.SearchAsync("hydration", workspace: "all"));
        Assert.Contains("in workspace `beta`", await tools.SearchAsync("hydration", workspace: "beta"));
        Assert.Contains("Cap hydration work per frame.", await tools.GetAsync("ledger:docs/DECISIONS.md#D1"));
        Assert.Contains("ledger", await tools.RegistryAsync("all"));
    }

    /// <summary>
    /// The binding is read from the connector's own surroundings, the variable the driver hands it from the record: a twin of the
    /// driver's <c>HelpCircle.Variable</c>, spelled alike. Blank binds nothing.
    /// </summary>
    [Fact]
    public void The_binding_is_read_from_the_variable_the_driver_names()
    {
        Assert.Equal("DAORIS_SESSION_WORKSPACE", AmbientWorkspace.BoundVariable);
        Assert.Equal("alpha", AmbientWorkspace.BoundFrom(name => name == AmbientWorkspace.BoundVariable ? " alpha " : null));
        Assert.Null(AmbientWorkspace.BoundFrom(_ => " "));
        Assert.Null(AmbientWorkspace.BoundFrom(_ => null));
    }
}
