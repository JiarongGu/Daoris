using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Fetch, rebase, push (D68 §3, design §8), in process: two machines' stores and a remote's, synced
/// by exactly the steps the driver runs over HTTP. Every verb commits locally; the remote orders what
/// it accepts; the first push wins and the loser is kept as a conflict.
/// </summary>
public sealed class QuestSyncTests : IAsyncLifetime
{
    private readonly List<SqliteConnection> _connections = [];
    private QuestStore _a = null!;
    private QuestStore _b = null!;
    private QuestStore _remote = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    public async Task InitializeAsync()
    {
        _a = await OpenAsync();
        _b = await OpenAsync();
        _remote = await OpenAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
    }

    private async Task<QuestStore> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return await QuestStore.OpenAsync(connection);
    }

    /// <summary>
    /// One pass of one machine against the remote — the real <see cref="QuestSync"/>, through the real
    /// wire: cursor, fetch, integrate, push what is pending, record what was accepted, and round again
    /// while the remote says a quest moved first.
    /// </summary>
    private Task<QuestSyncReport> SyncAsync(QuestStore machine, Func<string, bool>? shared = null) =>
        QuestSync.RunAsync(machine, shared ?? (_ => true), new StoreRemote(_remote), Workspaces.Default);

    private static Task<Quest> Publish(QuestStore store, string title = "Cross the machines", string body = "why") =>
        store.PublishAsync("Asker", "Federated", title, body, Now);

    [Fact]
    public async Task A_quest_published_on_one_machine_reaches_the_other_through_the_remote()
    {
        var published = await _a.PublishAsync(
            "Asker", "Federated", "Cross the machines", "why", Now,
            links: ["https://tickets.example/T-1"],
            attachments: [new QuestAttachment("trace.log", new string('a', 64), 300)],
            then: [new QuestStep("Federated", "Verify {parent}", "b")]);

        await SyncAsync(_a);
        await SyncAsync(_b);

        var arrived = (await _b.FindAsync(published.Id))!;
        Assert.Equal(QuestStatus.Open, arrived.Status);
        Assert.Equal(published.Filed, arrived.Filed);
        Assert.Equal(["https://tickets.example/T-1"], arrived.Links);
        Assert.Equal("trace.log", Assert.Single(arrived.Attachments).Name);
        Assert.Equal("Verify {parent}", Assert.Single(arrived.Then).Title);
        Assert.Equal(_a.Machine, Assert.Single(await _b.HistoryAsync(published.Id)).Machine);
    }

    [Fact]
    public async Task A_move_made_on_the_other_machine_travels_back()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddHours(2));
        await SyncAsync(_b);
        await SyncAsync(_a);

        var back = (await _a.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Done, back.Status);
        Assert.Equal("Landed.", back.Note);
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>
    /// 🔴 The race (design §5): both machines take one quest before either pushes. The remote's order
    /// decides — the first push wins, and the second machine's take is REBASED into a conflict on the
    /// quest, pushed like anything else, and seen everywhere. Nothing is dropped.
    /// </summary>
    [Fact]
    public async Task The_first_push_wins_and_the_losing_take_is_kept_as_a_conflict()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1))).Moved);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2))).Moved);

        await SyncAsync(_a);
        var lost = await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Empty(lost.Refused);
        Assert.Null(lost.Problem);
        Assert.Equal(QuestStatus.Taken, Assert.Single(lost.Conflicts).Attempted);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));
        Assert.Equal(QuestClaim.Held, await _a.ClaimAsync(quest.Id));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal(QuestStatus.Taken, held.Status);
            Assert.Equal(Now.AddHours(1), held.Updated);
            var conflict = Assert.Single(held.Conflicts);
            Assert.Equal(_b.Machine, conflict.Machine);
            Assert.Equal(QuestStatus.Taken, conflict.Attempted);
            Assert.Equal("B's session.", conflict.Note);
        }
    }

    /// <summary>
    /// 🔴 D69: once a machine's take has lost, its later moves on that quest were made on a claim it
    /// never held — an offline session that finished its work would otherwise CLOSE the quest over the
    /// winner's take, because the table allows done from taken. They become conflicts too.
    /// </summary>
    [Fact]
    public async Task A_close_made_on_a_take_that_lost_is_a_conflict_too()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(2));
        await _b.MoveAsync(quest.Id, QuestStatus.Done, "Finished offline.", Now.AddHours(3));
        await SyncAsync(_a);
        await SyncAsync(_b);

        foreach (var store in new[] { _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal(QuestStatus.Taken, held.Status);
            Assert.Equal(
                [QuestStatus.Taken, QuestStatus.Done],
                held.Conflicts.Select(c => c.Attempted));
        }
    }

    /// <summary>A close that lost is a conflict too — and the quest stays where the winner put it.</summary>
    [Fact]
    public async Task A_losing_close_is_a_conflict_on_a_quest_the_winner_closed()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(quest.Id, QuestStatus.Declined, "Not ours.", Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Done, "Landed anyway.", Now.AddHours(2));
        await SyncAsync(_a);
        await SyncAsync(_b);

        var onB = (await _b.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Declined, onB.Status);
        Assert.Equal("Not ours.", onB.Note);
        Assert.Equal(QuestStatus.Done, Assert.Single(onB.Conflicts).Attempted);
    }

    /// <summary>
    /// The same ask made on two machines is one quest (design §7): the first publish to reach the
    /// remote is the quest, and the second machine's copy — never anybody's decision — is dropped by
    /// the rebase rather than kept as a conflict.
    /// </summary>
    [Fact]
    public async Task The_same_ask_from_two_machines_is_one_quest()
    {
        var first = await Publish(_a, body: "A's words.");
        var second = await Publish(_b, body: "B's words.");
        Assert.Equal(first.Id, second.Id);

        await SyncAsync(_a);
        await SyncAsync(_b);

        var onB = (await _b.FindAsync(first.Id))!;
        Assert.Equal("A's words.", onB.Body);
        Assert.Empty(onB.Conflicts);
        Assert.Equal(_a.Machine, Assert.Single(await _b.HistoryAsync(first.Id)).Machine);
        Assert.Single(await _remote.HistoryAsync(first.Id));
    }

    /// <summary>
    /// Silence means local (design §8): a quest whose receiver is not shared is never pending, never
    /// pushed, and the remote never hears of it — however many syncs run.
    /// </summary>
    [Fact]
    public async Task A_quest_to_a_receiver_that_is_not_shared_never_leaves_the_machine()
    {
        await _a.PublishAsync("Asker", "Homebody", "Stay home", "why", Now);
        var shared = await Publish(_a);

        await SyncAsync(_a, shared: receiver => receiver == "Federated");

        Assert.Equal([shared.Id], (await _remote.ListAsync()).Select(q => q.Id));
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, receiver => receiver == "Federated"));
        Assert.Single(await _a.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>
    /// The cursor is what was FETCHED, never the highest number held: a push numbered past other
    /// quests' operations this machine has not seen must not skip them.
    /// </summary>
    [Fact]
    public async Task The_cursor_is_what_was_fetched_not_what_was_pushed()
    {
        var fromB = await Publish(_b, "B's own quest");
        await SyncAsync(_b);

        // A pushes WITHOUT fetching first — straight at the remote, on a base of zero.
        var fromA = await Publish(_a, "A's own quest");
        var pushed = await _remote.ReceiveAsync(
            0, await _a.PendingAsync(Workspaces.Default, _ => true), _ => null, _ => Workspaces.Default);
        await _a.AcceptedAsync(pushed.Accepted);

        Assert.Equal(0, await _a.CursorAsync(Workspaces.Default));
        await SyncAsync(_a);

        Assert.NotNull(await _a.FindAsync(fromB.Id));
        Assert.Equal(2, await _a.CursorAsync(Workspaces.Default));
        Assert.Single(await _a.HistoryAsync(fromA.Id));
    }

    /// <summary>A push retried after its answer was lost is answered with the numbers it was given, and kept once.</summary>
    [Fact]
    public async Task A_retried_push_is_answered_with_the_numbers_it_was_given()
    {
        await Publish(_a);
        var pending = await _a.PendingAsync(Workspaces.Default, _ => true);

        var first = await _remote.ReceiveAsync(0, pending, _ => null, _ => Workspaces.Default);
        var again = await _remote.ReceiveAsync(0, pending, _ => null, _ => Workspaces.Default);

        Assert.Equal(first.Accepted, again.Accepted);
        Assert.Empty(again.Behind);
        Assert.Single((await _remote.OperationsSinceAsync(0)).Operations);
    }

    /// <summary>
    /// The remote re-judges: a move on a quest it never had published does not apply, and a publish its
    /// exchange refuses is not kept. Either is refused for that quest alone, in the remote's own words.
    /// </summary>
    [Fact]
    public async Task A_push_the_remote_cannot_keep_is_refused_for_that_quest_alone()
    {
        var kept = await Publish(_a, "Fine");
        var orphan = new QuestOperation("feedfacecafe", QuestOperationKind.Taken, _a.Machine, 99, Now);
        var unfit = await Publish(_a, "Unfit");

        var pushed = await _remote.ReceiveAsync(
            0, [.. await _a.PendingAsync(Workspaces.Default, _ => true), orphan],
            asked => asked.Title == "Unfit" ? "not addressable here" : null,
            _ => Workspaces.Default);

        Assert.Equal([kept.Id], (await _remote.ListAsync()).Select(q => q.Id));
        Assert.Contains(pushed.Refused, r => r.Quest == unfit.Id && r.Reason == "not addressable here");
        Assert.Contains(pushed.Refused, r => r.Quest == "feedfacecafe" && r.Reason.Contains("never had published"));
    }

    /// <summary>
    /// 🔴 Neither door carries a workspace (SYNC0a): a publish is filed by the RECEIVING side's wiring —
    /// at the remote by the receiver's registration, on a machine by the sync it came through.
    /// </summary>
    [Fact]
    public async Task A_publish_is_filed_by_the_receiving_sides_wiring()
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Wherever", "why", Now, workspace: "elsewhere");
        var pending = await _a.PendingAsync("elsewhere", _ => true);

        await _remote.ReceiveAsync(0, pending, _ => null, _ => "aurora");
        var fetched = await _remote.OperationsSinceAsync(0);
        await _b.IntegrateAsync("borealis-circle", fetched.Operations, fetched.Through);

        Assert.Equal("aurora", (await _remote.FindAsync(quest.Id))!.Workspace);
        Assert.Equal("borealis-circle", (await _b.FindAsync(quest.Id))!.Workspace);
    }

    /// <summary>
    /// A follow-up published only by a close that lost goes with it (D65 §4): the chain did not move,
    /// so the step it would have published is not anybody's open quest.
    /// </summary>
    [Fact]
    public async Task A_losing_close_takes_the_follow_up_it_published_with_it()
    {
        var parent = await _a.PublishAsync(
            "Asker", "Federated", "Develop", "b", Now, then: [new QuestStep("Federated", "Verify {parent}", "c")]);
        await SyncAsync(_a);
        await SyncAsync(_b);

        // Declined is terminal, so B's close cannot land on top of it. A take would not do: done from
        // taken is a move the table allows, whoever took it — that race is SYNC3's, not the rebase's.
        await _a.MoveAsync(parent.Id, QuestStatus.Declined, "Not ours.", Now.AddHours(1));
        var followUp = (await _b.MoveAsync(parent.Id, QuestStatus.Done, null, Now.AddHours(2))).FollowUp!;
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Null(await _b.FindAsync(followUp.Id));
        Assert.Empty(await _b.HistoryAsync(followUp.Id));
        Assert.Null(await _remote.FindAsync(followUp.Id));
        Assert.Equal(QuestStatus.Done, Assert.Single((await _b.FindAsync(parent.Id))!.Conflicts).Attempted);
    }

    /// <summary>
    /// A store written by the build before this one — a log with no numbers, a quests table that still
    /// marks mirror rows — opens as a machine with nothing fetched: the mirror row goes (the first fetch
    /// brings it back), the log gains its column, and what it held is pending, ready to push.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_sync_opens_with_its_history_pending()
    {
        var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        _connections.Add(old);
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE quests (
                  id TEXT PRIMARY KEY, sender TEXT NOT NULL, receiver TEXT NOT NULL, title TEXT NOT NULL,
                  body TEXT NOT NULL, status TEXT NOT NULL, note TEXT NULL, filed TEXT NOT NULL, updated TEXT NOT NULL,
                  home TEXT NULL, workspace TEXT NOT NULL DEFAULT 'default', links TEXT NOT NULL DEFAULT '[]',
                  attachments TEXT NOT NULL DEFAULT '[]', then_steps TEXT NOT NULL DEFAULT '[]', parent TEXT NULL
                );
                CREATE TABLE quest_log (
                  position INTEGER PRIMARY KEY, quest TEXT NOT NULL, kind TEXT NOT NULL, machine TEXT NOT NULL,
                  sequence INTEGER NOT NULL, at TEXT NOT NULL, payload TEXT NOT NULL, UNIQUE (machine, sequence)
                );
                CREATE TABLE quest_machine (one INTEGER PRIMARY KEY CHECK (one = 1), id TEXT NOT NULL);
                INSERT INTO quest_machine VALUES (1, 'feedbeeffeedbeef');
                INSERT INTO quests VALUES ('aaaaaaaaaaaa', 'Asker', 'Federated', 'Mine', 'b', 'Open', NULL,
                  '2026-09-23T10:00:00.0000000+00:00', '2026-09-23T10:00:00.0000000+00:00', NULL, 'default', '[]', '[]', '[]', NULL);
                INSERT INTO quests VALUES ('bbbbbbbbbbbb', 'Asker', 'Federated', 'Theirs', 'b', 'Open', NULL,
                  '2026-09-23T10:00:00.0000000+00:00', '2026-09-23T10:00:00.0000000+00:00', 'remote', 'default', '[]', '[]', '[]', NULL);
                INSERT INTO quest_log (quest, kind, machine, sequence, at, payload) VALUES ('aaaaaaaaaaaa', 'published',
                  'feedbeeffeedbeef', 1, '2026-09-23T10:00:00.0000000+00:00',
                  '{"from":"Asker","to":"Federated","title":"Mine","body":"b","workspace":"default","links":[],"attachments":[],"then":[]}');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await QuestStore.OpenAsync(old);

        Assert.Null(await store.FindAsync("bbbbbbbbbbbb"));
        Assert.Equal("feedbeeffeedbeef", store.Machine);
        Assert.Equal(0, await store.CursorAsync(Workspaces.Default));
        var pending = Assert.Single(await store.PendingAsync(Workspaces.Default, _ => true));
        Assert.Equal(("aaaaaaaaaaaa", 1L, (long?)null), (pending.Quest, pending.Sequence, pending.Number));
        Assert.True((await store.MoveAsync("aaaaaaaaaaaa", QuestStatus.Taken, null, Now)).Moved);
    }

    /// <summary>
    /// A remote that cannot be reached is a wall the pass NAMES, never an exception out of it — the take
    /// that ran it stands here, unconfirmed, and nothing about the store moved.
    /// </summary>
    [Fact]
    public async Task An_unreachable_remote_is_named_and_leaves_everything_pending()
    {
        var quest = await Publish(_a);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);

        var pass = await QuestSync.RunAsync(_a, _ => true, new UnreachableRemote(), Workspaces.Default);

        Assert.Contains("could not be reached", pass.Problem);
        Assert.Equal(2, (await _a.PendingAsync(Workspaces.Default, _ => true)).Count);
        Assert.Equal(QuestClaim.Unconfirmed, await _a.ClaimAsync(quest.Id));
        Assert.Equal(QuestClaim.None, await _b.ClaimAsync(quest.Id));
    }

    // ——— Where a circle stands (SYNC6a): what this machine has not pushed, the quests carrying a
    // conflict, and how the last pass ended — what the status bar and `daoris-driver sync status` read.

    [Fact]
    public async Task A_circle_stands_at_what_is_unpushed_and_when_it_last_reached_its_remote()
    {
        var never = await _a.StandingAsync(Workspaces.Default, _ => true);
        Assert.Equal(0, never.Ahead);
        Assert.Null(never.Synced);
        Assert.Null(never.Tried);

        await Publish(_a);
        Assert.Equal(1, (await _a.StandingAsync(Workspaces.Default, _ => true)).Ahead);

        await SyncAsync(_a);
        var synced = await _a.StandingAsync(Workspaces.Default, _ => true);

        Assert.Equal(0, synced.Ahead);
        Assert.NotNull(synced.Synced);
        Assert.Equal(synced.Synced, synced.Tried);
        Assert.Null(synced.Problem);
        Assert.Empty(synced.Behind);
    }

    /// <summary>
    /// A pass that hit a wall keeps the time the circle last reached its remote and names the wall —
    /// "synced at ten, and the try at quarter past could not reach it" is two facts, not one.
    /// </summary>
    [Fact]
    public async Task A_pass_that_hit_a_wall_keeps_the_last_sync_and_names_the_wall()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        var reached = (await _a.StandingAsync(Workspaces.Default, _ => true)).Synced;
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));

        await QuestSync.RunAsync(_a, _ => true, new UnreachableRemote(), Workspaces.Default);
        var walled = await _a.StandingAsync(Workspaces.Default, _ => true);

        Assert.Equal(reached, walled.Synced);
        Assert.True(walled.Tried >= walled.Synced);
        Assert.Contains("could not be reached", walled.Problem);
        Assert.Equal(1, walled.Ahead);

        await SyncAsync(_a);
        Assert.Null((await _a.StandingAsync(Workspaces.Default, _ => true)).Problem);
    }

    /// <summary>A quest carrying a conflict is listed wherever the circle's standing is read, on both machines.</summary>
    [Fact]
    public async Task The_quests_carrying_a_conflict_are_where_the_circle_stands()
    {
        var quest = await Publish(_a);
        await Publish(_a, "Quiet quest");
        await SyncAsync(_a);
        await SyncAsync(_b);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(2));
        await SyncAsync(_a);
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Equal([quest.Id], (await _a.StandingAsync(Workspaces.Default, _ => true)).Conflicts);
        Assert.Equal([quest.Id], (await _b.StandingAsync(Workspaces.Default, _ => true)).Conflicts);
        Assert.Empty((await _b.StandingAsync("elsewhere", _ => true)).Conflicts);
    }

    /// <summary>What never leaves the machine is not ahead of anything: a quest to a local receiver waits on nobody.</summary>
    [Fact]
    public async Task What_may_not_leave_the_machine_is_not_ahead()
    {
        await Publish(_a);

        Assert.Equal(0, (await _a.StandingAsync(Workspaces.Default, _ => false)).Ahead);
    }

    /// <summary>
    /// The wire both hosts speak carries an operation whole — a publish's ask with its files by name,
    /// its chain and its parent; a conflict with what it attempted; a number — and nothing half-made.
    /// </summary>
    [Fact]
    public void An_operation_crosses_the_wire_whole_and_a_half_made_one_does_not_cross_at_all()
    {
        var asked = new Quest("abcdefabcdef", "Asker", "Federated", "Do it", "why", QuestStatus.Open, null, Now, Now, "aurora")
        {
            Links = ["https://tickets.example/T-1"],
            Attachments = [new QuestAttachment("trace.log", new string('a', 64), 300)],
            Then = [new QuestStep("Federated", "Verify {parent}", "b")],
            Parent = "fedcbafedcba",
        };
        var page = new QuestFetch(
        [
            new QuestOperation("abcdefabcdef", QuestOperationKind.Published, "m1", 1, Now, Published: asked, Number: 7),
            new QuestOperation("abcdefabcdef", QuestOperationKind.Conflict, "m2", 4, Now, "late", Attempted: QuestStatus.Taken, Number: 8),
        ], 8, More: true);

        var json = QuestWire.Page(page);
        var back = QuestWire.ReadPage(json)!;

        Assert.DoesNotContain("aurora", json);
        Assert.True(back.More);
        Assert.Equal(8, back.Through);
        var published = back.Operations[0];
        Assert.Equal((7L, "m1", 1L), (published.Number!.Value, published.Machine, published.Sequence));
        Assert.Equal("trace.log", Assert.Single(published.Published!.Attachments).Name);
        Assert.Equal("Verify {parent}", Assert.Single(published.Published.Then).Title);
        Assert.Equal("fedcbafedcba", published.Published.Parent);
        Assert.Equal(Workspaces.Default, published.Published.Workspace);
        Assert.Equal((QuestStatus.Taken, "late"), (back.Operations[1].Attempted!.Value, back.Operations[1].Note));
        Assert.Null(QuestWire.ReadPage("""{ "operations": [{ "machine": "m1", "quest": "q", "kind": "taken", "at": "2026-09-24T10:00:00Z" }] }"""));
        Assert.Null(QuestWire.ReadPush("""{ "base": 1, "operations": [{ "machine": "m1", "sequence": 1, "quest": "q", "kind": "conflict", "at": "2026-09-24T10:00:00Z" }] }"""));
        Assert.Null(QuestWire.ReadPage("[]"));
    }

    [Fact]
    public async Task A_fetch_pages_through_what_the_remote_accepted()
    {
        for (var i = 0; i < 5; i++) await Publish(_a, $"Ask {i}");
        await SyncAsync(_a);

        var first = await _remote.OperationsSinceAsync(0, limit: 3);
        var rest = await _remote.OperationsSinceAsync(first.Through, limit: 3);

        Assert.Equal([1L, 2L, 3L], first.Operations.Select(o => o.Number!.Value));
        Assert.True(first.More);
        Assert.Equal([4L, 5L], rest.Operations.Select(o => o.Number!.Value));
        Assert.False(rest.More);
        Assert.Equal(5, rest.Through);
    }
}
