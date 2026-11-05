using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The reads a list makes for each record it answers are index searches: a scan of every record there, once per record, is what
/// made a list of three thousand take seconds.
/// </summary>
public sealed class SessionIndexesTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await SessionStore.OpenAsync(_connection);
        await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Theory]
    [InlineData("SELECT * FROM sessions WHERE from_session = 'a' AND origin IS NULL ORDER BY created DESC, rowid DESC LIMIT 1")]
    [InlineData("SELECT id FROM sessions WHERE quest = 'q' AND origin IS NULL ORDER BY created DESC, rowid DESC LIMIT 1")]
    [InlineData("SELECT * FROM sessions WHERE took_quest = 'q' COLLATE NOCASE AND quest IS NULL ORDER BY origin IS NOT NULL, created, rowid LIMIT 1")]
    [InlineData("SELECT * FROM sessions WHERE id = 'a' OR (origin IS NOT NULL AND substr(id, instr(id, '/') + 1) = 'a') ORDER BY origin IS NOT NULL, revision LIMIT 1")]
    [InlineData("SELECT * FROM quests WHERE parent = 'q' ORDER BY filed")]
    public async Task A_read_a_list_makes_per_record_searches_an_index(string read)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + read;
        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.DoesNotContain(plan, step => step.StartsWith("SCAN sessions", StringComparison.Ordinal) || step.StartsWith("SCAN quests", StringComparison.Ordinal));
    }
}
