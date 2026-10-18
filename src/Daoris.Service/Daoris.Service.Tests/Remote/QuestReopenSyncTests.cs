using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-CLOSE1c (§E5, D173's note): the person's reopen is operations, so it travels (D68), and a seal kept in one store could
/// not. Two machines and a remote, synced by the real <see cref="QuestSync"/>: a reopen on the take's machine meets another
/// machine's take of the step it withdraws. Whichever reached the remote first, both machines and the remote read one step, the
/// reasoned hold or the withdrawal, and the source's old submission closes nowhere.
/// </summary>
public sealed class QuestReopenSyncTests : IAsyncLifetime
{
    private readonly List<SqliteConnection> _connections = [];
    private QuestStore _a = null!;
    private QuestStore _b = null!;
    private QuestStore _remote = null!;
    private QuestStore _c = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T10:00:00Z");
    private const string Graph = "sha256:0f1e2d3c4b5a6978";

    public async Task InitializeAsync()
    {
        _a = await OpenAsync();
        _b = await OpenAsync();
        _remote = await OpenAsync();
        // FABRIC-CLOSE1c18: a third machine that only reads, fetching as the others move.
        _c = await OpenAsync();
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

    private Task<QuestSyncReport> SyncAsync(QuestStore machine) =>
        QuestSync.RunAsync(machine, _ => true, new StoreRemote(_remote), Workspaces.Default);

    /// <summary>A takes, binds and submits; nothing holds the submission, so its step is published and seals it; both machines hold it.</summary>
    private async Task<(Quest Quest, Quest Step, QuestOperationRef Submission)> SealedOnBothAsync()
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Close in a graph", "why", Now,
            then: [new QuestStep("Federated", "Verify {parent}", "Look at it.")]);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _a.BindAsync(quest.Id, Graph, Now.AddHours(1))).Moved);
        var submitted = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2), session: "s0a1b2c3");
        Assert.True(submitted.Moved);
        await SyncAsync(_a);
        await SyncAsync(_b);
        var current = submitted.Quest!.Submission!;
        return (submitted.Quest, submitted.FollowUp!, new QuestOperationRef(current.Machine, current.Sequence));
    }

    [Fact]
    public async Task A_reopen_meeting_a_take_of_its_step_that_reached_the_remote_first_holds_the_step_everywhere()
    {
        var (quest, step, submission) = await SealedOnBothAsync();
        Assert.Equal(submission, (await _b.FindAsync(step.Id))!.GrewFrom);
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(3))).Refusal);
        // As A sees it before any sync, nobody took the step: withdrawn here.
        Assert.Equal((QuestStatus.Open, QuestHold.SourceReopened), ((await _a.FindAsync(step.Id))!.Status, (await _a.FindAsync(step.Id))!.Hold));

        await SyncAsync(_b);
        var report = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Taken, QuestHold.SourceReopened, submission), (held.Status, held.Hold, held.SourceReopened));
            Assert.Null((await store.FindAsync(quest.Id))!.Submission);
        }

        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(step.Id));
        // B's session is told at its next step: its done moves nothing.
        var done = await _b.MoveAsync(step.Id, QuestStatus.Done, "Verified.", Now.AddHours(4), byAgent: true);
        Assert.False(done.Moved);
        Assert.True(done.SourceReopened);
        // The take's owner refuses the old submission's close.
        var close = await _a.CloseAsync(quest.Id, [(quest.Id, submission)], new QuestClose("k1", "t1"), Now.AddHours(4));
        Assert.Equal(QuestCloseRefusal.Stale, close.Refusal);
    }

    [Fact]
    public async Task A_reopen_that_reached_the_remote_first_withdraws_the_step_and_the_late_take_loses_everywhere()
    {
        var (quest, step, submission) = await SealedOnBothAsync();
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(3))).Refusal);

        await SyncAsync(_a);
        var report = await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var withdrawn = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Open, QuestHold.SourceReopened), (withdrawn.Status, withdrawn.Hold));
        }

        // B's take lost: its claim reads lost, which the driver stops its session by, and the pass names the take. It lost to the
        // person's reopen, and is said so (FABRIC-CLOSE1c18).
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(step.Id));
        Assert.True(await _b.LostToTheReopenAsync(step.Id));
        Assert.Contains(report.Conflicts, c => c.Quest == step.Id && c.Attempted == QuestStatus.Taken);
        var close = await _a.CloseAsync(quest.Id, [(quest.Id, submission)], new QuestClose("k1", "t1"), Now.AddHours(4));
        Assert.Equal(QuestCloseRefusal.Stale, close.Refusal);
    }

    /// <summary>
    /// FABRIC-CLOSE1c15: a publication's rebind is judged while the step is open. A reopens its source and submits again before B's take
    /// of the step reaches it, so A's publication rebinds the step A reads withdrawn; B's take reached the remote first, and a started
    /// step goes on only by the person's word. Both machines and the remote read the step held on the reopened submission, and the go-on
    /// then lets it go everywhere.
    /// </summary>
    [Fact]
    public async Task A_publications_rebind_never_lets_go_a_step_another_machine_took_first()
    {
        var (quest, step, submission) = await SealedOnBothAsync();
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        await SyncAsync(_b);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(3))).Refusal);
        var again = await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(4));
        Assert.True(again.Moved);
        var grows = new QuestOperationRef(again.Quest!.Submission!.Machine, again.Quest.Submission.Sequence);
        // As A sees it before any sync, nobody took the step, so the new submission's publication rebound it.
        var seen = (await _a.FindAsync(step.Id))!;
        Assert.Equal((QuestStatus.Open, (QuestHold?)null, grows), (seen.Status, seen.Hold, seen.GrewFrom));

        var report = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var held = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Taken, QuestHold.SourceReopened, submission, submission),
                (held.Status, held.Hold, held.SourceReopened, held.GrewFrom));
        }

        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(step.Id));
        // A remote handed a publication's rebind on the step taken there refuses it, in the words a decline made while open is refused in.
        var marked = new QuestOperation(step.Id, QuestOperationKind.Rebound, "m-elsewhere", 7, Now.AddHours(4), Submission: grows,
            WhileOpen: true);
        var pushed = await new StoreRemote(_remote).PushQuestsAsync((await _remote.OperationsSinceAsync(0)).Through, [marked]);
        Assert.Empty(pushed.Accepted);
        Assert.Contains("only while it is open", Assert.Single(pushed.Refused, r => r.Quest == step.Id).Reason);
        Assert.Equal(QuestHold.SourceReopened, (await _remote.FindAsync(step.Id))!.Hold);

        // Nothing seals the new submission until the person's word: the source's close waits.
        var early = await _a.CloseAsync(quest.Id, [(quest.Id, grows)], new QuestClose("k1", "t1"), Now.AddHours(5));
        Assert.Equal(QuestCloseRefusal.Unsealed, early.Refusal);

        Assert.Equal(QuestReopenRefusal.None, (await _b.ContinueAsync(step.Id, Now.AddHours(5))).Refusal);
        await SyncAsync(_b);
        await SyncAsync(_a);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var going = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Taken, (QuestHold?)null, grows), (going.Status, going.Hold, going.GrewFrom));
        }

        // The step stands on the new submission now, so the source's close runs.
        var close = await _a.CloseAsync(quest.Id, [(quest.Id, grows)], new QuestClose("k2", "t2"), Now.AddHours(6));
        Assert.Equal(QuestCloseRefusal.None, close.Refusal);
    }

    /// <summary>
    /// FABRIC-CLOSE1c14: the go-on on B's step clears its result, and reopens the successor that result published in the same write. The
    /// reopen travels as the person's does, so both machines and the remote read the successor withdrawn, or held where A took it; the
    /// step's next submission rebinds a withdrawn one, a held one waits on the person's word, and then the step's close runs.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_go_on_withdraws_or_holds_the_steps_own_successor_alike_on_every_machine(bool started)
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Close in a chain", "why", Now,
            then: [new QuestStep("Federated", "Verify {parent}", "Look at it."), new QuestStep("Federated", "Publish {parent}", "Put it live.")]);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _a.BindAsync(quest.Id, Graph, Now.AddHours(1))).Moved);
        var built = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2));
        var step = built.FollowUp!;
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        Assert.True((await _b.BindAsync(step.Id, Graph, Now.AddHours(3))).Moved);
        var verified = await _b.SubmitAsync(step.Id, "Verified.", [], Now.AddHours(3));
        var next = verified.FollowUp!;
        var old = new QuestOperationRef(verified.Quest!.Submission!.Machine, verified.Quest.Submission.Sequence);
        await SyncAsync(_b);
        await SyncAsync(_a);
        if (started)
        {
            Assert.True((await _a.MoveAsync(next.Id, QuestStatus.Taken, "A's other session.", Now.AddHours(4))).Moved);
        }

        var first = new QuestOperationRef(built.Quest!.Submission!.Machine, built.Quest.Submission.Sequence);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, first, "Build it again.", Now.AddHours(4))).Refusal);
        Assert.True((await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(5))).Moved);
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Equal(QuestReopenRefusal.None, (await _b.ContinueAsync(step.Id, Now.AddHours(6))).Refusal);
        await SyncAsync(_b);
        var report = await SyncAsync(_a);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        var stands = started ? QuestStatus.Taken : QuestStatus.Open;
        foreach (var store in new[] { _a, _b, _remote })
        {
            var reopened = (await store.FindAsync(next.Id))!;
            Assert.Equal((stands, QuestHold.SourceReopened, old), (reopened.Status, reopened.Hold, reopened.SourceReopened));
        }

        var again = await _b.SubmitAsync(step.Id, "Verified again.", [], Now.AddHours(7));
        Assert.True(again.Moved);
        var grows = new QuestOperationRef(again.Quest!.Submission!.Machine, again.Quest.Submission.Sequence);
        await SyncAsync(_b);
        await SyncAsync(_a);
        if (started)
        {
            foreach (var store in new[] { _a, _b, _remote })
            {
                Assert.Equal((QuestHold.SourceReopened, old), ((await store.FindAsync(next.Id))!.Hold, (await store.FindAsync(next.Id))!.GrewFrom));
            }

            Assert.Equal(QuestReopenRefusal.None, (await _a.ContinueAsync(next.Id, Now.AddHours(8))).Refusal);
            await SyncAsync(_a);
            await SyncAsync(_b);
        }

        foreach (var store in new[] { _a, _b, _remote })
        {
            var rebound = (await store.FindAsync(next.Id))!;
            Assert.Equal((stands, (QuestHold?)null, grows), (rebound.Status, rebound.Hold, rebound.GrewFrom));
        }

        var close = await _b.CloseAsync(step.Id, [(step.Id, grows)], new QuestClose("k3", "t3"), Now.AddHours(9));
        Assert.Equal(QuestCloseRefusal.None, close.Refusal);
    }

    /// <summary>
    /// FABRIC-CLOSE1c4: the source's last reopen and the session that said its submission's ready are read from the log, so the machine
    /// that synced it and the remote answer them as the take's machine does, and the next ready answers the reopen on every one.
    /// </summary>
    [Fact]
    public async Task A_sources_reopen_and_the_session_that_said_ready_read_the_same_on_every_machine()
    {
        var (quest, _, submission) = await SealedOnBothAsync();
        foreach (var store in new[] { _a, _b, _remote })
        {
            Assert.Equal("s0a1b2c3", (await store.FindAsync(quest.Id))!.Submission!.Session);
        }

        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(3))).Refusal);
        var reopen = (await _a.HistoryAsync(quest.Id))[^1];
        // FABRIC-CLOSE1c8: and the session that said the reopened ready, which the reopen cleared with the submission.
        var expected = new QuestReopen(reopen.Machine, reopen.Sequence, submission, "Build it again.", Now.AddHours(3), "s0a1b2c3");
        await SyncAsync(_a);
        await SyncAsync(_b);

        foreach (var store in new[] { _a, _b, _remote })
        {
            var reopened = (await store.FindAsync(quest.Id))!;
            Assert.Equal(expected, reopened.Reopened);
            Assert.Null(reopened.Submission);
        }

        Assert.True((await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(4), session: "s4d5e6f7")).Moved);
        await SyncAsync(_a);
        await SyncAsync(_b);

        foreach (var store in new[] { _a, _b, _remote })
        {
            var answered = (await store.FindAsync(quest.Id))!;
            Assert.Null(answered.Reopened);
            Assert.Equal("s4d5e6f7", answered.Submission!.Session);
        }
    }

    /// <summary>
    /// FABRIC-CLOSE1c10: the person's go-on clears the held step's own result by the replay's rule, so the step's machine, the source's
    /// machine and the remote read it taken with nothing submitted, grown from the new submission, and its old submission closes nowhere.
    /// </summary>
    [Fact]
    public async Task A_go_on_clears_the_held_steps_own_result_alike_on_every_machine()
    {
        var (quest, step, submission) = await SealedOnBothAsync();
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(3))).Moved);
        Assert.True((await _b.BindAsync(step.Id, Graph, Now.AddHours(3))).Moved);
        var verified = await _b.SubmitAsync(step.Id, "Verified.", [], Now.AddHours(3), session: "s7b8c9d0");
        Assert.True(verified.Moved);
        var old = verified.Quest!.Submission!;
        await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(4))).Refusal);
        var again = await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(5));
        Assert.True(again.Moved);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.Equal(QuestHold.SourceReopened, (await _b.FindAsync(step.Id))!.Hold);

        Assert.Equal(QuestReopenRefusal.None, (await _b.ContinueAsync(step.Id, Now.AddHours(6))).Refusal);
        await SyncAsync(_b);
        var report = await SyncAsync(_a);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        var grows = new QuestOperationRef(again.Quest!.Submission!.Machine, again.Quest.Submission.Sequence);
        foreach (var store in new[] { _a, _b, _remote })
        {
            var cleared = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Taken, (QuestHold?)null, grows), (cleared.Status, cleared.Hold, cleared.GrewFrom));
            Assert.Null(cleared.Submission);
            Assert.Empty(cleared.Answers);
        }

        // The step's machine refuses its old submission's close; its agent submits anew.
        var close = await _b.CloseAsync(step.Id, [(step.Id, new QuestOperationRef(old.Machine, old.Sequence))], new QuestClose("k2", "t2"),
            Now.AddHours(7));
        Assert.Equal(QuestCloseRefusal.Stale, close.Refusal);
    }

    // ——— FABRIC-CLOSE1c18: a take carries the step's source as it found it, so one made before a reopen and its rebind reached its
    // machine loses wherever it is judged. B first is CLOSE1c15's case above: B's take stands, judged where the replay reaches it.

    /// <summary>
    /// A reopens its source and submits again, so its publication rebinds the open step to the new submission, and reaches the remote
    /// first. B, not yet fetched, takes the step as it grew from the reopened submission.
    /// </summary>
    private async Task<(Quest Quest, Quest Step, QuestOperationRef Reopened, QuestOperationRef Grows)> TakenBeforeTheReopenReachedBAsync()
    {
        var (quest, step, submission) = await SealedOnBothAsync();
        await SyncAsync(_c);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, submission, "Build it again.", Now.AddHours(3))).Refusal);
        var again = await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(4));
        Assert.True(again.Moved);
        await SyncAsync(_a);
        Assert.Equal(submission, (await _b.FindAsync(step.Id))!.GrewFrom);
        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(5))).Moved);
        return (quest, step, submission, new QuestOperationRef(again.Quest!.Submission!.Machine, again.Quest.Submission.Sequence));
    }

    /// <summary>
    /// 🔴 B's take loses at its rebase: its claim reads lost, which its driver stops the session by, and every machine (a third fetching
    /// as they move, a fourth fetching from nothing) and the remote read the step open on the new submission with B's take as a conflict
    /// on it, as the replay of their history gives it. The new submission is sealed by the rebound step, and a fresh take of it holds.
    /// </summary>
    [Fact]
    public async Task A_take_made_before_a_reopen_and_its_rebind_reached_its_machine_loses_everywhere()
    {
        var (quest, step, _, grows) = await TakenBeforeTheReopenReachedBAsync();

        var report = await SyncAsync(_b);
        await SyncAsync(_a);
        await SyncAsync(_c);
        var zero = await OpenAsync();
        await SyncAsync(zero);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(step.Id));
        // Lost to the person's reopen, which the claim's answer and the driver's stop say: never another machine's take.
        Assert.Equal((true, false), (await _b.LostToTheReopenAsync(step.Id), await _a.LostToTheReopenAsync(step.Id)));
        var lost = Assert.Single(report.Conflicts, c => c.Quest == step.Id);
        Assert.Equal((QuestStatus.Taken, "B's session.", (QuestOperationRef?)null), (lost.Attempted, lost.Note, lost.Submission));
        foreach (var store in new[] { _a, _b, _c, zero, _remote })
        {
            var open = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Open, (QuestHold?)null, grows, (QuestOperationRef?)null),
                (open.Status, open.Hold, open.GrewFrom, open.SourceReopened));
            Assert.Equal([(_b.Machine, QuestStatus.Taken, "B's session.")], open.Conflicts.Select(c => (c.Machine, c.Attempted, c.Note)));
            var replayed = QuestLog.Replay(await store.HistoryAsync(step.Id))!;
            Assert.Equal((open.Status, open.GrewFrom, open.Conflicts.Count), (replayed.Status, replayed.GrewFrom, replayed.Conflicts.Count));
            Assert.Equal(grows, (await store.FindAsync(quest.Id))!.Submission is { } current ? new QuestOperationRef(current.Machine, current.Sequence) : null);
        }

        var close = await _a.CloseAsync(quest.Id, [(quest.Id, grows)], new QuestClose("k1", "t1"), Now.AddHours(6));
        Assert.Equal(QuestCloseRefusal.None, close.Refusal);

        var fresh = await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's second session.", Now.AddHours(7));
        Assert.True(fresh.Moved);
        Assert.Equal(grows, (await _b.HistoryAsync(step.Id))[^1].Submission);
        Assert.Empty((await SyncAsync(_b)).Refused);
        Assert.Equal(QuestClaim.Held, await _b.ClaimAsync(step.Id));
        Assert.False(await _b.LostToTheReopenAsync(step.Id));
        Assert.Equal(QuestStatus.Taken, (await _remote.FindAsync(step.Id))!.Status);
    }

    /// <summary>
    /// 🔴 A take carrying the source the step grew from before the reopen, pushed straight at the remote's current base, is refused
    /// there, saying the person's reopen moved the step; one carrying the step's source now is kept.
    /// </summary>
    [Fact]
    public async Task A_stale_take_pushed_at_the_remotes_current_base_is_refused_and_a_current_one_is_kept()
    {
        var (_, step, reopened, grows) = await TakenBeforeTheReopenReachedBAsync();
        var remote = new StoreRemote(_remote);
        var through = (await _remote.OperationsSinceAsync(0)).Through;
        var stale = new QuestOperation(step.Id, QuestOperationKind.Taken, "m-elsewhere", 7, Now.AddHours(5), "Elsewhere's session.",
            Submission: reopened);

        var pushed = await remote.PushQuestsAsync(through, [stale]);

        Assert.Empty(pushed.Accepted);
        var refusal = Assert.Single(pushed.Refused, r => r.Quest == step.Id).Reason;
        Assert.Contains("the person's reopen", refusal);
        Assert.Equal((QuestStatus.Open, grows), ((await _remote.FindAsync(step.Id))!.Status, (await _remote.FindAsync(step.Id))!.GrewFrom));

        var current = await remote.PushQuestsAsync(through, [stale with { Sequence = 8, Submission = grows }]);
        Assert.Single(current.Accepted);
        Assert.Equal(QuestStatus.Taken, (await _remote.FindAsync(step.Id))!.Status);
    }

    /// <summary>
    /// What a machine on a build before FABRIC-CLOSE1c18 writes for a move it makes after its take lost to the reopen: the store's own
    /// append, with no claim judged on an open step, pending as any move is. Written by hand because no verb of this build writes it.
    /// </summary>
    private async Task WriteAsAnOlderBuildAsync(QuestStore machine, string quest, QuestOperationKind kind, string note, DateTimeOffset at)
    {
        await using var append = _connections[1].CreateCommand();
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
    /// 🔴 A take lost to the reopen leaves the step open, where the table allows a done or a decline from open. B's session runs until its
    /// driver's next look stops it, and closes or declines nothing meanwhile: B's store refuses the move inside its write, in the reopen's
    /// words; a remote refuses one pushed by a build that did not; and B's rebase loses one such a build left pending, as a conflict of its
    /// own. The step stays open on the new submission everywhere. A take is not refused so: a fresh one of the open step is B's to make.
    /// </summary>
    [Theory]
    [InlineData(QuestOperationKind.Done)]
    [InlineData(QuestOperationKind.Declined)]
    public async Task After_a_take_lost_to_the_reopen_the_losers_move_lands_on_the_open_step_nowhere(QuestOperationKind kind)
    {
        var (_, step, _, grows) = await TakenBeforeTheReopenReachedBAsync();
        await SyncAsync(_b);
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(step.Id));
        var status = QuestTransitions.Target(kind)!.Value;

        var move = await _b.MoveAsync(step.Id, status, "After the take lost.", Now.AddHours(6), byAgent: kind == QuestOperationKind.Done);
        Assert.Equal((false, true), (move.Moved, move.ClaimLost));

        var through = (await _remote.OperationsSinceAsync(0)).Through;
        var pushed = await new StoreRemote(_remote).PushQuestsAsync(through,
            [new QuestOperation(step.Id, kind, _b.Machine, 999, Now.AddHours(6), "After the take lost.")]);
        Assert.Empty(pushed.Accepted);
        Assert.Contains("the person's reopen", Assert.Single(pushed.Refused, r => r.Quest == step.Id).Reason);

        await WriteAsAnOlderBuildAsync(_b, step.Id, kind, "After the take lost.", Now.AddHours(6));
        var upgraded = await SyncAsync(_b);
        await SyncAsync(_a);
        Assert.Empty(upgraded.Refused);
        Assert.Equal((status, "After the take lost."), (Assert.Single(upgraded.Conflicts).Attempted, upgraded.Conflicts[0].Note));
        Assert.Empty(await _b.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var open = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Open, grows), (open.Status, open.GrewFrom));
        }

        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's second session.", Now.AddHours(7))).Moved);
    }

    // ——— FABRIC-CLOSE1c21: the pass that finds such a take lost, stopped part way and run again after a restart; and what the take
    // made on the step going with it.

    /// <summary>Where B's pass stops: inside its integration before the commit, or after it, before its push reached the remote or before
    /// the answer to a push the remote kept.</summary>
    public enum Stop { BeforeTheCommit, BeforeThePushReached, BeforeThePushAnswered }

    /// <summary>
    /// A machine that stops inside a write: the cursor's write is the integration's last, so every rewrite the rebase made before it is
    /// undone with it. Dropped again before the restart.
    /// </summary>
    private static async Task StopAtTheCursorAsync(SqliteConnection connection, bool stopping)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = stopping
            ? """
              CREATE TRIGGER stop_insert BEFORE INSERT ON quest_cursor BEGIN SELECT RAISE(ABORT, 'the machine stopped'); END;
              CREATE TRIGGER stop_update BEFORE UPDATE ON quest_cursor BEGIN SELECT RAISE(ABORT, 'the machine stopped'); END;
              """
            : "DROP TRIGGER stop_insert; DROP TRIGGER stop_update;";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// B's pass finds its take lost to the reopen and stops part way, and B restarts: its store opened again on the same connection, then
    /// a pass run again. Stopped before the commit, the integration is undone whole and the next pass finds the loss; stopped after it, the
    /// loss and the cursor stand, and the next pass pushes the conflict, or numbers the one the remote already kept. Either way the take is
    /// lost once: one conflict, reported by one pass, kept once at the remote and on every machine (a third fetching as they move, a fourth
    /// from nothing), with nothing left to go up, B's cursor where the remote's numbers end, and a fresh take of the open step holding.
    /// </summary>
    [Theory]
    [InlineData(Stop.BeforeTheCommit)]
    [InlineData(Stop.BeforeThePushReached)]
    [InlineData(Stop.BeforeThePushAnswered)]
    public async Task A_pass_that_loses_a_stale_take_stopped_part_way_and_run_again_after_a_restart_loses_it_once(Stop stop)
    {
        var (_, step, _, grows) = await TakenBeforeTheReopenReachedBAsync();
        var connection = _connections[1];
        var fetched = (await _remote.OperationsSinceAsync(0)).Through;
        var cursor = await _b.CursorAsync(Workspaces.Default);
        var reported = new List<QuestOperation>();

        if (stop == Stop.BeforeTheCommit)
        {
            await StopAtTheCursorAsync(connection, stopping: true);
            await Assert.ThrowsAsync<SqliteException>(() => SyncAsync(_b));
            await StopAtTheCursorAsync(connection, stopping: false);
            Assert.Equal((QuestClaim.Unconfirmed, cursor, QuestStatus.Taken),
                (await _b.ClaimAsync(step.Id), await _b.CursorAsync(Workspaces.Default), (await _b.FindAsync(step.Id))!.Status));
        }
        else
        {
            var stopped = await QuestSync.RunAsync(
                _b, _ => true, new InterruptedRemote(new StoreRemote(_remote), reaches: stop == Stop.BeforeThePushAnswered), Workspaces.Default);
            Assert.NotNull(stopped.Problem);
            reported.AddRange(stopped.Conflicts);
            Assert.Equal((QuestClaim.Lost, fetched), (await _b.ClaimAsync(step.Id), await _b.CursorAsync(Workspaces.Default)));
        }

        var restarted = await QuestStore.OpenAsync(connection);
        Assert.Equal(_b.Machine, restarted.Machine);
        var again = await SyncAsync(restarted);
        reported.AddRange(again.Conflicts);
        await SyncAsync(_a);
        await SyncAsync(_c);
        var zero = await OpenAsync();
        await SyncAsync(zero);

        Assert.Null(again.Problem);
        Assert.Empty(again.Refused);
        Assert.Equal(stop == Stop.BeforeThePushAnswered ? 0 : 1, again.Pushed);
        var lost = Assert.Single(reported);
        Assert.Equal((step.Id, QuestStatus.Taken, "B's session."), (lost.Quest, lost.Attempted, lost.Note));
        Assert.Equal((QuestClaim.Lost, true), (await restarted.ClaimAsync(step.Id), await restarted.LostToTheReopenAsync(step.Id)));
        Assert.Empty(await restarted.PendingAsync(Workspaces.Default, _ => true));
        // The cursor is consistent: B holds every operation the remote numbered, under the number the remote gave it, and its cursor
        // names no number the remote has not given. A pass that pushed leaves its cursor where its fetch ended, as every push does, and
        // the next pass brings it level with nothing to push or report.
        var numbered = (await _remote.OperationsSinceAsync(0)).Operations;
        foreach (var id in numbered.Select(o => o.Quest).Distinct())
        {
            Assert.Equal(numbered.Where(o => o.Quest == id).Select(o => (o.Machine, o.Sequence, o.Number)),
                (await restarted.HistoryAsync(id)).Select(o => (o.Machine, o.Sequence, o.Number)));
        }

        Assert.InRange(await restarted.CursorAsync(Workspaces.Default), fetched, numbered[^1].Number!.Value);
        var level = await SyncAsync(restarted);
        Assert.Equal((0, 0, numbered[^1].Number), (level.Pushed, level.Conflicts.Count, (long?)await restarted.CursorAsync(Workspaces.Default)));
        var kept = await _remote.HistoryAsync(step.Id);
        Assert.Equal(QuestOperationKind.Conflict, Assert.Single(kept, o => o.Machine == _b.Machine).Kind);
        foreach (var store in new[] { _a, restarted, _c, zero, _remote })
        {
            Assert.Equal(kept.Select(o => (o.Machine, o.Sequence, o.Kind)), (await store.HistoryAsync(step.Id)).Select(o => (o.Machine, o.Sequence, o.Kind)));
            var open = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Open, grows), (open.Status, open.GrewFrom));
            Assert.Equal([(_b.Machine, QuestStatus.Taken, "B's session.")], open.Conflicts.Select(c => (c.Machine, c.Attempted, c.Note)));
        }

        Assert.True((await restarted.MoveAsync(step.Id, QuestStatus.Taken, "B's second session.", Now.AddHours(7))).Moved);
        Assert.Empty((await SyncAsync(restarted)).Refused);
        Assert.Equal(QuestClaim.Held, await restarted.ClaimAsync(step.Id));
        Assert.Equal(QuestStatus.Taken, (await _remote.FindAsync(step.Id))!.Status);
    }

    /// <summary>
    /// B works on the step it took before the reopen and its rebind reached it, before syncing: it binds, submits, and its submission
    /// publishes the chain's next step. When its take loses, the bind, the submission and the step go with it: none reaches the remote, and
    /// none stands on A, on B or at the remote. B's claim reads lost to the person's reopen, the conflict says so, and the step stays open on
    /// the new submission, bound to nothing and sealed by nothing. Raced, A's reopen, submission and rebind reach the remote between B's
    /// fetch and its push, so B's push stands on a base the step has moved past: the remote takes nothing of the step, and nothing of the
    /// step it published either, since that grew from a submission the remote does not hold; B goes round, and its rebase loses them.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_take_lost_to_the_reopen_takes_its_bind_its_submission_and_the_step_it_published_with_it_everywhere(bool raced)
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Close in a chain", "why", Now,
            then: [new QuestStep("Federated", "Verify {parent}", "Look at it."), new QuestStep("Federated", "Publish {parent}", "Put it live.")]);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _a.BindAsync(quest.Id, Graph, Now.AddHours(1))).Moved);
        var built = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2));
        var step = built.FollowUp!;
        var first = new QuestOperationRef(built.Quest!.Submission!.Machine, built.Quest.Submission.Sequence);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, first, "Build it again.", Now.AddHours(3))).Refusal);
        var again = await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(4));
        var grows = new QuestOperationRef(again.Quest!.Submission!.Machine, again.Quest.Submission.Sequence);
        if (!raced) await SyncAsync(_a);

        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(5))).Moved);
        Assert.True((await _b.BindAsync(step.Id, Graph, Now.AddHours(5))).Moved);
        var verified = await _b.SubmitAsync(step.Id, "Verified.", [], Now.AddHours(6), session: "s0b1c2d3");
        var next = verified.FollowUp!;
        Assert.Equal(step.Id, next.Parent);
        var report = raced
            ? await QuestSync.RunAsync(_b, _ => true, new RacedRemote(new StoreRemote(_remote), () => SyncAsync(_a)), Workspaces.Default)
            : await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        Assert.Empty(report.Behind);
        Assert.Equal((QuestClaim.Lost, true), (await _b.ClaimAsync(step.Id), await _b.LostToTheReopenAsync(step.Id)));
        var lost = Assert.Single(report.Conflicts);
        Assert.Equal((step.Id, QuestStatus.Taken), (lost.Quest, lost.Attempted));
        Assert.StartsWith("B's session. ", lost.Note);
        Assert.Contains("submission", lost.Note);
        Assert.Contains("reopen", lost.Note);
        Assert.DoesNotContain("winner", lost.Note);
        Assert.Empty(await _b.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var open = (await store.FindAsync(step.Id))!;
            Assert.Equal((QuestStatus.Open, (QuestHold?)null, grows), (open.Status, open.Hold, open.GrewFrom));
            Assert.Equal(((string?)null, (QuestSubmission?)null), (open.Closure, open.Submission));
            Assert.Equal([(_b.Machine, QuestStatus.Taken, lost.Note)], open.Conflicts.Select(c => (c.Machine, c.Attempted, c.Note)));
            Assert.DoesNotContain(await store.HistoryAsync(step.Id),
                o => o.Kind is QuestOperationKind.Bound or QuestOperationKind.Submitted or QuestOperationKind.Taken);
            Assert.Null(await store.FindAsync(next.Id));
            Assert.Empty(await store.HistoryAsync(next.Id));
        }

        Assert.Equal(QuestClaim.None, await _b.ClaimAsync(next.Id));
        // The step is A's chain's again: a fresh take of it binds, and its submission publishes the next step anew.
        Assert.True((await _a.MoveAsync(step.Id, QuestStatus.Taken, "A's step session.", Now.AddHours(7))).Moved);
        Assert.True((await _a.BindAsync(step.Id, Graph, Now.AddHours(7))).Moved);
        var anew = await _a.SubmitAsync(step.Id, "Verified anew.", [], Now.AddHours(8));
        Assert.Equal(next.Id, anew.FollowUp!.Id);
        Assert.Empty((await SyncAsync(_a)).Refused);
        Assert.Equal(QuestStatus.Open, (await _remote.FindAsync(next.Id))!.Status);
    }

    /// <summary>
    /// 🔴 FABRIC-CLOSE1c22: B, before syncing, also takes the next step its submission published, so a session of B's works it. The
    /// step goes with B's take of its source, lost to the person's reopen, and B's take of it is kept as a conflict on a quest no list
    /// shows, whose history holds nothing else. Its words say the person's reopen, never another machine's take, and B's claim on it
    /// reads lost to the reopen, which the claim's answer says beside it and B's driver stops that session by; so it does after a restart.
    /// Once the step's chain publishes the step anew, B's conflict goes up on it saying the same, and B's done on the open step lands
    /// nowhere.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_next_step_taken_before_the_sync_goes_with_the_take_lost_to_the_reopen_and_says_the_reopen(bool raced)
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Close in a chain", "why", Now,
            then: [new QuestStep("Federated", "Verify {parent}", "Look at it."), new QuestStep("Federated", "Publish {parent}", "Put it live.")]);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _a.BindAsync(quest.Id, Graph, Now.AddHours(1))).Moved);
        var built = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2));
        var step = built.FollowUp!;
        var first = new QuestOperationRef(built.Quest!.Submission!.Machine, built.Quest.Submission.Sequence);
        await SyncAsync(_a);
        await SyncAsync(_b);
        Assert.Equal(QuestReopenRefusal.None, (await _a.ReopenAsync(quest.Id, first, "Build it again.", Now.AddHours(3))).Refusal);
        Assert.True((await _a.SubmitAsync(quest.Id, "Built again.", [], Now.AddHours(4))).Moved);
        if (!raced) await SyncAsync(_a);

        Assert.True((await _b.MoveAsync(step.Id, QuestStatus.Taken, "B's session.", Now.AddHours(5))).Moved);
        Assert.True((await _b.BindAsync(step.Id, Graph, Now.AddHours(5))).Moved);
        var next = (await _b.SubmitAsync(step.Id, "Verified.", [], Now.AddHours(6), session: "s0b1c2d3")).FollowUp!;
        Assert.True((await _b.MoveAsync(next.Id, QuestStatus.Taken, "B's next session.", Now.AddHours(6))).Moved);
        var report = raced
            ? await QuestSync.RunAsync(_b, _ => true, new RacedRemote(new StoreRemote(_remote), () => SyncAsync(_a)), Workspaces.Default)
            : await SyncAsync(_b);
        await SyncAsync(_a);

        Assert.Null(report.Problem);
        Assert.Empty(report.Refused);
        var gone = Assert.Single(report.Conflicts, c => c.Quest == next.Id);
        Assert.Equal((QuestStatus.Taken, _b.Machine), (gone.Attempted, gone.Machine));
        Assert.StartsWith("B's next session. ", gone.Note);
        Assert.Contains($"#{step.Id}", gone.Note);
        Assert.Contains("the person's reopen", gone.Note);
        Assert.DoesNotContain("another machine", gone.Note);
        Assert.Equal((QuestClaim.Lost, true), (await _b.ClaimAsync(next.Id), await _b.LostToTheReopenAsync(next.Id)));
        Assert.Equal((QuestClaim.Lost, true), (await _b.ClaimAsync(step.Id), await _b.LostToTheReopenAsync(step.Id)));
        Assert.Equal([QuestOperationKind.Conflict], (await _b.HistoryAsync(next.Id)).Select(o => o.Kind));
        Assert.Empty(await _b.PendingAsync(Workspaces.Default, _ => true));
        foreach (var store in new[] { _a, _b, _remote }) Assert.Null(await store.FindAsync(next.Id));

        // Read back from the log alone, as a restarted machine reads it.
        var restarted = await QuestStore.OpenAsync(_connections[1]);
        Assert.Equal((QuestClaim.Lost, true), (await restarted.ClaimAsync(next.Id), await restarted.LostToTheReopenAsync(next.Id)));

        // The step is A's chain's again, and its submission publishes the next step anew under the same id: B's conflict goes up on it.
        Assert.True((await _a.MoveAsync(step.Id, QuestStatus.Taken, "A's step session.", Now.AddHours(7))).Moved);
        Assert.True((await _a.BindAsync(step.Id, Graph, Now.AddHours(7))).Moved);
        Assert.Equal(next.Id, (await _a.SubmitAsync(step.Id, "Verified anew.", [], Now.AddHours(8))).FollowUp!.Id);
        await SyncAsync(_a);
        Assert.Empty((await SyncAsync(_b)).Refused);
        await SyncAsync(_a);

        Assert.Equal((QuestClaim.Lost, true), (await _b.ClaimAsync(next.Id), await _b.LostToTheReopenAsync(next.Id)));
        foreach (var store in new[] { _a, _b, _remote })
        {
            var open = (await store.FindAsync(next.Id))!;
            Assert.Equal(QuestStatus.Open, open.Status);
            Assert.Equal([(_b.Machine, QuestStatus.Taken, gone.Note)], open.Conflicts.Select(c => (c.Machine, c.Attempted, c.Note)));
            Assert.True(Assert.Single(await store.HistoryAsync(next.Id), o => o.Kind == QuestOperationKind.Conflict).LostToTheReopen);
        }

        var done = await _b.MoveAsync(next.Id, QuestStatus.Done, "Published.", Now.AddHours(9), byAgent: true);
        Assert.Equal((false, true), (done.Moved, done.ClaimLost));
    }

    /// <summary>
    /// A step whose source never leaves its machine (the source's receiver is local, the step's shared) goes up as it did: the remote holds
    /// nothing of the source, so it waits on no submission of it, and takes the step in the first push.
    /// </summary>
    [Fact]
    public async Task A_step_whose_source_never_leaves_its_machine_still_goes_up_at_once()
    {
        var quest = await _a.PublishAsync("Asker", "Local", "Close in a graph", "why", Now,
            then: [new QuestStep("Federated", "Verify {parent}", "Look at it.")]);
        Assert.True((await _a.MoveAsync(quest.Id, QuestStatus.Taken, "A's session.", Now.AddHours(1))).Moved);
        Assert.True((await _a.BindAsync(quest.Id, Graph, Now.AddHours(1))).Moved);
        var built = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2));
        var grewFrom = new QuestOperationRef(built.Quest!.Submission!.Machine, built.Quest.Submission.Sequence);

        var report = await QuestSync.RunAsync(_a, to => to == "Federated", new StoreRemote(_remote), Workspaces.Default);

        Assert.Equal((1, 0, 0), (report.Pushed, report.Behind.Count, report.Refused.Count));
        Assert.Null(await _remote.FindAsync(quest.Id));
        Assert.Equal(grewFrom, (await _remote.FindAsync(built.FollowUp!.Id))!.GrewFrom);
    }
}
