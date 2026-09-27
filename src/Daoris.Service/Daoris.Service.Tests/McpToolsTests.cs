using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// The MCP door itself — what an agent actually calls. The exchange behind it is tested on its own;
/// what only the door does is turn a PATH into bytes (D65 §2) and take a chain as a list of steps
/// (D65 §4), and a schema the agent cannot read is a parameter that does not exist.
/// </summary>
public sealed class McpToolsTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-mcp-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestFiles _files = null!;
    private KnowledgeService _service = null!;
    private KnowledgeTools _tools = null!;

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
        _tools = new KnowledgeTools(
            service, _quests, new QuestExchange(service, _quests, files: _files),
            new AmbientWorkspace(Path.Combine(_root, "family", "Asker")));
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Publishing_carries_links_a_file_by_its_path_and_a_chain()
    {
        var trace = Path.Combine(_root, "trace.log");
        await File.WriteAllTextAsync(trace, "chunk 7: field not found");

        var answer = await _tools.PublishQuestAsync(
            "Asker", "Owner", "Read the field names from config", "They are hard-coded.",
            links: ["https://tickets.example/T-1"],
            attachments: [trace],
            then: [new ChainStep("Checker", "Verify {parent} in the browser", "Open it and look.")]);

        Assert.Contains("Published quest", answer);
        var quest = (await _quests.ListAsync(receiver: "Owner")).Single();
        Assert.Equal(["https://tickets.example/T-1"], quest.Links);
        Assert.True(_files.Has(quest.Id, Assert.Single(quest.Attachments)));
        Assert.Equal("Checker", Assert.Single(quest.Then).To);
    }

    /// <summary>
    /// An intake's connector (D65 §1b): the room is no repository, so the quest is asked BY THE ASK, in
    /// the ask's circle — and because the session publishing is the ask's own intake, the ask says a
    /// harness decided. The `from` an agent fills in is not what decides who asked.
    /// </summary>
    [Fact]
    public async Task An_intakes_connector_publishes_as_its_ask_in_its_circle()
    {
        var asks = await AskStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var exchange = new QuestExchange(_service, _quests, files: _files);
        var desk = new AskDesk(_service, asks, exchange, _files);
        var ask = (await desk.AskAsync(new AskRequest("default", "the field names are hard-coded"), DateTimeOffset.UtcNow)).Ask!;
        var room = Path.Combine(_root, "home", "intake", "default");
        var session = (await new SessionLedger(_quests, sessions, _service, asks)
            .OpenIntakeAsync(ask.Id, "stub", room, DateTimeOffset.UtcNow)).Session!;
        var tools = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(room, ask.Workspace), desk,
            new IntakeScope(ask.Id, session.Id));

        var answer = await tools.PublishQuestAsync(
            "intake", "Owner", "Read the field names from config", "The ticket names both fields.",
            then: [new ChainStep("Checker", "Verify {parent}", "Look.")]);

        Assert.Contains("Published quest", answer);
        var quest = (await _quests.ListAsync(receiver: "Owner")).Single();
        Assert.Equal($"ask #{ask.Id}", quest.From);
        Assert.Equal("Checker", Assert.Single(quest.Then).To);
        var answered = (await asks.FindAsync(ask.Id))!;
        Assert.Equal(AskDesk.ByIntake, answered.Tier);
        Assert.Equal([quest.Id], answered.Quests);
        // Its own circle, though nothing registered sits where it runs.
        Assert.Contains("in workspace `default`", await tools.RegistryAsync());
    }

    /// <summary>
    /// The driver names the session on every connector it hands over (PERM2), so a rule proposal says
    /// who made it. With no ask that changes nothing about a publish: only an intake publishes as its ask.
    /// </summary>
    [Fact]
    public async Task A_session_named_without_an_ask_publishes_from_its_own_repository()
    {
        var asks = await AskStore.OpenAsync(_connection);
        var exchange = new QuestExchange(_service, _quests, files: _files);
        var tools = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(Path.Combine(_root, "family", "Asker")),
            new AskDesk(_service, asks, exchange, _files), new IntakeScope(null, "s1a2b3c4"));

        var answer = await tools.PublishQuestAsync("Asker", "Owner", "Read the field names from config", "b");

        Assert.Contains("Published quest", answer);
        Assert.Equal("Asker", (await _quests.ListAsync(receiver: "Owner")).Single().From);
    }

    /// <summary>
    /// STANDDOWN2: a session the driver started takes its quest through its own connector, and the take
    /// is written on its record. FG5's verify session took its quest and stopped to ask the person, and
    /// its end read "someone else has it", because nothing said whose take it was.
    /// </summary>
    [Fact]
    public async Task A_take_through_a_sessions_own_connector_is_written_on_its_record()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var exchange = new QuestExchange(_service, _quests, files: _files);
        await _tools.PublishQuestAsync("Asker", "Owner", "Verify it", "Open it and look.");
        var quest = (await _quests.ListAsync(receiver: "Owner")).Single();
        var session = (await ledger.OpenAsync(quest.Id, "stub", DateTimeOffset.UtcNow)).Session!;
        var tools = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(Path.Combine(_root, "family", "Owner")),
            new AskDesk(_service, asks, exchange, _files), new IntakeScope(null, session.Id), ledger: ledger);

        await tools.RespondToQuestAsync(quest.Id, "take");

        Assert.True((await sessions.FindAsync(session.Id))!.Took);
    }

    [Fact]
    public async Task A_path_that_is_not_a_file_is_refused_and_nothing_is_published()
    {
        var answer = await _tools.PublishQuestAsync(
            "Asker", "Owner", "An ask", "b", attachments: [Path.Combine(_root, "missing.png")]);

        Assert.Contains("missing.png", answer);
        Assert.Contains("Nothing was published", answer);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>What an agent reading the list is told: what each quest carries, what follows it, what it follows.</summary>
    [Fact]
    public async Task The_list_says_what_a_quest_carries_what_follows_it_and_what_it_follows()
    {
        await _tools.PublishQuestAsync(
            "Asker", "Owner", "Develop it", "b",
            links: ["https://tickets.example/T-2"],
            then: [new ChainStep("Checker", "Verify {parent}", "Look.")]);
        var develop = (await _quests.ListAsync(receiver: "Owner")).Single();
        await _tools.RespondToQuestAsync(develop.Id, "done", "Landed.");

        var list = await _tools.ListQuestsAsync(includeClosed: true, workspace: "all");

        Assert.Contains("links: https://tickets.example/T-2", list);
        Assert.Contains("then → `Checker`: Verify {parent}", list);
        Assert.Contains($"follows `#{develop.Id}`", list);
    }

    /// <summary>
    /// The schema is the parameter, as far as an agent is concerned: a chain it cannot see described
    /// as a list of steps, each with whom to ask and what, is a chain it cannot compose.
    /// </summary>
    [Fact]
    public void The_tool_offers_a_chain_as_a_list_of_steps_it_can_read()
    {
        var tool = McpServerTool.Create(
            typeof(KnowledgeTools).GetMethod(nameof(KnowledgeTools.PublishQuestAsync))!, _tools);

        var then = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("then");
        var step = then.GetProperty("items").GetProperty("properties");

        Assert.Contains("array", then.GetProperty("type").ToString());
        Assert.True(step.TryGetProperty("to", out _));
        Assert.True(step.TryGetProperty("title", out _));
        Assert.True(step.TryGetProperty("body", out var body));
        Assert.Contains("{parent}", JsonSerializer.Serialize(then));
        Assert.False(string.IsNullOrWhiteSpace(body.ToString()));
    }
}

