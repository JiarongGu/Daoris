using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The session entry's key (SESSONE1a, D167 points 2 and 3): <c>&lt;origin&gt;/&lt;repository&gt;</c>, one rule in the service
/// that the ledger writes at the open and every read answers for a record from before, never written back. The origin is the
/// ask a quest was asked by, else the origin of the session that published it, else its chain parent's, else the quest itself;
/// a conversation is its own, and one handed over takes the one it continues.
/// </summary>
public sealed class SessionEntryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-14T06:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("app", Adopted: true, "An app.", [], [], [], Entries: 0, Root: "/trees/app"), Now);
        _ledger = new SessionLedger(_quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>Facts held in memory, so a table row names a quest and a session by the id it wants.</summary>
    private sealed class Facts : ISessionEntryFacts
    {
        public readonly Dictionary<string, Quest> Quests = [];
        public readonly Dictionary<string, Session> Sessions = [];

        public Task<Quest?> QuestAsync(string id, CancellationToken ct = default) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<Session?> SessionAsync(string id, CancellationToken ct = default) => Task.FromResult(Sessions.GetValueOrDefault(id));

        public Task<Session?> ChatTookAsync(string questId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.Values.FirstOrDefault(s => string.Equals(s.TookQuest, questId, StringComparison.OrdinalIgnoreCase)));

        public Facts Quest(string id, string from, string to = "app", string? publishedBy = null, string? parent = null)
        {
            Quests[id] = new Quest(id, from, to, "t", "b", QuestStatus.Open, null, Now, Now) { PublishedBy = publishedBy, Parent = parent };
            return this;
        }

        public Session Session(
            string id, string repository = "app", string? quest = null, string? ask = null, string? from = null,
            string? entry = null, SessionKind? kind = null)
        {
            var session = new Session(
                id, quest, repository, "stub", SessionState.Completed, null, null, null, Now, Now,
                Kind: kind ?? (quest is null ? SessionKind.Chat : SessionKind.Driven))
            { Ask = ask, From = from, Entry = entry };
            Sessions[id] = session;
            return session;
        }
    }

    private static Task<string> KeyOf(Facts facts, Session session) => SessionEntries.KeyAsync(session, facts);

    // ——— The rule's table, one case per origin rule (D167 point 2).

    /// <summary>Rule one: the ask a quest was asked by, from its sender, a chain step's included.</summary>
    [Fact]
    public async Task A_quest_an_ask_asked_takes_the_ask()
    {
        var facts = new Facts().Quest("q1", "ask #c98a7e");
        Assert.Equal("ask:c98a7e/app", await KeyOf(facts, facts.Session("s1", quest: "q1")));
    }

    /// <summary>Rule two: a quest a session published takes the origin of that session's entry, however many hops.</summary>
    [Fact]
    public async Task A_quest_a_session_published_takes_that_sessions_origin()
    {
        var facts = new Facts().Quest("q1", "ask #c98a7e", "db").Quest("q2", "db", "app", publishedBy: "s1").Quest("q3", "app", "db", publishedBy: "s2");
        facts.Session("s1", "db", quest: "q1");
        facts.Session("s2", "app", quest: "q2");

        Assert.Equal("ask:c98a7e/app", await KeyOf(facts, facts.Session("s2b", "app", quest: "q2")));
        Assert.Equal("ask:c98a7e/db", await KeyOf(facts, facts.Session("s3", "db", quest: "q3")));
    }

    /// <summary>Rule three: a chain step no ask and no session published takes its parent's origin.</summary>
    [Fact]
    public async Task A_chain_step_with_no_ask_and_no_publisher_takes_its_parents_origin()
    {
        var facts = new Facts().Quest("p1", "ask #c98a7e").Quest("q2", "someone", parent: "p1");
        Assert.Equal("ask:c98a7e/app", await KeyOf(facts, facts.Session("s2", quest: "q2")));
    }

    /// <summary>Rule four: a quest a person or an outside session published roots its own line.</summary>
    [Fact]
    public async Task A_quest_with_none_of_these_is_its_own_line()
    {
        var facts = new Facts().Quest("q1", "someone");
        Assert.Equal("quest:q1/app", await KeyOf(facts, facts.Session("s1", quest: "q1")));
    }

    /// <summary>The publisher is read before the parent, and the ask before both.</summary>
    [Fact]
    public async Task The_order_is_the_ask_then_the_publisher_then_the_parent()
    {
        var facts = new Facts()
            .Quest("pa", "ask #one").Quest("pb", "someone")
            .Quest("viaPublisher", "someone", publishedBy: "sp", parent: "pa")
            .Quest("viaAsk", "ask #two", publishedBy: "sp", parent: "pa");
        facts.Session("sp", quest: "pb");

        Assert.Equal("quest:pb/app", await KeyOf(facts, facts.Session("s1", quest: "viaPublisher")));
        Assert.Equal("ask:two/app", await KeyOf(facts, facts.Session("s2", quest: "viaAsk")));
    }

    /// <summary>A publisher whose record is gone is passed over, not an end of the rule: the parent, then the quest, answer.</summary>
    [Fact]
    public async Task A_publisher_that_is_gone_passes_to_the_next_rule()
    {
        var facts = new Facts().Quest("p1", "ask #c98a7e").Quest("q2", "someone", publishedBy: "gone", parent: "p1").Quest("q3", "someone", publishedBy: "gone");
        Assert.Equal("ask:c98a7e/app", await KeyOf(facts, facts.Session("s2", quest: "q2")));
        Assert.Equal("quest:q3/app", await KeyOf(facts, facts.Session("s3", quest: "q3")));
    }

    /// <summary>Two quests each published by the other's session are no loop for the rule: it ends, at one of them.</summary>
    [Fact]
    public async Task A_cycle_of_publishers_ends_at_a_quest()
    {
        var facts = new Facts().Quest("q1", "x", publishedBy: "s2").Quest("q2", "x", publishedBy: "s1");
        facts.Session("s1", quest: "q1");
        facts.Session("s2", quest: "q2");

        Assert.EndsWith("/app", await KeyOf(facts, facts.Sessions["s1"]));
    }

    /// <summary>A conversation is its own entry: <c>session:&lt;id&gt;</c>.</summary>
    [Fact]
    public async Task A_conversation_is_its_own_line()
    {
        var facts = new Facts();
        Assert.Equal("session:c1/app", await KeyOf(facts, facts.Session("c1")));
    }

    /// <summary>One handed over or started from another's words takes that one's entry, in its own repository.</summary>
    [Fact]
    public async Task A_conversation_handed_over_takes_the_one_it_continues()
    {
        var facts = new Facts().Quest("q1", "ask #c98a7e");
        facts.Session("s1", quest: "q1");
        facts.Session("c1");

        Assert.Equal("ask:c98a7e/db", await KeyOf(facts, facts.Session("c2", "db", from: "s1")));
        Assert.Equal("session:c1/app", await KeyOf(facts, facts.Session("c3", from: "c1")));
    }

    /// <summary>A hand-over whose source is gone is its own line, as every missing fact is.</summary>
    [Fact]
    public async Task A_hand_over_from_a_record_that_is_gone_is_its_own_line()
    {
        var facts = new Facts();
        Assert.Equal("session:c2/app", await KeyOf(facts, facts.Session("c2", from: "gone")));
    }

    /// <summary>A quest a clear forgot (HIST1b) leaves the record its own entry, <c>session:&lt;id&gt;/&lt;repository&gt;</c>.</summary>
    [Fact]
    public async Task A_forgotten_quest_leaves_the_record_its_own_line()
    {
        var facts = new Facts();
        Assert.Equal("session:s1/app", await KeyOf(facts, facts.Session("s1", quest: "forgotten")));
    }

    /// <summary>An intake is the ask's, read apart from its repositories: <c>ask:&lt;id&gt;/intake</c>.</summary>
    [Fact]
    public async Task An_intake_is_the_asks_own_entry()
    {
        var facts = new Facts();
        Assert.Equal("ask:c98a7e/intake", await KeyOf(facts, facts.Session("i1", "ask #c98a7e", ask: "c98a7e")));
    }

    /// <summary>A reviewer's or helper's record keeps its own entry and is no part of the line it serves.</summary>
    [Fact]
    public async Task A_helper_keeps_its_own_entry()
    {
        var facts = new Facts().Quest("q1", "ask #c98a7e");
        var helper = facts.Session("h1", quest: null) with { Serves = "s1" };
        Assert.Equal("session:h1/app", await KeyOf(facts, helper));
    }

    /// <summary>A stored key is the answer: the rule runs for a record with none.</summary>
    [Fact]
    public async Task A_stored_key_is_answered_as_it_is()
    {
        var facts = new Facts().Quest("q1", "ask #c98a7e");
        Assert.Equal("session:old/app+2", await KeyOf(facts, facts.Session("s1", quest: "q1", entry: "session:old/app+2")));
    }

    // ——— The evidence, as the design's section 3 replays it.

    /// <summary>
    /// 🔴 Ask #c98a7e's seven records and the intake: every record of one repository answers one key, the retry, the resume and
    /// the sibling's requests included, so the ask has one entry per repository.
    /// </summary>
    [Fact]
    public async Task The_evidence_gives_one_entry_per_repository_for_the_ask()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "session-entries-ask.json")));
        var facts = new Facts();
        foreach (var quest in fixture.RootElement.GetProperty("quests").EnumerateArray())
        {
            facts.Quest(
                quest.GetProperty("id").GetString()!, quest.GetProperty("from").GetString()!, quest.GetProperty("to").GetString()!,
                quest.TryGetProperty("publishedBy", out var by) ? by.GetString() : null);
        }

        foreach (var row in fixture.RootElement.GetProperty("sessions").EnumerateArray())
        {
            facts.Session(
                row.GetProperty("id").GetString()!, row.GetProperty("repository").GetString()!,
                row.TryGetProperty("quest", out var quest) ? quest.GetString() : null,
                row.TryGetProperty("ask", out var ask) ? ask.GetString() : null);
        }

        var expected = fixture.RootElement.GetProperty("entries").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        Assert.Equal(expected.Count, facts.Sessions.Count);
        foreach (var (id, entry) in expected) Assert.Equal(entry, await KeyOf(facts, facts.Sessions[id]));

        var repositories = expected.Values.Where(entry => !entry.EndsWith("/intake", StringComparison.Ordinal)).Distinct().Order().ToList();
        Assert.Equal(["ask:c98a7e/app", "ask:c98a7e/db"], repositories);
    }

    // ——— The ledger writes it at the open, and a record from before is answered one, never written back.

    private async Task<string?> StoredAsync(string id)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT entry FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteScalarAsync() is string text ? text : null;
    }

    [Fact]
    public async Task The_ledger_writes_the_key_when_a_session_opens_for_a_quest_an_ask_asked()
    {
        var quest = await _quests.PublishAsync("ask #c98a7e", "app", "Do it", "Why.", Now);

        var opened = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;

        Assert.Equal("ask:c98a7e/app", opened.Entry);
        Assert.Equal("ask:c98a7e/app", await StoredAsync(opened.Id));
    }

    /// <summary>A sibling's request takes the origin of the session that published it: here, from its stored key.</summary>
    [Fact]
    public async Task A_session_for_a_request_a_session_published_joins_that_sessions_entry()
    {
        var first = await _quests.PublishAsync("ask #c98a7e", "app", "Do it", "Why.", Now);
        var firstSession = (await _ledger.OpenAsync(first.Id, "stub", Now)).Session!;
        var sibling = await _quests.PublishAsync("db", "app", "Need a column", "Why.", Now, publishedBy: firstSession.Id);

        // The first session ended, as the evidence's did, so its repository is free for the next part.
        await _sessions.SetStateAsync(firstSession.Id, SessionState.Failed, "x", null, null, Now);
        var next = (await _ledger.OpenAsync(sibling.Id, "stub", Now)).Session!;

        Assert.Equal("ask:c98a7e/app", next.Entry);
    }

    [Fact]
    public async Task A_conversation_opens_as_its_own_line_and_a_hand_over_takes_the_first()
    {
        var chat = (await _ledger.OpenChatAsync("app", "stub", Now)).Session!;
        Assert.Equal($"session:{chat.Id}/app", chat.Entry);

        await _sessions.SetStateAsync(chat.Id, SessionState.Completed, null, null, null, Now);
        var carried = (await _ledger.OpenChatAsync("app", "stub", Now, from: chat.Id, fromWhy: "press", fromBy: "person")).Session!;
        Assert.Equal($"session:{chat.Id}/app", carried.Entry);
    }

    /// <summary>
    /// 🔴 A record from before the key: answered by the same rule at read, and the column stays empty, so it groups the day the
    /// reader lands and nothing was rewritten.
    /// </summary>
    [Fact]
    public async Task A_record_from_before_is_answered_a_derived_key_and_never_written_back()
    {
        var quest = await _quests.PublishAsync("ask #c98a7e", "app", "Do it", "Why.", Now);
        var old = await _sessions.CreateAsync(quest.Id, "app", "stub", Now);
        Assert.Null(old.Entry);
        Assert.Null(await StoredAsync(old.Id));

        var answered = await _ledger.WithEntryAsync(old);
        var listed = (await _ledger.WithEntriesAsync(await _sessions.ListAsync(includeClosed: true))).Single();

        Assert.Equal("ask:c98a7e/app", answered.Entry);
        Assert.Equal("ask:c98a7e/app", listed.Entry);
        Assert.Null(await StoredAsync(old.Id));
    }

    /// <summary>A teammate's mirrored record is the publisher too: <c>PublishedBy</c> names its own id, the mirror is keyed <c>origin/id</c>.</summary>
    [Fact]
    public async Task A_publisher_that_is_a_teammates_mirror_is_found_by_its_own_id()
    {
        var asked = await _quests.PublishAsync("ask #c98a7e", "db", "Do it", "Why.", Now);
        await _sessions.MirrorAsync(new Session(
            "mate@one/283ef343", asked.Id, "db", "stub", SessionState.Completed, null, null, null, Now, Now) { Origin = "mate@one" });
        var request = await _quests.PublishAsync("db", "app", "Need a column", "Why.", Now, publishedBy: "283ef343");
        var record = await _sessions.CreateAsync(request.Id, "app", "stub", Now);

        Assert.Equal("ask:c98a7e/app", (await _ledger.WithEntryAsync(record)).Entry);
    }

    // ——— A quest a conversation took joins that conversation's line (CHATTAKE1, design §1.2 rule 2).

    /// <summary>🔴 The chat's take records the quest's id, and a later part on that quest opens under the chat's entry.</summary>
    [Fact]
    public async Task A_quest_a_chat_took_joins_the_chats_line_for_a_later_part()
    {
        var chat = (await _ledger.OpenChatAsync("app", "stub", Now)).Session!;
        var quest = await _quests.PublishAsync("someone", "app", "Do it", "Why.", Now);
        Assert.True(await _ledger.MarkTookAsync(chat.Id, $"#{quest.Id}"));
        await _sessions.SetStateAsync(chat.Id, SessionState.Completed, null, null, null, Now);

        Assert.Equal(quest.Id, (await _sessions.FindAsync(chat.Id))!.TookQuest);
        var part = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        Assert.Equal($"session:{chat.Id}/app", part.Entry);
    }

    /// <summary>The rule's place: after the ask that asked the quest, before the session that published it and the chain parent.</summary>
    [Fact]
    public async Task The_chat_that_took_is_read_after_the_ask_and_before_the_publisher()
    {
        var facts = new Facts().Quest("viaAsk", "ask #one").Quest("viaChat", "someone", publishedBy: "sp").Quest("pb", "someone");
        facts.Session("sp", quest: "pb");
        var chat = facts.Session("chat1", kind: SessionKind.Chat);
        facts.Sessions["chat1"] = chat with { TookQuest = "viaChat" };
        facts.Sessions["chat2"] = chat with { Id = "chat2", TookQuest = "viaAsk" };

        Assert.Equal("session:chat1/app", await KeyOf(facts, facts.Session("s1", quest: "viaChat")));
        Assert.Equal("ask:one/app", await KeyOf(facts, facts.Session("s2", quest: "viaAsk")));
    }

    /// <summary>An older chat record marked a take without the quest: nothing says which, so the quest keys as it did.</summary>
    [Fact]
    public async Task A_chat_record_from_before_the_field_leaves_the_quest_keyed_as_before()
    {
        var chat = (await _ledger.OpenChatAsync("app", "stub", Now)).Session!;
        Assert.True(await _sessions.MarkTookAsync(chat.Id));
        Assert.Null((await _sessions.FindAsync(chat.Id))!.TookQuest);
        await _sessions.SetStateAsync(chat.Id, SessionState.Completed, null, null, null, Now);
        var quest = await _quests.PublishAsync("someone", "app", "Do it", "Why.", Now);

        var part = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        Assert.Equal($"quest:{quest.Id}/app", part.Entry);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}
