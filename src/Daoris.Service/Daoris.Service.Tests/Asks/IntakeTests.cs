using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The intake session (D65 §1b, INT4b): an ask the declarations did not settle is answered by a
/// SESSION — a conversation Daoris opens for the ask in a room it owns — which publishes the quests
/// itself, asked by the ask, in the ask's circle. The harness carries the model; nothing here names one.
/// </summary>
public sealed class IntakeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-intake-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("media-api", """{ "summary": "The media service and its config.", "owns": ["media config", "video and image field names"], "accepts": ["a media bug"] }""");
        Repo("storefront", """{ "summary": "The storefront.", "owns": ["product pages", "checkout"], "accepts": ["a UI bug"] }""");

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

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    private string Room(string workspace = "work") => Path.Combine(_root, "home", "intake", workspace);

    private async Task<Ask> Proposed(string sentence = "use the media config instead of hard coding the video field name") =>
        (await _desk.AskAsync(new AskRequest("work", sentence) { Links = ["https://tickets.example/T-9"] }, Now)).Ask!;

    [Fact]
    public async Task An_intake_opens_a_conversation_for_the_ask_in_its_circle_and_the_ask_names_it()
    {
        var ask = await Proposed();

        var opened = await _ledger.OpenIntakeAsync(ask.Id, "stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var session = opened.Session!;
        // A conversation (SES2) — the kind every build already reads safely: no quest, never planned from.
        Assert.Equal(SessionKind.Chat, session.Kind);
        Assert.Null(session.Quest);
        Assert.Equal($"ask #{ask.Id}", session.Repository);
        Assert.Equal("work", session.Workspace);
        Assert.Equal(ask.Id, session.Ask);
        Assert.Equal(Trees.Normalize(Room()), session.Tree);
        Assert.Null(session.BaseCommit);
        Assert.Equal(ask.Id, (await _sessions.FindAsync(session.Id))!.Ask);
        Assert.Equal(session.Id, (await _asks.FindAsync(ask.Id))!.Intake);
    }

    /// <summary>An intake answers an ask nothing else has — one that is gone, closed, published or already served is not its to take.</summary>
    [Fact]
    public async Task An_intake_is_refused_for_an_ask_it_cannot_serve()
    {
        var served = await Proposed();
        var first = (await _ledger.OpenIntakeAsync(served.Id, "stub", Room(), Now)).Session!;
        await _sessions.SetStateAsync(first.Id, SessionState.Completed, null, null, null, Now);
        var closed = await Proposed("close me with a reason");
        await _desk.CloseAsync(closed.Id, "Not needed.", Now);
        var named = (await _desk.AskAsync(new AskRequest("work", "straight to the storefront") { To = "storefront" }, Now)).Ask!;

        var unknown = await _ledger.OpenIntakeAsync("abcdef", "stub", Room(), Now);
        var again = await _ledger.OpenIntakeAsync(served.Id, "stub", Room(), Now);
        var shut = await _ledger.OpenIntakeAsync(closed.Id, "stub", Room(), Now);
        var answered = await _ledger.OpenIntakeAsync(named.Id, "stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.AskNotFound, unknown.Refusal);
        Assert.Equal(SessionOpenRefusal.AskAnswered, again.Refusal);
        Assert.Contains(first.Id, again.Message);
        Assert.Equal(SessionOpenRefusal.AskAnswered, shut.Refusal);
        Assert.Equal(SessionOpenRefusal.AskAnswered, answered.Refusal);
        Assert.Null(again.Session);
    }

    /// <summary>The room is a circle's shared working directory, so it holds one intake at a time — whichever ask it serves.</summary>
    [Fact]
    public async Task One_intake_at_a_time_holds_a_circles_room()
    {
        var one = await Proposed("the first ask in this circle");
        var two = await Proposed("the second ask in this circle");
        var holding = (await _ledger.OpenIntakeAsync(one.Id, "stub", Room(), Now)).Session!;

        var busy = await _ledger.OpenIntakeAsync(two.Id, "stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, busy.Refusal);
        Assert.Contains(holding.Id, busy.Message);
        // Refused is refused: the ask is not marked served by an intake that never opened.
        Assert.Null((await _asks.FindAsync(two.Id))!.Intake);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenIntakeAsync(two.Id, "stub", Room("other"), Now)).Refusal);
    }

    /// <summary>
    /// A PARKED intake has no process — it asked the person and ended — so it does not hold the room:
    /// otherwise one unanswered question would stop every later ask in the circle.
    /// </summary>
    [Fact]
    public async Task A_parked_intake_leaves_the_room_to_the_next_ask()
    {
        var one = await Proposed("the first ask in this circle");
        var two = await Proposed("the second ask in this circle");
        var parked = (await _ledger.OpenIntakeAsync(one.Id, "stub", Room(), Now)).Session!;
        await _sessions.SetStateAsync(parked.Id, SessionState.AwaitingPerson, "asked the person", null, null, Now);

        var next = await _ledger.OpenIntakeAsync(two.Id, "stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.None, next.Refusal);
    }

    /// <summary>A driven session or a chat in a repository is not in the room, and the room holds none of theirs.</summary>
    [Fact]
    public async Task The_room_is_its_own_lock_and_no_repositorys()
    {
        var ask = await Proposed();
        var quest = await _exchange.PublishAsync(new QuestAsk("storefront", "media-api", "t", "b"), Now);
        Assert.NotNull(quest.Quest);
        var driven = await _ledger.OpenAsync(quest.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "family", "media-api"));
        Assert.Equal(SessionOpenRefusal.None, driven.Refusal);

        var opened = await _ledger.OpenIntakeAsync(ask.Id, "stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
    }

    /// <summary>
    /// What the intake publishes is the SESSION's words — it read the ticket — asked by the ask, in the
    /// ask's circle, carrying the ask's links, with its chain. The ask then says the intake answered it.
    /// </summary>
    [Fact]
    public async Task What_an_intake_publishes_is_its_own_words_asked_by_the_ask_and_the_ask_says_the_intake_answered()
    {
        var ask = await Proposed();
        var session = (await _ledger.OpenIntakeAsync(ask.Id, "stub", Room(), Now)).Session!;

        var outcome = await _desk.PublishAsync(
            ask.Id, "media-api", Now,
            draft: new AskDraft("Read the video field name from the media config", "The ticket T-9 names the field.")
            {
                Links = ["https://tickets.example/T-9#comment"],
                Then = [new QuestStep("storefront", "Verify {parent} on the product page", "Open it and look.")],
            },
            session: session.Id);

        Assert.Equal(AskRefusal.None, outcome.Refusal);
        var quest = outcome.Quest!;
        Assert.Equal($"ask #{ask.Id}", quest.From);
        Assert.Equal("Read the video field name from the media config", quest.Title);
        Assert.StartsWith("The ticket T-9 names the field.", quest.Body);
        Assert.Contains($"ask `#{ask.Id}`", quest.Body);
        // The person's own words travel beneath the intake's, verbatim: a paraphrase is weighed against them.
        Assert.Contains("> use the media config instead of hard coding the video field name", quest.Body);
        Assert.Equal("work", quest.Workspace);
        Assert.Equal(["https://tickets.example/T-9", "https://tickets.example/T-9#comment"], quest.Links);
        Assert.Equal("storefront", Assert.Single(quest.Then).To);

        var answered = outcome.Ask!;
        Assert.Equal(AskState.Published, answered.State);
        Assert.Equal(AskDesk.ByIntake, answered.Tier);
        Assert.Equal([quest.Id], answered.Quests);
    }

    /// <summary>A person publishing, or anyone naming a session that is not this ask's intake, is not the intake answering.</summary>
    [Fact]
    public async Task A_publish_by_anyone_but_the_asks_intake_keeps_the_tier()
    {
        var ask = await Proposed();
        await _ledger.OpenIntakeAsync(ask.Id, "stub", Room(), Now);

        var byPerson = await _desk.PublishAsync(ask.Id, "media-api", Now);
        var byStranger = await _desk.PublishAsync(
            ask.Id, "storefront", Now, draft: new AskDraft("Also the storefront", "why"), session: "notitsown");

        Assert.Equal(AskDesk.ByDeclarations, byPerson.Ask!.Tier);
        Assert.Equal(AskDesk.ByDeclarations, byStranger.Ask!.Tier);
        Assert.Equal(2, byStranger.Ask.Quests.Count);
    }

    /// <summary>A store from before the intake gains the two columns and keeps every row it had.</summary>
    [Fact]
    public async Task A_store_from_before_the_intake_gains_its_columns_and_keeps_its_rows()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE asks (
                  id TEXT PRIMARY KEY, workspace TEXT NOT NULL, sentence TEXT NOT NULL, state TEXT NOT NULL,
                  tier TEXT NOT NULL, asked TEXT NOT NULL, updated TEXT NOT NULL, asker TEXT NULL, note TEXT NULL,
                  links TEXT NOT NULL DEFAULT '[]', attachments TEXT NOT NULL DEFAULT '[]',
                  proposal TEXT NOT NULL DEFAULT '[]', quests TEXT NOT NULL DEFAULT '[]'
                );
                INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated)
                VALUES ('a1b2c3', 'work', 'an older ask', 'Proposed', 'declarations', '2026-09-23T10:00:00Z', '2026-09-23T10:00:00Z');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var asks = await AskStore.OpenAsync(old);
        var sessions = await SessionStore.OpenAsync(old);

        var kept = (await asks.FindAsync("a1b2c3"))!;
        Assert.Null(kept.Intake);
        Assert.Equal("an older ask", kept.Sentence);
        var chat = await sessions.CreateAsync(null, "ask #a1b2c3", "stub", Now, "work", SessionKind.Chat, ask: "a1b2c3");
        Assert.Equal("a1b2c3", (await sessions.FindAsync(chat.Id))!.Ask);
    }
}
