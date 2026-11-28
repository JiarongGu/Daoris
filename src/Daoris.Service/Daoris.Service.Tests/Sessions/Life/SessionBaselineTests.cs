using System.Security.Cryptography;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The baseline a store is measured by before the trial: its sessions not ended and their ages, those shown in progress with no
/// process, those not moved for two hours, those the fold says other than the status (a record older than its journal folded from
/// the one fact its status says), the census, the fold's time for a list and an append's; read only, so it runs against a copy of an
/// installed store and leaves the file untouched.
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

    private static async Task<string> MovedAsync(SessionStore sessions, SessionState state, TimeSpan ago, string? noteParts = null)
    {
        var opened = await sessions.CreateAsync("q", "Owner", "stub", Now.AddHours(-6), witness: WitnessDoor.QuestOpen);
        await sessions.SetStateAsync(opened.Id, state, noteParts is null ? null : "waits", null, null, Now - ago, witness: WitnessDoor.Advance, noteParts: noteParts);
        return opened.Id;
    }

    private Task<string> MovedAsync(SessionState state, TimeSpan ago, string? noteParts = null) => MovedAsync(_sessions, state, ago, noteParts);

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task A_store_reads_its_live_sessions_the_stuck_ones_the_census_and_where_the_fold_differs()
    {
        await _sessions.Generations.BeginAsync(Now.AddHours(-1), Now.AddHours(-1));
        var alive = await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        var survived = await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await MovedAsync(SessionState.AwaitingStep, TimeSpan.FromHours(3), """[{"code":"waits.check","values":{}}]""");
        await MovedAsync(SessionState.AwaitingStep, TimeSpan.FromHours(3));
        await MovedAsync(SessionState.AwaitingPerson, TimeSpan.FromMinutes(5));
        var ended = await MovedAsync(SessionState.Completed, TimeSpan.FromHours(4));
        // A record from before the journal: its fact is the one its stored status says.
        var older = await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(20));
        await ExecuteAsync($"DELETE FROM session_facts WHERE session = '{older}'");
        // A state written past the journal's door, which the census finds.
        await ExecuteAsync($"UPDATE sessions SET state = 'Stopped' WHERE id = '{ended}'");

        var reading = await SessionBaseline.ReadAsync(_connection, [new SessionLease(alive, 1, true), new SessionLease(survived, 0, false)], Now, count: 50, appends: 5);

        Assert.Equal((7, 6), (reading.Records, reading.NotEnded));
        Assert.Equal((3, 2, 1), (reading.ByState["working"], reading.ByState["awaiting-step"], reading.ByState["awaiting-person"]));
        Assert.Equal((2, 1), reading.Leases);
        Assert.All(reading.Ages, age => Assert.Equal(TimeSpan.FromHours(6), age));
        Assert.Equal(2, reading.InProgressNoProcess);
        Assert.Equal(2, reading.StillOverTwoHours);
        Assert.Equal(1, reading.Seeded);
        // Shown working on a void lease, working with none (seeded), and awaiting a step that names nothing.
        Assert.Equal((3, 1), (reading.Differ, reading.DifferSeeded));
        // The opened rows of the forgotten record and the move past the door.
        Assert.Equal(3, reading.Unwitnessed);
        Assert.Equal(7, reading.Folded);
        Assert.Equal(5, reading.Appended);
        Assert.NotNull(reading.Append);
        Assert.NotNull(reading.MoveSpan);
        Assert.Contains(reading.Lines(), line => line.StartsWith("differ: 3", StringComparison.Ordinal));
        Assert.Contains(reading.Lines(), line => line.StartsWith("state moves no fact witnesses: 3", StringComparison.Ordinal));
        Assert.Contains(reading.Lines(), line => line.StartsWith("an append: median", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_store_from_before_the_journal_folds_every_record_from_its_status()
    {
        await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await ExecuteAsync("DROP TABLE session_facts; DROP TABLE app_generations;");

        var reading = await SessionBaseline.ReadAsync(_connection, [], Now, count: 50, appends: 0);

        Assert.Equal((1, 1, 1, 0), (reading.NotEnded, reading.Seeded, reading.Differ, reading.Unwitnessed));
    }

    [Fact]
    public async Task A_store_with_a_journal_and_no_generations_reads_with_none()
    {
        await MovedAsync(SessionState.Working, TimeSpan.FromMinutes(10));
        await ExecuteAsync("DROP TABLE app_generations;");

        var reading = await SessionBaseline.ReadAsync(_connection, [], Now, count: 50, appends: 0);

        Assert.Equal((1, 0, 1), (reading.Journalled, reading.Seeded, reading.Differ));
        Assert.Equal(TimeSpan.Zero, reading.Buckets[TimeBucket.Down]);
    }

    [Fact]
    public async Task A_store_read_from_its_file_is_opened_read_only_and_left_as_it_was()
    {
        var folder = Path.Combine(Path.GetTempPath(), "baseline-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "knowledge.db");
        try
        {
            await using (var writing = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString()))
            {
                await writing.OpenAsync();
                var sessions = await SessionStore.OpenAsync(writing);
                await MovedAsync(sessions, SessionState.Working, TimeSpan.FromMinutes(10));
            }

            var before = SHA256.HashData(await File.ReadAllBytesAsync(file));
            await using (var store = await SessionBaseline.OpenReadOnlyAsync(file))
            {
                // Its append is timed on a copy in memory: the file's own bytes prove it is never written.
                var reading = await SessionBaseline.ReadAsync(store, [], Now, count: 50, appends: 2);
                Assert.Equal(1, reading.NotEnded);
                await using var write = store.CreateCommand();
                write.CommandText = "DELETE FROM sessions";
                Assert.Throws<SqliteException>(() => write.ExecuteNonQuery());
            }

            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(file)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
