using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The judgement half of the session system, in ONE place for the same reason `QuestExchange` is:
/// the driver and the HTTP host both move sessions, and two copies of "what may move where" would
/// drift into a session that is finished through one door and still active through the other.
///
/// The deeper rule under test: the ledger never writes QUEST state. The spawned session claims its
/// own quest through its own connector (D46), so driven and outside work stay indistinguishable at
/// the quest layer — the ledger only guards the records the driver acts on.
/// </summary>
public sealed class SessionLedgerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(_quests, _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string to = "Owner", string title = "Do the thing") =>
        _quests.PublishAsync("Asker", to, title, "Here is why.", Now);

    [Fact]
    public async Task Opening_a_session_for_an_open_quest_queues_it()
    {
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
        Assert.Equal(SessionState.Queued, outcome.Session!.State);
        Assert.Equal("Owner", outcome.Session.Repository);
        Assert.Contains(outcome.Session.Id, outcome.Message);
    }

    [Fact]
    public async Task An_unknown_quest_is_refused()
    {
        var outcome = await _ledger.OpenAsync("zzzzzz", "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotFound, outcome.Refusal);
        Assert.Null(outcome.Session);
    }

    /// <summary>A printed id is pasted back with its `#`, so the ledger must accept one.</summary>
    [Fact]
    public async Task A_hash_prefixed_quest_id_works()
    {
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync($"#{quest.Id}", "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    /// <summary>
    /// `Taken` is the mutex between the driver and outside work: a quest an interactive session has
    /// already claimed is not the driver's to start, and the refusal must say who has it.
    /// </summary>
    [Fact]
    public async Task A_quest_already_taken_is_refused()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, outcome.Refusal);
        Assert.Contains("Taken", outcome.Message);
    }

    [Fact]
    public async Task A_closed_quest_is_refused()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "landed", Now);

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, outcome.Refusal);
    }

    /// <summary>One session per repository: the working tree is the unit of exclusion (D46).</summary>
    [Fact]
    public async Task A_busy_repository_refuses_a_second_session_and_names_the_first()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        var opened = await _ledger.OpenAsync(first.Id, "stub", Now);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
        Assert.Contains(opened.Session!.Id, outcome.Message);
    }

    [Fact]
    public async Task A_repository_frees_up_when_its_session_finishes()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        var opened = await _ledger.OpenAsync(first.Id, "stub", Now);
        await _ledger.AdvanceAsync(opened.Session!.Id, "stood-down", null, null, null, Now);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    /// <summary>Two repositories working at once is the point of the whole direction.</summary>
    [Fact]
    public async Task Different_repositories_run_concurrently()
    {
        var here = await Publish();
        var there = await Publish(to: "Elsewhere");

        await _ledger.OpenAsync(here.Id, "stub", Now);
        var outcome = await _ledger.OpenAsync(there.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    [Fact]
    public async Task The_ledger_never_touches_the_quest()
    {
        var quest = await Publish();

        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await _ledger.AdvanceAsync(opened.Session!.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(opened.Session.Id, "failed", "exit 2", null, null, Now);

        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync(quest.Id))!.Status);
    }

    [Fact]
    public async Task The_ordinary_life_advances_cleanly()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        var id = opened.Session!.Id;

        Assert.Equal(SessionState.Starting, (await Advance(id, "starting")).Session!.State);
        Assert.Equal(SessionState.Working, (await Advance(id, "working")).Session!.State);
        var done = await _ledger.AdvanceAsync(id, "completed", null, "gates: green; 2 commits", null, Now.AddHours(1));
        Assert.Equal(SessionState.Completed, done.Session!.State);
        Assert.Equal("gates: green; 2 commits", done.Session.Evidence);
    }

    /// <summary>The state names travel over HTTP in the design's spelling, not the enum's.</summary>
    [Fact]
    public async Task Kebab_case_state_names_are_understood()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");
        await Advance(opened.Session.Id, "working");

        var outcome = await Advance(opened.Session.Id, "awaiting-person");

        Assert.Equal(SessionAdvanceRefusal.None, outcome.Refusal);
        Assert.Equal(SessionState.AwaitingPerson, outcome.Session!.State);
    }

    [Fact]
    public async Task An_unknown_state_is_refused_and_lists_the_real_ones()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await Advance(opened.Session!.Id, "paused");

        Assert.Equal(SessionAdvanceRefusal.UnknownState, outcome.Refusal);
        Assert.Contains("awaiting-person", outcome.Message);
    }

    [Fact]
    public async Task An_unknown_session_is_not_found()
    {
        var outcome = await Advance("zzzzzzzz", "working");

        Assert.Equal(SessionAdvanceRefusal.NotFound, outcome.Refusal);
    }

    /// <summary>A finished session is a record, and records do not move.</summary>
    [Fact]
    public async Task A_terminal_session_refuses_to_move()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "stopped");

        var outcome = await Advance(opened.Session.Id, "working");

        Assert.Equal(SessionAdvanceRefusal.Terminal, outcome.Refusal);
    }

    /// <summary>Forward only: a session that skips the states its meaning depends on is lying.</summary>
    [Fact]
    public async Task An_illegal_move_is_refused_and_names_what_is_allowed()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await Advance(opened.Session!.Id, "completed");

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, outcome.Refusal);
        Assert.Contains("starting", outcome.Message);
    }

    /// <summary>Nothing advances TO queued — it is where a session begins, never where it returns.</summary>
    [Fact]
    public async Task Nothing_moves_back_to_queued()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");

        var outcome = await Advance(opened.Session.Id, "queued");

        Assert.NotEqual(SessionAdvanceRefusal.None, outcome.Refusal);
    }

    /// <summary>The person clears a parked session; a resume-capable adapter may put it back to work.</summary>
    [Fact]
    public async Task A_parked_session_can_resume_or_close()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");
        await Advance(opened.Session.Id, "working");
        await Advance(opened.Session.Id, "awaiting-person");

        var resumed = await Advance(opened.Session.Id, "working");

        Assert.Equal(SessionAdvanceRefusal.None, resumed.Refusal);
    }

    private Task<SessionAdvanceOutcome> Advance(string id, string state) =>
        _ledger.AdvanceAsync(id, state, null, null, null, Now);
}
