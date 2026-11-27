using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The baseline a store is measured by before the trial: its sessions not ended and their ages, those shown in progress with no
/// process, those not moved for two hours, those the fold says other than the status (a record older than its journal folded from
/// the one fact its status says), and the fold's time for a list; read only, so it runs against a copy of an installed store.
/// </summary>
public sealed class SessionBaselineTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-28T12:00:00Z");
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<string> MovedAsync(SessionState state, TimeSpan ago, string? noteParts = null)
    {
        var opened = await _sessions.CreateAsync("q", "Owner", "stub", Now.AddHours(-6), witness: "quest-open");
        await _sessions.SetStateAsync(opened.Id, state, noteParts is null ? null : "waits", null, null, Now - ago, witness: "advance", noteParts: noteParts);
        return opened.Id;
    }

    [Fact]
    public async Task A_store_reads_its_live_sessions_the_stuck_ones_and_where_the_fold_differs()
    {
        await _sessions.Generations.BeginAsync(Now.AddHours(-1), Now.AddHours(-1));
        var alive = await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await MovedAsync(SessionState.AwaitingStep, TimeSpan.FromHours(3), """[{"code":"waits.check","values":{}}]""");
        await MovedAsync(SessionState.AwaitingStep, TimeSpan.FromHours(3));
        await MovedAsync(SessionState.AwaitingPerson, TimeSpan.FromMinutes(5));
        await MovedAsync(SessionState.Completed, TimeSpan.FromHours(4));
        // A record from before the journal: its fact is the one its stored status says.
        var older = await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(20));
        await using (var forget = _connection.CreateCommand())
        {
            forget.CommandText = "DELETE FROM session_facts WHERE session = $id";
            forget.Parameters.AddWithValue("$id", older);
            await forget.ExecuteNonQueryAsync();
        }

        var reading = await SessionBaseline.ReadAsync(_connection, [new ShadowLease(alive, Now.AddMinutes(-30))], Now, count: 50);

        Assert.Equal(7, reading.Records);
        Assert.Equal(6, reading.NotEnded);
        Assert.All(reading.Ages, age => Assert.Equal(TimeSpan.FromHours(6), age));
        Assert.Equal(2, reading.InProgressNoProcess);
        Assert.Equal(2, reading.StillOverTwoHours);
        Assert.Equal(1, reading.Seeded);
        // Shown working with no process twice (one of them seeded), and awaiting a step that names nothing once.
        Assert.Equal(3, reading.Differ);
        Assert.Equal(1, reading.DifferSeeded);
        Assert.Equal(7, reading.Folded);
        Assert.True(reading.FoldTook >= TimeSpan.Zero);
        Assert.Contains(reading.Lines(), line => line.StartsWith("differ: 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_store_from_before_the_journal_folds_every_record_from_its_status()
    {
        await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await using (var drop = _connection.CreateCommand())
        {
            drop.CommandText = "DROP TABLE session_facts; DROP TABLE app_generations;";
            await drop.ExecuteNonQueryAsync();
        }

        var reading = await SessionBaseline.ReadAsync(_connection, [], Now, count: 50);

        Assert.Equal(1, reading.NotEnded);
        Assert.Equal(1, reading.Seeded);
        Assert.Equal(1, reading.Differ);
    }
}
