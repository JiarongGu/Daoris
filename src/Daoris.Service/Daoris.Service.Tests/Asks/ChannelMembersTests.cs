using System.Text.RegularExpressions;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An agent the person runs outside Daoris on a channel (<see cref="ChannelMembers"/>): a member of its own, kept in the asks store and
/// never synced, whose messages carry its own id as their poster, so none is ever the person's and no session door takes its id.
/// </summary>
public sealed class ChannelMembersTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-channel-members-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskDesk _asks = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private ChannelDesk _channel = null!;
    private ChannelStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T09:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var askStore = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _asks = new AskDesk(service, askStore, new QuestExchange(service, _quests, files: files), files);
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

    private async Task<(Ask Ask, Session Session)> AskedAsync(string sentence = "the report needs a check")
    {
        var outcome = await _asks.AskAsync(new AskRequest("work", sentence) { To = "reports" }, Now);
        var session = (await _ledger.OpenAsync(outcome.Quest!.Id, "stub", Now, tree: Path.Combine(_root, "trees", sentence.Length.ToString()))).Session!;
        return (outcome.Ask!, session);
    }

    private Task<ChannelMemberOutcome> JoinAsync(string? ask = null, string? line = null, string? agent = "claude-code", DateTimeOffset? at = null) =>
        _channel.Members.JoinAsync(new ChannelJoin(ask, line, agent, "checker", "conf-1"), at ?? Now);

    /// <summary>
    /// A member's post is kept on the ask's channel under its own id, with its agent, its repository and its own entry: never the person's,
    /// read by the person as an outside agent's and by a part of the line as another session's message.
    /// </summary>
    [Fact]
    public async Task CHANNELJOIN1a_a_member_posts_under_its_own_id_and_is_never_read_as_the_person()
    {
        var (ask, session) = await AskedAsync();
        var joined = await JoinAsync(ask: "#" + ask.Id);

        Assert.Equal(ChannelRefusal.None, joined.Refusal);
        var member = joined.Member!;
        Assert.Matches(new Regex("^o[0-9a-f]{7}$"), member.Id);
        Assert.Equal((SessionEntries.AskOrigin(ask.Id), $"{SessionEntries.AskOrigin(ask.Id)}/outside:{member.Id}", "claude-code", "checker", "conf-1"),
            (member.Origin, member.Entry, member.Agent, member.Repository, member.Confirmation));
        Assert.Null(await _ledger.FindAsync(member.Id));

        var posted = await _channel.Members.PostAsync(member.Id, "the check passes on my side", null, null, question: false, urgent: false, Now.AddMinutes(1));

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        var kept = Assert.Single(await _store.OfAsync(ask.Id));
        Assert.Equal((member.Id, "checker", "claude-code", member.Entry, false), (kept.Session, kept.Repository, kept.Agent, kept.Entry, kept.Urgent));
        Assert.False(kept.ByPerson);
        Assert.True(kept.Outside);

        var own = await _channel.PostAsSessionAsync(session.Id, "thanks", null, null, false, Now.AddMinutes(2));
        Assert.False(own.Message!.Outside);
        var person = (await _channel.ReadAsync(ask.Id, ChannelDesk.PersonEntry, null, moveMark: false))!;
        Assert.Equal(2, person.Unread);
        Assert.False(person.Messages.Single(m => m.Id == kept.Id).ByPerson);
        var part = (await _channel.ReadAsSessionAsync(session.Id, null, now: Now.AddMinutes(3))).Read!;
        Assert.Contains(part.Messages, m => m.Id == kept.Id && !m.ByPerson && m.Outside);
    }

    /// <summary>The member reads the channel with its own mark: what came since its last read, Daoris's lines among it, and earlier with a moment.</summary>
    [Fact]
    public async Task CHANNELJOIN1a_a_member_reads_with_its_own_mark_and_reads_earlier_with_since()
    {
        var (ask, session) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;
        await _channel.PostAsPersonAsync(ask.Id, "the person's word", null, null, Now.AddMinutes(1));
        await _channel.PostAsSessionAsync(session.Id, "a part's word", null, null, false, Now.AddMinutes(2));

        var first = await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(3));

        Assert.Equal(ChannelRefusal.None, first.Refusal);
        Assert.Equal(member.Entry, first.Entry);
        Assert.Equal(["the person's word", "a part's word"], first.Read!.Messages.Select(m => m.Text));
        Assert.NotEmpty(first.Read.Lines);
        Assert.Empty((await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(4))).Read!.Messages);
        Assert.Equal(2, (await _channel.Members.ReadAsync(member.Id, Now, Now.AddMinutes(5))).Read!.Messages.Count);
        Assert.Equal(Now.AddMinutes(5), (await _channel.Members.OfAsync(member.Origin)).Single().Seen);
    }

    /// <summary>
    /// Every refusal of the design's §5.6 the service judges: not a member before admission or after leaving (its record kept), a part named,
    /// the bounds of a part, three members on a channel and eight on the machine, and a helper's line.
    /// </summary>
    [Fact]
    public async Task CHANNELJOIN1a_the_service_refuses_what_a_member_may_not_do()
    {
        var (ask, session) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;

        Assert.Equal(ChannelRefusal.NotMember, (await _channel.Members.PostAsync("o1234567", "hello", null, null, false, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NotMember, (await _channel.Members.ReadAsync("o1234567", null, Now)).Refusal);
        Assert.Equal(ChannelRefusal.BadPart, (await _channel.Members.PostAsync(member.Id, "to one part", session.Id, null, false, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.TooLong, (await _channel.Members.PostAsync(member.Id, new string('x', ChannelBounds.MaxText + 1), null, null, false, false, Now)).Refusal);

        var levels = new List<bool?>();
        for (var i = 0; i < ChannelBounds.MaxPerPart; i++)
        {
            levels.Add((await _channel.Members.PostAsync(member.Id, $"post {i}", null, null, false, urgent: true, Now.AddSeconds(i))).Message!.Urgent);
        }

        Assert.Equal([true, true, true, false, false, false, false, false, false, false], levels);
        Assert.Equal(ChannelRefusal.TooMany, (await _channel.Members.PostAsync(member.Id, "an 11th", null, null, false, false, Now.AddMinutes(1))).Refusal);

        Assert.Equal(ChannelRefusal.None, (await JoinAsync(ask: ask.Id)).Refusal);
        Assert.Equal(ChannelRefusal.None, (await JoinAsync(ask: ask.Id)).Refusal);
        Assert.Equal(ChannelRefusal.TooMany, (await JoinAsync(ask: ask.Id)).Refusal);

        var left = await _channel.Members.LeaveAsync(member.Id, ChannelMember.ByItself, Now.AddMinutes(2));
        Assert.Equal((ChannelRefusal.None, Now.AddMinutes(2), ChannelMember.ByItself), (left.Refusal, left.Member!.Left, left.Member.Why));
        Assert.Equal(ChannelRefusal.NotMember, (await _channel.Members.PostAsync(member.Id, "after", null, null, false, false, Now.AddMinutes(3))).Refusal);
        Assert.Equal(ChannelRefusal.NotMember, (await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(3))).Refusal);
        Assert.Equal(ChannelRefusal.NotMember, (await _channel.Members.LeaveAsync(member.Id, ChannelMember.ByPerson, Now.AddMinutes(3))).Refusal);
        Assert.All((await _store.OfAsync(ask.Id)).Where(m => m.Entry == member.Entry), m => Assert.Equal(member.Id, m.Session));
        Assert.Equal(ChannelRefusal.None, (await JoinAsync(ask: ask.Id)).Refusal);

        for (var i = 0; i < 2; i++)
        {
            var (other, _) = await AskedAsync($"another check, number {i}");
            for (var j = 0; j < 2; j++) Assert.Equal(ChannelRefusal.None, (await JoinAsync(ask: other.Id)).Refusal);
        }

        var (last, _) = await AskedAsync("one more check");
        Assert.Equal(ChannelRefusal.None, (await JoinAsync(ask: last.Id)).Refusal);
        Assert.Equal(ChannelRefusal.TooMany, (await JoinAsync(ask: last.Id)).Refusal);

        var helper = await _sessions.CreateAsync(null, "reports", "stub", Now, kind: SessionKind.Chat, serves: session.Id, ownEntry: true);
        Assert.Equal(ChannelRefusal.Helper, (await JoinAsync(line: helper.Id)).Refusal);
        Assert.Equal(ChannelRefusal.NoSession, (await JoinAsync(line: "nosuch12")).Refusal);
        Assert.Equal(ChannelRefusal.NotFound, (await JoinAsync(ask: "nosuch")).Refusal);
    }

    /// <summary>A member id opens no session door: the connector's two channel tools answer it no-session, as every door that looks it up in the ledger.</summary>
    [Fact]
    public async Task CHANNELJOIN1a_the_connector_and_a_session_door_refuse_a_member_id()
    {
        var (ask, _) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;

        Assert.Equal(ChannelRefusal.NoSession, (await _channel.PostAsSessionAsync(member.Id, "as a session", null, null, false, Now)).Refusal);
        Assert.Equal(ChannelRefusal.NoSession, (await _channel.ReadAsSessionAsync(member.Id, null)).Refusal);
        Assert.Same(ChannelWaiting.None, await _channel.WaitingForAsync(member.Id));
        Assert.Null(await _channel.Parts.WaitingAsync(member.Id));
    }

    /// <summary>🔴 A teammate's message mirrored after a read keeps its older moment, and is still handed by the next read.</summary>
    [Fact]
    public async Task A_member_is_handed_a_message_that_arrives_behind_its_mark()
    {
        await _store.MirrorAsync(new ChannelMessage("m1", "abc123", "work", Now, null, null, null, null, null, null, false, "first", Origin: "machine-b"));
        var member = (await JoinAsync(ask: "abc123")).Member!;
        Assert.Equal(["first"], (await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(2))).Read!.Messages.Select(m => m.Text));

        await _store.MirrorAsync(new ChannelMessage("m2", "abc123", "work", Now.AddMinutes(1), null, null, null, null, null, null, false, "late", Origin: "machine-b"));

        Assert.Equal(["late"], (await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(3))).Read!.Messages.Select(m => m.Text));
        Assert.Empty((await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(4))).Read!.Messages);
    }

    [Fact]
    public async Task A_member_of_a_line_is_handed_a_post_kept_after_a_read_that_its_moment_precedes()
    {
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var member = (await JoinAsync(line: chat.Id)).Member!;
        Assert.Empty((await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(3))).Read!.Messages);

        await _channel.PostAsSessionAsync(chat.Id, "kept late", null, null, false, Now.AddMinutes(1));

        Assert.Equal(["kept late"], (await _channel.Members.ReadAsync(member.Id, null, Now.AddMinutes(4))).Read!.Messages.Select(m => m.Text));
    }

    /// <summary>On a line on no ask, the member speaks on the line's own channel, kept apart and never travelling, under its own id.</summary>
    [Fact]
    public async Task CHANNELJOIN1a_a_member_of_a_lines_own_channel_posts_there_under_its_id()
    {
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Path.Combine(_root, "trees", "chat"))).Session!;
        var member = (await JoinAsync(line: chat.Id)).Member!;
        var origin = $"session:{chat.Id}";

        Assert.Equal(origin, member.Origin);
        var posted = await _channel.Members.PostAsync(member.Id, "on the line", null, null, false, false, Now.AddMinutes(1));

        Assert.Equal(ChannelRefusal.None, posted.Refusal);
        var kept = Assert.Single(await _store.Addressed.OfAsync(origin));
        Assert.Equal((member.Id, (string?)null, true), (kept.Session, kept.Part, kept.Outside));
        Assert.Contains((await _channel.ReadAsSessionAsync(chat.Id, null, now: Now.AddMinutes(2))).Read!.Messages, m => m.Id == kept.Id && !m.ByPerson);
    }

    /// <summary>
    /// On a teammate's ask the member's message is kept as this machine's own and travels to the holder with its id as its poster, where it
    /// reads as an agent's claim, never the person's: the id marks it outside on either machine, with no field an older build must know.
    /// </summary>
    [Fact]
    public async Task CHANNELJOIN1a_a_members_message_on_a_teammates_ask_travels_as_an_agents_claim()
    {
        await _store.MirrorAsync(new ChannelMessage(
            "m1", "abc123", "work", Now, null, null, null, null, null, null, false, "from the holder's person", Origin: "machine-b"));
        var member = (await JoinAsync(ask: "abc123")).Member!;

        Assert.Equal(ChannelRefusal.None, (await _channel.Members.PostAsync(member.Id, "seen from here", null, null, false, false, Now.AddMinutes(1))).Refusal);

        var pushed = (await _store.OwnChangedSinceAsync(0, "work")).Select(each => each.Message).Single();
        var arrived = Assert.Single(ChannelWire.ReadFeed(ChannelWire.Feed([pushed]))!);
        Assert.Equal(member.Id, arrived.Session);
        Assert.False(arrived.ByPerson);
        Assert.True(arrived.Outside);
        Assert.True((arrived with { Session = $"machine-a/{member.Id}" }).Outside);
        Assert.False((arrived with { Session = "machine-a/0a1b2c3d" }).Outside);
    }

    /// <summary>Each join and leave journals a <c>member</c> row of the channel's family, so a reader of the journal redraws who is on it.</summary>
    [Fact]
    public async Task CHANNELJOIN1a_each_join_and_leave_journals_a_member_row()
    {
        var (ask, _) = await AskedAsync();
        var member = (await JoinAsync(ask: ask.Id)).Member!;
        await _channel.Members.LeaveAsync(member.Id, ChannelMember.ByPerson, Now.AddMinutes(1));

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT record, ask, entry, from_session, marks FROM event_journal WHERE family = 'channel' AND change = 'member' ORDER BY position";
        var rows = new List<(string, string?, string?, string?, string?)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        Assert.Equal(
            [(member.Id, ask.Id, member.Entry, member.Id, "joined"), (member.Id, ask.Id, member.Entry, member.Id, "removed")],
            rows);
    }
}
