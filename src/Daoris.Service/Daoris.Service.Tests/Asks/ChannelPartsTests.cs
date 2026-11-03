using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A channel for every origin, and parts that hear each other (<see cref="ChannelParts"/>): a message to one part is kept apart from the
/// ask's channel and read by the head of that part's line alone; a line on no ask has a channel of its own; an exact recipient outside
/// the poster's line, or on another machine, is refused and never widened.
/// </summary>
public sealed class ChannelPartsTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-channel-parts-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _asks = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private ChannelDesk _channel = null!;
    private ChannelStore _store = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-14T09:00:00Z");

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
        _service = service;

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

    private async Task<(Ask Ask, Quest Quest, Session Session)> AskedAsync(string sentence = "the report needs the v3 bridge")
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", sentence) { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", sentence.Length.ToString()))).Session!;
        return (outcome.Ask!, outcome.Quest, session);
    }

    /// <summary>A lead on the ask and a lane part it published alongside it: two parts of one line of work, one entry.</summary>
    private async Task<(Ask Ask, Session Lead, Session Lane, Quest LaneQuest)> AlongsideAsync()
    {
        var (ask, _, lead) = await AskedAsync();
        var reports = (await _service.RegistryAsync()).Named("reports")!;
        await _service.RegisterAsync(reports with { Lanes = [new("web", "Web", "The page.")] }, Now);
        var published = await _exchange.PublishAsync(new QuestAsk("reports", "reports:web", "The page's half", "Please.") { PublishedBy = lead.Id }, Now);
        var lane = (await _ledger.OpenAsync(published.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "lane"))).Session!;
        return (ask, lead, lane, published.Quest);
    }

    /// <summary>
    /// Two alongside parts post to each other: a post to one part is kept apart from the ask's channel, read by that part alone (never by
    /// its poster, nor by a part of another line), and the person reads it beside the channel with its receipt in that part's line.
    /// </summary>
    [Fact]
    public async Task COMMS1k_two_alongside_parts_post_to_each_other_and_only_the_part_named_reads_it()
    {
        var (ask, lead, lane, _) = await AlongsideAsync();
        Assert.Equal(
            (await _ledger.WithEntryAsync(lead)).Entry,
            (await _ledger.WithEntryAsync(lane)).Entry);
        var other = (await _asks.AskAsync(new AskRequest("work", "check the bridge too") { To = "checker" }, Now)).Quest!;
        var checker = (await _ledger.OpenAsync(other.Id, "stub", Now, tree: Path.Combine(_root, "trees", "checker"))).Session!;

        var toLane = await _channel.PostAsSessionAsync(lead.Id, "the contract changed: the bridge takes a region", lane.Id, null, false, Now.AddMinutes(1));
        var toLead = await _channel.PostAsSessionAsync(lane.Id, "taken; the page reads region now", lead.Id, toLane.Message?.Id, false, Now.AddMinutes(2));

        Assert.Equal(ChannelRefusal.None, toLane.Refusal);
        Assert.Equal((lane.Id, SessionEntries.AskOrigin(ask.Id), (string?)null), (toLane.Message!.Part, toLane.Message.Channel, toLane.Message.To));
        Assert.Equal(ChannelRefusal.None, toLead.Refusal);
        Assert.Empty(await _store.OfAsync(ask.Id));
        var laneRead = (await _channel.ReadAsSessionAsync(lane.Id, null, now: Now.AddMinutes(3))).Read!;
        Assert.Equal([toLane.Message.Id], laneRead.Messages.Select(m => m.Id));
        var leadRead = (await _channel.ReadAsSessionAsync(lead.Id, null, now: Now.AddMinutes(3))).Read!;
        Assert.Equal([toLead.Message!.Id], leadRead.Messages.Select(m => m.Id));
        Assert.Empty((await _channel.ReadAsSessionAsync(checker.Id, null)).Read!.Messages);
        var person = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: false))!;
        Assert.Empty(person.Messages);
        Assert.Equal([toLane.Message.Id, toLead.Message.Id], person.Addressed.Select(m => m.Id));
        Assert.Equal(ChannelReceipts.Read, person.AddressedReceipts[toLane.Message.Id]);
        // Read once, it no longer waits for the part.
        Assert.Empty((await _channel.WaitingForAsync(lane.Id)).Waiting);
    }

    /// <summary>
    /// An exact recipient is refused where it is no other part of the poster's line (a part of another line, the poster itself, a
    /// helper) or lives on another machine, and nothing is kept: never widened to everyone.
    /// </summary>
    [Fact]
    public async Task COMMS1k_an_exact_recipient_outside_the_line_or_elsewhere_is_refused_never_broadcast()
    {
        var (ask, lead, lane, laneQuest) = await AlongsideAsync();
        var other = (await _asks.AskAsync(new AskRequest("work", "check the bridge too") { To = "checker" }, Now)).Quest!;
        var checker = (await _ledger.OpenAsync(other.Id, "stub", Now, tree: Path.Combine(_root, "trees", "checker"))).Session!;
        var helper = await _sessions.CreateAsync(null, "reports", "stub", Now, kind: SessionKind.Chat, serves: lead.Id, ownEntry: true);
        await _sessions.MirrorAsync(new Session(
            "alice-laptop/ab12cd34", laneQuest.Id, "reports", "stub", SessionState.Working, null, null, null, Now, Now));

        Assert.Equal(ChannelRefusal.BadPart, (await _channel.PostAsSessionAsync(lead.Id, "hello", checker.Id, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadPart, (await _channel.PostAsSessionAsync(lead.Id, "hello", lead.Id, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadPart, (await _channel.PostAsSessionAsync(lead.Id, "hello", helper.Id, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.PartElsewhere, (await _channel.PostAsSessionAsync(lead.Id, "hello", "alice-laptop/ab12cd34", null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.PartElsewhere, (await _channel.PostAsPersonAsync(ask.Id, "hello", "alice-laptop/ab12cd34", null, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadPart, (await _channel.PostAsPersonAsync(ask.Id, "hello", checker.Id, null, Now)).Refusal);
        Assert.Empty(await _store.OfAsync(ask.Id));
        Assert.Empty(await _store.Addressed.OfAsync(SessionEntries.AskOrigin(ask.Id)));
        Assert.Empty((await _channel.ReadAsSessionAsync(lane.Id, null)).Read!.Messages);
    }

    /// <summary>
    /// A replacement part of the same line inherits the line's pending message (it is the line's head now), and its read settles it for
    /// the line; an unrelated sibling never reads it. The person's own post to one part is that part's alone, kept on no ask's words.
    /// </summary>
    [Fact]
    public async Task COMMS1k_a_replacement_part_inherits_its_line_s_pending_message_and_a_sibling_does_not()
    {
        var (ask, lead, lane, laneQuest) = await AlongsideAsync();
        var posted = await _channel.PostAsSessionAsync(lead.Id, "use the new region column", lane.Id, null, false, Now.AddMinutes(1));
        var told = await _channel.PostAsPersonAsync(ask.Id, "and keep the label short", lane.Id, null, Now.AddMinutes(2));
        Assert.Equal(ChannelRefusal.None, told.Refusal);
        Assert.DoesNotContain((await _asks.FindAsync(ask.Id))!.Words, word => word.Text == "and keep the label short");

        await _ledger.AdvanceAsync(lane.Id, "failed", null, null, null, Now.AddMinutes(3));
        var replacement = (await _ledger.OpenAsync(laneQuest.Id, "stub", Now.AddMinutes(4), tree: Path.Combine(_root, "trees", "lane-2"), from: lane.Id, fromWhy: "handed-over")).Session!;

        var waiting = (await _channel.WaitingForAsync(replacement.Id)).Waiting;
        Assert.Equal([posted.Message!.Id, told.Message!.Id], waiting.Select(m => m.Id));
        Assert.Empty((await _channel.WaitingForAsync(lane.Id)).Waiting);
        Assert.Empty((await _channel.WaitingForAsync(lead.Id)).Waiting);
        await _channel.ReadAsSessionAsync(replacement.Id, null, now: Now.AddMinutes(5));
        var person = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: false))!;
        Assert.Equal(ChannelReceipts.Read, person.AddressedReceipts[posted.Message.Id]);
    }

    /// <summary>
    /// A line on no ask has a channel of its own: a chat's post reaches the session on the quest it published to another repository, and
    /// the chat's own read holds none of its own; a session of another line reads nothing of it.
    /// </summary>
    [Fact]
    public async Task COMMS1k_a_line_on_no_ask_has_a_channel_of_its_own()
    {
        var (_, _, onAsk) = await AskedAsync();
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var published = await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the export", "Please.") { PublishedBy = chat.Id }, Now);
        var checker = (await _ledger.OpenAsync(published.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "check"))).Session!;

        var posted = await _channel.PostAsSessionAsync(chat.Id, "the export's schema moved to v4", null, null, false, Now.AddMinutes(1));
        var toChecker = await _channel.PostAsSessionAsync(chat.Id, "only for you", "checker", null, false, Now.AddMinutes(2));

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        Assert.Equal(($"session:{chat.Id}", "", (string?)null), (posted.Message!.Channel, posted.Message.Ask, posted.Message.Part));
        Assert.Equal("checker", toChecker.Message!.To);
        var read = await _channel.ReadAsSessionAsync(checker.Id, null);
        Assert.Null(read.Ask);
        Assert.Equal($"session:{chat.Id}", read.Origin);
        Assert.Equal([posted.Message.Id, toChecker.Message.Id], read.Read!.Messages.Select(m => m.Id));
        Assert.Empty((await _channel.ReadAsSessionAsync(chat.Id, null)).Read!.Messages);
        Assert.Empty((await _channel.ReadAsSessionAsync(onAsk.Id, null)).Read!.Messages);
        Assert.Equal(ChannelRefusal.BadTo, (await _channel.PostAsSessionAsync(chat.Id, "hello", "nowhere", null, false, Now)).Refusal);
    }
}
