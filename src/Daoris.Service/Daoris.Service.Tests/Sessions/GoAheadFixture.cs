using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// What the go-ahead test classes stand on (PART6): two repositories, an ask desk, a ledger over in-memory stores, and a
/// session working a quest in a tree of its own. xunit builds one per test, so a derived class shares the setup and no state.
/// </summary>
public abstract class GoAheadFixture : IAsyncLifetime
{
    protected readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-goahead-" + Guid.NewGuid().ToString("N")[..8]);

    protected SqliteConnection _connection = null!;
    protected QuestStore _quests = null!;
    protected AskStore _asks = null!;
    protected QuestExchange _exchange = null!;
    protected AskDesk _desk = null!;
    protected SessionStore _sessions = null!;
    protected SessionLedger _ledger = null!;
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T09:00:00Z");

    protected const string Sentence = "the dashboard figure reads zero";

    public async Task InitializeAsync()
    {
        Repo("dashboards", """{ "summary": "The dashboards.", "owns": ["dashboard figures"], "accepts": ["a figure"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files);
        _desk = new AskDesk(service, _asks, _exchange, files);
        _ledger = new SessionLedger(_quests, _sessions, service, _asks);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    protected void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    protected string Tree(string name) => Path.Combine(_root, "trees", name);

    protected async Task<(Ask Ask, Quest Quest)> Asked(string sentence = Sentence)
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", sentence) { To = "dashboards" }, Now);
        return (outcome.Ask!, outcome.Quest!);
    }

    /// <summary>A session on <paramref name="quest"/>, working, in a tree of its own name.</summary>
    protected async Task<Session> Working(Quest quest, string tree)
    {
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now, tree: Tree(tree))).Session!;
        foreach (var step in new[] { "starting", "working" })
        {
            await _ledger.AdvanceAsync(session.Id, step, null, null, null, Now);
        }

        return session;
    }

    /// <summary>The session parks, and a carry-on opens on its quest in the same tree.</summary>
    protected async Task<Session> CarriedOn(Session parked, Quest quest)
    {
        await _ledger.AdvanceAsync(parked.Id, "awaiting-person", "It needs the production write.", null, null, Now);
        await _ledger.AnswerAsync(parked.Id, "carry on", Now.AddMinutes(1));
        await _ledger.AdvanceAsync(parked.Id, "completed", "The answer is carried on in a new session.", null, null, Now.AddMinutes(2));
        var next = (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(3), tree: Tree("carry"))).Session!;
        Assert.NotNull(next);
        return next;
    }
}
