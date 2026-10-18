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
        var submitted = await _a.SubmitAsync(quest.Id, "Built.", [], Now.AddHours(2));
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
}
