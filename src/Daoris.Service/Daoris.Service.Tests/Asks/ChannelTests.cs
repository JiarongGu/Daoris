using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSONE2a (D167 points 6 to 8; the co-working design §2.1, §2.2, §2.5 and §2.6): an ask's channel is service records, one row per
/// message, with a read mark per entry; Daoris's lines are read from the ask's quests and never stored; a refusal is a code.
/// </summary>
public sealed class ChannelTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-channel-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _asks = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private ChannelDesk _channel = null!;
    private ChannelStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-14T09:00:00Z");

    private const string Sentence = "the report needs the v3 bridge, and the bridge needs a column";

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var askStore = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files);
        _asks = new AskDesk(service, askStore, _exchange, files);
        _ledger = new SessionLedger(_quests, _sessions, service, askStore);
        _store = await ChannelStore.OpenAsync(_connection);
        _channel = new ChannelDesk(_store, _asks, askStore, _ledger, _quests, new StoreEntryFacts(_quests, _sessions));
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

    /// <summary>An ask published to <c>reports</c> at once, and a session running its quest: that session's entry is the ask's.</summary>
    private async Task<(Ask Ask, Quest Quest, Session Session)> AskedAsync(string sentence = Sentence)
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", sentence) { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", sentence.Length.ToString()))).Session!;
        return (outcome.Ask!, outcome.Quest, session);
    }

    /// <summary>🔴 A session's post is read back with who posted it, its entry and its words as written; with no <c>to</c> it is to everyone.</summary>
    [Fact]
    public async Task A_sessions_post_is_kept_with_its_poster_and_read_back()
    {
        var (ask, _, session) = await AskedAsync();

        var posted = await _channel.PostAsSessionAsync(session.Id, "  the bridge wants a `region` column  ", null, null, false, Now.AddMinutes(1));

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        var message = posted.Message!;
        Assert.Equal(ask.Id, message.Ask);
        Assert.Equal(session.Id, message.Session);
        Assert.Equal("reports", message.Repository);
        Assert.Equal("stub", message.Agent);
        Assert.Equal($"ask:{ask.Id}/reports", message.Entry);
        Assert.Null(message.To);
        Assert.False(message.ByPerson);
        Assert.Equal("the bridge wants a `region` column", message.Text);
        var read = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, since: null, moveMark: false))!;
        Assert.Equal([message.Id], read.Messages.Select(m => m.Id));
    }

    /// <summary>🔴 The person's post is kept on the channel, by no session, and on the ask as a word of the <c>channel</c> kind.</summary>
    [Fact]
    public async Task The_persons_post_is_kept_on_the_ask_as_a_channel_word()
    {
        var (ask, _, _) = await AskedAsync();

        var posted = await _channel.PostAsPersonAsync(ask.Id, "use the older bridge", "reports", null, Now.AddMinutes(2));

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        Assert.True(posted.Message!.ByPerson);
        Assert.Equal("reports", posted.Message.To);
        var word = Assert.Single((await _asks.FindAsync(ask.Id))!.Later);
        Assert.Equal(new AskWord(AskWordKind.Channel, "use the older bridge", Now.AddMinutes(2)), word);
        Assert.Equal("channel", AskWord.Spell(AskWordKind.Channel));
    }

    /// <summary>🔴 Each refusal is a code: no words, words past the bound, no ask, a helper, an unknown ask, a repository the ask does not reach, an answer to nothing.</summary>
    [Fact]
    public async Task A_refusal_is_a_code()
    {
        var (ask, _, session) = await AskedAsync();
        var chat =(await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var helper = await _sessions.CreateAsync(
            null, "reports", "stub", Now, kind: SessionKind.Chat, serves: session.Id, ownEntry: true);

        Assert.Equal(ChannelRefusal.Empty, (await _channel.PostAsSessionAsync(session.Id, "  \n ", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.Empty, (await _channel.PostAsSessionAsync(session.Id, null, null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.TooLong, (await _channel.PostAsSessionAsync(session.Id, new string('x', ChannelBounds.MaxText + 1), null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.None, (await _channel.PostAsSessionAsync(session.Id, new string('x', ChannelBounds.MaxText), null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NoSession, (await _channel.PostAsSessionAsync(null, "hello", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NoSession, (await _channel.PostAsSessionAsync("nosuch1", "hello", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NoAsk, (await _channel.PostAsSessionAsync(chat.Id, "hello", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.Helper, (await _channel.PostAsSessionAsync(helper.Id, "hello", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadTo, (await _channel.PostAsSessionAsync(session.Id, "hello", "nowhere", null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadRe, (await _channel.PostAsSessionAsync(session.Id, "hello", null, "nosuchid", false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NotFound, (await _channel.PostAsPersonAsync("ffffff", "hello", null, null, Now)).Refusal);
        Assert.Equal(ChannelRefusal.Empty, (await _channel.PostAsPersonAsync(ask.Id, " ", null, null, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadTo, (await _channel.PostAsPersonAsync(ask.Id, "hello", "checker", null, Now)).Refusal);
    }

    /// <summary>A message may be addressed to one repository the ask reaches, by the name the registry gives it, however it is cased.</summary>
    [Fact]
    public async Task A_message_may_be_addressed_to_one_of_the_asks_repositories()
    {
        var (ask, _, session) = await AskedAsync();
        // A quest the session published reaches `checker` on the ask (design §1.2 rule 4).
        await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the bridge", "Please.") { PublishedBy = session.Id }, Now);

        var to = await _channel.PostAsSessionAsync(session.Id, "for you", "CHECKER", null, false, Now.AddMinutes(1));

        Assert.Equal(ChannelRefusal.None, to.Refusal);
        Assert.Equal("checker", to.Message!.To);
        Assert.Equal(ChannelRefusal.None, (await _channel.PostAsPersonAsync(ask.Id, "for all", "everyone", null, Now.AddMinutes(2))).Refusal);
    }

    /// <summary>A question is open until a message answers it by <c>re</c>.</summary>
    [Fact]
    public async Task A_question_is_open_until_a_message_answers_it()
    {
        var (ask, _, session) = await AskedAsync();
        var question = (await _channel.PostAsSessionAsync(session.Id, "which column?", null, null, true, Now.AddMinutes(1))).Message!;

        Assert.True(Assert.Single((await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Messages).Open);

        var answer = await _channel.PostAsPersonAsync(ask.Id, "region", null, question.Id, Now.AddMinutes(2));

        Assert.Equal(question.Id, answer.Message!.Re);
        var read = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Messages;
        Assert.False(read.Single(m => m.Id == question.Id).Open);
        Assert.False(read.Single(m => m.Id == answer.Message.Id).Open);
    }

    /// <summary>A part posts at most <see cref="ChannelBounds.MaxPerPart"/> messages; another part still posts.</summary>
    [Fact]
    public async Task A_part_posts_at_most_ten_messages()
    {
        var (ask, _, session) = await AskedAsync();
        for (var n = 0; n < ChannelBounds.MaxPerPart; n++)
        {
            Assert.Equal(ChannelRefusal.None, (await _channel.PostAsSessionAsync(session.Id, $"note {n}", null, null, false, Now.AddMinutes(n))).Refusal);
        }

        Assert.Equal(ChannelRefusal.TooMany, (await _channel.PostAsSessionAsync(session.Id, "one more", null, null, false, Now.AddHours(1))).Refusal);

        // The cap is the part's, and the person has none.
        Assert.Equal(ChannelRefusal.None, (await _channel.PostAsPersonAsync(ask.Id, "and me", null, null, Now.AddHours(1))).Refusal);
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §9, trap 10): every message kept from now says its level. A part's post is urgent on
    /// its word for at most <see cref="ChannelBounds.UrgentPerPart"/> of its posts, judged in the insert's own statement, so its
    /// fourth urgent post is kept ordinary; an ordinary post is an explicit false, never absent, which is a message from before.
    /// </summary>
    [Fact]
    public async Task A_parts_fourth_urgent_post_is_kept_ordinary_and_every_post_says_its_level()
    {
        var (ask, _, session) = await AskedAsync();

        var urgent = new List<ChannelMessage>();
        for (var n = 0; n <= ChannelBounds.UrgentPerPart; n++)
        {
            urgent.Add((await _channel.PostAsSessionAsync(session.Id, $"urgent {n}", null, null, false, Now.AddMinutes(n), urgent: true)).Message!);
        }

        var ordinary = (await _channel.PostAsSessionAsync(session.Id, "ordinary", null, null, false, Now.AddMinutes(9))).Message!;

        Assert.Equal<bool?>([true, true, true, false], urgent.Select(m => m.Urgent));
        Assert.Equal(false, ordinary.Urgent);
        var read = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Messages;
        Assert.Equal<bool?>([true, true, true, false, false], read.Select(m => m.Urgent));
    }

    /// <summary>The person's message is kept at their default, urgent, or ordinary where they asked; a mirrored one from before says none.</summary>
    [Fact]
    public async Task The_persons_post_is_urgent_unless_asked_ordinary_and_one_from_before_says_no_level()
    {
        var (ask, _, _) = await AskedAsync();

        var urgent = (await _channel.PostAsPersonAsync(ask.Id, "now please", null, null, Now.AddMinutes(1))).Message!;
        var ordinary = (await _channel.PostAsPersonAsync(ask.Id, "when you can", null, null, Now.AddMinutes(2), urgent: false)).Message!;
        await _store.MirrorAsync(new ChannelMessage(
            "old1", "elsewhere1", "work", Now, "a@two/s9", "reports", "stub", "ask:elsewhere1/reports", null, null, false, "from before",
            Origin: "a@two"));

        Assert.Equal((true, false), (urgent.Urgent, ordinary.Urgent));
        Assert.Null(Assert.Single(await _store.OfAsync("elsewhere1")).Urgent);
    }

    // ——— COWORK1d: the entry's acknowledgments, by message id (the message-urgency design §6, §9).

    /// <summary>
    /// 🔴 Trap 21: one part of an entry claims a message, in one statement; another part of the same entry takes nothing while the
    /// first runs and holds it, and claims it once the first lets it go.
    /// </summary>
    [Fact]
    public async Task One_part_of_an_entry_claims_a_message_and_another_takes_nothing_until_it_lets_go()
    {
        var (ask, _, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var id = (await _channel.PostAsPersonAsync(ask.Id, "use the older bridge", "reports", null, Now.AddMinutes(1))).Message!.Id;

        Assert.Equal([id], await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taking, [id], Now.AddMinutes(2)));
        Assert.Empty((await _channel.AcknowledgeAsync(ask.Id, entry, "other1", ChannelAcks.Taking, [id], Now.AddMinutes(2)))!);

        Assert.Equal([id], await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Released, [id], Now.AddMinutes(3)));
        Assert.Equal([id], await _channel.AcknowledgeAsync(ask.Id, entry, "other1", ChannelAcks.Taking, [id], Now.AddMinutes(4)));
        Assert.Null(await _channel.AcknowledgeAsync("ffffff", entry, session.Id, ChannelAcks.Taking, [id], Now));
    }

    /// <summary>
    /// 🔴 Trap 22: a message a start's packet handed partly is handed again to a new part, never to the part that had it; a receipt
    /// never moves back, so a late <c>sent</c> leaves <c>taken</c> as it was.
    /// </summary>
    [Fact]
    public async Task A_message_handed_partly_goes_again_to_a_new_part_and_a_receipt_never_moves_back()
    {
        var (ask, _, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var id = (await _channel.PostAsPersonAsync(ask.Id, "long words", null, null, Now.AddMinutes(1))).Message!.Id;

        await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Partly, [id], Now.AddMinutes(2));
        Assert.Empty((await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taking, [id], Now.AddMinutes(3)))!);
        Assert.Equal([id], await _channel.AcknowledgeAsync(ask.Id, entry, "next1", ChannelAcks.Taking, [id], Now.AddMinutes(3)));
        await _channel.AcknowledgeAsync(ask.Id, entry, "next1", ChannelAcks.Taken, [id], Now.AddMinutes(4));
        Assert.Empty((await _channel.AcknowledgeAsync(ask.Id, entry, "next1", ChannelAcks.Sent, [id], Now.AddMinutes(5)))!);

        var read = (await _channel.ReadAsync(ask.Id, entry, null, moveMark: false))!;
        Assert.Equal((ChannelAcks.Taken, "next1"), (read.Acknowledged[id].State, read.Acknowledged[id].Session));
    }

    /// <summary>
    /// 🔴 The design §6: a claim left by a part that no longer runs (a restart cut it off) reads to its entry as none and to the person
    /// as waiting, so the entry's next part is handed it and claims it.
    /// </summary>
    [Fact]
    public async Task A_claim_whose_part_no_longer_runs_reads_as_none_and_goes_to_the_next_part()
    {
        var (ask, _, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var id = (await _channel.PostAsPersonAsync(ask.Id, "use the older bridge", "reports", null, Now.AddMinutes(1))).Message!.Id;
        await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taking, [id], Now.AddMinutes(2));
        Assert.Equal(ChannelAcks.Taking, (await _channel.ReadAsync(ask.Id, entry, null, false))!.Acknowledged[id].State);

        await _ledger.AdvanceAsync(session.Id, "failed", null, null, null, Now.AddMinutes(3));

        Assert.False((await _channel.ReadAsync(ask.Id, entry, null, false))!.Acknowledged.ContainsKey(id));
        Assert.Equal(ChannelReceipts.Waiting, Assert.Single((await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Receipts[id]).State);
        Assert.Equal([id], await _channel.AcknowledgeAsync(ask.Id, entry, "next1", ChannelAcks.Taking, [id], Now.AddMinutes(4)));
    }

    /// <summary>The entry's mark, kept for display, moves to before the oldest message for it that it has not acknowledged.</summary>
    [Fact]
    public async Task The_entrys_mark_stays_before_the_oldest_message_it_has_not_acknowledged()
    {
        var (ask, _, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var first = (await _channel.PostAsPersonAsync(ask.Id, "one", null, null, Now.AddMinutes(1))).Message!;
        var second = (await _channel.PostAsPersonAsync(ask.Id, "two", null, null, Now.AddMinutes(2))).Message!;
        await _channel.PostAsPersonAsync(ask.Id, "three", null, null, Now.AddMinutes(3));

        await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taken, [second.Id], Now.AddMinutes(4));
        Assert.True(await _store.MarkAsync(ask.Id, entry) is not { } before || before < first.At);

        await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taken, [first.Id], Now.AddMinutes(4));
        Assert.Equal(second.At, await _store.MarkAsync(ask.Id, entry));
    }

    /// <summary>
    /// 🔴 Traps 9 and 22: a session's own read acknowledges each message for its entry it returned whole, and still returns one a
    /// teammate's machine mirrored late with an older moment than the mark, which a mark alone would have passed.
    /// </summary>
    [Fact]
    public async Task A_sessions_read_acknowledges_what_it_returned_and_still_returns_a_message_mirrored_late()
    {
        var (ask, _, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var first = (await _channel.PostAsPersonAsync(ask.Id, "one", null, null, Now.AddMinutes(2))).Message!;

        Assert.Equal([first.Id], (await _channel.ReadAsSessionAsync(session.Id, null)).Read!.Messages.Select(m => m.Id));
        Assert.Equal(ChannelAcks.Read, (await _channel.ReadAsync(ask.Id, entry, null, false))!.Acknowledged[first.Id].State);

        await _store.MirrorAsync(new ChannelMessage(
            "late1", ask.Id, "work", Now.AddMinutes(1), "b@two/s9", "checker", "stub", $"ask:{ask.Id}/checker", null, null, false,
            "late words", Origin: "b@two") { Urgent = false });

        Assert.Equal(["late1"], (await _channel.ReadAsSessionAsync(session.Id, null)).Read!.Messages.Select(m => m.Id));
        Assert.Empty((await _channel.ReadAsSessionAsync(session.Id, null)).Read!.Messages);
    }

    /// <summary>
    /// 🔴 Trap 15: each message shows, for each repository it addresses, what reached that repository's line of work: waiting, taken,
    /// answered by a reply from it, and not reached once the ask has no quest open and no part of the entry runs here; a part still
    /// running keeps it waiting.
    /// </summary>
    [Fact]
    public async Task A_message_never_taken_is_not_reached_once_the_ask_has_no_quest_open_and_no_part_of_its_entry_runs()
    {
        var (ask, quest, session) = await AskedAsync();
        var entry = $"ask:{ask.Id}/reports";
        var taken = (await _channel.PostAsPersonAsync(ask.Id, "one", null, null, Now.AddMinutes(1))).Message!;
        var waiting = (await _channel.PostAsPersonAsync(ask.Id, "two", "reports", null, Now.AddMinutes(2))).Message!;
        await _channel.AcknowledgeAsync(ask.Id, entry, session.Id, ChannelAcks.Taken, [taken.Id], Now.AddMinutes(3));

        async Task<IReadOnlyDictionary<string, IReadOnlyList<ChannelReceipt>>> ReceiptsAsync() =>
            (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Receipts;

        Assert.Equal([("reports", ChannelReceipts.Taken)], (await ReceiptsAsync())[taken.Id].Select(r => (r.Repository, r.State)));
        Assert.Equal([("reports", ChannelReceipts.Waiting)], (await ReceiptsAsync())[waiting.Id].Select(r => (r.Repository, r.State)));

        await _channel.PostAsSessionAsync(session.Id, "done that", null, taken.Id, false, Now.AddMinutes(3));
        Assert.Equal(ChannelReceipts.Answered, Assert.Single((await ReceiptsAsync())[taken.Id]).State);

        // Its quest closes while its part still runs: the message still waits for that part.
        await _exchange.RespondAsync(quest.Id, "decline", "not ours", Now.AddMinutes(4));
        Assert.Equal(ChannelReceipts.Waiting, Assert.Single((await ReceiptsAsync())[waiting.Id]).State);

        await _ledger.AdvanceAsync(session.Id, "failed", null, null, null, Now.AddMinutes(5));
        Assert.Equal(ChannelReceipts.NotReached, Assert.Single((await ReceiptsAsync())[waiting.Id]).State);
        Assert.Equal(ChannelReceipts.Answered, Assert.Single((await ReceiptsAsync())[taken.Id]).State);
    }

    /// <summary>
    /// 🔴 Trap 5 (the message-urgency design §4.2, §9): a claim the person passed on is kept on an ended part's record by the say door as
    /// another agent's words, <c>from: agent</c> with its channel message, never the person's; the person's own channel message, kept
    /// with its channel alone, is still theirs. Authorship is never read from the channel.
    /// </summary>
    [Fact]
    public async Task A_claim_the_person_passed_on_is_kept_as_an_agents_and_the_persons_own_channel_message_as_theirs()
    {
        var (_, _, session) = await AskedAsync();
        await _ledger.AdvanceAsync(session.Id, "failed", null, null, null, Now.AddMinutes(1));

        var passed = await _ledger.SayAsync(session.Id, "Another session says: the bridge wants a column.", null, Now.AddMinutes(2), default, channel: "m1", from: SaidFrom.Agent);
        var own = await _ledger.SayAsync(session.Id, "Use the older bridge.", null, Now.AddMinutes(3), default, channel: "p1");

        Assert.Equal((SessionSayRefusal.None, SessionSayRefusal.None), (passed.Refusal, own.Refusal));
        var said = (await _sessions.FindAsync(session.Id))!.Said;
        Assert.Equal([("m1", "agent", false), ("p1", null, true)], said.Select(word => (word.Channel, word.From, word.Persons)));
    }

    /// <summary>
    /// 🔴 A read moves the entry's mark, so the next read holds only what is new; a second entry has its own mark; the person's entry
    /// is read without moving one unless asked; and <c>since</c> reads from a moment instead.
    /// </summary>
    [Fact]
    public async Task A_read_moves_the_entrys_mark_and_each_entry_has_its_own()
    {
        var (ask, _, session) = await AskedAsync();
        await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the bridge", "Please.") { PublishedBy = session.Id }, Now);
        var checker = (await _ledger.OpenAsync(
            (await _quests.ListAsync(receiver: "checker")).Single().Id, "stub", Now, tree: Path.Combine(_root, "trees", "checker"))).Session!;
        var first = (await _channel.PostAsSessionAsync(session.Id, "first", null, null, false, Now.AddMinutes(1))).Message!;

        var one = (await _channel.ReadAsSessionAsync(session.Id, null))!;
        Assert.Equal(ChannelRefusal.None, one.Refusal);
        Assert.Equal([first.Id], one.Read!.Messages.Select(m => m.Id));

        Assert.Empty((await _channel.ReadAsSessionAsync(session.Id, null))!.Read!.Messages);
        var second = (await _channel.PostAsPersonAsync(ask.Id, "second", null, null, Now.AddMinutes(4))).Message!;
        Assert.Equal([second.Id], (await _channel.ReadAsSessionAsync(session.Id, null))!.Read!.Messages.Select(m => m.Id));

        // The checker's entry has read nothing yet.
        Assert.Equal([first.Id, second.Id], (await _channel.ReadAsSessionAsync(checker.Id, null))!.Read!.Messages.Select(m => m.Id));

        // A `since` reads from that moment, whatever the mark says.
        Assert.Equal([second.Id], (await _channel.ReadAsSessionAsync(session.Id, Now.AddMinutes(2)))!.Read!.Messages.Select(m => m.Id));

        // The person's read counts without moving a mark (the two quests' lines and the two messages), and moves it only when asked to.
        Assert.Equal(4, (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: false))!.Unread);
        Assert.Equal(4, (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: true))!.Unread);
        Assert.Equal(0, (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: false))!.Unread);
    }

    /// <summary>
    /// 🔴 Daoris's lines are read from the ask's quests, by code: published, taken, awaiting and closed done or declined; the quests of
    /// another ask are not on it, and nothing is stored.
    /// </summary>
    [Fact]
    public async Task Daoriss_lines_are_read_from_the_asks_quests_and_never_stored()
    {
        var (ask, quest, session) = await AskedAsync();
        var published = (await _exchange.PublishAsync(
            new QuestAsk("reports", "checker", "Check the bridge", "Please.") { PublishedBy = session.Id }, Now.AddMinutes(1))).Quest!;
        var (other, _, _) = await AskedAsync("a different ask entirely, about the checker pages");

        var open = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Lines;

        Assert.Equal(
            [("published", quest.Id, "ask #" + ask.Id, "reports"), ("published", published.Id, "reports", "checker")],
            open.Select(line => (line.Code, line.Quest, line.From, line.To)));
        Assert.Equal("Check the bridge", open[1].Name);

        await _exchange.RespondAsync(published.Id, "take", null, Now.AddMinutes(2));
        await _exchange.RespondAsync(quest.Id, "decline", "not ours", Now.AddMinutes(3));
        var later = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Lines;
        Assert.Equal(
            [("published", quest.Id, "declined"), ("published", published.Id, "taken")],
            later.GroupBy(line => line.Quest).Select(g => ("published", g.Key, g.Last().Code)).OrderBy(x => x.Item3));
        Assert.DoesNotContain(later, line => line.Quest == (_quests.ListAsync(receiver: "reports").Result.Single(q => q.From == "ask #" + other.Id)).Id);

        // Read in time order, the lines come before a message posted after them and after one posted before.
        var message = (await _channel.PostAsPersonAsync(ask.Id, "between", null, null, Now.AddMinutes(2).AddSeconds(30))).Message!;
        var items = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Items;
        Assert.Equal(message.Id, items.OfType<ChannelMessage>().Single().Id);
        Assert.Equal(items.Select(i => i.At).Order(), items.Select(i => i.At));
    }

    /// <summary>Forgetting an ask's channel removes its messages and its marks, and no other ask's.</summary>
    [Fact]
    public async Task An_asks_channel_is_forgotten_with_the_ask()
    {
        var (ask, _, session) = await AskedAsync();
        var (other, _, otherSession) = await AskedAsync("a different ask entirely, about the checker pages");
        await _channel.PostAsSessionAsync(session.Id, "mine", null, null, false, Now);
        await _channel.PostAsSessionAsync(otherSession.Id, "theirs", null, null, false, Now);
        await _channel.ReadAsSessionAsync(session.Id, null);

        await _channel.ForgetAsync(ask.Id);

        Assert.Empty((await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Messages);
        Assert.Single((await _channel.ReadAsync(other.Id, ChannelDesk.PersonEntry, null, false))!.Messages);
    }

    /// <summary>A read of an ask this machine does not hold is null, not an empty channel.</summary>
    [Fact]
    public async Task A_read_of_an_unknown_ask_is_null()
    {
        Assert.Null(await _channel.ReadAsync("ffffff", ChannelDesk.PersonEntry, null, false));
    }
}