/// <summary>
/// TIER1 at the agent's door: `knowledge_search` says the tier that ANSWERED (D24). With the embedder
/// down it said nothing at all, because the configured tier was semantic — an agent read a
/// words-only answer as complete.
/// </summary>
public sealed class SearchToolTierTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;

    private sealed class DownSearch : IKnowledgeSearch
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default) =>
            throw new HttpRequestException("the embedding endpoint is unreachable");
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// The tools over one stored entry, with a meaning half that is configured and down. The lexical
    /// half is made from the store, so a test can hand in one that works or one that is down too.
    /// </summary>
    private async Task<KnowledgeTools> ToolsAsync(Func<IKnowledgeStore, IKnowledgeSearch> lexicalHalf)
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [
            new KnowledgeEntry("alpha", EntryKind.Decision, Provenance.Local, "D1", "drift is measured against the lock",
                "docs/DECISIONS.md", "D1"),
        ]);
        var service = new KnowledgeService(
            store, new HybridKnowledgeSearch(lexicalHalf(store), new DownSearch()), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, new DimensionEmbedder(["drift"]), new Lyntai.Memory.InMemoryVectorStore(),
            registry: new Registry());
        return new KnowledgeTools(
            service, _quests, new QuestExchange(service, _quests), new AmbientWorkspace(Path.GetTempPath()));
    }

    [Fact]
    public async Task A_configured_meaning_half_that_did_not_answer_is_said_beside_the_results()
    {
        var tools = await ToolsAsync(store => new LexicalKnowledgeSearch(store));

        var said = await tools.SearchAsync("drift", workspace: "all");

        Assert.Contains("1 result(s)", said);
        Assert.Contains("Matched on words only", said);
        Assert.Contains("the search by meaning did not answer: the embedding endpoint is unreachable", said);
    }

    [Fact]
    public async Task Nothing_answering_is_not_reported_as_no_matches()
    {
        var tools = await ToolsAsync(_ => new DownSearch());

        var said = await tools.SearchAsync("drift", workspace: "all");

        Assert.StartsWith("No search answered", said);
        Assert.DoesNotContain("No matches", said);
    }
}
