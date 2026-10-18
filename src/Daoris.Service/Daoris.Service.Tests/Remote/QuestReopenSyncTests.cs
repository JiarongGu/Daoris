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
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T10:00:00Z");
    private const string Graph = "sha256:0f1e2d3c4b5a6978";

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

        // B's take lost: its claim reads lost, which the driver stops its session by, and the pass names the take.
        Assert.Equal(QuestClaim.Lost, await _b.ClaimAsync(step.Id));
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
}
