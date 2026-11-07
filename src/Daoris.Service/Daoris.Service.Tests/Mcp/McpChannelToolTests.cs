using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// <c>channel_post</c> and <c>channel_read</c> speak only for the session the
/// connector names, say each refusal in a sentence the agent reads, and read what is new since that session's entry last read.
/// </summary>
public sealed class McpChannelToolTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-mcpchannel-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskDesk _asks = null!;
    private SessionLedger _ledger = null!;
    private ChannelDesk _channel = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(-30);

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "reports", "checker" })
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
        var askStore = await AskStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);
        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files);
        _asks = new AskDesk(service, askStore, _exchange, files);
        _ledger = new SessionLedger(_quests, sessions, service, askStore);
        _channel = new ChannelDesk(await ChannelStore.OpenAsync(_connection), _asks, askStore, _ledger, _quests, new StoreEntryFacts(_quests, sessions));
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<(Ask Ask, Session Session)> RunningAsync()
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", "the report needs the bridge") { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "tree"))).Session!;
        return (outcome.Ask!, session);
    }

    private static ChannelTools As(ChannelDesk channel, string? session) => new(channel, new IntakeScope(null, session));

    /// <summary>🔴 A post is kept for the session the connector names, and a read marks the poster as another agent's claim and moves the mark.</summary>
    [Fact]
    public async Task A_post_is_kept_for_the_connectors_session_and_a_read_moves_the_mark()
    {
        var (ask, session) = await RunningAsync();
        await _channel.PostAsPersonAsync(ask.Id, "use the older bridge", null, null, DateTimeOffset.UtcNow.AddMinutes(-2));
        var tools = As(_channel, session.Id);

        var posted = await tools.PostAsync("the bridge wants a region column", to: null, re: null, question: true);

        Assert.StartsWith("Posted to ask `#" + ask.Id + "`", posted);
        var read = await tools.ReadAsync();
        Assert.Contains("use the older bridge", read);
        Assert.Contains("the bridge wants a region column", read);
        Assert.Contains("the person", read);
        Assert.Contains("claim", read);
        Assert.Contains("question", read);
        Assert.Contains("Daoris:", read);

        var again = await tools.ReadAsync();
        Assert.Contains("Nothing new", again);
        Assert.DoesNotContain("use the older bridge", again);
    }

    /// <summary>
    /// 🔴 A member outside Daoris is read as such: not "another session's claim", and named by its agent where it said no repository, so
    /// the line never opens on an empty name (rule 17).
    /// </summary>
    [Fact]
    public async Task A_read_says_an_agent_outside_Daoris_wrote_a_member_s_message()
    {
        var (ask, session) = await RunningAsync();
        var member = (await _channel.Members.JoinAsync(new ChannelJoin(ask.Id, null, "claude-code", null, null), Now)).Member!;
        await _channel.Members.PostAsync(member.Id, "the check passes on my side", null, null, question: false, urgent: false, DateTimeOffset.UtcNow);

        var read = await As(_channel, session.Id).ReadAsync();

        var line = read.Split('\n').Single(each => each.Contains("the check passes on my side"));
        Assert.Contains($"an agent outside Daoris ({member.Id}, claude-code)", line);
        Assert.DoesNotContain("another session's claim", line);
        Assert.DoesNotContain("from  (", line);
    }

    /// <summary>
    /// 🔴 <c>channel_post</c>'s <c>urgent</c> says the post reaches a working session at its next step; the
    /// answer says so, and a part's fourth urgent post is kept ordinary, which the answer says too. An ordinary post says it waits.
    /// </summary>
    [Fact]
    public async Task An_urgent_post_says_where_it_reaches_and_the_fourth_is_kept_ordinary_and_said_so()
    {
        var (_, session) = await RunningAsync();
        var tools = As(_channel, session.Id);

        var ordinary = await tools.PostAsync("a note", urgent: null);
        var answers = new List<string>();
        for (var n = 0; n <= ChannelBounds.UrgentPerPart; n++) answers.Add(await tools.PostAsync($"urgent {n}", urgent: true));

        Assert.Contains("ordinary", ordinary);
        Assert.All(answers[..ChannelBounds.UrgentPerPart], answer => Assert.Contains("urgent", answer));
        Assert.Contains($"kept as ordinary: a part posts at most {ChannelBounds.UrgentPerPart} urgent messages", answers[^1]);
    }

    /// <summary>
    /// 🔴 <c>channel_read</c> marks an urgent message so, and answers a poster what each
    /// of its own messages reached, per repository it addresses.
    /// </summary>
    [Fact]
    public async Task A_read_marks_urgent_messages_and_answers_the_poster_what_its_messages_reached()
    {
        var (_, session) = await RunningAsync();
        await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the bridge", "Please.") { PublishedBy = session.Id }, Now);
        var tools = As(_channel, session.Id);
        await tools.PostAsync("for the checker", to: "checker", urgent: true);

        var read = await tools.ReadAsync();

        Assert.Contains("urgent message from reports", read);
        Assert.Contains("What your messages reached", read);
        Assert.Contains("checker: waiting", read);
    }

    /// <summary>
    /// 🔴 The line every answer ends with names what waits for the session's
    /// entry, how many and how many urgent, once per new message; nothing once the agent read them, and nothing for a session on no ask.
    /// </summary>
    [Fact]
    public async Task What_waits_is_said_once_per_new_message_and_not_once_read()
    {
        var (ask, session) = await RunningAsync();
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "chat"))).Session!;
        var line = new WaitingLine(_channel, new IntakeScope(null, session.Id));

        Assert.Null(await line.NextAsync(default));
        await _channel.PostAsPersonAsync(ask.Id, "use the older bridge", null, null, DateTimeOffset.UtcNow.AddMinutes(-2));
        var first = await line.NextAsync(default);
        Assert.Contains($"1 message waits for you on ask `#{ask.Id}`'s channel, 1 of them urgent", first);
        Assert.Contains("channel_read", first);
        Assert.Null(await line.NextAsync(default));

        await _channel.PostAsPersonAsync(ask.Id, "and log the port", null, null, DateTimeOffset.UtcNow.AddMinutes(-1), urgent: false);
        Assert.Contains("2 messages wait for you", await line.NextAsync(default));

        await As(_channel, session.Id).ReadAsync();
        await _channel.PostAsPersonAsync(ask.Id, "one more", null, null, DateTimeOffset.UtcNow, urgent: false);
        Assert.Contains("1 message waits for you", await line.NextAsync(default));
        Assert.Null(await new WaitingLine(_channel, new IntakeScope(null, chat.Id)).NextAsync(default));
        Assert.Null(await new WaitingLine(null, new IntakeScope(null, session.Id)).NextAsync(default));
    }

    /// <summary>🔴 Each refusal is said in a sentence the agent reads, and nothing is kept.</summary>
    [Fact]
    public async Task Each_refusal_is_a_sentence_and_nothing_is_kept()
    {
        var (ask, session) = await RunningAsync();
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "chat"))).Session!;

        Assert.Contains("no words", await As(_channel, session.Id).PostAsync("  "));
        Assert.Contains("4000", await As(_channel, session.Id).PostAsync(new string('x', 4001)));
        Assert.Contains("not one of this channel's repositories", await As(_channel, session.Id).PostAsync("hello", to: "nowhere"));
        // A session of another line named in `to` is no sibling part.
        Assert.Contains("no other part of your own line of work", await As(_channel, session.Id).PostAsync("hello", to: chat.Id));
        Assert.Contains("no message", await As(_channel, session.Id).PostAsync("hello", re: "nosuchid"));
        Assert.Contains("no session", await As(_channel, null).PostAsync("hello"));
        Assert.Contains("no session", await As(_channel, null).ReadAsync());
        Assert.Contains("moment", await As(_channel, session.Id).ReadAsync(since: "yesterday-ish"));
        Assert.Empty((await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, false))!.Messages);
    }

    /// <summary>
    /// A chat on no ask posts on its own line's channel, which the session on the quest it published reads with its poster named,
    /// and the chat's own read says its line's channel holds nothing new for it; the line every answer ends with names that channel.
    /// </summary>
    [Fact]
    public async Task COMMS1k_a_chat_posts_on_its_own_line_s_channel_and_the_quest_it_published_reads_it()
    {
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "chat"))).Session!;
        var published = await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the export", "Please.") { PublishedBy = chat.Id }, Now);
        var checker = (await _ledger.OpenAsync(published.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "check"))).Session!;

        var posted = await As(_channel, chat.Id).PostAsync("the export's schema moved to v4");
        var line = await new WaitingLine(_channel, new IntakeScope(null, checker.Id)).NextAsync(default);
        var read = await As(_channel, checker.Id).ReadAsync();

        Assert.StartsWith("Posted to your line's own channel", posted);
        Assert.Contains("1 message waits for you on your line's own channel", line);
        Assert.StartsWith("Your line's own channel, oldest first:", read);
        Assert.Contains("the export's schema moved to v4", read);
        Assert.Contains("another session's claim", read);
        Assert.StartsWith("Nothing new on your line's own channel", await As(_channel, chat.Id).ReadAsync());
    }
}
