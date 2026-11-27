using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The journal in the store: every move of the stored status appends its fact with its witness and generation, in the move's own
/// transaction, and the shadow folds it beside the status, saying each difference once.
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

    [Fact]
    public async Task Each_write_of_the_status_appends_its_fact_with_the_named_witness_and_generation()
    {
        var opened = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: "quest-open");
        var generation = await _sessions.Generations.BeginAsync(Now.AddMinutes(-5), Now);
        await _sessions.SetStateAsync(opened.Id, SessionState.Working, null, null, null, Now.AddMinutes(1), witness: "advance");
        await _sessions.SetStateAsync(
            opened.Id, SessionState.AwaitingStep, "waits", null, null, Now.AddMinutes(2), witness: "advance",
            noteParts: """[{"code":"waits.step","values":{"run":"r9","node":"check","attempt":2}}]""");
        await _sessions.SetStateAsync(opened.Id, SessionState.Completed, null, null, null, Now.AddMinutes(3));

        var journal = await _sessions.Journal.OfAsync(opened.Id);

        Assert.Equal(
            [SessionFactKind.Opened, SessionFactKind.TurnBegan, SessionFactKind.StepAwaited, SessionFactKind.Ended],
            journal.Select(fact => fact.Kind));
        Assert.Equal(["quest-open", "advance", "advance: waits.step", "store move"], journal.Select(fact => fact.Witness));
        Assert.Equal([0L, generation.Number, generation.Number, generation.Number], journal.Select(fact => fact.Generation));
        Assert.Equal(("run:r9", "check", 2), (journal[2].Holder, journal[2].Step, journal[2].Attempt));
        Assert.Equal(SessionOutcome.Done, journal[3].Outcome);
        Assert.True(journal.Select(fact => fact.Position).SequenceEqual(journal.Select(fact => fact.Position).Order()));
    }

    [Fact]
    public async Task A_mirror_of_another_machines_record_appends_nothing()
    {
        var own = await _sessions.CreateAsync("q", "Owner", "stub", Now);
        var read = (await _sessions.FindAsync(own.Id))!;

        await _sessions.MirrorAsync(read with { Id = "other/" + own.Id, Origin = "other", State = SessionState.Working });

        Assert.Empty(await _sessions.Journal.OfAsync("other/" + own.Id));
    }

    [Fact]
    public async Task The_shadow_says_where_the_fold_differs_once_and_voids_a_lease_of_an_earlier_start()
    {
        var first = await _sessions.Generations.BeginAsync(Now.AddHours(-2), Now.AddHours(-2));
        var working = await _sessions.CreateAsync("q", "Owner", "stub", Now.AddHours(-1), witness: "quest-open");
        await _sessions.SetStateAsync(working.Id, SessionState.Working, null, null, null, Now.AddHours(-1), witness: "advance");
        var waiting = await _sessions.CreateAsync("q2", "Owner", "stub", Now.AddHours(-1), witness: "quest-open");
        await _sessions.SetStateAsync(waiting.Id, SessionState.Working, null, null, null, Now.AddHours(-1), witness: "advance");
        await _sessions.SetStateAsync(waiting.Id, SessionState.AwaitingStep, "rewritten", null, null, Now.AddMinutes(-30), witness: "advance");
        var leased = await _sessions.CreateAsync("q3", "Owner", "stub", Now.AddHours(-1), witness: "quest-open");
        await _sessions.SetStateAsync(leased.Id, SessionState.Working, null, null, null, Now.AddMinutes(-10), witness: "advance");
        await _sessions.Generations.BeginAsync(Now.AddMinutes(-20), Now.AddMinutes(-20));
        var said = new List<ShadowDifference>();
        var shadow = new SessionShadow(_sessions, new FixedClock(Now)) { Said = said.Add };
        // The first's process started under the first start and survived the second; the third's started after it.
        IReadOnlyList<ShadowLease> leases = [new(working.Id, Now.AddMinutes(-50)), new(leased.Id, Now.AddMinutes(-10))];

        var reading = await shadow.ReadAsync(leases);
        await shadow.ReadAsync(leases);

        Assert.Equal(first.Number + 1, reading.Generation);
        Assert.Equal(3, reading.Folded);
        Assert.Equal(
            new[] { working.Id, waiting.Id }.Order(StringComparer.Ordinal),
            reading.Differences.Select(each => each.Session).Order(StringComparer.Ordinal));
        var stale = reading.Differences.Single(each => each.Session == working.Id);
        Assert.Equal((SessionState.Working, FoldedState.NeedsYou), (stale.Stored, stale.Fold.State));
        Assert.Contains($"generation {first.Number}", stale.Fold.Breach);
        var unheld = reading.Differences.Single(each => each.Session == waiting.Id);
        Assert.Equal((SessionState.AwaitingStep, FoldedState.NeedsYou), (unheld.Stored, unheld.Fold.State));
        Assert.Equal(3, unheld.Facts.Count);
        Assert.Equal(2, said.Count);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
