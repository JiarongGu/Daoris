using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

public sealed class SessionOpensTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var quests = _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", true, "A repo.", [], [], [], 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<Session> RecordAsync(SessionState state = SessionState.Completed, string? from = null, string? quest = null)
    {
        var record = await _sessions.CreateAsync(quest, "Owner", "stub", Now, tree: "/trees/owner", kind: quest is null ? SessionKind.Chat : SessionKind.Driven, from: from);
        return (await _sessions.SetStateAsync(record.Id, state, null, null, null, Now))!;
    }

    private Task<SessionOpenOutcome> OpenAsync(string from, string key, string repository = "Owner") =>
        _ledger.OpenChatAsync(repository, "stub", Now, from: from, opens: key);

    [Fact]
    public async Task A_repeat_is_answered_before_any_check_and_after_a_planted_word_is_taken()
    {
        var left = await RecordAsync();
        var opened = await OpenAsync(left.Id, "move/1");
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var word = new SaidWord("planted", "waiting", Now, [], Key: "producer/1");
        await _sessions.KeepSaidAsync(opened.Session!.Id, word);
        await _sessions.TakeSaidAsync(opened.Session.Id, [word.Id]);
        var repeat = await OpenAsync("missing", "move/1", "unregistered");
        Assert.Equal(SessionOpenRefusal.None, repeat.Refusal);
        Assert.Equal(opened.Session.Id, repeat.Session!.Id);
        Assert.Equal(2, (await _sessions.ListAsync(includeClosed: true)).Count);
        Assert.Empty((await _sessions.FindAsync(left.Id))!.Said);
    }

    [Fact]
    public async Task An_older_delete_keeps_a_gone_receipt_until_the_record_left_goes()
    {
        var left = await RecordAsync();
        var opened = await OpenAsync(left.Id, "move/1");
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE session_opens SET transfer = '[{\"Word\":\"w\",\"Key\":\"k\",\"Transfer\":\"t\",\"Order\":0}]' WHERE key = 'move/1'";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", opened.Session!.Id);
        await command.ExecuteNonQueryAsync();
        var gone = await OpenAsync(left.Id, "move/1");
        Assert.Equal("Gone", gone.Refusal.ToString());
        var receipt = (await _sessions.Opens.FindAsync("move/1"))!;
        Assert.True(receipt.Gone);
        Assert.Empty(receipt.Transfer);
        Assert.Single(await _sessions.ListAsync(includeClosed: true));
        command.Parameters["$id"].Value = left.Id;
        await command.ExecuteNonQueryAsync();
        Assert.Equal("Gone", (await OpenAsync(left.Id, "move/1")).Refusal.ToString());
        command.CommandText = "SELECT COUNT(*) FROM session_opens";
        command.Parameters.Clear();
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData(SessionState.Working)]
    [InlineData(SessionState.Queued)]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.AwaitingPerson)]
    public async Task An_active_record_cannot_be_left(SessionState state)
    {
        var left = await RecordAsync(state);
        var answer = await OpenAsync(left.Id, "active");
        Assert.Equal("Superseded", answer.Refusal.ToString());
        Assert.Contains(left.Id, answer.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_successor_or_a_later_run_of_its_quest_is_the_head(bool questRun)
    {
        var left = await RecordAsync(quest: questRun ? "quest" : null);
        var head = await RecordAsync(from: questRun ? null : left.Id, quest: questRun ? "quest" : null);
        var answer = await OpenAsync(left.Id, "late");
        Assert.Equal("Superseded", answer.Refusal.ToString());
        Assert.Contains(head.Id, answer.Message);
    }

    [Theory]
    [InlineData(SessionState.Completed, "working")]
    [InlineData(SessionState.AwaitingPerson, "working")]
    public async Task Every_go_on_of_a_record_left_is_refused(SessionState state, string target)
    {
        var left = await RecordAsync();
        await _sessions.KeepSaidAsync(left.Id, new SaidWord("word", "go", Now, []));
        var opened = await OpenAsync(left.Id, "move");
        await _sessions.SetStateAsync(opened.Session!.Id, SessionState.Completed, null, null, null, Now);
        // A parked record is active and cannot itself be keyed-opened. Plant the older writer's state to hold every exit guard.
        await _sessions.SetStateAsync(left.Id, state, null, null, null, Now);
        var answer = await _ledger.AdvanceAsync(left.Id, target, null, null, null, Now);
        Assert.NotEqual(SessionAdvanceRefusal.None, answer.Refusal);
        Assert.Contains(opened.Session!.Id, answer.Message);
        Assert.Equal(state, (await _sessions.FindAsync(left.Id))!.State);
    }

    [Fact]
    public async Task A_committed_go_on_wins_over_the_keyed_open()
    {
        var left = await RecordAsync();
        await _sessions.KeepSaidAsync(left.Id, new SaidWord("word", "go", Now, []));
        Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(left.Id, "working", null, null, null, Now)).Refusal);
        Assert.Equal("Superseded", (await OpenAsync(left.Id, "move")).Refusal.ToString());
    }

    [Fact]
    public async Task A_record_that_ended_waiting_on_its_helper_cannot_move_back_to_the_person_after_an_open()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Work", "Why", Now);
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);
        var left = await RecordAsync(quest: quest.Id);
        await _sessions.SetStateAsync(left.Id, SessionState.Completed, "waiting", null, null, Now,
            noteParts: """[{"code":"ended.awaits-helper"}]""");
        var opened = await _ledger.OpenChatAsync("Owner", "stub", Now, from: left.Id, opens: "helper/move", tree: "/trees/owner-next");
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        await _sessions.SetStateAsync(opened.Session!.Id, SessionState.Completed, null, null, null, Now);
        var answer = await _ledger.AdvanceAsync(left.Id, "awaiting-person", null, null, null, Now);
        Assert.Equal(SessionAdvanceRefusal.Terminal, answer.Refusal);
        Assert.Contains(opened.Session!.Id, answer.Message);
        Assert.Equal(SessionState.Completed, (await _sessions.FindAsync(left.Id))!.State);
    }

    [Fact]
    public async Task A_chain_across_kinds_and_a_cycle_are_judged_inside_the_step()
    {
        var first = await RecordAsync(quest: "q");
        var second = await RecordAsync(from: first.Id);
        var third = await RecordAsync(from: second.Id);
        Assert.Equal(third.Id, (await Lines.HeadOf(_sessions, first))!.Id);
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET from_session = $third WHERE id = $first";
        command.Parameters.AddWithValue("$third", third.Id);
        command.Parameters.AddWithValue("$first", first.Id);
        await command.ExecuteNonQueryAsync();
        Assert.Null(await Lines.HeadOf(_sessions, first));
        Assert.Equal("Superseded", (await OpenAsync(first.Id, "cycle")).Refusal.ToString());
    }

    [Fact]
    public async Task A_replay_keeps_its_transfer_after_the_words_leave_said()
    {
        var left = await RecordAsync();
        var opened = await OpenAsync(left.Id, "recorded/transfer");
        // d-i moves none. Plant the row d-ii would write, using the service's own statements, to prove replay reads the receipt.
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE session_opens SET transfer = $transfer WHERE key = 'recorded/transfer'";
        command.Parameters.AddWithValue("$transfer", """[{"Word":"w","Key":"producer","Transfer":"t","Order":0}]""");
        await command.ExecuteNonQueryAsync();
        var word = new SaidWord("w", "word", Now, [], Key: "producer");
        await _sessions.KeepSaidAsync(opened.Session!.Id, word);
        await _sessions.TakeSaidAsync(opened.Session.Id, [word.Id]);
        var replay = await OpenAsync(left.Id, "recorded/transfer");
        Assert.Equal(opened.Session.Id, replay.Session!.Id);
        var transfer = Assert.Single(replay.Transfer);
        Assert.Equal(new OpenTransfer("w", "producer", "t", 0), transfer);
    }

    [Fact]
    public async Task A_driven_open_replays_before_the_quest_and_access_checks()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Work", "Why", Now);
        var left = await RecordAsync(quest: quest.Id);
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now, from: left.Id, opens: "driven");
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var repeat = await _ledger.OpenAsync("missing", "stub", Now, from: "missing", opens: "driven",
            access: new Dictionary<string, string> { ["invalid"] = "invalid" });
        Assert.Equal(SessionOpenRefusal.None, repeat.Refusal);
        Assert.Equal(opened.Session!.Id, repeat.Session!.Id);
    }

    [Fact]
    public async Task The_receipt_and_the_record_open_are_one_transaction()
    {
        var left = await RecordAsync();
        await using var command = _connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER fail_receipt BEFORE INSERT ON session_opens BEGIN SELECT RAISE(ABORT, 'receipt failed'); END";
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<SqliteException>(() => OpenAsync(left.Id, "failed"));
        Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Null(await _sessions.Opens.FindAsync("failed"));
    }

    [Fact]
    public async Task Two_keys_on_two_records_open_two_destinations_and_unkeyed_opens_stay_as_today()
    {
        var a = await RecordAsync();
        var b = await RecordAsync();
        var first = await OpenAsync(a.Id, "a");
        // Another tree is needed while the first destination is queued.
        await _sessions.SetStateAsync(first.Session!.Id, SessionState.Completed, null, null, null, Now);
        var second = await OpenAsync(b.Id, "b");
        Assert.NotEqual(first.Session.Id, second.Session!.Id);
        Assert.Equal(first.Session.Id, (await _sessions.Opens.FindAsync("a"))!.Opened);
        Assert.Equal(second.Session.Id, (await _sessions.Opens.FindAsync("b"))!.Opened);
        await _sessions.SetStateAsync(second.Session.Id, SessionState.Completed, null, null, null, Now);
        var unkeyed = await _ledger.OpenChatAsync("Owner", "stub", Now, from: a.Id);
        Assert.Equal(SessionOpenRefusal.None, unkeyed.Refusal);
        Assert.NotEqual(first.Session.Id, unkeyed.Session!.Id);
    }
}
