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

    private sealed class Entries(params KnowledgeEntry[] entries) : IKnowledgeSource
    {
        public string Name => "fixture";

        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }

    /// <summary>
    /// SEM3 (D123): an entry longer than the window becomes several vectors, and the agent's door says how
    /// many did and at what window — a reader of the index sees it rather than assumes it.
    /// </summary>
    [Fact]
    public async Task A_refresh_says_how_many_entries_were_split_and_at_what_window()
    {
        var store = new InMemoryKnowledgeStore();
        var embedder = new DimensionEmbedder(["window"]);
        var vectors = new Lyntai.Memory.InMemoryVectorStore();
        var longOne = new KnowledgeEntry(
            "Owner", EntryKind.Decision, Provenance.Local, "D1", string.Concat(Enumerable.Repeat("A sentence of the body. ", 300)),
            "docs/DECISIONS.md", "D1");
        var shortOne = new KnowledgeEntry(
            "Owner", EntryKind.Fix, Provenance.Local, "F1", "Short.", "docs/FIX-LOG.md", "F1");
        var service = new KnowledgeService(
            store,
            new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store), new SemanticKnowledgeSearch(store, embedder, vectors)),
            new Entries(longOne, shortOne), DisclosurePolicy.LocalOnly, embedder, vectors);
        var tools = new KnowledgeTools(
            service, _quests, new QuestExchange(service, _quests, files: _files),
            new AmbientWorkspace(Path.Combine(_root, "family", "Asker")));

        var said = await tools.RefreshAsync();

        var pieces = EntryPieces.Of(longOne, EntryPieces.DefaultWindow).Count + 1;
        Assert.Contains(
            $"Embedded 2 entries as {pieces} vectors of at most 2000 characters ({ServiceOptions.WindowVariable}): "
            + "1 longer than that was split, each part a vector of its own.",
            said);
        Assert.Contains("Lexical and semantic recall are both active.", said);
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
    /// D115 §2.2 (DEV4): the registry's answer lists each repository's lanes, so an asker can see what it
    /// may address and how; a quest to one is published through this door and listed with its lanes.
    /// </summary>
    [Fact]
    public async Task The_registry_lists_lanes_and_a_quest_to_one_is_listed_with_them()
    {
        var owner = (await _service.RegistryAsync()).Named("Owner")!;
        await _service.RegisterAsync(
            owner with { Lanes = [new("core", "Core", "The runtime."), new("records", "Records", "The backlog.", Steward: true)] },
            DateTimeOffset.UtcNow);

        var registry = await _tools.RegistryAsync();
        Assert.Contains("`Owner:core`", registry);
        Assert.Contains("The runtime.", registry);
        Assert.Contains("the steward's", registry);

        Assert.Contains("Published quest", await _tools.PublishQuestAsync("Asker", "Owner:core", "Cap the frame", "It is unbounded."));
        Assert.Contains("lanes: `core`", await _tools.ListQuestsAsync("Owner"));
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
    /// DRIFT1c (D133 §3): an intake names what the person requires, each in their own words with the check
    /// that proves it. A quote they said is published and listed on the quest; one they never said is
    /// refused, naming the words, and nothing is published.
    /// </summary>
    [Fact]
    public async Task An_intakes_requirements_quote_the_person_and_one_they_never_said_is_refused()
    {
        var asks = await AskStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var exchange = new QuestExchange(_service, _quests, files: _files, asks: asks);
        var desk = new AskDesk(_service, asks, exchange, _files);
        var ask = (await desk.AskAsync(new AskRequest("default", "the report will need the v3 bridge"), DateTimeOffset.UtcNow)).Ask!;
        var room = Path.Combine(_root, "home", "intake", "default");
        var session = (await new SessionLedger(_quests, sessions, _service, asks)
            .OpenIntakeAsync(ask.Id, "stub", room, DateTimeOffset.UtcNow)).Session!;
        var tools = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(room, ask.Workspace), desk, new IntakeScope(ask.Id, session.Id));

        var refused = await tools.PublishQuestAsync(
            "intake", "Owner", "Build the report", "Reached through the bridge.",
            requirements: [new Requirement("make it reachable through the bridge", "It opens in the older shell.")]);
        Assert.Contains("\"make it reachable through the bridge\"", refused);
        Assert.Empty(await _quests.ListAsync());

        var published = await tools.PublishQuestAsync(
            "intake", "Owner", "Build the report", "Reached through the bridge.",
            requirements: [new Requirement("will need the v3 bridge", "The report opens through the bridge's route.")]);

        Assert.Contains("Published quest", published);
        var quest = (await _quests.ListAsync(receiver: "Owner")).Single();
        Assert.Equal([new QuestRequirement("will need the v3 bridge", "The report opens through the bridge's route.")], quest.Requirements);
        var listed = await tools.ListQuestsAsync("Owner");
        Assert.Contains("requires \"will need the v3 bridge\"", listed);
        Assert.Contains("The report opens through the bridge's route.", listed);
    }

    /// <summary>
    /// A requirement the agent leaves half of reaches the exchange half-made and is refused there, naming
    /// which — a missing half is never published blank.
    /// </summary>
    [Fact]
    public async Task A_requirement_missing_its_check_is_refused_naming_which()
    {
        var answer = await _tools.PublishQuestAsync(
            "Asker", "Owner", "Read the field names from config", "b", requirements: [new Requirement("the field names", null)]);

        Assert.Contains("Requirement 1", answer);
        Assert.Empty(await _quests.ListAsync());
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
    /// SESS1: a quest says which session published it, from the name its connector was handed, so the
    /// session's view can say what it caused without guessing from the times — an intake's publish on
    /// its ask's behalf too. A publish no session made, the platform's own composer, names none.
    /// </summary>
    [Fact]
    public async Task A_quest_says_which_session_published_it()
    {
        var asks = await AskStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var exchange = new QuestExchange(_service, _quests, files: _files);
        var desk = new AskDesk(_service, asks, exchange, _files);
        var driven = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(Path.Combine(_root, "family", "Asker")),
            desk, new IntakeScope(null, "s1a2b3c4"));

        await driven.PublishQuestAsync("Asker", "Owner", "Read the field names from config", "b");
        await _tools.PublishQuestAsync("Asker", "Checker", "Verify the field names", "b");

        var ask = (await desk.AskAsync(new AskRequest("default", "the chunk budget is hard-coded"), DateTimeOffset.UtcNow)).Ask!;
        var room = Path.Combine(_root, "home", "intake", "default");
        var intake = (await new SessionLedger(_quests, sessions, _service, asks)
            .OpenIntakeAsync(ask.Id, "stub", room, DateTimeOffset.UtcNow)).Session!;
        await new KnowledgeTools(
                _service, _quests, exchange, new AmbientWorkspace(room, ask.Workspace), desk, new IntakeScope(ask.Id, intake.Id))
            .PublishQuestAsync("intake", "Owner", "Cap the chunk budget", "The ask names it.");

        var owed = await _quests.ListAsync(includeClosed: true);
        Assert.Equal("s1a2b3c4", owed.Single(quest => quest.Title == "Read the field names from config").PublishedBy);
        Assert.Null(owed.Single(quest => quest.To == "Checker").PublishedBy);
        Assert.Equal(intake.Id, owed.Single(quest => quest.Title == "Cap the chunk budget").PublishedBy);
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

    /// <summary>
    /// HELP6: Ask Daoris's four further tools each write one proposal under the home, naming the
    /// conversation its connector was handed — and a malformed one writes nothing, said in the agent's terms.
    /// </summary>
    [Fact]
    public void Ask_Daoris_proposes_an_agent_action_a_delete_an_accounts_settings_and_a_screen()
    {
        var home = Path.Combine(_root, "help-home");
        var tools = new KnowledgeTools(
            _service, _quests, new QuestExchange(_service, _quests, files: _files),
            new AmbientWorkspace(Path.Combine(_root, "family", "Asker")),
            intake: new IntakeScope(null, "h1e1p000"), help: new HelpProposalBox(home));

        Assert.Contains("Proposed", tools.ProposeAgent("pin", "claude-code", "the person wants that release", version: "2.1.300"));
        Assert.Contains("Proposed", tools.ProposeDelete("made by mistake", quest: "q1a2b3c4"));
        Assert.Contains("Proposed", tools.ProposeAgentSettings("claude-code", "work", "the person asked", effort: "high"));
        Assert.Contains("Proposed", tools.ProposeGo("settings", "the person asked where accounts are", domain: "agents"));
        Assert.Contains("Nothing was proposed", tools.ProposeDelete("no id at all"));

        var written = Directory.GetFiles(HelpProposalBox.FolderOf(home))
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .ToList();
        Assert.Equal(["account", "agent", "delete", "go"], written.Select(file => file.GetProperty("kind").GetString()).Order());
        Assert.All(written, file => Assert.Equal("h1e1p000", file.GetProperty("by").GetProperty("session").GetString()));
    }

    /// <summary>
    /// PLUG9: Ask Daoris proposes adding a plugin that has landed, from its folder in a repository's
    /// checkout, or switching one on or off; the file names the conversation, and a malformed one writes nothing.
    /// </summary>
    [Fact]
    public void Ask_Daoris_proposes_adding_a_plugin_or_switching_one()
    {
        var home = Path.Combine(_root, "help-plugin-home");
        var tools = new KnowledgeTools(
            _service, _quests, new QuestExchange(_service, _quests, files: _files),
            new AmbientWorkspace(Path.Combine(_root, "family", "Asker")),
            intake: new IntakeScope(null, "h1e1p000"), help: new HelpProposalBox(home));

        Assert.Contains("Proposed", tools.ProposePlugin("add", "the person wants quests held overnight", repository: "house-plugins", folder: "quiet-hours"));
        Assert.Contains("Proposed", tools.ProposePlugin("enable", "the person wants it on", id: "example.lands"));
        Assert.Contains("Nothing was proposed", tools.ProposePlugin("add", "no folder named"));
        // PLUG9 (c) and (d): one of the install's own by its id, and an update of an installed one.
        Assert.Contains("Proposed", tools.ProposePlugin("add", "the person wants pull requests opened", offer: "github-pull-request"));
        Assert.Contains("Proposed", tools.ProposePlugin("update", "a newer one has landed", id: "example.lands"));

        var written = Directory.GetFiles(HelpProposalBox.FolderOf(home))
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .ToList();
        Assert.Equal(["add", "add", "enable", "update"], written.Select(file => file.GetProperty("door").GetString()).Order());
        Assert.All(written, file => Assert.Equal(("plugin", "h1e1p000"),
            (file.GetProperty("kind").GetString(), file.GetProperty("by").GetProperty("session").GetString())));
    }

    /// <summary>
    /// WSR5b: Ask Daoris proposes handing a branch a landing made to a landing plugin; the file names the
    /// conversation, and a malformed one writes nothing.
    /// </summary>
    [Fact]
    public void Ask_Daoris_proposes_handing_a_landed_branch_on()
    {
        var home = Path.Combine(_root, "help-hand-home");
        var tools = new KnowledgeTools(
            _service, _quests, new QuestExchange(_service, _quests, files: _files),
            new AmbientWorkspace(Path.Combine(_root, "family", "Asker")),
            intake: new IntakeScope(null, "h1e1p000"), help: new HelpProposalBox(home));

        Assert.Contains("Proposed", tools.ProposeHand("s2a3b4c5", "the person wants its pull request opened"));
        Assert.Contains("Proposed", tools.ProposeHand("feature/q2-second", "the rule names none yet", repository: "engine", plugin: "example.lands"));
        Assert.Contains("Nothing was proposed", tools.ProposeHand("", "no branch named"));

        var written = Directory.GetFiles(HelpProposalBox.FolderOf(home))
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .ToList();
        Assert.Equal(["feature/q2-second", "s2a3b4c5"], written.Select(file => file.GetProperty("target").GetString()).Order());
        Assert.All(written, file => Assert.Equal(("hand", "h1e1p000"),
            (file.GetProperty("kind").GetString(), file.GetProperty("by").GetProperty("session").GetString())));
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
