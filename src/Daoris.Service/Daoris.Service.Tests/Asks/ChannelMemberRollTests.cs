using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The members a channel holds, as the person's page and the look read them (<see cref="ChannelMemberRoll"/>): only those still on it, each
/// with whether its channel's claims have expired (rule 15: its ask has no quest open and no part runs), judged as it is read and never
/// stored, so a quest opened again makes it not spent again.
/// </summary>
public sealed class ChannelMemberRollTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-channel-roll-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private AskDesk _asks = null!;
    private SessionLedger _ledger = null!;
    private SessionStore _sessions = null!;
    private ChannelDesk _channel = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T09:00:00Z");

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "reports");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), """{ "source": "s", "packs": [], "domain": { "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        var askStore = await AskStore.OpenAsync(_connection);
        var sessions = _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, quests, files: files);
        _asks = new AskDesk(service, askStore, _exchange, files);
        _ledger = new SessionLedger(quests, sessions, service, askStore);
        _channel = new ChannelDesk(await ChannelStore.OpenAsync(_connection), _asks, askStore, _ledger, quests, new StoreEntryFacts(quests, sessions));
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<(Ask Ask, Quest Quest, Session Session)> AskedAsync()
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", "the report needs a check") { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", "one"))).Session!;
        return (outcome.Ask!, outcome.Quest, session);
    }

    private Task<ChannelMemberOutcome> JoinAsync(string? ask = null, string? line = null) =>
        _channel.Members.JoinAsync(new ChannelJoin(ask, line, "claude-code", "reports", "conf-1"), Now);

    /// <summary>It is spent once its ask has no quest open and no part runs, and not before; one that left is not listed.</summary>
    [Fact]
    public async Task CHANNELJOIN1c_a_member_is_spent_once_its_asks_quest_is_closed_and_no_part_runs()
    {
        var (ask, quest, session) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;

        Assert.False(Assert.Single(await _channel.Roll.ListAsync(ask.Id, null)).Spent);

        await _exchange.RespondAsync(quest.Id, "decline", "not ours", Now.AddMinutes(1));
        Assert.False(Assert.Single(await _channel.Roll.ListAsync(ask.Id, null)).Spent);

        await _ledger.AdvanceAsync(session.Id, "failed", null, null, null, Now.AddMinutes(2));
        var spent = Assert.Single(await _channel.Roll.ListAsync(ask.Id, null));
        Assert.Equal((member.Id, true), (spent.Member.Id, spent.Spent));

        await _channel.Members.LeaveAsync(member.Id, ChannelMember.ByPerson, Now.AddMinutes(3));
        Assert.Empty(await _channel.Roll.ListAsync(ask.Id, null));
    }

    /// <summary>What a part asked of a helper is open while the helper runs, though no part of the line does: the channel is not spent until it ends.</summary>
    [Fact]
    public async Task CHANNELJOIN1_a_channel_is_not_spent_while_a_helper_a_part_asked_still_runs()
    {
        var (ask, quest, session) = await AskedAsync();
        await JoinAsync(ask: ask.Id);
        await _exchange.RespondAsync(quest.Id, "decline", "not ours", Now.AddMinutes(1));
        await _ledger.AdvanceAsync(session.Id, "failed", null, null, null, Now.AddMinutes(2));
        var helper = (await new HelperDesk(_sessions).OpenAsync(new HelperOpen(session.Id, "codex-acp", Path.Combine(_root, "trees", "helper")), Now.AddMinutes(3))).Session!;

        Assert.False(Assert.Single(await _channel.Roll.ListAsync(ask.Id, null)).Spent);

        await _ledger.AdvanceAsync(helper.Id, "failed", null, null, null, Now.AddMinutes(4));
        Assert.True(Assert.Single(await _channel.Roll.ListAsync(ask.Id, null)).Spent);
    }

    /// <summary>A channel is asked by its ask or by a session of its line, which the line's channel is read as; with no argument every member on the machine is listed.</summary>
    [Fact]
    public async Task CHANNELJOIN1c_the_roll_lists_a_channels_members_by_its_ask_or_a_session_and_all_with_no_argument()
    {
        var (ask, _, session) = await AskedAsync();
        var onAsk = (await JoinAsync(ask: ask.Id)).Member!;
        var onLine = (await JoinAsync(line: session.Id)).Member!;
        string[] both = [onAsk.Id, onLine.Id];

        Assert.Equal(both.Order(), (await _channel.Roll.ListAsync(null, null)).Select(row => row.Member.Id).Order());
        Assert.Equal(both.Order(), (await _channel.Roll.ListAsync(null, session.Id)).Select(row => row.Member.Id).Order());
        Assert.Empty(await _channel.Roll.ListAsync("ffffff", null));
    }

    /// <summary>An ended member is the look's, not the person's: its reason is kept as <c>expired</c>.</summary>
    [Fact]
    public async Task CHANNELJOIN1c_a_membership_the_look_ends_keeps_its_reason()
    {
        var (ask, _, _) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;

        var ended = await _channel.Members.LeaveAsync(member.Id, ChannelMember.Expired, Now.AddDays(2));

        Assert.Equal((ChannelRefusal.None, "expired"), (ended.Refusal, ended.Member!.Why));
    }
}
