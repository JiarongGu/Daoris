using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A memory operation's author names its door by <c>door</c> and the version its spawn observed by <c>door_version</c>:
/// a log whose table spells them <c>adapter</c> and <c>harness</c> has both renamed as it opens, every row kept.
/// </summary>
public sealed class MemoryAuthorColumnTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_log_spelling_the_old_columns_reads_its_authors_door_and_version_by_the_new_ones()
    {
        var at = DateTimeOffset.Parse("2026-10-09T10:00:00Z");
        var memory = await MemoryOpStore.OpenAsync(_connection);
        var op = await memory.StageAsync(
            "Owner", new MemoryChange(MemoryOpKind.End, Text: "stale", Target: "e", Revision: 1),
            new MemoryAuthor("s-1", "default", null, "claude-code", "2.1.0", "builder", "r3", at));
        foreach (var rename in new[] { "door TO adapter", "door_version TO harness" })
        {
            await using var age = _connection.CreateCommand();
            age.CommandText = $"ALTER TABLE memory_ops RENAME COLUMN {rename}";
            await age.ExecuteNonQueryAsync();
        }

        var reopened = await MemoryOpStore.OpenAsync(_connection);
        await MemoryOpStore.OpenAsync(_connection);

        var by = (await reopened.FindAsync(op.Id))!.By!;
        Assert.Equal(("claude-code", "2.1.0"), (by.Door, by.DoorVersion));
        await using var probe = _connection.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('memory_ops') WHERE name IN ('adapter', 'harness')";
        Assert.Equal(0L, (long)(await probe.ExecuteScalarAsync())!);
    }
}
