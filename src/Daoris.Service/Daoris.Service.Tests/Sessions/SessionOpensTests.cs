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
        // Row d-ii part 3: an element that names no id on the record opened reads as its word's own, which a moved word keeps.
        Assert.Equal(new OpenTransfer("w", "producer", "t", 0, "w"), transfer);
    }

    // ---- row d-ii part 3: the open moves the words (the design §3.4 rule 3) ----

    private Task<SessionOpenOutcome> MoveAsync(string from, string key, IReadOnlyList<string> words, IReadOnlyList<string> read, string? tree = null) =>
        _ledger.OpenChatAsync("Owner", "stub", Now, tree: tree, from: from, opens: key, moves: new OpenMoves(from, words, read));

    private async Task<IReadOnlyList<SaidWord>> SaidAsync(string id) => (await _sessions.FindAsync(id))!.Said;

    /// <summary>
    /// 🔴 The selection is .NET's: after the course read A, the person said a late word and a helper answered, another agent's claim was passed
    /// on, and the person withdrew one word the course named. The open moves the named words still waiting and the person's late word, in A's
    /// order, each under its producer key with its transfer id; keeps Daoris's word, the helper's answer, the agent's claim and the person's
    /// late word a dispatcher attempted on A; keeps every field of a moved word but the attempt, which was A's; and B's later word goes behind.
    /// </summary>
    [Fact]
    public async Task The_open_moves_the_named_words_still_waiting_and_the_persons_late_ones_in_order_and_keeps_the_rest()
    {
        var a = await RecordAsync();
        var first = new SaidWord("p0000001", "the first", Now.AddSeconds(-9), ["notes.md"], Reopens: true, Key: "press/1", Level: SaidLevel.Urgent);
        var refused = new SaidWord("p0000002", "refused at a limit", Now.AddSeconds(-8), [], Key: "press/2");
        var withdrawn = new SaidWord("p0000003", "taken back", Now.AddSeconds(-7), []);
        var daoris = new SaidWord("d0000001", "Daoris's go-on", Now.AddSeconds(-6), [], From: SaidFrom.Daoris, Key: "daoris/go-on");
        foreach (var word in new[] { first, refused, withdrawn, daoris }) await _sessions.KeepSaidAsync(a.Id, word);
        await _sessions.MarkAttemptAsync(a.Id, refused.Id, Now);
        var read = (await SaidAsync(a.Id)).Select(word => word.Id).ToList();
        string[] named = [first.Id, refused.Id, withdrawn.Id];

        var late = new SaidWord("p0000004", "a late word", Now.AddSeconds(-5), ["late.png"], Key: "press/4", Level: SaidLevel.Ordinary);
        var helper = new SaidWord("h0000001", "the helper's answer", Now.AddSeconds(-4), [], Helper: "helper-1", Key: "helper:1");
        var agent = new SaidWord("g0000001", "another agent's claim", Now.AddSeconds(-3), [], Channel: "message-1", From: SaidFrom.Agent);
        var attempted = new SaidWord("p0000005", "attempted on A", Now.AddSeconds(-2), []);
        var keyless = new SaidWord("p0000006", "no key at all", Now.AddSeconds(-1), []);
        foreach (var word in new[] { late, helper, agent, attempted, keyless }) await _sessions.KeepSaidAsync(a.Id, word);
        await _sessions.MarkAttemptAsync(a.Id, attempted.Id, Now);
        await _sessions.TakeSaidAsync(a.Id, [withdrawn.Id]);

        var opened = await MoveAsync(a.Id, "move/select", named, read);

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var b = opened.Session!.Id;
        Assert.Equal(
            [
                new OpenTransfer(first.Id, "press/1", $"move/select/{first.Id}", 0, first.Id),
                new OpenTransfer(refused.Id, "press/2", $"move/select/{refused.Id}", 1, refused.Id),
                new OpenTransfer(late.Id, "press/4", $"move/select/{late.Id}", 2, late.Id),
                new OpenTransfer(keyless.Id, null, $"move/select/{keyless.Id}", 3, keyless.Id),
            ],
            opened.Transfer);
        Assert.Equal(opened.Transfer, (await _sessions.Opens.FindAsync("move/select"))!.Transfer);

        var onB = await SaidAsync(b);
        Assert.Equal([first.Id, refused.Id, late.Id, keyless.Id], onB.Select(word => word.Id));
        Assert.Equal(onB.Select(word => word.Id), opened.Session.Said.Select(word => word.Id));
        foreach (var (moved, was) in onB.Zip(new[] { first, refused, late, keyless }))
        {
            Assert.True(SameWord(was with { Attempt = null }, moved), $"{moved.Id} keeps every field but A's attempt");
        }

        Assert.Equal([daoris.Id, helper.Id, agent.Id, attempted.Id], (await SaidAsync(a.Id)).Select(word => word.Id));

        await _sessions.SetStateAsync(b, SessionState.Completed, null, null, null, Now);
        var after = await _ledger.SayAsync(b, "said after the open", null, Now);
        Assert.Equal(SessionSayRefusal.None, after.Refusal);
        Assert.Equal([first.Id, refused.Id, late.Id, keyless.Id, after.Word!.Id], (await SaidAsync(b)).Select(word => word.Id));
    }

    private static bool SameWord(SaidWord one, SaidWord other) =>
        one.Id == other.Id && one.Text == other.Text && one.At == other.At && one.Files.SequenceEqual(other.Files) && one.Reopens == other.Reopens
        && one.By == other.By && one.Helper == other.Helper && one.Channel == other.Channel && one.From == other.From && one.Key == other.Key
        && one.Level == other.Level && one.Attempt == other.Attempt && one.Decision == other.Decision && one.Question == other.Question
        && one.Choice == other.Choice;

    /// <summary>
    /// 🔴 The transfer is stored, never reconstructed: the open's answer lost, every moved word delivered and taken on B, and a word an older
    /// build's say door kept on A meanwhile; the key's replay answers B and the complete, ordered transfer from the receipt, and moves nothing
    /// more, whatever the replay's own moves name.
    /// </summary>
    [Fact]
    public async Task A_replay_after_every_moved_word_was_taken_answers_the_destination_and_the_whole_transfer_and_moves_nothing_more()
    {
        var a = await RecordAsync();
        var one = new SaidWord("w0000001", "one", Now, [], Key: "press/1");
        var two = new SaidWord("w0000002", "two", Now, []);
        foreach (var word in new[] { one, two }) await _sessions.KeepSaidAsync(a.Id, word);
        var opened = await MoveAsync(a.Id, "move/lost", [one.Id, two.Id], [one.Id, two.Id]);
        Assert.Equal(2, opened.Transfer.Count);

        await _sessions.TakeSaidAsync(opened.Session!.Id, [one.Id, two.Id]);
        var older = new SaidWord("w0000003", "kept by an older door", Now, []);
        await _sessions.KeepSaidAsync(a.Id, older);

        var replay = await MoveAsync(a.Id, "move/lost", [one.Id, two.Id, older.Id], []);

        Assert.Equal(SessionOpenRefusal.None, replay.Refusal);
        Assert.Equal(opened.Session.Id, replay.Session!.Id);
        Assert.Equal(opened.Transfer, replay.Transfer);
        Assert.Empty(await SaidAsync(opened.Session.Id));
        Assert.Equal([older.Id], (await SaidAsync(a.Id)).Select(word => word.Id));
        Assert.Equal(2, (await _sessions.ListAsync(includeClosed: true)).Count);
    }

    /// <summary>🔴 A receipt that cannot be written rolls the whole open back, its move with it: the words wait on A and nothing settled.</summary>
    [Fact]
    public async Task A_receipt_that_cannot_be_written_rolls_back_the_open_and_its_move()
    {
        var a = await RecordAsync();
        var word = new SaidWord("w0000001", "stays", Now, [], Key: "press/1");
        await _sessions.KeepSaidAsync(a.Id, word);
        await using var command = _connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER fail_receipt BEFORE INSERT ON session_opens BEGIN SELECT RAISE(ABORT, 'receipt failed'); END";
        await command.ExecuteNonQueryAsync();

        await Assert.ThrowsAsync<SqliteException>(() => MoveAsync(a.Id, "failed", [word.Id], [word.Id]));

        Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Equal([word.Id], (await SaidAsync(a.Id)).Select(each => each.Id));
        command.CommandText = "SELECT COUNT(*) FROM said_settled";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    /// <summary>
    /// 🔴 The source is its own contract: start-from moves A's words into B, which names no <c>from</c>, so the record they move from is the
    /// move's own. A stays its line's head: a word said to it is kept there and its go-on is never refused as a record a keyed open left.
    /// A source that is running is refused as a record left would be.
    /// </summary>
    [Fact]
    public async Task Start_from_moves_words_from_its_own_source_into_a_record_with_no_from_and_leaves_that_source_its_lines_head()
    {
        var a = await RecordAsync();
        var word = new SaidWord("w0000001", "start a new one", Now, ["plan.md"], Key: "press/start");
        await _sessions.KeepSaidAsync(a.Id, word);

        var opened = await _ledger.OpenChatAsync("Owner", "stub", Now, opens: "start/1", moves: new OpenMoves(a.Id, [word.Id], [word.Id]));

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        Assert.Null(opened.Session!.From);
        Assert.Equal([word.Id], (await SaidAsync(opened.Session.Id)).Select(each => each.Id));
        Assert.Empty(await SaidAsync(a.Id));
        Assert.Equal(a.Id, (await _sessions.Opens.FindAsync("start/1"))!.Left);

        await _sessions.SetStateAsync(opened.Session.Id, SessionState.Completed, null, null, null, Now);
        Assert.Equal(SessionSayRefusal.None, (await _ledger.SayAsync(a.Id, "and more here", null, Now)).Refusal);
        Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(a.Id, "working", null, null, null, Now)).Refusal);

        var running = await _ledger.OpenChatAsync("Owner", "stub", Now, tree: "/trees/owner-2", opens: "start/2", moves: new OpenMoves(a.Id, [], []));
        Assert.Equal(SessionOpenRefusal.Superseded, running.Refusal);
        Assert.Null(await _sessions.Opens.FindAsync("start/2"));
    }

    /// <summary>
    /// 🔴 Only a request that asks moves words: an older desktop's keyed open, which names no moves, opens and moves nothing, as an unkeyed open
    /// does; moves without a key are refused before anything opens, since a move that no key answers could not be asked again.
    /// </summary>
    [Fact]
    public async Task An_open_that_names_no_moves_moves_nothing_and_moves_without_a_key_are_refused()
    {
        var a = await RecordAsync();
        var word = new SaidWord("w0000001", "waits", Now, [], Key: "press/1");
        await _sessions.KeepSaidAsync(a.Id, word);

        var keyed = await OpenAsync(a.Id, "older/desktop");

        Assert.Equal(SessionOpenRefusal.None, keyed.Refusal);
        Assert.Empty(keyed.Transfer);
        Assert.Empty((await _sessions.Opens.FindAsync("older/desktop"))!.Transfer);
        Assert.Equal([word.Id], (await SaidAsync(a.Id)).Select(each => each.Id));
        Assert.Empty(await SaidAsync(keyed.Session!.Id));

        var b = await RecordAsync();
        await _sessions.KeepSaidAsync(b.Id, word with { Id = "w0000002" });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ledger.OpenChatAsync("Owner", "stub", Now, tree: "/trees/owner-2", from: b.Id, moves: new OpenMoves(b.Id, ["w0000002"], [])));
        var quest = await _quests.PublishAsync("Asker", "Owner", "Work", "Why", Now);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ledger.OpenAsync(quest.Id, "stub", Now, from: b.Id, moves: new OpenMoves(b.Id, ["w0000002"], [])));
        Assert.Equal(["w0000002"], (await SaidAsync(b.Id)).Select(each => each.Id));
        Assert.Equal(3, (await _sessions.ListAsync(includeClosed: true)).Count);
    }

    /// <summary>
    /// 🔴 Moves that do not read are refused after the key's replay and before anything opens: a source other than the record left, a blank
    /// source, a blank id, and lists that are missing. A committed key is answered from its receipt whatever its retry's moves say.
    /// </summary>
    [Fact]
    public async Task Moves_that_do_not_read_are_refused_after_the_replay_and_before_anything_opens()
    {
        var a = await RecordAsync();
        var other = await RecordAsync();
        var word = new SaidWord("w0000001", "waits", Now, []);
        await _sessions.KeepSaidAsync(a.Id, word);

        foreach (var moves in new[]
                 {
                     new OpenMoves(other.Id, [word.Id], []), new OpenMoves(" ", [word.Id], []), new OpenMoves(a.Id, [" "], []),
                     new OpenMoves(a.Id, [], [""]), new OpenMoves(a.Id, null!, []), new OpenMoves(a.Id, [], null!),
                 })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _ledger.OpenChatAsync("Owner", "stub", Now, from: a.Id, opens: "malformed", moves: moves));
        }

        Assert.Null(await _sessions.Opens.FindAsync("malformed"));
        Assert.Equal(2, (await _sessions.ListAsync(includeClosed: true)).Count);
        Assert.Equal([word.Id], (await SaidAsync(a.Id)).Select(each => each.Id));

        var opened = await MoveAsync(a.Id, "committed", [word.Id], [word.Id]);
        var replay = await _ledger.OpenChatAsync("Owner", "stub", Now, from: a.Id, opens: "committed", moves: new OpenMoves(other.Id, [" "], null!));
        Assert.Equal(opened.Session!.Id, replay.Session!.Id);
        Assert.Equal(opened.Transfer, replay.Transfer);
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
