using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// What every MCP door test class stands on (PART6): a family of three repositories, an in-memory quest store and a connector
/// for the Asker, as its tool classes (PART10a). xunit builds one per test, so a class that derives from it shares the setup
/// and none of the state.
/// </summary>
public abstract class McpToolsFixture : IAsyncLifetime
{
    protected readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-mcp-" + Guid.NewGuid().ToString("N")[..8]);

    protected SqliteConnection _connection = null!;
    protected QuestStore _quests = null!;
    protected QuestFiles _files = null!;
    protected KnowledgeService _service = null!;

    /// <summary>The Asker's search tools.</summary>
    protected KnowledgeSearchTools _search = null!;

    /// <summary>The Asker's quest tools, over an exchange of their own.</summary>
    protected QuestTools _questTools = null!;

    /// <summary>The tools not yet a class of their own, as a connector that speaks for no session holds them.</summary>
    protected KnowledgeTools _tools = null!;

    /// <summary>The workspace a session running in <paramref name="room"/> answers from, or its ask's circle.</summary>
    protected WorkspaceScope Circle(string room, string? circle = null) => new(_service, new AmbientWorkspace(room, circle));

    /// <summary>The workspace a session in one of the family's repositories answers from.</summary>
    protected WorkspaceScope In(string repository) => Circle(Path.Combine(_root, "family", repository));

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "Asker", "Owner", "Checker" })
        {
            var dir = Path.Combine(_root, "family", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), """
                { "source": "s", "packs": [], "domain": { "summary": "s", "owns": ["o"], "accepts": ["a"] } }
                """);
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), DateTimeOffset.UtcNow);
        _service = service;

        _files = new QuestFiles(Path.Combine(_root, "home"));
        _search = new KnowledgeSearchTools(service, In("Asker"));
        _questTools = new QuestTools(_quests, new QuestExchange(service, _quests, files: _files), In("Asker"));
        _tools = new KnowledgeTools();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
