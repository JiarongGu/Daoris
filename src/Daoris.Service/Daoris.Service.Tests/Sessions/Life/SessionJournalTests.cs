using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The journal in the store: every write of the stored status appends its fact with its door, its reason code and the generation its
/// writer names, in the write's own transaction; a row this build cannot read is refused; and the tick's audit folds it beside the
/// status, saying each difference once.
/// </summary>
public sealed class SessionJournalTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<string> WorkingAsync(string quest, long writer, DateTimeOffset at)
    {
        using var writing = FactWriter.As(writer);
        var opened = await _sessions.CreateAsync(quest, "Owner", "stub", at, witness: WitnessDoor.QuestOpen);
        await _sessions.SetStateAsync(opened.Id, SessionState.Working, null, null, null, at, witness: WitnessDoor.Advance);
        return opened.Id;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Each_write_appends_its_fact_with_its_door_reason_and_the_generation_its_writer_names()
    {
        var opened = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: WitnessDoor.QuestOpen, node: "build", attempt: 2);
        using (FactWriter.As(7))
        {
            await _sessions.SetStateAsync(opened.Id, SessionState.Working, null, null, null, Now.AddMinutes(1), witness: WitnessDoor.Advance);
            await _sessions.SetStateAsync(
                opened.Id, SessionState.AwaitingStep, "waits", null, null, Now.AddMinutes(2), witness: WitnessDoor.Advance,
                noteParts: """[{"code":"waits.step","values":{"run":"r9","node":"check","attempt":2}}]""");
            // A move that keeps its state mints no state row of event_journal, and names none.
            await _sessions.SetStateAsync(opened.Id, SessionState.AwaitingStep, null, null, null, Now.AddMinutes(3), witness: WitnessDoor.Advance);
        }

        await _sessions.SetStateAsync(opened.Id, SessionState.Completed, null, null, null, Now.AddMinutes(4));

        var journal = await _sessions.Journal.OfAsync(opened.Id);

        Assert.Equal(
            [SessionFactKind.Opened, SessionFactKind.TurnBegan, SessionFactKind.StepAwaited, SessionFactKind.StepAwaited, SessionFactKind.Ended],
            journal.Select(fact => fact.Kind));
        Assert.Equal(
            [new FactWitness(WitnessDoor.QuestOpen), new(WitnessDoor.Advance), new(WitnessDoor.Advance, "waits.step"), new(WitnessDoor.Advance), new(WitnessDoor.Store)],
            journal.Select(fact => fact.Witness));
        Assert.Equal([0L, 7, 7, 7, 0], journal.Select(fact => fact.Generation));
        Assert.Equal(("build", 2, SessionHolder.MachineRoom), (journal[0].Step, journal[0].Attempt, journal[0].Holder));
        Assert.Equal((new SessionHolder(HolderKind.Step, "r9"), "check", 2), (journal[2].Holder, journal[2].Step, journal[2].Attempt));
        Assert.Equal(SessionOutcome.Done, journal[4].Outcome);
        Assert.Equal([true, true, true, false, true], journal.Select(fact => fact.Minted is not null));
        Assert.Empty(await _sessions.Journal.UnwitnessedAsync());
    }

    [Fact]
    public async Task Two_writers_on_one_home_each_name_their_own_generation_and_a_survivor_of_a_restart_keeps_its_earlier_one()
    {
        var first = await _sessions.Generations.BeginAsync(Now.AddHours(-2), Now.AddHours(-2));
        // The holder's driver and a terminal's driver beside it write in the one generation; neither begins one.
        var holders = await WorkingAsync("q1", first.Number, Now.AddHours(-1));
        var besides = await WorkingAsync("q2", first.Number, Now.AddHours(-1));
        var second = await _sessions.Generations.BeginAsync(Now.AddMinutes(-20), Now.AddMinutes(-20));

        // A process the first start left running writes after the restart, in its own generation.
        using (FactWriter.As(first.Number))
        {
            await _sessions.SetStateAsync(besides, SessionState.AwaitingPerson, "asks", null, null, Now.AddMinutes(-5), witness: WitnessDoor.Advance);
        }

        Assert.All(await _sessions.Journal.OfAsync(holders), fact => Assert.Equal(first.Number, fact.Generation));
        var after = (await _sessions.Journal.OfAsync(besides))[^1];
        Assert.Equal((SessionFactKind.QuestionOpened, first.Number), (after.Kind, after.Generation));
        Assert.Equal(second.Number, await _sessions.Generations.CurrentAsync());
    }

    [Fact]
    public async Task The_audit_voids_a_survivor_s_lease_keeps_one_whose_driver_runs_and_says_each_difference_once()
    {
        var first = await _sessions.Generations.BeginAsync(Now.AddHours(-2), Now.AddHours(-2));
        var survived = await WorkingAsync("q1", first.Number, Now.AddHours(-1));
        var beside = await WorkingAsync("q2", first.Number, Now.AddHours(-1));
        var unheld = await WorkingAsync("q3", first.Number, Now.AddHours(-1));
        await _sessions.SetStateAsync(unheld, SessionState.AwaitingStep, "rewritten", null, null, Now.AddMinutes(-30), witness: WitnessDoor.Advance);
        var second = await _sessions.Generations.BeginAsync(Now.AddMinutes(-20), Now.AddMinutes(-20));
        var started = await WorkingAsync("q4", second.Number, Now.AddMinutes(-10));
        var said = new List<(ShadowDifference Difference, IReadOnlyList<SessionFact> Journal)>();
        var shadow = new SessionShadow(_sessions, new FixedClock(Now)) { Said = (difference, journal) => said.Add((difference, journal)) };
        // The first start's process outlived its driver; a terminal's driver beside the holder still runs and owns its process.
        IReadOnlyList<SessionLease> leases = [new(survived, first.Number, false), new(beside, first.Number, true), new(started, second.Number, true)];

        Assert.Same(ShadowReading.None, shadow.Last);
        var reading = await shadow.AuditAsync(leases, second.Number);
        await shadow.AuditAsync(leases, second.Number);

        Assert.Equal((second.Number, 4), (reading.Generation, reading.Folded));
        Assert.Equal(new[] { survived, unheld }.Order(StringComparer.Ordinal), reading.Differences.Select(each => each.Session).Order(StringComparer.Ordinal));
        var stale = reading.Differences.Single(each => each.Session == survived);
        Assert.Equal((SessionState.Working, BreachCode.TurnOnVoidLease), (stale.Stored, stale.Fold.Breach));
        Assert.Equal(BreachCode.WaitWithoutHolder, reading.Differences.Single(each => each.Session == unheld).Fold.Breach);
        // Said once each, the first line with the session's whole journal; the list's reading carries the last fact alone.
        Assert.Equal(2, said.Count);
        Assert.Equal(3, said.Single(each => each.Difference.Session == unheld).Journal.Count);
        Assert.Equal(SessionFactKind.StepAwaited, reading.Differences.Single(each => each.Session == unheld).Last.Kind);
        Assert.NotNull((await _sessions.Generations.AllAsync()).Single(generation => generation.Number == second.Number).Seen);
        Assert.Equal(reading.Differences.Count, shadow.Last.Differences.Count);
    }

    [Fact]
    public async Task What_the_audit_remembers_is_bounded_by_the_sessions_that_differ_and_a_changed_difference_is_said_again()
    {
        var gone = await WorkingAsync("q1", 0, Now.AddHours(-1));
        var said = new List<string>();
        var shadow = new SessionShadow(_sessions, new FixedClock(Now)) { Said = (difference, _) => said.Add(difference.Session) };

        await shadow.AuditAsync([], 0);
        await _sessions.SetStateAsync(gone, SessionState.Stopped, "stopped", null, null, Now, witness: WitnessDoor.Advance);
        await shadow.AuditAsync([], 0);
        await _sessions.SetStateAsync(gone, SessionState.Working, "again", null, null, Now, witness: WitnessDoor.GoOn, forgive: true);
        await shadow.AuditAsync([], 0);

        Assert.Equal([gone, gone], said);
    }

    [Fact]
    public async Task A_row_of_an_unknown_kind_or_an_unreadable_time_is_refused_said_once_never_skipped()
    {
        var odd = await WorkingAsync("q1", 0, Now.AddHours(-1));
        var late = await WorkingAsync("q2", 0, Now.AddHours(-1));
        await ExecuteAsync($"UPDATE session_facts SET kind = 'a-later-kind' WHERE session = '{odd}' AND kind = 'turn-began'");
        await ExecuteAsync($"UPDATE session_facts SET at = 'not a time' WHERE session = '{late}' AND kind = 'opened'");
        var refused = new List<UnreadableFact>();
        var shadow = new SessionShadow(_sessions, new FixedClock(Now)) { Refused = refused.Add };

        var reading = await shadow.AuditAsync([], 0);
        await shadow.AuditAsync([], 0);

        Assert.Equal(odd, Assert.Single(reading.Unreadable).Session);
        Assert.Equal(("kind", "a-later-kind"), (refused.Single().Column, refused.Single().Value));
        Assert.DoesNotContain(reading.Differences, each => each.Session == odd);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _sessions.Journal.OfAsync(late));
        Assert.Contains("`at`", error.Message);
        var read = await _sessions.Journal.OfManyAsync([odd, late]);
        Assert.Empty(read.Journals);
        Assert.Equal(2, read.Unreadable.Count);
    }

    [Fact]
    public async Task A_write_inside_a_transaction_the_caller_holds_joins_it_so_the_fact_and_the_move_go_together()
    {
        var opened = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: WitnessDoor.QuestOpen);

        await using (var outer = _connection.BeginTransaction())
        {
            await _sessions.SetStateAsync(opened.Id, SessionState.Working, null, null, null, Now, witness: WitnessDoor.Advance);
            await outer.RollbackAsync();
        }

        Assert.Equal(SessionState.Queued, (await _sessions.FindAsync(opened.Id))!.State);
        Assert.Single(await _sessions.Journal.OfAsync(opened.Id));
    }

    [Fact]
    public async Task A_mirror_of_another_machines_record_appends_nothing()
    {
        var own = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: WitnessDoor.QuestOpen);
        var read = (await _sessions.FindAsync(own.Id))!;

        await _sessions.MirrorAsync(read with { Id = "other/" + own.Id, Origin = "other", State = SessionState.Working });

        Assert.Empty(await _sessions.Journal.OfAsync("other/" + own.Id));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
