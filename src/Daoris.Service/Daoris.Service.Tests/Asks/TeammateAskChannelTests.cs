using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Posting to a teammate's ask (SESSONE2i, <see cref="TeammateAskChannel"/>): an ask held on another machine, whose messages the sync
/// mirrored here. The person and a session here on it post as on an ask's channel, kept as this machine's own so the sync carries them,
/// at their level; the session reads it as an entry reads an ask's. A line's own channel is read by the person (<see cref="LineChannel"/>).
/// </summary>
public sealed class TeammateAskChannelTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-teammate-ask-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestExchange _exchange = null!;
    private AskStore _askStore = null!;
    private AskDesk _asks = null!;
    private SessionLedger _ledger = null!;
    private ChannelDesk _channel = null!;
    private ChannelStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-05T09:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _askStore = await AskStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, quests, files: files);
        _asks = new AskDesk(service, _askStore, _exchange, files);
        _ledger = new SessionLedger(quests, sessions, service, _askStore);
        _store = await ChannelStore.OpenAsync(_connection);
        _channel = new ChannelDesk(_store, _asks, _askStore, _ledger, quests, new StoreEntryFacts(quests, sessions));
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

    /// <summary>
    /// A session here on an ask its teammate holds: the ask's record is not on this machine, the session's entry keeps the ask's origin it
    /// was opened with, and one message of the holder's is mirrored.
    /// </summary>
    private async Task<(string Ask, Session Session, ChannelMessage Theirs)> OnTeammatesAskAsync()
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", "the report needs the v3 bridge") { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "reports"))).Session!;
        var ask = outcome.Ask!.Id;
        await _askStore.DeleteAsync(ask);
        var theirs = new ChannelMessage(
            "t1", ask, "work", Now.AddMinutes(1), Session: "ab12cd34", Repository: "checker", Agent: "claude", Entry: $"ask:{ask}/checker",
            To: null, Re: null, Question: false, "the check is green on their side", Origin: "teammate-key");
        await _store.MirrorAsync(theirs);
        return (ask, session, theirs);
    }

    /// <summary>
    /// 🔴 The person's post to a teammate's ask is kept as this machine's own (no origin, so the sync carries it), at their level, on no
    /// ask's words; <c>to</c> may name a repository its messages name, <c>re</c> one of its messages; an ask nothing names is not found.
    /// </summary>
    [Fact]
    public async Task SESSONE2i_the_person_posts_to_a_teammates_ask_and_the_message_travels_at_its_level()
    {
        var (ask, _, theirs) = await OnTeammatesAskAsync();

        var posted = await _channel.PostAsPersonAsync(ask, "thanks: keep the old column a week", "checker", theirs.Id, Now.AddMinutes(2), urgent: false);

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        Assert.Equal(("checker", theirs.Id, (bool?)false, (string?)null, "work"),
            (posted.Message!.To, posted.Message.Re, posted.Message.Urgent, posted.Message.Origin, posted.Message.Workspace));
        Assert.True(posted.Message.ByPerson);
        var kept = await _store.OwnChangedSinceAsync(0, "work");
        Assert.Contains(kept, row => row.Message.Id == posted.Message.Id);
        Assert.Equal(ChannelRefusal.BadTo, (await _channel.PostAsPersonAsync(ask, "hello", "nowhere", null, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadRe, (await _channel.PostAsPersonAsync(ask, "hello", null, "zzzz", Now)).Refusal);
        Assert.Equal(ChannelRefusal.NotFound, (await _channel.PostAsPersonAsync("0f0f0f", "hello", null, null, Now)).Refusal);
    }

    /// <summary>
    /// 🔴 A session here on a teammate's ask posts on that ask's channel, never on a channel of its own that would not travel, within its
    /// part's bounds; its read holds the holder's message once, acknowledged, and never its own.
    /// </summary>
    [Fact]
    public async Task SESSONE2i_a_session_on_a_teammates_ask_posts_to_it_and_reads_it()
    {
        var (ask, session, theirs) = await OnTeammatesAskAsync();

        var posted = await _channel.PostAsSessionAsync(session.Id, "the page reads region now", null, theirs.Id, false, Now.AddMinutes(2), urgent: true);

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        Assert.Equal((ask, (string?)null, (bool?)true, session.Id), (posted.Message!.Ask, posted.Message.Channel, posted.Message.Urgent, posted.Message.Session));
        Assert.Contains(await _store.OfAsync(ask), m => m.Id == posted.Message.Id);
        Assert.Empty(await _store.Addressed.OfAsync(SessionEntries.AskOrigin(ask)));

        var read = await _channel.ReadAsSessionAsync(session.Id, null, now: Now.AddMinutes(3));
        Assert.Equal(ChannelRefusal.None, read.Refusal);
        Assert.Equal(SessionEntries.AskOrigin(ask), read.Origin);
        Assert.Contains(read.Read!.Messages, m => m.Id == theirs.Id);
        var again = await _channel.ReadAsSessionAsync(session.Id, null, now: Now.AddMinutes(4));
        Assert.DoesNotContain(again.Read!.Messages, m => m.Id == theirs.Id);
    }

    /// <summary>
    /// 🔴 A part here on a teammate's ask is handed the holder's message by the look (COMMS1 loose item): its entry's claim is one part's
    /// only, taken once, and the entry's read carries what it acknowledged, so a message taken is not handed again; an ask nothing
    /// mirrors has none to acknowledge.
    /// </summary>
    [Fact]
    public async Task COMMS1_a_part_on_a_teammates_ask_claims_the_holder_s_message_once_and_its_read_carries_the_acknowledgment()
    {
        var (ask, session, theirs) = await OnTeammatesAskAsync();
        var entry = (await _ledger.WithEntryAsync(session)).Entry!;

        var claimed = await _channel.AcknowledgeAsync(ask, entry, session.Id, ChannelAcks.Taking, [theirs.Id], Now.AddMinutes(2));
        var again = await _channel.AcknowledgeAsync(ask, entry, "another-part", ChannelAcks.Taking, [theirs.Id], Now.AddMinutes(2));

        Assert.Equal([theirs.Id], claimed);
        Assert.Empty(again!);
        Assert.Equal(ChannelAcks.Taking, (await _channel.ReadAsync(ask, entry, null, moveMark: false))!.Acknowledged[theirs.Id].State);
        await _channel.AcknowledgeAsync(ask, entry, session.Id, ChannelAcks.Taken, [theirs.Id], Now.AddMinutes(3));
        Assert.Equal(ChannelAcks.Taken, (await _channel.ReadAsync(ask, entry, null, moveMark: false))!.Acknowledged[theirs.Id].State);
        Assert.Null(await _channel.AcknowledgeAsync("0f0f0f", entry, session.Id, ChannelAcks.Taking, ["x"], Now));
    }

    /// <summary>
    /// The person reads a line's own channel by one of its parts: a chat's post to everyone and to one repository, with its origin; a line
    /// on an ask held here has none of its own, so it is read at the ask.
    /// </summary>
    [Fact]
    public async Task COMMS1k_the_person_reads_a_line_s_own_channel_and_a_line_on_an_ask_reads_the_ask_s()
    {
        var onAsk = (await _ledger.OpenAsync(
            (await _asks.AskAsync(new AskRequest("work", "check it") { To = "checker" }, Now)).Quest!.Id, "stub", Now,
            tree: Path.Combine(_root, "trees", "on-ask"))).Session!;
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var published = await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the export", "Please.") { PublishedBy = chat.Id }, Now);
        var checker = (await _ledger.OpenAsync(published.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "check"))).Session!;
        var posted = await _channel.PostAsSessionAsync(chat.Id, "the export's schema moved to v4", null, null, false, Now.AddMinutes(1));

        var read = await _channel.Line.ReadAsync(checker.Id);

        Assert.NotNull(read);
        Assert.Equal($"session:{chat.Id}", read!.Origin);
        Assert.Equal([posted.Message!.Id], read.Messages.Select(m => m.Id));
        Assert.Equal(read.Origin, (await _channel.Line.ReadAsync(chat.Id))!.Origin);
        Assert.Null(await _channel.Line.ReadAsync(onAsk.Id));
        Assert.Null(await _channel.Line.ReadAsync("nope"));
    }

    /// <summary>
    /// 🔴 The person posts on a line's own channel (COMMS1 loose item): their words, to everyone or to one repository the line holds, kept at
    /// their level with no cap; the line's parts are handed it as the person's, never a session's claim; a blank, a repository the line does
    /// not hold, and a session on an ask held here are refused or answered none.
    /// </summary>
    [Fact]
    public async Task COMMS1_the_person_posts_on_a_line_s_own_channel_and_its_parts_are_waiting_with_it()
    {
        var onAsk = (await _ledger.OpenAsync(
            (await _asks.AskAsync(new AskRequest("work", "check it") { To = "checker" }, Now)).Quest!.Id, "stub", Now,
            tree: Path.Combine(_root, "trees", "on-ask"))).Session!;
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var published = await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the export", "Please.") { PublishedBy = chat.Id }, Now);
        var checker = (await _ledger.OpenAsync(published.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "check"))).Session!;

        var posted = await _channel.Line.PostAsPersonAsync(checker.Id, "use v4 from now on", "checker", urgent: false, Now.AddMinutes(1));

        Assert.Equal(ChannelRefusal.None, posted!.Refusal);
        Assert.True(posted.Message!.ByPerson);
        Assert.Equal(("checker", (bool?)false, $"session:{chat.Id}"), (posted.Message.To, posted.Message.Urgent, posted.Message.Channel));
        var waiting = await _channel.Parts.WaitingAsync(checker.Id);
        Assert.Contains(waiting!, each => each.Message.Id == posted.Message.Id);
        Assert.Contains(await _channel.Line.ReadAsync(chat.Id) is { } read ? read.Messages : [], m => m.Id == posted.Message.Id);
        Assert.Equal(ChannelRefusal.Empty, (await _channel.Line.PostAsPersonAsync(chat.Id, "  ", null, urgent: true, Now))!.Refusal);
        Assert.Equal(ChannelRefusal.BadTo, (await _channel.Line.PostAsPersonAsync(chat.Id, "hello", "nowhere", urgent: true, Now))!.Refusal);
        Assert.Null(await _channel.Line.PostAsPersonAsync(onAsk.Id, "hello", null, urgent: true, Now));
        Assert.Null(await _channel.Line.PostAsPersonAsync("nope", "hello", null, urgent: true, Now));
    }
}
