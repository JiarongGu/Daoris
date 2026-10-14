using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSUX1j (D126 §9, the session management design §6): a quest carries a short title, the few words that tell it apart
/// in a list. Whoever publishes it may give one, at most 40 characters on one line, kept with the quest and travelling in
/// its publish operation. Where none is given, its name is derived from its own words, never cut mid-word, a note line
/// skipped and a ticket key leading, and never written into the record as the publisher's.
/// </summary>
public sealed class QuestShortTitleTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-short-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-05T09:00:00Z");

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "engine", "game" })
        {
            var dir = Path.Combine(_root, "family", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""
                { "source": "s", "packs": [], "domain": { "summary": "the {{name}}", "owns": ["o"], "accepts": ["a"] } }
                """);
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(_service, _quests, files: files, asks: _asks);
        _desk = new AskDesk(_service, _asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<QuestPublishOutcome> Publish(string title, string? shortTitle, string body = "It runs unbounded; here is the trace.") =>
        _exchange.PublishAsync(new QuestAsk("game", "engine", title, body) { Short = shortTitle }, Now);

    // ─── The derivation ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_title_that_fits_is_its_own_name()
    {
        Assert.Equal("Cap the frame's work", QuestTitles.Derive("Cap the frame's work", "It runs unbounded."));
    }

    /// <summary>Whole words up to the limit, and an ellipsis saying more follows: never a word cut in half.</summary>
    [Fact]
    public void A_long_title_is_cut_at_a_word_and_says_so()
    {
        var name = QuestTitles.Derive("Continue the production half of the ticket once the dev half has landed", "");

        Assert.Equal("Continue the production half of the…", name);
        Assert.True(name.Length <= QuestTitles.MaxShort);
    }

    /// <summary>
    /// The install's re-filed asks put a note on their first line, which every list then showed. A line that is wholly a
    /// note, in brackets, is not what the quest asks: the name is taken from the first line that is.
    /// </summary>
    [Fact]
    public void A_note_line_is_skipped_for_the_first_line_that_says_what_is_wanted()
    {
        const string Note = "(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)";
        var name = QuestTitles.Derive(Note, $"{Note}\ncontinue the prod half of the ticket (the owner, 2026-10-04: …)\nWhere it stands: the dev half is done.");

        Assert.Equal("continue the prod half of the ticket…", name);
    }

    [Fact]
    public void A_leading_note_on_the_line_itself_is_set_aside()
    {
        Assert.Equal("Fix the login crash", QuestTitles.Derive("[re-filed] Fix the login crash", ""));
    }

    /// <summary>A ticket key in the ask leads the name, so two quests on one ticket's work read as that ticket's.</summary>
    [Theory]
    [InlineData("Continue the production half", "Per TK-2203, the dev half is done.", "TK-2203 Continue the production half")]
    [InlineData("TK-2203: continue the production half", "", "TK-2203: continue the production half")]
    [InlineData("Fix the crash reported in TK-2203 last week by support", "", "TK-2203 Fix the crash reported in last…")]
    public void A_ticket_key_leads_the_name(string title, string body, string expected)
    {
        var name = QuestTitles.Derive(title, body);

        Assert.Equal(expected, name);
        Assert.True(name.Length <= QuestTitles.MaxShort);
    }

    /// <summary>A standard's name is spelled as a key is, and is not a ticket: nothing leads.</summary>
    [Fact]
    public void A_standards_name_is_not_a_ticket_key()
    {
        Assert.Equal("Read every log as UTF-8", QuestTitles.Derive("Read every log as UTF-8", "Hashes are SHA-256."));
    }

    /// <summary>Chinese has no spaces: a character is the smallest cut, and each counts twice against the width.</summary>
    [Fact]
    public void Chinese_is_cut_at_a_character_and_counted_as_wide()
    {
        var name = QuestTitles.Derive("继续完成工单的生产部分，开发部分已经合并到主干上了", "");

        Assert.Equal("继续完成工单的生产部分，开发部分已经合…", name);
    }

    /// <summary>
    /// SHORTFIT1: a verb and then a branch name too long to fit whole named the quest by the verb alone (*Create…*). Where
    /// a name would keep a single word, the next is cut by its characters, at the last joiner that fits, and never ends on
    /// the joiner.
    /// </summary>
    [Theory]
    [InlineData("Create release/delta-northwind-and-harbor from master with three branches merged (no push)", "Create release/delta-northwind-and…")]
    [InlineData("Create release/northwindharborandcoastline from master", "Create release…")]
    [InlineData("Merge feature_northwind_harbor_coastline_docs into main", "Merge feature_northwind_harbor…")]
    public void A_long_word_after_a_single_one_is_cut_at_its_last_joiner_that_fits(string title, string expected)
    {
        var name = QuestTitles.Derive(title, "");

        Assert.Equal(expected, name);
        Assert.True(name.Length <= QuestTitles.MaxShort);
    }

    /// <summary>SHORTFIT1: with no joiner inside the room, the next word is cut by its characters, up to the width.</summary>
    [Theory]
    [InlineData("Rename TheConfigurationLoaderFactoryProviderService everywhere")]
    [InlineData("Rename TheConfigurationLoaderFactoryProvider-Service everywhere")]
    public void A_long_word_with_no_joiner_that_fits_is_cut_by_its_characters(string title)
    {
        var name = QuestTitles.Derive(title, "");

        Assert.Equal("Rename TheConfigurationLoaderFactoryPro…", name);
        Assert.Equal(QuestTitles.MaxShort, name.Length);
    }

    [Fact]
    public void A_title_of_nothing_but_a_note_keeps_its_words()
    {
        Assert.Equal("(fix it)", QuestTitles.Derive("(fix it)", "(fix it)"));
    }

    // ─── What a publisher gives ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Frame cap  ", "Frame cap")]
    public void A_given_short_title_is_kept_trimmed_and_a_blank_one_is_none(string? given, string? kept)
    {
        var (shortTitle, refusal) = QuestTitles.Judge(given);

        Assert.Null(refusal);
        Assert.Equal(kept, shortTitle);
    }

    [Theory]
    [InlineData("A short title that runs well past the forty characters a list gives it")]
    [InlineData("Two\nlines")]
    public void A_short_title_past_forty_characters_or_over_a_line_break_is_refused(string given)
    {
        var (_, refusal) = QuestTitles.Judge(given);

        Assert.NotNull(refusal);
        Assert.Contains("40 characters", refusal);
    }

    // ─── Kept, replayed and carried ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_publishers_short_title_is_kept_with_the_quest_and_names_it()
    {
        var outcome = await Publish("Cap the frame's work so the editor stays responsive", "Frame cap");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal("Frame cap", outcome.Quest!.Short);
        Assert.Equal("Frame cap", outcome.Quest.Name);

        var held = Assert.Single(await _quests.ListAsync(receiver: "engine"));
        Assert.Equal("Frame cap", held.Short);
        Assert.Equal("Cap the frame's work so the editor stays responsive", held.Title);
    }

    /// <summary>A derived name is the quest's words, read when asked: the record keeps only what a publisher gave.</summary>
    [Fact]
    public async Task With_none_given_the_name_is_derived_and_the_record_holds_none()
    {
        var quest = (await Publish("Cap the frame's work so the editor stays responsive", null)).Quest!;

        Assert.Null(quest.Short);
        Assert.Equal("Cap the frame's work so the editor…", quest.Name);
        Assert.DoesNotContain("\"short\"", QuestWire.Push(0, await _quests.HistoryAsync(quest.Id)));
    }

    [Fact]
    public async Task A_short_title_past_forty_characters_is_refused_at_the_exchange_and_nothing_is_kept()
    {
        var outcome = await Publish("Cap the frame's work", "A short title that runs well past the forty characters a list gives it");

        Assert.Equal(QuestPublishRefusal.BadShortTitle, outcome.Refusal);
        Assert.Contains("40 characters", outcome.Message);
        Assert.Empty(await _quests.ListAsync(receiver: "engine"));
    }

    /// <summary>The short title is the publisher's words, not the quest's identity: the same ask with another is the same quest.</summary>
    [Fact]
    public async Task The_id_does_not_change_with_the_short_title()
    {
        var first = (await Publish("Cap the frame's work", "Frame cap")).Quest!;
        var again = (await Publish("Cap the frame's work", "Another")).Quest!;

        Assert.Equal(first.Id, again.Id);
        Assert.Equal("Frame cap", again.Short);
    }

    [Fact]
    public async Task A_short_title_is_in_the_quests_history_and_crosses_the_wire()
    {
        var quest = (await Publish("Cap the frame's work so the editor stays responsive", "Frame cap")).Quest!;
        var history = await _quests.HistoryAsync(quest.Id);

        Assert.Equal("Frame cap", QuestLog.Replay(history)!.Short);
        var pushed = QuestWire.ReadPush(QuestWire.Push(0, history))!.Value;
        Assert.Equal("Frame cap", pushed.Operations[0].Published!.Short);
    }

    /// <summary>
    /// A store from before the short title has no column: it gains one, its quests have none, and each is named from its
    /// own words.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_short_title_opens_and_its_quests_are_named_from_their_words()
    {
        await using var older = new SqliteConnection("Data Source=:memory:");
        await older.OpenAsync();
        var store = await QuestStore.OpenAsync(older);
        await store.PublishAsync("game", "engine", "Cap the frame's work so the editor stays responsive", "b", Now);
        await using (var drop = older.CreateCommand())
        {
            drop.CommandText = "ALTER TABLE quests DROP COLUMN short_title";
            await drop.ExecuteNonQueryAsync();
        }

        var reopened = await QuestStore.OpenAsync(older);
        var quest = Assert.Single(await reopened.ListAsync(receiver: "engine"));

        Assert.Null(quest.Short);
        Assert.Equal("Cap the frame's work so the editor…", quest.Name);
    }

    [Fact]
    public async Task A_remote_refuses_a_pushed_quest_whose_short_title_no_publish_would_keep()
    {
        var registered = await _service.RegistryAsync();
        var asked = new Quest("abcdefabcdef", "game", "engine", "t", "b", QuestStatus.Open, null, Now, Now);

        Assert.Null(_exchange.JudgeReceived(asked with { Short = "Frame cap" }, registered));
        Assert.Contains("40 characters", _exchange.JudgeReceived(asked with { Short = "two\nlines" }, registered));
    }

    // ─── From an ask ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The intake gives the quest its short title in its draft, beside its own title and body.</summary>
    [Fact]
    public async Task An_intakes_draft_carries_its_short_title()
    {
        var ask = (await _desk.AskAsync(new AskRequest("default", "cap the frame's work, the editor stalls"), Now)).Ask!;
        var published = await _desk.PublishAsync(
            ask.Id, "engine", Now, draft: new AskDraft("Cap the frame's work", "The editor stalls.") { Short = "Frame cap" });

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal("Frame cap", published.Quest!.Short);
    }

    /// <summary>
    /// A re-filed ask with its note on the first line: nothing gave a short title, and the quest's name skips the note,
    /// leading with the ticket key the ask names.
    /// </summary>
    [Fact]
    public async Task A_refiled_asks_quest_is_named_past_its_note_with_its_ticket_key()
    {
        var ask = (await _desk.AskAsync(new AskRequest(
            "default",
            "(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)\n"
            + "continue the prod half of TK-2203\nWhere it stands: the dev half is done.")
        {
            To = "engine",
        }, Now)).Ask!;

        var quest = Assert.Single(await _quests.ListAsync(receiver: "engine"));
        Assert.Equal(AskState.Published, ask.State);
        Assert.Null(quest.Short);
        Assert.Equal("TK-2203 continue the prod half of", quest.Name);
    }
}
