using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The shape of a day's development work replayed on the ledger: a quest worked in a run, its agent's done a submission that leaves it
/// taken, the check step's red going back to the same open session as Daoris's word, the fix submitted again, and the quest's Done written
/// with the session's end at the run's end; no check is asked of a session and nothing waits on the person.
/// </summary>
public sealed class RunReplayTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-14T09:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private QuestExchange _exchange = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(_quests, _sessions, service);
        _exchange = new QuestExchange(service, _quests, sessions: _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<Session> Moved(string id, string state, int minutes, string? note = null)
    {
        var moved = await _ledger.AdvanceAsync(id, state, note, null, null, Now.AddMinutes(minutes));
        Assert.Equal(SessionAdvanceRefusal.None, moved.Refusal);
        return moved.Session!;
    }

    private async Task<IReadOnlyList<string>> StatesOf(string id)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT state_new FROM event_journal WHERE record = $id AND change = 'state' ORDER BY position";
        command.Parameters.AddWithValue("$id", id);
        var states = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) states.Add(reader.GetString(0));
        return states;
    }

    [Fact]
    public async Task The_agents_done_is_the_one_verb_of_a_submission_and_ready_is_no_verb()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Mend the bridge", "It drops a frame.", Now);
        await _exchange.RespondAsync(quest.Id, "take", null, Now);
        Assert.True((await _quests.BindAsync(quest.Id, "q-run", Now)).Moved);

        var ready = await _exchange.RespondAsync(quest.Id, "ready", "Built.", Now);

        Assert.Equal(QuestRespondRefusal.UnknownAction, ready.Refusal);
        Assert.Equal("Unknown action 'ready' — one of: take, done, decline, wait.", ready.Message);
        Assert.Null((await _quests.FindAsync(quest.Id))!.Submission);
        Assert.Equal(QuestStatus.Taken, (await _exchange.RespondAsync(quest.Id, "done", "Built.", Now)).Quest!.Status);
    }

    [Fact]
    public async Task A_quest_worked_in_a_run_is_submitted_sent_back_by_a_red_check_and_done_only_at_the_runs_end()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Mend the bridge", "It drops a frame.", Now);
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now, tree: "/trees/s-1")).Session!;
        await Moved(session.Id, "starting", 1);
        await Moved(session.Id, "working", 1);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(quest.Id, "take", null, Now.AddMinutes(1), session: session.Id)).Refusal);
        Assert.True((await _quests.BindAsync(quest.Id, "q-run", Now.AddMinutes(1))).Moved);

        // The agent's done is the submission: the quest stays taken, and the session waits on the run's next step.
        var first = await _exchange.RespondAsync(quest.Id, "done", "Built.", Now.AddMinutes(20), session: session.Id);
        Assert.Equal((QuestRespondRefusal.None, QuestStatus.Taken), (first.Refusal, first.Quest!.Status));
        await Moved(session.Id, "awaiting-step", 20, "It waits on its workflow's next step (run `q-run`, after `check`).");

        // The check's red goes back to the same open session as Daoris's word, which wakes it; a bare go-on would not.
        await _ledger.SayAsync(session.Id, "Daoris: `check` failed at `a1b2c3d`: `suite` is red.", null, Now.AddMinutes(30),
            from: SaidFrom.Daoris, key: "changes/q-run/check/1");
        await Moved(session.Id, "working", 31);

        // Its fix is submitted again, replacing the first, and it waits again; the green check says nothing to it.
        var second = await _exchange.RespondAsync(quest.Id, "done", "Fixed the dropped frame.", Now.AddMinutes(50), session: session.Id);
        Assert.Equal(QuestStatus.Taken, second.Quest!.Status);
        Assert.True(second.Quest.Submission!.Sequence > first.Quest.Submission!.Sequence);
        await Moved(session.Id, "awaiting-step", 50, "It waits on its workflow's next step (run `q-run`, after `check`).");
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);

        // The opinion, the look and the landing pass; the run's end writes the Done on the last submission with the session's end.
        var submission = new QuestOperationRef(second.Quest.Submission.Machine, second.Quest.Submission.Sequence);
        var ended = await _ledger.EndRunAsync(
            "q-run", [(quest.Id, submission)], [session.Id], "completed", "Its workflow's run `q-run` came to its end.", null, Now.AddMinutes(90));

        Assert.Equal(QuestCloseRefusal.None, ended.Refusal);
        var closed = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal((QuestStatus.Done, "Fixed the dropped frame.", false), (closed.Status, closed.Note, closed.Held || closed.Left));
        Assert.Equal(SessionState.Completed, (await _sessions.FindAsync(session.Id))!.State);
        Assert.Equal(["Starting", "Working", "AwaitingStep", "Working", "AwaitingStep", "Completed"], await StatesOf(session.Id));
        Assert.DoesNotContain(nameof(SessionState.AwaitingPerson), await StatesOf(session.Id));
    }
}
