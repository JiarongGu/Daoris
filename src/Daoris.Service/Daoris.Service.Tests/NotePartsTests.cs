using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A session note's parts on the service's side (LANG1a, D142 point 2; the language design §3): the ledger's own codes held to
/// both catalogues, the store's rule for a note written with or without parts, the ledger's lines added after a record's
/// parts, and every string in them cleaned where the note is.
/// </summary>
public sealed class NotePartsTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(_quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private const string Coded =
        """[{"code":"ended.parked-asked","values":{},"text":"It stopped with its quest still taken, to ask you:"},{"words":"Which port?","by":"agent"}]""";

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    private static JsonElement Catalogue(string language)
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new JsonObject();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(File.ReadAllText(file))!.AsObject()) merged[key] = value?.DeepClone();
        }

        return JsonDocument.Parse(merged.ToJsonString()).RootElement.Clone();
    }

    private static IReadOnlyList<(string? Code, string? Text, string? Words, string? By)> Parts(string? json)
    {
        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json);
        return
        [
            .. document.RootElement.EnumerateArray().Select(part => (
                part.TryGetProperty("code", out var code) ? code.GetString() : null,
                part.TryGetProperty("text", out var text) ? text.GetString() : null,
                part.TryGetProperty("words", out var words) ? words.GetString() : null,
                part.TryGetProperty("by", out var by) ? by.GetString() : null)),
        ];
    }

    private static string? Value(string json, int index, string name)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement[index].GetProperty("values").GetProperty(name).GetString();
    }

    /// <summary>Each coded part's text, and each words part's words, inside the note it was built into.</summary>
    private static void Holds(string? note, string? parts)
    {
        foreach (var (code, text, words, _) in Parts(parts))
        {
            Assert.Contains(code is null ? words! : text!, note!);
            if (code is not null) Assert.Contains(LedgerNoteCodes.All.Select(c => c.Code).Append("ended.parked-asked"), c => c == code);
        }
    }

    // ——— The ledger's codes, held to both catalogues (the design §5).

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_ledger_code_has_an_entry_that_says_each_of_its_values_and_no_other(string language)
    {
        var catalogue = Catalogue(language);
        Assert.NotEmpty(LedgerNoteCodes.All);
        foreach (var code in LedgerNoteCodes.All)
        {
            Assert.True(
                catalogue.TryGetProperty(code.CatalogueKey, out var entry) && !string.IsNullOrWhiteSpace(entry.GetString()),
                $"`{code.CatalogueKey}` has no entry in {language}: the line would reach the person as its English.");
            var said = Regex.Matches(entry.GetString()!, @"\{\{\s*([\w.]+)[^}]*\}\}").Select(match => match.Groups[1].Value).ToHashSet();
            Assert.True(said.SetEquals(code.Values), $"{language}: `{code.CatalogueKey}` says {{{string.Join(", ", said)}}}, and `{code.Code}` carries {{{string.Join(", ", code.Values)}}}.");
        }
    }

    /// <summary>The web's twin test parses this file one declaration per line (<c>locales/note.test.ts</c>): each is in its form.</summary>
    [Fact]
    public void Every_ledger_code_is_declared_once_in_the_form_the_page_s_test_parses()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core", "NoteParts.cs"));
        var parsed = Regex.Matches(source, @"new\(""(?<code>[a-z][a-z.-]*)"", \[(?<values>[^\]]*)\]\)").Select(match => match.Groups["code"].Value);

        Assert.Equal(LedgerNoteCodes.All.Select(code => code.Code).Order(StringComparer.Ordinal), parsed.Order(StringComparer.Ordinal));
        Assert.Equal(LedgerNoteCodes.All.Count, LedgerNoteCodes.All.Select(code => code.Code).Distinct().Count());
        Assert.Throws<ArgumentException>(() => NoteLine.Coded(LedgerNoteCodes.Answered, "Answered:", ("at", "x")));
    }

    // ——— What a door gives, kept as the store keeps it.

    [Fact]
    public void A_door_s_parts_are_kept_as_what_a_part_is_and_nothing_else()
    {
        using var given = JsonDocument.Parse(
            """[{"code":"ended.exit","values":{"exit":3},"text":"exit 3","extra":true},{"words":"w","by":"agent"},{"nothing":1},7]""");

        var kept = NoteParts.Normalize(given.RootElement)!;

        Assert.Equal("""[{"code":"ended.exit","values":{"exit":3},"text":"exit 3"},{"words":"w","by":"agent"}]""", kept);
        Assert.Null(NoteParts.Normalize(null));
        using var none = JsonDocument.Parse("""[{"nothing":1}]""");
        Assert.Null(NoteParts.Normalize(none.RootElement));
    }

    [Fact]
    public void Every_string_in_the_parts_is_cleaned_as_the_note_is_where_they_leave_the_machine()
    {
        var tree = string.Concat("C:", "/somewhere/private/tree");
        var session = new Session("s1", "q1", "Owner", "stub", SessionState.Failed, null, null, null, Now, Now, Profile: "janes-own-account", Tree: tree);
        var parts = $$"""
            [{"code":"started.tree","values":{"branch":"daoris/s1","basedOn":"main"},"text":"opened a session tree at {{tree}} on `daoris/s1`"},
             {"code":"account.refused","values":{"owner":"claude-code"},"text":"Its provider refused the `claude-code` account `janes-own-account` (401)."},
             {"words":"Could not find {{tree}}/x.log","by":"program"}]
            """;

        var cleaned = NoteParts.ForAnotherMachine(parts, session)!;

        Assert.DoesNotContain("somewhere", cleaned);
        Assert.DoesNotContain("janes-own-account", cleaned);
        Assert.Contains("daoris/s1", cleaned);
        Assert.Contains("\"owner\":\"claude-code\"", cleaned);
        Assert.Equal(["started.tree", "account.refused", null], Parts(cleaned).Select(part => part.Code));
    }

    // ——— The store's rule, through the ledger's move.

    /// <summary>A session brought to working on a quest it took, in a tree of its own so a test may hold two.</summary>
    private async Task<Session> WorkingSession(string tree = "/trees/owner")
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", $"Do it in {tree}", "Why.", Now);
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now, tree: tree);
        foreach (var state in new[] { "starting", "working" }) await _ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, Now);
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);
        return opened.Session!;
    }

    [Fact]
    public async Task A_note_moved_with_parts_keeps_them_one_without_clears_them_and_a_move_without_a_note_keeps_both()
    {
        var session = await WorkingSession();
        const string Note = "It stopped with its quest still taken, to ask you:\n\nWhich port?";

        await _ledger.AdvanceAsync(session.Id, "awaiting-person", Note, null, null, Now, noteParts: Coded);
        var parked = (await _sessions.FindAsync(session.Id))!;
        Assert.Equal((Note, Coded), (parked.Note, parked.NoteParts));

        // A move with no note keeps both, as the store keeps a note today.
        await _ledger.AdvanceAsync(session.Id, "working", null, null, null, Now);
        Assert.Equal((Note, Coded), ((await _sessions.FindAsync(session.Id))!.Note, (await _sessions.FindAsync(session.Id))!.NoteParts));

        // An older driver's note, without parts, never sits beside stale ones.
        await _ledger.AdvanceAsync(session.Id, "failed", "exit 2 with the quest still taken.", null, null, Now);
        var failed = (await _sessions.FindAsync(session.Id))!;
        Assert.Equal(("exit 2 with the quest still taken.", null), (failed.Note, failed.NoteParts));
    }

    // ——— The ledger's own lines, after the record's parts: rows 64–66.

    [Fact]
    public async Task An_answer_follows_the_record_s_parts_with_its_own_lead_in_and_the_person_s_words()
    {
        var session = await WorkingSession();
        const string Note = "It stopped with its quest still taken, to ask you:\n\nWhich port?";
        await _ledger.AdvanceAsync(session.Id, "awaiting-person", Note, null, null, Now, noteParts: Coded);

        var answered = (await _ledger.AnswerAsync(session.Id, "8080", Now.AddMinutes(1))).Session!;

        Assert.Equal(Note + "\n\nAnswered: 8080", answered.Note);
        Holds(answered.Note, answered.NoteParts);
        Assert.Equal(
            [("ended.parked-asked", (string?)null), (null, "agent"), ("ledger.answered", null), (null, "person")],
            Parts(answered.NoteParts).Select(part => (part.Code, part.By)));
        Assert.Equal("8080", Parts(answered.NoteParts)[3].Words);
    }

    [Fact]
    public async Task An_answer_to_a_park_that_said_nothing_says_the_ledger_s_line_and_one_from_before_carries_its_note_whole()
    {
        var silent = await WorkingSession();
        await _ledger.AdvanceAsync(silent.Id, "awaiting-person", null, null, null, Now);
        var answered = (await _ledger.AnswerAsync(silent.Id, null, Now.AddMinutes(1))).Session!;
        Assert.Equal("It stopped to ask the person; its question is in its transcript.\n\nAnswered: carry on.", answered.Note);
        Holds(answered.Note, answered.NoteParts);
        Assert.Equal(["ledger.parked", "ledger.answered", null], Parts(answered.NoteParts).Select(part => part.Code));

        var older = await WorkingSession(tree: "/trees/owner-s2");
        await _ledger.AdvanceAsync(older.Id, "awaiting-person", "an older driver's question.", null, null, Now);
        var said = (await _ledger.SayAsync(older.Id, "yes", null, Now.AddMinutes(1))).Session!;
        Assert.Equal("an older driver's question.\n\nAnswered: yes", said.Note);
        Assert.Equal(
            [(null, "before"), ("ledger.answered", null), (null, "person")],
            Parts(said.NoteParts).Select(part => (part.Code, part.By)));
    }

    [Fact]
    public async Task Going_on_says_when_as_a_moment_after_the_record_s_parts_and_before_the_driver_s()
    {
        var session = await WorkingSession();
        await _ledger.AdvanceAsync(session.Id, "completed", "the quest reached done.", null, null, Now,
            noteParts: """[{"code":"ended.done","values":{},"text":"the quest reached done."}]""");
        await _ledger.SayAsync(session.Id, "Also the changelog.", null, Now.AddMinutes(20));

        const string GoingOn = "It goes on with your words in its own conversation, in the tree it worked in.";
        var went = (await _ledger.AdvanceAsync(
            session.Id, "working", GoingOn, null, null, Now.AddMinutes(30),
            noteParts: $$"""[{"code":"working.goes-on","values":{},"text":"{{GoingOn}}"}]""")).Session!;

        Assert.Equal($"the quest reached done.\n\nWent on with your words at 2026-10-03 10:30 UTC.\n\n{GoingOn}", went.Note);
        Assert.Equal(["ended.done", "ledger.went-on", "working.goes-on"], Parts(went.NoteParts).Select(part => part.Code));
        Assert.Equal("2026-10-03T10:30:00Z", Value(went.NoteParts!, 1, "at"));
    }

    [Fact]
    public void Going_on_from_a_record_and_a_note_from_before_parts_carries_each_whole()
    {
        var session = new Session("s1", "q1", "Owner", "stub", SessionState.Completed, "an older note.", null, null, Now, Now);

        var (note, parts) = SessionLedger.WentOnNote(session, "an older driver's line.", null, Now);

        Assert.Equal("an older note.\n\nWent on with your words at 2026-10-03 10:00 UTC.\n\nan older driver's line.", note);
        Assert.Equal(
            [(null, "before"), ("ledger.went-on", null), (null, "before")],
            Parts(parts).Select(part => (part.Code, part.By)));
        Holds(note, parts);
    }

    // ——— The wire carries them, a record from before carries none.

    [Fact]
    public void The_wire_carries_the_parts_both_ways_and_a_record_from_before_carries_none()
    {
        var feed = SessionWire.Feed(
        [
            new FedSessionRecord("s1", "q1", "Owner", "stub", "awaiting-person", "x", null, Now, Now, NoteParts: Coded),
            new FedSessionRecord("s2", "q2", "Owner", "stub", "completed", "an older note.", null, Now, Now),
        ]);

        var read = SessionWire.ReadFeed(feed)!;

        Assert.Equal(Coded, read[0].NoteParts);
        Assert.Null(read[1].NoteParts);

        var page = SessionWire.ReadPage(SessionWire.Page(new SessionFetch(
            [new Session("a@one/s1", "q1", "Owner", "stub", SessionState.Completed, "x", null, null, Now, Now) { Origin = "a@one", NoteParts = Coded }],
            1, false)))!;
        Assert.Equal(Coded, page.Records.Single().NoteParts);
    }
}
