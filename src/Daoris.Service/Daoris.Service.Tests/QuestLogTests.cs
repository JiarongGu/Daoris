using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest is its operations in order (D68 §1, SYNC1): every verb appends to the quest's history, and
/// the status table is a cache of replaying that history through the one transition table. Local
/// behaviour is unchanged — <see cref="QuestStoreTests"/> passes on the log as it did on the table.
/// </summary>
public sealed class QuestLogTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string title = "Adopt the canon") =>
        _quests.PublishAsync("Asker", "Owner", title, "Four rules collide.", Now);

    /// <summary>
    /// Everything a reader can see of a quest, as one comparable value — the record's own equality
    /// compares its lists by reference, which would call two identical quests different.
    /// </summary>
    private static string Shape(Quest quest) => string.Join(" | ",
        quest.Id, quest.From, quest.To, quest.Title, quest.Body, quest.Status, quest.Note ?? "-",
        quest.Filed.ToString("O"), quest.Updated.ToString("O"), quest.Workspace,
        string.Join(",", quest.Conflicts.Select(c => $"{c.Machine}:{c.Attempted}:{c.Note}:{c.At:O}")),
        string.Join(",", quest.Links),
        string.Join(",", quest.Attachments.Select(a => $"{a.Name}:{a.Sha256}:{a.Bytes}")),
        string.Join(",", quest.Then.Select(s => $"{s.To}:{s.Title}:{s.Body}")),
        quest.Parent ?? "-");

    // ——— Ids (design §7): every machine will hold every quest touching its repositories, so 24 bits
    // were enough for one machine and are not enough for a team.

    [Fact]
    public async Task A_quest_id_is_twelve_hex_characters_and_still_derived_from_the_ask()
    {
        var quest = await Publish();

        var again = await _quests.PublishAsync("Asker", "Owner", "  Adopt the canon ", "Other words.", Now.AddDays(1));

        Assert.Matches("^[0-9a-f]{12}$", quest.Id);
        Assert.Equal(quest.Id, again.Id);
        Assert.Equal("Four rules collide.", again.Body);
    }

    /// <summary>
    /// A quest published before the id widened keeps its six characters — it is quoted in commit
    /// messages — and it is still the answer to its own ask: an agent retrying an ask it made before
    /// the upgrade must not open a second copy of it.
    /// </summary>
    [Fact]
    public async Task A_quest_from_before_the_wider_id_still_answers_its_own_ask()
    {
        var legacyId = QuestStore.MakeId("A", "B", "Old ask")[..6];
        await using var old = await LegacyStoreAsync(
            $"('{legacyId}', 'A', 'B', 'Old ask', 'why', 'Open', NULL, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', NULL, 'default')");
        var store = await QuestStore.OpenAsync(old);

        var republished = await store.PublishAsync("A", "B", "Old ask", "why, again", Now);

        Assert.Equal(legacyId, republished.Id);
        Assert.Single(await store.ListAsync());
    }

    // ——— The history.

    [Fact]
    public async Task Every_verb_is_an_operation_in_the_quests_history()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddHours(2));

        var history = await _quests.HistoryAsync(quest.Id);

        Assert.Equal(
            [QuestOperationKind.Published, QuestOperationKind.Taken, QuestOperationKind.Done],
            history.Select(o => o.Kind));
        Assert.All(history, o => Assert.Equal(quest.Id, o.Quest));
        Assert.All(history, o => Assert.Equal(_quests.Machine, o.Machine));
        Assert.Equal([Now, Now.AddHours(1), Now.AddHours(2)], history.Select(o => o.At));
        Assert.Equal("Landed.", history[2].Note);
        Assert.Equal("Four rules collide.", history[0].Published!.Body);
    }

    /// <summary>
    /// A refused move is not an event in the quest's life: the losing take and the late decline leave
    /// no trace, so a history never holds an operation its own replay would have to skip.
    /// </summary>
    [Fact]
    public async Task A_refused_move_writes_no_history()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(2));
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, null, Now.AddHours(3));
        await _quests.MoveAsync(quest.Id, QuestStatus.Declined, "Too late.", Now.AddHours(4));

        Assert.Equal(
            [QuestOperationKind.Published, QuestOperationKind.Taken, QuestOperationKind.Done],
            (await _quests.HistoryAsync(quest.Id)).Select(o => o.Kind));
    }

    /// <summary>Publishing an ask already held is an answer, not an operation.</summary>
    [Fact]
    public async Task Publishing_the_same_ask_twice_writes_one_operation()
    {
        var quest = await Publish();
        await Publish();

        Assert.Single(await _quests.HistoryAsync(quest.Id));
    }

    /// <summary>
    /// The sequence is the machine's, not the quest's (design §2): machine + sequence names one
    /// operation anywhere, which is what a remote will key what it accepts by.
    /// </summary>
    [Fact]
    public async Task The_sequence_counts_this_machines_operations_across_every_quest()
    {
        var one = await Publish("One");
        var two = await Publish("Two");
        await _quests.MoveAsync(one.Id, QuestStatus.Taken, null, Now);
        await _quests.MoveAsync(two.Id, QuestStatus.Declined, "Not ours.", Now);

        var sequences = (await _quests.HistoryAsync(one.Id))
            .Concat(await _quests.HistoryAsync(two.Id))
            .Select(o => o.Sequence)
            .Order();

        Assert.Equal([1L, 2L, 3L, 4L], sequences);
    }

    /// <summary>
    /// The claim SYNC1 exists to make true: what the platform reads is what replaying the history
    /// gives — for every quest, whatever it carries and however it ended, a chain's step included.
    /// </summary>
    [Fact]
    public async Task The_status_table_is_what_the_history_replays_to()
    {
        await Publish("Still open");
        var taken = await Publish("Taken");
        await _quests.MoveAsync(taken.Id, QuestStatus.Taken, null, Now.AddHours(1));
        var declined = await Publish("Declined");
        await _quests.MoveAsync(declined.Id, QuestStatus.Declined, "Deliberately local.", Now.AddHours(2));
        var chained = await _quests.PublishAsync(
            "Intake", "Owner", "Develop the media config", "Read the names from config.", Now, "circle",
            links: ["https://tickets.example/T-1"],
            attachments: [new QuestAttachment("before.png", new string('a', 64), 2048)],
            then: [new QuestStep("Owner", "Verify {parent}", "Check {parent} landed.")]);
        var next = (await _quests.MoveAsync(chained.Id, QuestStatus.Done, "Landed.", Now.AddHours(3))).FollowUp!;

        var held = await _quests.ListAsync(includeClosed: true);

        Assert.Equal(5, held.Count);
        Assert.Contains(held, q => q.Id == next.Id && q.Parent == chained.Id);
        foreach (var quest in held)
        {
            var replayed = QuestLog.Replay(await _quests.HistoryAsync(quest.Id));
            Assert.NotNull(replayed);
            Assert.Equal(Shape(quest), Shape(replayed));
        }
    }

    /// <summary>
    /// A chain's next step is published BY the close, so its history begins in the same transaction:
    /// the step's own `published` follows the parent's `done` in this machine's sequence.
    /// </summary>
    [Fact]
    public async Task A_chains_next_step_is_published_by_the_close_in_the_same_sequence()
    {
        var parent = await _quests.PublishAsync(
            "Intake", "Owner", "Develop", "b", Now, then: [new QuestStep("Owner", "Verify {parent}", "c")]);

        var next = (await _quests.MoveAsync(parent.Id, QuestStatus.Done, null, Now.AddHours(1))).FollowUp!;

        var done = (await _quests.HistoryAsync(parent.Id))[^1];
        var published = Assert.Single(await _quests.HistoryAsync(next.Id));
        Assert.Equal(QuestOperationKind.Published, published.Kind);
        Assert.Equal(done.Sequence + 1, published.Sequence);
        Assert.Equal(parent.Id, published.Published!.Parent);
    }

    // ——— The replay and the table, pure.

    private static QuestOperation Op(
        long sequence, QuestOperationKind kind, string? note = null, Quest? published = null) =>
        new("q1", kind, "m1", sequence, Now.AddHours(sequence), note, published);

    private static readonly Quest Asked = new(
        "q1", "Asker", "Owner", "Do it", "why", QuestStatus.Open, null, Now, Now);

    /// <summary>
    /// Replay IS the transition table applied in order: a take after a take and anything after a
    /// close are not moves, whichever machine's history they came from — so no order of operations
    /// can reach a state the table forbids. SYNC2's rebase stands on exactly this.
    /// </summary>
    [Fact]
    public void Replay_goes_through_the_transition_table()
    {
        var replayed = QuestLog.Replay(
        [
            Op(0, QuestOperationKind.Published, published: Asked),
            Op(1, QuestOperationKind.Taken),
            Op(2, QuestOperationKind.Taken, "A second taker."),
            Op(3, QuestOperationKind.Done, "Landed."),
            Op(4, QuestOperationKind.Declined, "Too late."),
        ])!;

        Assert.Equal(QuestStatus.Done, replayed.Status);
        Assert.Equal("Landed.", replayed.Note);
        Assert.Equal(Now, replayed.Filed);
        Assert.Equal(Now.AddHours(3), replayed.Updated);
    }

    /// <summary>The ask is the quest: a history with nothing published is no quest at all.</summary>
    [Fact]
    public void A_history_with_nothing_published_is_no_quest()
    {
        Assert.Null(QuestLog.Replay([Op(1, QuestOperationKind.Taken)]));
        Assert.Null(QuestLog.Replay([]));
    }

    /// <summary>D47 §5's table: taken only from open; closed only from live; nothing leaves a close; nothing reopens.</summary>
    [Theory]
    [InlineData(QuestStatus.Open, QuestStatus.Taken, true)]
    [InlineData(QuestStatus.Taken, QuestStatus.Taken, false)]
    [InlineData(QuestStatus.Open, QuestStatus.Done, true)]
    [InlineData(QuestStatus.Taken, QuestStatus.Done, true)]
    [InlineData(QuestStatus.Open, QuestStatus.Declined, true)]
    [InlineData(QuestStatus.Taken, QuestStatus.Declined, true)]
    [InlineData(QuestStatus.Done, QuestStatus.Taken, false)]
    [InlineData(QuestStatus.Done, QuestStatus.Declined, false)]
    [InlineData(QuestStatus.Declined, QuestStatus.Done, false)]
    [InlineData(QuestStatus.Taken, QuestStatus.Open, false)]
    [InlineData(QuestStatus.Open, QuestStatus.Open, false)]
    public void The_transition_table_is_D47s(QuestStatus from, QuestStatus to, bool allowed) =>
        Assert.Equal(allowed, QuestTransitions.Allows(from, to));

    // ——— The machine.

    /// <summary>
    /// A stable machine id, kept in the store beside the sequence it numbers (design §2): two hosts
    /// over one file are one machine, a reopen is the same machine, and another store is another.
    /// </summary>
    [Fact]
    public async Task The_machine_is_stable_across_opens_and_one_per_store()
    {
        var path = Path.Combine(Path.GetTempPath(), "daoris-machine-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        try
        {
            string first;
            await using (var one = new SqliteConnection($"Data Source={path};Pooling=False"))
            await using (var two = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await one.OpenAsync();
                await two.OpenAsync();
                first = (await QuestStore.OpenAsync(one)).Machine;
                Assert.Equal(first, (await QuestStore.OpenAsync(two)).Machine);
            }

            await using var reopened = new SqliteConnection($"Data Source={path};Pooling=False");
            await reopened.OpenAsync();

            Assert.Matches("^[0-9a-f]{16}$", first);
            Assert.Equal(first, (await QuestStore.OpenAsync(reopened)).Machine);
            Assert.NotEqual(first, _quests.Machine);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Pins what the store's commit-on-refusal rests on: a host serves concurrent requests over one
    /// connection, and a statement run while a quest transaction is open JOINS it rather than failing.
    /// A driver version that changed this would change which of the two hazards the store must guard.
    /// </summary>
    [Fact]
    public async Task A_statement_run_during_an_open_transaction_joins_it_rather_than_failing()
    {
        await using var transaction = _connection.BeginTransaction(deferred: false);
        await using var elsewhere = _connection.CreateCommand();
        elsewhere.CommandText = "CREATE TABLE joined (x INTEGER)";

        await elsewhere.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();

        await using var probe = _connection.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'joined'";
        Assert.Equal(0L, (long)(await probe.ExecuteScalarAsync())!);
    }

    /// <summary>
    /// A host answers requests at once over its one connection, and every quest write is now a
    /// transaction — which the driver does not nest. Two writes at the same moment (a take from the
    /// driver while the page publishes) must both land, not fail the second on the first's lock.
    /// </summary>
    [Fact]
    public async Task Quest_writes_at_the_same_moment_on_one_host_all_land()
    {
        var asked = await Task.WhenAll(Enumerable.Range(0, 24).Select(i =>
            Task.Run(() => _quests.PublishAsync("Asker", "Owner", $"Ask {i}", "why", Now))));
        var moved = await Task.WhenAll(asked.Select(quest =>
            Task.Run(() => _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1)))));

        Assert.All(moved, move => Assert.True(move.Moved));
        Assert.Equal(48, (await Task.WhenAll(asked.Select(q => _quests.HistoryAsync(q.Id)))).Sum(h => h.Count));
        Assert.Equal(
            Enumerable.Range(1, 48).Select(i => (long)i),
            (await Task.WhenAll(asked.Select(q => _quests.HistoryAsync(q.Id)))).SelectMany(h => h).Select(o => o.Sequence).Order());
    }

    // ——— A store from before the log.

    private static async Task<SqliteConnection> LegacyStoreAsync(string rows)
    {
        var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using var create = old.CreateCommand();
        create.CommandText = $"""
            CREATE TABLE quests (
              id TEXT PRIMARY KEY, sender TEXT NOT NULL, receiver TEXT NOT NULL, title TEXT NOT NULL,
              body TEXT NOT NULL, status TEXT NOT NULL, note TEXT NULL, filed TEXT NOT NULL, updated TEXT NOT NULL,
              home TEXT NULL, workspace TEXT NOT NULL DEFAULT 'default'
            );
            INSERT INTO quests VALUES {rows};
            """;
        await create.ExecuteNonQueryAsync();
        return old;
    }

    /// <summary>
    /// Quests are not derivable from anything, so a store from before the log is given each local
    /// quest's history from what its row says — once, however many hosts open it. A mirror row was a
    /// copy of a remote's quest, so it is dropped rather than migrated (design §8): the first fetch
    /// brings it back as history. After that the old quests move exactly as new ones do.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_log_gains_each_local_quests_history_once()
    {
        await using var old = await LegacyStoreAsync("""
            ('e1de11', 'A', 'B', 'Open ask', 'why', 'Open', NULL, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', NULL, 'default'),
            ('e1de12', 'A', 'B', 'Done ask', 'why', 'Done', 'landed', '2026-01-01T00:00:00Z', '2026-01-02T00:00:00Z', NULL, 'circle'),
            ('e1de13', 'A', 'B', 'Taken ask', 'why', 'Taken', NULL, '2026-01-01T00:00:00Z', '2026-01-03T00:00:00Z', NULL, 'default'),
            ('e1de14', 'A', 'B', 'Mirrored ask', 'why', 'Open', NULL, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 'remote', 'default')
            """);

        await QuestStore.OpenAsync(old);
        var store = await QuestStore.OpenAsync(old);

        var done = await store.HistoryAsync("e1de12");
        Assert.Equal([QuestOperationKind.Published, QuestOperationKind.Done], done.Select(o => o.Kind));
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), done[1].At);
        Assert.Equal("landed", done[1].Note);
        Assert.Equal("circle", done[0].Published!.Workspace);
        Assert.Empty(await store.HistoryAsync("e1de14"));
        Assert.Null(await store.FindAsync("e1de14"));

        foreach (var quest in await store.ListAsync(includeClosed: true))
        {
            Assert.Equal(Shape(quest), Shape(QuestLog.Replay(await store.HistoryAsync(quest.Id))!));
        }

        Assert.True((await store.MoveAsync("e1de11", QuestStatus.Taken, null, Now)).Moved);
        Assert.False((await store.MoveAsync("e1de13", QuestStatus.Taken, null, Now)).Moved);
        Assert.Equal(
            [QuestOperationKind.Published, QuestOperationKind.Taken],
            (await store.HistoryAsync("e1de11")).Select(o => o.Kind));
    }
}
