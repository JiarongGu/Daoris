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
            then: [new QuestStep("Federated", "Verify {parent}", "b")],
            publishedBy: "s1a2b3c4");

        await SyncAsync(_a);
        await SyncAsync(_b);

        var arrived = (await _b.FindAsync(published.Id))!;
        Assert.Equal(QuestStatus.Open, arrived.Status);
        Assert.Equal(published.Filed, arrived.Filed);
        Assert.Equal(["https://tickets.example/T-1"], arrived.Links);
        Assert.Equal("trace.log", Assert.Single(arrived.Attachments).Name);
        Assert.Equal("Verify {parent}", Assert.Single(arrived.Then).Title);
        // Which session published it travels with it (SESS1): the session's record syncs too (SYNC4).
        Assert.Equal("s1a2b3c4", arrived.PublishedBy);
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
    /// 🔴 HIST1a (D153 point 4, H1): both sides know an operation by machine and sequence alone, so a sequence handed out
    /// twice loses the second move without an error: the remote answers its push as the retry above, and the fetch before
    /// the push brings the remote's operation back under that number and takes it for the new move. Here the machine's
    /// newest operations are removed after the remote numbered them, as the hand purge of 2026-10-07 removed them and a
    /// clear will forget them (HIST1b), and the next move still reaches the remote and the other machine.
    /// </summary>
    [Fact]
    public async Task A_move_made_after_the_newest_operations_were_removed_reaches_the_remote_and_is_no_retry()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        var machine = await QuestStore.OpenAsync(connection);
        var kept = await Publish(machine, "Kept");
        var removed = await Publish(machine, "Removed");
        await SyncAsync(machine);
        await using (var purge = connection.CreateCommand())
        {
            purge.CommandText = "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quests WHERE id = $id";
            purge.Parameters.AddWithValue("$id", removed.Id);
            await purge.ExecuteNonQueryAsync();
        }

        Assert.True((await machine.MoveAsync(kept.Id, QuestStatus.Taken, "Machine A's session.", Now.AddHours(1))).Moved);
        var pass = await SyncAsync(machine);
        await SyncAsync(_b);

        Assert.Equal((1, (string?)null), (pass.Pushed, pass.Problem));
        Assert.Equal(QuestStatus.Taken, (await _remote.FindAsync(kept.Id))!.Status);
        Assert.Equal(QuestStatus.Taken, (await _b.FindAsync(kept.Id))!.Status);
        Assert.Equal(QuestStatus.Open, (await _remote.FindAsync(removed.Id))!.Status);
        Assert.Equal(QuestClaim.Held, await machine.ClaimAsync(kept.Id));
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

    /// <summary>A chain's parent carrying the person's requirement (DRIFT1c), closed done on B departing from it (DRIFT1d).</summary>
    private async Task<Quest> HeldOnBothAsync()
    {
        var parent = await _a.PublishAsync(
            "ask #a1", "Federated", "Develop", "b", Now, then: [new QuestStep("Federated", "Verify {parent}", "c")],
            requirements: [new QuestRequirement("the v3 bridge", "It opens through the bridge.")]);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await _b.MoveAsync(parent.Id, QuestStatus.Taken, null, Now.AddHours(1));
        var closed = await _b.MoveAsync(
            parent.Id, QuestStatus.Done, "Built.", Now.AddHours(2),
            answers: [new QuestAnswer(1, Met: null, Departed: "Another route.", Quote: "the v3 bridge")]);
        Assert.Null(closed.FollowUp);
        await SyncAsync(_b);
        await SyncAsync(_a);
        Assert.True((await _a.FindAsync(parent.Id))!.Held);
        return parent;
    }

    /// <summary>
    /// DRIFT1d: a departure's yes said on two machines before either pushes is one yes. The second rebases away, as a
    /// second copy of a decision already made, and the step both published is the one quest everywhere.
    /// </summary>
    [Fact]
    public async Task Two_yeses_to_one_departure_are_one_and_the_held_step_is_published_once()
    {
        var parent = await HeldOnBothAsync();

        var onA = await _a.AcceptAsync(parent.Id, Now.AddHours(3));
        var onB = await _b.AcceptAsync(parent.Id, Now.AddHours(4));
        await SyncAsync(_a);
        var second = await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Equal(onA.FollowUp!.Id, onB.FollowUp!.Id);
        Assert.Empty(second.Refused);
        Assert.Empty(second.Conflicts);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var standing = (await store.FindAsync(parent.Id))!;
            Assert.Equal(Now.AddHours(3), standing.Accepted);
            Assert.False(standing.Held);
            Assert.Single(await store.HistoryAsync(parent.Id), operation => operation.Kind == QuestOperationKind.Accepted);
            Assert.Single(await store.HistoryAsync(onA.FollowUp.Id), operation => operation.Kind == QuestOperationKind.Published);
        }
    }

    /// <summary>
    /// DRIFT1d: a yes to a departure whose done lost goes with it, and so does the step it published: there is no
    /// departure left to accept, and the chain did not move.
    /// </summary>
    [Fact]
    public async Task A_yes_on_a_done_that_lost_goes_with_it_and_its_step_too()
    {
        var parent = await _a.PublishAsync(
            "ask #a1", "Federated", "Develop", "b", Now, then: [new QuestStep("Federated", "Verify {parent}", "c")],
            requirements: [new QuestRequirement("the v3 bridge", "It opens through the bridge.")]);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(parent.Id, QuestStatus.Declined, "Not ours.", Now.AddHours(1));
        await _b.MoveAsync(
            parent.Id, QuestStatus.Done, "Built.", Now.AddHours(2),
            answers: [new QuestAnswer(1, Met: null, Departed: "Another route.", Quote: "the v3 bridge")]);
        var step = (await _b.AcceptAsync(parent.Id, Now.AddHours(3))).FollowUp!;
        await SyncAsync(_a);
        var lost = await SyncAsync(_b);

        Assert.Empty(lost.Refused);
        Assert.DoesNotContain(await _b.HistoryAsync(parent.Id), operation => operation.Kind == QuestOperationKind.Accepted);
        Assert.Null(await _b.FindAsync(step.Id));
        Assert.Null(await _remote.FindAsync(step.Id));
        var standing = (await _b.FindAsync(parent.Id))!;
        Assert.Equal(QuestStatus.Declined, standing.Status);
        Assert.Equal(QuestStatus.Done, Assert.Single(standing.Conflicts).Attempted);
    }

    // ——— Evidence (EVID1a, D144): names and codes travel like every verb, never bytes or a machine's path.

    private static readonly QuestRequirement Reported =
        new("the bridge report in docs", "The report is in docs/report-bridge.md.") { Evidence = [new QuestEvidence("docs/report-bridge.md")] };

    private static QuestEvidenceVerdict Verdict(string result = "found") =>
        new("a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "session-end",
            [new QuestEvidenceRead(1, "docs/report-bridge.md", null, result)]);

    /// <summary>A chain's parent naming evidence (EVID1a), closed done met on B: held there, and on A once synced, for its evidence.</summary>
    private async Task<Quest> AwaitingOnBothAsync()
    {
        var parent = await _a.PublishAsync(
            "ask #a1", "Federated", "Report", "b", Now, then: [new QuestStep("Federated", "Verify {parent}", "c")],
            requirements: [Reported]);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.Equal([Reported], (await _b.FindAsync(parent.Id))!.Requirements);
        await _b.MoveAsync(parent.Id, QuestStatus.Taken, null, Now.AddHours(1));
        var closed = await _b.MoveAsync(parent.Id, QuestStatus.Done, "Built.", Now.AddHours(2), answers: [new QuestAnswer(1, Met: "Written.")]);
        Assert.Null(closed.FollowUp);
        await SyncAsync(_b);
        await SyncAsync(_a);
        Assert.Equal(QuestHold.EvidenceUnread, (await _a.FindAsync(parent.Id))!.Hold);
        return parent;
    }

    /// <summary>
    /// 🔴 EVID1a: a verdict read on B reaches A and the remote as an operation, the hold lifts on every machine, and the step
    /// its release published is one quest everywhere.
    /// </summary>
    [Fact]
    public async Task A_verdict_read_on_one_machine_releases_the_done_on_every_machine()
    {
        var parent = await AwaitingOnBothAsync();

        var read = await _b.EvidenceAsync(parent.Id, Verdict(), Now.AddHours(3));
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.True(read.Moved);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var standing = (await store.FindAsync(parent.Id))!;
            Assert.False(standing.Held);
            Assert.Equal("found", Assert.Single(standing.Evidence!.Items).Result);
            Assert.Equal(_b.Machine, standing.Evidence.Machine);
            Assert.Single(await store.HistoryAsync(read.FollowUp!.Id), operation => operation.Kind == QuestOperationKind.Published);
        }
    }

    /// <summary>
    /// EVID1a: a verdict found on two machines before either pushes is one: the second no longer applies when it lands on
    /// the first, and rebases away as a second yes does, with no conflict and the step published once.
    /// </summary>
    [Fact]
    public async Task Two_verdicts_finding_one_dones_evidence_are_one()
    {
        var parent = await AwaitingOnBothAsync();

        var onA = await _a.EvidenceAsync(parent.Id, Verdict(), Now.AddHours(3));
        var onB = await _b.EvidenceAsync(parent.Id, Verdict(), Now.AddHours(4));
        await SyncAsync(_a);
        var second = await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Equal(onA.FollowUp!.Id, onB.FollowUp!.Id);
        Assert.Empty(second.Refused);
        Assert.Empty(second.Conflicts);
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Single(await store.HistoryAsync(parent.Id), operation => operation.Kind == QuestOperationKind.Evidenced);
            Assert.Equal(_a.Machine, (await store.FindAsync(parent.Id))!.Evidence!.Machine);
        }
    }

    /// <summary>
    /// EVID1a: a verdict on a done that lost goes with it, and so does the step its release published: there is no done
    /// left whose evidence it read, and the chain did not move.
    /// </summary>
    [Fact]
    public async Task A_verdict_on_a_done_that_lost_goes_with_it_and_its_step_too()
    {
        var parent = await _a.PublishAsync(
            "ask #a1", "Federated", "Report", "b", Now, then: [new QuestStep("Federated", "Verify {parent}", "c")],
            requirements: [Reported]);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(parent.Id, QuestStatus.Declined, "Not ours.", Now.AddHours(1));
        await _b.MoveAsync(parent.Id, QuestStatus.Done, "Built.", Now.AddHours(2), answers: [new QuestAnswer(1, Met: "Written.")]);
        var step = (await _b.EvidenceAsync(parent.Id, Verdict(), Now.AddHours(3))).FollowUp!;
        await SyncAsync(_a);
        var lost = await SyncAsync(_b);

        Assert.Empty(lost.Refused);
        Assert.DoesNotContain(await _b.HistoryAsync(parent.Id), operation => operation.Kind == QuestOperationKind.Evidenced);
        Assert.Null(await _b.FindAsync(step.Id));
        Assert.Null(await _remote.FindAsync(step.Id));
        Assert.Equal(QuestStatus.Declined, (await _b.FindAsync(parent.Id))!.Status);
    }

    /// <summary>
    /// EVID1a: a missing verdict on one machine and a later found one on the other both stand, in order: the first keeps
    /// the done held, the second lets it go, on every machine.
    /// </summary>
    [Fact]
    public async Task A_missing_verdict_then_a_found_one_from_another_machine_both_stand()
    {
        var parent = await AwaitingOnBothAsync();

        await _b.EvidenceAsync(parent.Id, Verdict("missing"), Now.AddHours(3));
        await SyncAsync(_b);
        await SyncAsync(_a);
        Assert.Equal(QuestHold.EvidenceMissing, (await _a.FindAsync(parent.Id))!.Hold);
        await _a.EvidenceAsync(parent.Id, Verdict(), Now.AddHours(4));
        await SyncAsync(_a);
        await SyncAsync(_b);

        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Equal(2, (await store.HistoryAsync(parent.Id)).Count(operation => operation.Kind == QuestOperationKind.Evidenced));
            Assert.False((await store.FindAsync(parent.Id))!.Held);
        }
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

    // ——— Dismissing a conflict (SYNC6c): a person's act on one machine, and the conflict goes on every
    // machine — an operation like any other, pushed and fetched, which moves no status.

    /// <summary>Both machines take one quest; the loser is kept on it. A person on either machine dismisses it, and it goes everywhere.</summary>
    private async Task<Quest> RacedAsync()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2));
        await SyncAsync(_a);
        await SyncAsync(_b);
        await SyncAsync(_a);
        return quest;
    }

    [Fact]
    public async Task A_dismissal_on_one_machine_clears_the_conflict_on_every_machine()
    {
        var quest = await RacedAsync();
        var conflict = Assert.Single((await _b.FindAsync(quest.Id))!.Conflicts);

        var dismissed = await _b.DismissAsync(quest.Id, conflict.Machine, conflict.Sequence, Now.AddHours(3));
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Equal(1, dismissed.Dismissed);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Empty(held.Conflicts);
            Assert.Equal(QuestStatus.Taken, held.Status);
        }

        Assert.Empty((await _a.StandingAsync(Workspaces.Default, _ => true)).Conflicts);
    }

    /// <summary>Naming no conflict dismisses every one the quest carries — the terminal's form; naming one leaves the rest.</summary>
    /// <summary>
    /// A cache from before dismissals holds conflicts with no sequence, and a conflict with no sequence
    /// cannot be named to dismiss. The store fills them in from the log as it opens.
    /// </summary>
    [Fact]
    public async Task A_cached_conflict_from_before_dismissals_gains_its_sequence_on_open()
    {
        var quest = await RacedAsync();
        var original = Assert.Single((await _b.FindAsync(quest.Id))!.Conflicts);
        var connection = _connections[1];
        await using (var strip = connection.CreateCommand())
        {
            strip.CommandText = $"UPDATE quests SET conflicts = replace(conflicts, '\"sequence\":{original.Sequence},', '') WHERE id = $id";
            strip.Parameters.AddWithValue("$id", quest.Id);
            await strip.ExecuteNonQueryAsync();
        }

        Assert.Equal(0, Assert.Single((await _b.FindAsync(quest.Id))!.Conflicts).Sequence);
        var reopened = await QuestStore.OpenAsync(connection);

        Assert.Equal(original.Sequence, Assert.Single((await reopened.FindAsync(quest.Id))!.Conflicts).Sequence);
    }

    [Fact]
    public async Task Dismissing_names_one_conflict_or_every_one_the_quest_carries()
    {
        var quest = await RacedAsync();

        var elsewhere = await _a.DismissAsync(quest.Id, "not-a-machine", 1, Now.AddHours(3));
        Assert.Equal(0, elsewhere.Dismissed);
        Assert.Single(elsewhere.Quest!.Conflicts);

        // A sequence with no machine names no conflict — sequences are per machine — so it dismisses
        // none. It used to fall into "naming none" and dismiss every one (REV3 service F18).
        var conflict = Assert.Single(elsewhere.Quest.Conflicts);
        var half = await _a.DismissAsync(quest.Id, machine: null, conflict.Sequence, Now.AddHours(3));
        Assert.Equal(0, half.Dismissed);
        Assert.Single(half.Quest!.Conflicts);

        var every = await _a.DismissAsync(quest.Id, machine: null, sequence: null, Now.AddHours(4));
        Assert.Equal(1, every.Dismissed);
        Assert.Empty(every.Quest!.Conflicts);

        Assert.Equal(0, (await _a.DismissAsync(quest.Id, machine: null, sequence: null, Now.AddHours(5))).Dismissed);
        Assert.Null((await _a.DismissAsync("nosuchquest", null, null, Now)).Quest);
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

    /// <summary>
    /// 🔴 REV3 CLEAN1: a publish whose attachments or chain hold something other than objects is not
    /// whole, and is refused like any other half-made operation. The reader looked inside each item as
    /// if it were an object, and on a number it threw instead, so a remote's malformed push was a 500
    /// at the door rather than a refusal.
    /// </summary>
    [Theory]
    [InlineData("\"attachments\": [1]")]
    [InlineData("\"then\": [\"not a step\"]")]
    public void A_publish_whose_items_are_not_objects_is_refused_not_thrown(string items)
    {
        var push = $$"""
            { "base": 1, "operations": [{ "machine": "m1", "sequence": 1, "quest": "q", "kind": "published",
              "at": "2026-09-24T10:00:00Z",
              "asked": { "from": "a", "to": "b", "title": "t", "body": "b", {{items}} } }] }
            """;

        Assert.Null(QuestWire.ReadPush(push));
    }

    /// <summary>
    /// The push's answer, read back on this machine, holds to the same rule: an acceptance that is not an
    /// object makes the answer not one, and a <c>behind</c> item that is not a quest id is skipped, as a
    /// link that is not a string is. Both threw.
    /// </summary>
    [Fact]
    public void A_push_answer_with_items_of_the_wrong_kind_is_read_not_thrown()
    {
        Assert.Null(QuestWire.ReadPushed("""{ "accepted": [1], "behind": [], "refused": [] }"""));

        var skipped = QuestWire.ReadPushed("""{ "accepted": [], "behind": [1, "abcdefabcdef"], "refused": [] }""");
        Assert.Equal(["abcdefabcdef"], skipped!.Behind);
    }

    /// <summary>A dismissal crosses naming the conflict it dismisses; one that names none is half-made and does not cross.</summary>
    [Fact]
    public void A_dismissal_crosses_the_wire_naming_its_conflict()
    {
        var page = new QuestFetch(
        [
            new QuestOperation("abcdefabcdef", QuestOperationKind.Dismissed, "m1", 5, Now,
                Dismisses: new QuestOperationRef("m2", 4), Number: 9),
        ], 9, More: false);

        var back = Assert.Single(QuestWire.ReadPage(QuestWire.Page(page))!.Operations);

        Assert.Equal(QuestOperationKind.Dismissed, back.Kind);
        Assert.Equal(new QuestOperationRef("m2", 4), back.Dismisses);
        Assert.Null(QuestWire.ReadPush("""{ "base": 1, "operations": [{ "machine": "m1", "sequence": 1, "quest": "q", "kind": "dismissed", "at": "2026-09-24T10:00:00Z" }] }"""));
    }

    // ——— A delete (QUEST1, D95): a tombstone in the quest's history, pushed and fetched like any
    // operation, so no sync — from any cursor — brings the quest back.

    /// <summary>
    /// 🔴 The owner's condition: a delete must reach the remote and not be resurrected by the next sync.
    /// It is an operation, so the remote keeps it, the other machine drops the quest when it fetches, and
    /// a machine syncing from cursor zero never sees the quest at all.
    /// </summary>
    [Fact]
    public async Task A_delete_travels_and_no_later_sync_brings_the_quest_back()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.NotNull(await _b.FindAsync(quest.Id));

        var deleted = await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.True(deleted.Deleted);
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Null(await store.FindAsync(quest.Id));
            Assert.Empty(await store.ListAsync(includeClosed: true));
        }

        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        Assert.Equal(QuestOperationKind.Deleted, (await _remote.HistoryAsync(quest.Id))[^1].Kind);

        var newcomer = await OpenAsync();
        await SyncAsync(newcomer);
        Assert.Null(await newcomer.FindAsync(quest.Id));
    }

    /// <summary>
    /// A take that reached the remote first means somebody is working the quest: a delete made
    /// meanwhile on another machine is dropped by the rebase — its condition, nobody has taken it, no
    /// longer held — and the quest stands taken everywhere, with nothing for a person to reconcile.
    /// </summary>
    [Fact]
    public async Task A_delete_that_lost_to_a_take_is_dropped_and_the_quest_stays_taken()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        Assert.True((await _a.DeleteAsync(quest.Id, Now.AddHours(2), travels: true)).Deleted);
        await SyncAsync(_b);
        var lost = await SyncAsync(_a);

        Assert.Empty(lost.Refused);
        Assert.Empty(lost.Conflicts);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal(QuestStatus.Taken, held.Status);
            Assert.Empty(held.Conflicts);
        }

        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        Assert.DoesNotContain(await _a.HistoryAsync(quest.Id), o => o.Kind == QuestOperationKind.Deleted);
    }

    /// <summary>
    /// A take that reaches the remote after a delete lost, as a take that reaches it after another take
    /// does: it becomes a conflict, so its machine's claim reads lost and that machine's driver stops the
    /// session. The quest stays gone, and the conflict is kept in the log on every side.
    /// </summary>
    [Fact]
    public async Task A_take_that_lost_to_a_delete_is_a_conflict_and_the_quest_stays_gone()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2));
        await SyncAsync(_a);
        var lost = await SyncAsync(_b);

        Assert.Empty(lost.Refused);
        Assert.Equal(QuestStatus.Taken, Assert.Single(lost.Conflicts).Attempted);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));
        Assert.Empty(await _b.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Null(await store.FindAsync(quest.Id));
        }

        Assert.Contains(await _remote.HistoryAsync(quest.Id), o => o is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken });
    }

    /// <summary>Two people deleting one quest make one delete — never a refusal, and never a conflict.</summary>
    [Fact]
    public async Task Two_deletes_of_one_quest_are_one()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);
        await _b.DeleteAsync(quest.Id, Now.AddHours(2), travels: true);
        await SyncAsync(_a);
        var second = await SyncAsync(_b);

        Assert.Empty(second.Refused);
        Assert.Empty(second.Conflicts);
        Assert.Empty(await _b.PendingAsync(Workspaces.Default, _ => true));
        Assert.Single(await _remote.HistoryAsync(quest.Id), o => o.Kind == QuestOperationKind.Deleted);
    }

    /// <summary>
    /// A quest published and deleted before any push travels as both — the remote cannot be sure it
    /// never took the publish, so the history goes whole, and it ends in no quest there too.
    /// </summary>
    [Fact]
    public async Task A_quest_published_and_deleted_before_a_push_goes_up_whole_and_stays_gone()
    {
        var quest = await Publish(_a);
        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);

        var pass = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Empty(pass.Refused);
        Assert.Equal(2, pass.Pushed);
        Assert.Null(await _remote.FindAsync(quest.Id));
        Assert.Null(await _b.FindAsync(quest.Id));
    }

    /// <summary>The same words asked after a delete are a quest again — on every machine.</summary>
    [Fact]
    public async Task The_same_ask_published_after_a_delete_is_a_quest_again()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);
        await SyncAsync(_a);

        var again = await Publish(_a, body: "Asked properly this time.");
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Equal(quest.Id, again.Id);
        Assert.Equal("Asked properly this time.", (await _b.FindAsync(quest.Id))!.Body);
        Assert.Equal(QuestStatus.Open, (await _remote.FindAsync(quest.Id))!.Status);
    }

    /// <summary>
    /// A quest that never left the machine simply goes: its receiver is not shared and nothing of it was
    /// numbered, so nothing anywhere holds a copy, and its history is removed rather than tombstoned.
    /// </summary>
    [Fact]
    public async Task A_quest_that_never_left_the_machine_simply_goes()
    {
        var quest = await Publish(_a);

        var deleted = await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: false);

        Assert.True(deleted.Deleted);
        Assert.Null(await _a.FindAsync(quest.Id));
        Assert.Empty(await _a.HistoryAsync(quest.Id));
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>A quest the remote numbered is tombstoned even where it is no longer shared: a copy is out there.</summary>
    [Fact]
    public async Task A_quest_a_remote_holds_is_tombstoned_even_when_asked_to_simply_go()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);

        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: false);

        Assert.Equal(QuestOperationKind.Deleted, (await _a.HistoryAsync(quest.Id))[^1].Kind);
        Assert.Single(await _a.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>Only an open quest is deleted — the store judges it inside the write, as it judges every move.</summary>
    [Fact]
    public async Task The_store_refuses_to_delete_a_quest_somebody_moved()
    {
        var quest = await Publish(_a);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));

        var refused = await _a.DeleteAsync(quest.Id, Now.AddHours(2), travels: false);

        Assert.False(refused.Deleted);
        Assert.Equal(QuestStatus.Taken, refused.Quest!.Status);
        Assert.NotNull(await _a.FindAsync(quest.Id));
        Assert.Null((await _a.DeleteAsync("nosuchquest", Now, travels: false)).Quest);
    }

    /// <summary>A delete crosses the wire as its kind alone: it carries nothing else.</summary>
    [Fact]
    public void A_delete_crosses_the_wire()
    {
        var page = new QuestFetch(
            [new QuestOperation("abcdefabcdef", QuestOperationKind.Deleted, "m1", 6, Now, Number: 10)], 10, More: false);

        var back = Assert.Single(QuestWire.ReadPage(QuestWire.Page(page))!.Operations);

        Assert.Equal((QuestOperationKind.Deleted, "m1", 6L, 10L), (back.Kind, back.Machine, back.Sequence, back.Number!.Value));
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

    // ——— A wait (D79): its taker's question, an operation that travels like every verb and moves no status.

    /// <summary>
    /// 🔴 A wait made on this machine after another machine closed the quest has nothing left to say, so the rebase drops
    /// it, as it drops a wait on a quest that is gone. Rewritten as a conflict it would attempt no status, which the wire
    /// refuses as half-made, and every later push of the circle with it (QUESTOP1).
    /// </summary>
    [Fact]
    public async Task A_wait_on_a_quest_another_machine_closed_first_is_dropped_and_the_circle_still_pushes()
    {
        var quest = await Publish(_a);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1));
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Declined, "Not ours after all.", Now.AddHours(2))).Moved);
        await SyncAsync(_b);
        var question = await Publish(_a, "A question for its owner");
        Assert.True((await _a.WaitAsync(quest.Id, question.Id, Now.AddHours(3))).Moved);
        var pass = await SyncAsync(_a);

        Assert.Null(pass.Problem);
        Assert.Empty(pass.Refused);
        Assert.Empty(pass.Conflicts);
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        Assert.NotNull(await _remote.FindAsync(question.Id));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Declined, (string?)null, 0), (held.Status, held.Awaits, held.Conflicts.Count));
        }
    }

    /// <summary>
    /// 🔴 A wait made after this machine's own take is part of that take's move (WAITCLAIM1, D69's note): when the take
    /// loses the race, the wait goes with it. Applied to the winner's quest, it would hold the winner's session on a
    /// question it never asked. The loss this machine reports, the take's conflict, names the wait, so whoever asked
    /// learns that nothing waits on the question's answer.
    /// </summary>
    [Fact]
    public async Task A_wait_made_on_a_take_that_lost_goes_with_the_take_and_the_loss_names_it()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        var question = await Publish(_a, "A question for its owner");
        Assert.True((await _a.WaitAsync(quest.Id, question.Id, Now.AddHours(2))).Moved);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        await SyncAsync(_b);
        var lost = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Null(lost.Problem);
        Assert.Empty(lost.Refused);
        Assert.Equal(QuestClaim.Lost, await _a.ClaimAsync(quest.Id));
        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(quest.Id));
        var reported = Assert.Single(lost.Conflicts);
        Assert.Equal(QuestStatus.Taken, reported.Attempted);
        Assert.StartsWith("A's session.", reported.Note);
        Assert.Contains($"#{question.Id}", reported.Note);
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Taken, (string?)null), (held.Status, held.Awaits));
            var conflict = Assert.Single(held.Conflicts);
            Assert.Equal((_a.Machine, QuestStatus.Taken, reported.Note), (conflict.Machine, conflict.Attempted, conflict.Note));
            Assert.DoesNotContain(await store.HistoryAsync(quest.Id), o => o.Kind == QuestOperationKind.Waited);
            Assert.Empty(await store.WaitingOnAsync(question.Id));
        }

        // The question is a quest of its own and still went up: only the wait on it went with the take.
        Assert.NotNull(await _remote.FindAsync(question.Id));
    }

    /// <summary>The take's note stays as it was given, and the waits that went with it follow it, each named (WAITCLAIM1).</summary>
    [Theory]
    [InlineData(null, new[] { "q2" },
        "This take's wait on #q2 went with it, so this quest does not wait on that question's answer.")]
    [InlineData("A's session. ", new[] { "q2", "q3", "q4" },
        "A's session. This take's waits on #q2, #q3 and #q4 went with it, so this quest does not wait on those questions' answers.")]
    public void A_lost_takes_conflict_names_each_wait_after_its_own_note(string? note, string[] questions, string said) =>
        Assert.Equal(said, QuestStore.WaitsWentWith(note, questions));

    /// <summary>
    /// A wait on a quest this machine did not take was never part of a take of its own (WAITCLAIM1): the pass that drops
    /// the wait on a lost take keeps it, and the quest waits on the question everywhere.
    /// </summary>
    [Fact]
    public async Task A_wait_on_a_quest_this_machine_did_not_take_survives_the_pass_that_drops_a_lost_takes()
    {
        var raced = await Publish(_a);
        var theirs = await Publish(_a, "Taken on the other machine");
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.True((await _b.MoveAsync(theirs.Id, QuestStatus.Taken, null, Now.AddHours(1))).Moved);
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.True((await _a.MoveAsync(raced.Id, QuestStatus.Taken, null, Now.AddHours(2))).Moved);
        var question = await Publish(_a, "A question for its owner");
        Assert.True((await _a.WaitAsync(raced.Id, question.Id, Now.AddHours(3))).Moved);
        Assert.True((await _a.WaitAsync(theirs.Id, question.Id, Now.AddHours(3))).Moved);

        // The other machine's take wins the race, and its own wait on the quest it holds reaches the remote first, so the
        // pass rebases this machine's wait on that quest too.
        Assert.True((await _b.MoveAsync(raced.Id, QuestStatus.Taken, null, Now.AddHours(4))).Moved);
        var asked = await Publish(_b, "B's question for its owner");
        Assert.True((await _b.WaitAsync(theirs.Id, asked.Id, Now.AddHours(1.5))).Moved);
        await SyncAsync(_b);
        var pass = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Null(pass.Problem);
        Assert.Equal(raced.Id, Assert.Single(pass.Conflicts).Quest);
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Null((await store.FindAsync(raced.Id))!.Awaits);
            var kept = (await store.FindAsync(theirs.Id))!;
            Assert.Equal((QuestStatus.Taken, question.Id, 0), (kept.Status, kept.Awaits, kept.Conflicts.Count));
            Assert.Contains(await store.HistoryAsync(theirs.Id), o => o is { Kind: QuestOperationKind.Waited } && o.Machine == _a.Machine);
        }
    }

    /// <summary>
    /// 🔴 A wait and a close made after the pass that found this machine's take lost (WAITCLAIM2, D69's note): the session
    /// has not been stopped yet and does not know, so it asks, waits and closes. The store refuses each as the claim reads,
    /// lost, and writes nothing, so neither reaches the remote: the winner's quest neither waits on this machine's question
    /// nor closes. What this machine keeps says why: each refusal names the lost claim, and its copy of the quest carries
    /// its take's conflict as every machine does.
    /// </summary>
    [Fact]
    public async Task A_wait_and_a_close_made_after_the_pass_that_found_the_take_lost_are_refused_and_never_reach_the_winner()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2))).Moved);
        await SyncAsync(_b);
        var lost = await SyncAsync(_a);
        Assert.Equal(QuestStatus.Taken, Assert.Single(lost.Conflicts).Attempted);
        Assert.Equal(QuestClaim.Lost, await _a.ClaimAsync(quest.Id));

        var question = await Publish(_a, "A question for its owner");
        var waited = await _a.WaitAsync(quest.Id, question.Id, Now.AddHours(3));
        var closed = await _a.MoveAsync(quest.Id, QuestStatus.Done, "Finished after the take lost.", Now.AddHours(4));
        var next = await SyncAsync(_a);
        await SyncAsync(_b);

        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Taken, "B's session.", (string?)null), (held.Status, held.Note, held.Awaits));
            var conflict = Assert.Single(held.Conflicts);
            Assert.Equal((_a.Machine, QuestStatus.Taken, "A's session."), (conflict.Machine, conflict.Attempted, conflict.Note));
            Assert.DoesNotContain(
                await store.HistoryAsync(quest.Id), o => o.Kind is QuestOperationKind.Waited or QuestOperationKind.Done);
            Assert.Empty(await store.WaitingOnAsync(question.Id));
        }

        Assert.Equal((false, true), (waited.Moved, waited.ClaimLost));
        Assert.Equal((false, true), (closed.Moved, closed.ClaimLost));
        Assert.Null(closed.FollowUp);
        Assert.Null(next.Problem);
        Assert.Empty(next.Refused);
        Assert.Empty(next.Conflicts);
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));

        // The question is a quest of its own and still went up: only the wait on it was refused.
        Assert.NotNull(await _remote.FindAsync(question.Id));
    }

    // ——— A remote judges a lost claim too (WAITCLAIM3, D69's note): a machine on a build before WAITCLAIM2 makes the move
    // its own store would now refuse, and pushes it.

    /// <summary>
    /// What a machine on a build before WAITCLAIM2 writes for a move or a wait it makes after its take lost: the store's
    /// own append, with no claim judged, so the operation is pending in its log as any move is and goes up with the next
    /// pass. Written by hand because no verb of this build writes it.
    /// </summary>
    private static async Task WriteAsAnOlderBuildAsync(
        SqliteConnection connection, QuestStore machine, string quest, QuestOperationKind kind, string note, DateTimeOffset at)
    {
        await using var append = connection.CreateCommand();
        append.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload)
            VALUES ($quest, $kind, $machine,
                    (SELECT MAX(
                       COALESCE((SELECT MAX(sequence) FROM quest_log WHERE machine = $machine), 0),
                       COALESCE((SELECT sequence FROM quest_machine WHERE one = 1 AND id = $machine), 0)) + 1),
                    $at, $payload)
            """;
        append.Parameters.AddWithValue("$quest", quest);
        append.Parameters.AddWithValue("$kind", kind.ToString().ToLowerInvariant());
        append.Parameters.AddWithValue("$machine", machine.Machine);
        append.Parameters.AddWithValue("$at", at.ToString("O"));
        append.Parameters.AddWithValue("$payload", System.Text.Json.JsonSerializer.Serialize(new { note }));
        await append.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 🔴 A machine on an older build goes on after its take lost and closes, declines or waits: its store does not refuse
    /// it, so the next pass pushes it. The remote judges it by the claim the store judges by (QuestLog.Claim) and refuses
    /// it for that quest, answering in the lost take's words: the winner's quest neither closes nor waits, on the remote
    /// or the winner's machine. The older machine's pass reads the refusal, its claim reads lost so its driver stops the
    /// session, and what it made stays pending there, since only its own build could rewrite it.
    /// </summary>
    [Theory]
    [InlineData(QuestOperationKind.Done, "Finished after the take lost.")]
    [InlineData(QuestOperationKind.Declined, "Not ours after all.")]
    [InlineData(QuestOperationKind.Waited, null)]
    public async Task A_move_or_a_wait_an_older_build_makes_after_its_take_lost_is_refused_at_the_remote_and_never_reaches_the_winner(
        QuestOperationKind kind, string? said)
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2))).Moved);
        await SyncAsync(_a);
        Assert.Equal(QuestStatus.Taken, Assert.Single((await SyncAsync(_b)).Conflicts).Attempted);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));

        var question = await Publish(_b, "B's question for its owner");
        var note = said ?? question.Id;
        await WriteAsAnOlderBuildAsync(_connections[1], _b, quest.Id, kind, note, Now.AddHours(3));
        var next = await SyncAsync(_b);
        await SyncAsync(_a);

        foreach (var store in new[] { _a, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Taken, "A's session.", (string?)null), (held.Status, held.Note, held.Awaits));
            var conflict = Assert.Single(held.Conflicts);
            Assert.Equal((_b.Machine, QuestStatus.Taken, "B's session."), (conflict.Machine, conflict.Attempted, conflict.Note));
            Assert.DoesNotContain(await store.HistoryAsync(quest.Id), o => o.Kind == kind);
            Assert.Empty(await store.WaitingOnAsync(question.Id));
        }

        Assert.Null(next.Problem);
        Assert.Empty(next.Conflicts);
        var refused = Assert.Single(next.Refused);
        Assert.Equal(quest.Id, refused.Quest);
        Assert.StartsWith(
            $"Quest `#{quest.Id}` was taken on another machine first: this machine's take lost and is kept on the quest as a conflict, ",
            refused.Reason);
        Assert.Contains(
            kind == QuestOperationKind.Waited
                ? $"so it does not wait on `#{question.Id}`. The remote kept nothing this push carried for the quest; `#{question.Id}` stays a quest of its own."
                : $"so this `{kind.ToString().ToLowerInvariant()}` is not this machine's to make. The remote kept nothing this push carried for the quest.",
            refused.Reason);
        Assert.EndsWith("Stand down rather than doubling the work.", refused.Reason);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));
        Assert.Equal(kind, Assert.Single(await _b.PendingAsync(Workspaces.Default, _ => true)).Kind);

        // The question is a quest of its own and still went up: the refusal is for the raced quest alone.
        Assert.NotNull(await _remote.FindAsync(question.Id));
    }

    /// <summary>
    /// The remote's refusal reaches only a move made on a lost take (WAITCLAIM3). The winner's own wait and close land, as
    /// does the losing machine's dismissal of its conflict, which is no move on the take.
    /// </summary>
    [Fact]
    public async Task The_remote_keeps_the_winners_own_wait_and_close_and_the_losers_dismissal()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2))).Moved);
        await SyncAsync(_a);
        await SyncAsync(_b);

        var question = await Publish(_a, "A question for its owner");
        Assert.True((await _a.WaitAsync(quest.Id, question.Id, Now.AddHours(3))).Moved);
        var waited = await SyncAsync(_a);
        Assert.Equal(question.Id, (await _remote.FindAsync(quest.Id))!.Awaits);
        Assert.Equal(1, (await _b.DismissAsync(quest.Id, machine: null, sequence: null, Now.AddHours(4))).Dismissed);
        var dismissed = await SyncAsync(_b);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddHours(5))).Moved);
        var closed = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Empty(waited.Refused.Concat(dismissed.Refused).Concat(closed.Refused));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Done, "Landed.", 0), (held.Status, held.Note, held.Conflicts.Count));
        }
    }

    /// <summary>
    /// An open quest is nobody's work (D95, WAITCLAIM3): published again after a delete, it takes a move from the machine
    /// whose take lost to that delete, though that machine's claim on the quest read lost. Its take then holds, and its
    /// close lands.
    /// </summary>
    [Fact]
    public async Task The_remote_keeps_a_move_on_a_quest_published_again_from_the_machine_whose_take_lost_to_its_delete()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await _a.DeleteAsync(quest.Id, Now.AddHours(1), travels: true);
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2));
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));

        Assert.Equal(quest.Id, (await Publish(_a, body: "Asked properly this time.")).Id);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's second session.", Now.AddHours(3))).Moved);
        var taken = await SyncAsync(_b);
        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddHours(4))).Moved);
        var closed = await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Empty(taken.Refused.Concat(closed.Refused));
        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(quest.Id));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Done, "Landed."), (held.Status, held.Note));
        }
    }

    // ——— A decline that applies only while open (PAUSE1c, D132 point 10, design §5.2): an abandon judged its decline
    // on an open quest, so one that reaches the remote after another machine's take is a conflict on the quest (D68
    // rule 2), and the take stands.

    private Task<QuestSyncReport> SyncThroughAsync(QuestStore machine, IRemote remote) =>
        QuestSync.RunAsync(machine, _ => true, remote, Workspaces.Default);

    private const string Reason = "Abandoned: the work went the wrong way.";

    /// <summary>
    /// 🔴 Take first: a teammate's take reached the remote before this machine's decline, made while the quest still
    /// looked open here. The rebase turns the decline into a conflict carrying the reason, and the quest stays taken
    /// by the machine that took it, on every side.
    /// </summary>
    [Fact]
    public async Task A_decline_made_while_open_that_lands_after_a_take_is_a_conflict_and_the_take_stands()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.True((await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(1))).Moved);
        await SyncAsync(_b);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Declined, Reason, Now.AddHours(2), whileOpen: true)).Moved);
        var lost = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Empty(lost.Refused);
        var conflict = Assert.Single(lost.Conflicts);
        Assert.Equal((QuestStatus.Declined, Reason), (conflict.Attempted!.Value, conflict.Note));
        Assert.Empty(await _a.PendingAsync(Workspaces.Default, _ => true));
        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(quest.Id));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Taken, "B's session."), (held.Status, held.Note));
            var kept = Assert.Single(held.Conflicts);
            Assert.Equal((_a.Machine, QuestStatus.Declined, Reason), (kept.Machine, kept.Attempted, kept.Note));
        }
    }

    /// <summary>
    /// Decline first: the decline reached the remote while the quest was open, so it is the quest's answer, and a
    /// take made meanwhile on another machine is the conflict, as any losing take is. The flag crosses the wire both
    /// ways: the remote keeps it, and the machine that fetched it holds the decline as it was made.
    /// </summary>
    [Fact]
    public async Task A_decline_made_while_open_that_lands_first_declines_and_the_later_take_is_the_conflict()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(quest.Id, QuestStatus.Declined, Reason, Now.AddHours(1), whileOpen: true);
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, "B's session.", Now.AddHours(2));
        await SyncAsync(_a);
        var lost = await SyncAsync(_b);

        Assert.Equal(QuestStatus.Taken, Assert.Single(lost.Conflicts).Attempted);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(quest.Id));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(quest.Id))!;
            Assert.Equal((QuestStatus.Declined, Reason), (held.Status, held.Note));
            Assert.True((await store.HistoryAsync(quest.Id)).Single(o => o.Kind == QuestOperationKind.Declined).WhileOpen);
        }
    }

    /// <summary>
    /// An older record: a decline that says nothing of the flag — every decline before PAUSE1c, and the quest page's
    /// plain *Decline…* today — crosses the wire exactly as before and still lands over a take, because a person
    /// declining work they see taken means to.
    /// </summary>
    [Fact]
    public async Task A_decline_with_no_flag_crosses_as_before_and_still_lands_over_a_take()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await SyncAsync(_b);
        await _a.MoveAsync(quest.Id, QuestStatus.Declined, "Not ours after all.", Now.AddHours(2));
        var pass = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Empty(pass.Conflicts);
        var declined = (await _a.HistoryAsync(quest.Id)).Single(o => o.Kind == QuestOperationKind.Declined);
        Assert.DoesNotContain("whileOpen", QuestWire.Push(0, [declined]));
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Equal(QuestStatus.Declined, (await store.FindAsync(quest.Id))!.Status);
        }
    }

    /// <summary>
    /// 🔴 A remote built before PAUSE1c reads the decline as a plain one and keeps it without the flag. The race still
    /// ends as it does at a remote of this build — take first, the decline is the conflict and the take stands; decline
    /// first, the later take is the conflict — because this machine's rebase judges the flag before it pushes, and a
    /// remote judges a push only when nothing reached the quest after the base it was rebased on. What the older remote
    /// loses is its own second judgement, and the flag on what it hands back.
    /// </summary>
    [Fact]
    public async Task A_remote_that_drops_the_flag_still_ends_the_race_in_a_conflict_and_keeps_the_decline_plain()
    {
        var older = new OlderRemote(_remote);
        var takenFirst = await Publish(_a, "Taken first");
        var declinedFirst = await Publish(_a, "Declined first");
        await SyncThroughAsync(_a, older);
        await SyncThroughAsync(_b, older);

        await _b.MoveAsync(takenFirst.Id, QuestStatus.Taken, "B's session.", Now.AddHours(1));
        await SyncThroughAsync(_b, older);
        await _a.MoveAsync(takenFirst.Id, QuestStatus.Declined, Reason, Now.AddHours(2), whileOpen: true);
        await _a.MoveAsync(declinedFirst.Id, QuestStatus.Declined, Reason, Now.AddHours(2), whileOpen: true);
        await _b.MoveAsync(declinedFirst.Id, QuestStatus.Taken, "B's other session.", Now.AddHours(3));
        var a = await SyncThroughAsync(_a, older);
        var b = await SyncThroughAsync(_b, older);

        Assert.Equal((takenFirst.Id, QuestStatus.Declined), (Assert.Single(a.Conflicts).Quest, a.Conflicts[0].Attempted!.Value));
        Assert.Equal((declinedFirst.Id, QuestStatus.Taken), (Assert.Single(b.Conflicts).Quest, b.Conflicts[0].Attempted!.Value));
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Equal(QuestStatus.Taken, (await store.FindAsync(takenFirst.Id))!.Status);
            Assert.Equal(QuestStatus.Declined, (await store.FindAsync(declinedFirst.Id))!.Status);
        }

        Assert.False((await _remote.HistoryAsync(declinedFirst.Id)).Single(o => o.Kind == QuestOperationKind.Declined).WhileOpen);
        Assert.False((await _b.HistoryAsync(declinedFirst.Id)).Single(o => o.Kind == QuestOperationKind.Declined).WhileOpen);
    }

    /// <summary>
    /// The remote judges the flag too, through the same table (D47 §5): a push it judges — nothing reached the quest
    /// after its base — whose decline would land on a taken quest is refused for that quest, naming why. An older
    /// remote, judging the same push, keeps it as a plain decline: that is the one place its build decides, and no
    /// machine of this build sends such a push, since its rebase made the decline a conflict first.
    /// </summary>
    [Fact]
    public async Task The_remote_refuses_a_decline_made_while_open_on_a_quest_taken_there_and_an_older_one_keeps_it()
    {
        var quest = await Publish(_a);
        await SyncAsync(_a);
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await SyncAsync(_a);
        var decline = new QuestOperation(quest.Id, QuestOperationKind.Declined, "m-elsewhere", 7, Now.AddHours(2), Reason,
            WhileOpen: true);

        var refused = await new StoreRemote(_remote).PushQuestsAsync(2, [decline]);
        Assert.Empty(refused.Accepted);
        Assert.Contains("only while it is open", Assert.Single(refused.Refused, r => r.Quest == quest.Id).Reason);
        Assert.Equal(QuestStatus.Taken, (await _remote.FindAsync(quest.Id))!.Status);

        var kept = await new OlderRemote(_remote).PushQuestsAsync(2, [decline]);
        Assert.Single(kept.Accepted);
        Assert.Equal(QuestStatus.Declined, (await _remote.FindAsync(quest.Id))!.Status);
    }

    /// <summary>
    /// The flag crosses the wire on a decline, written only when set; a decline that says nothing of it reads plain;
    /// and one that says something other than true or false is half-made and does not cross, since read as plain it
    /// would decline over a take.
    /// </summary>
    [Fact]
    public void A_decline_made_while_open_crosses_the_wire_and_a_malformed_flag_does_not_cross_at_all()
    {
        var page = new QuestFetch(
        [
            new QuestOperation("abcdefabcdef", QuestOperationKind.Declined, "m1", 3, Now, Reason, Number: 4, WhileOpen: true),
            new QuestOperation("fedcbafedcba", QuestOperationKind.Declined, "m1", 5, Now, "Not ours.", Number: 6),
        ], 6, More: false);

        var json = QuestWire.Page(page);
        var back = QuestWire.ReadPage(json)!.Operations;

        Assert.Equal([true, false], back.Select(o => o.WhileOpen));
        Assert.Equal(1, json.Split("whileOpen").Length - 1);
        Assert.False(Assert.Single(QuestWire.ReadPush("""{ "base": 1, "operations": [{ "machine": "m1", "sequence": 1, "quest": "q", "kind": "declined", "at": "2026-09-24T10:00:00Z", "note": "n", "whileOpen": false }] }""")!.Value.Operations).WhileOpen);
        Assert.Null(QuestWire.ReadPush("""{ "base": 1, "operations": [{ "machine": "m1", "sequence": 1, "quest": "q", "kind": "declined", "at": "2026-09-24T10:00:00Z", "note": "n", "whileOpen": "yes" }] }"""));
    }
}
