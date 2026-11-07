using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The session record names its door by <c>door</c> and the version its spawn observed by <c>door_version</c>: a store
/// whose table still spells them <c>adapter</c> and <c>harness_version</c> has both renamed as it opens, every record kept, once.
/// </summary>
public sealed class SessionDoorColumnTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await using var old = _connection.CreateCommand();
        old.CommandText = """
            CREATE TABLE sessions (
              id TEXT PRIMARY KEY, quest TEXT NULL, repository TEXT NOT NULL, adapter TEXT NOT NULL,
              state TEXT NOT NULL, note TEXT NULL, evidence TEXT NULL, transcript TEXT NULL,
              created TEXT NOT NULL, updated TEXT NOT NULL, workspace TEXT NOT NULL DEFAULT 'default',
              kind TEXT NOT NULL DEFAULT 'Driven', harness_version TEXT NULL, profile TEXT NULL,
              tree TEXT NULL, base_commit TEXT NULL
            );
            INSERT INTO sessions (id, quest, repository, adapter, state, harness_version, created, updated)
              VALUES ('abcd1234', 'q1', 'Shared', 'claude-code', 'Completed', 'claude 2.1.0', '2026-09-20T10:00:00Z', '2026-09-20T10:05:00Z');
            """;
        await old.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_store_spelling_the_old_columns_reads_its_records_door_and_version_by_the_new_ones()
    {
        var sessions = await SessionStore.OpenAsync(_connection);

        var record = await sessions.FindAsync("abcd1234");
        Assert.Equal("claude-code", record!.Door);
        Assert.Equal("claude 2.1.0", record.DoorVersion);
        Assert.Equal(["door", "door_version"], await ColumnsAsync("door", "door_version", "adapter", "harness_version"));
    }

    [Fact]
    public async Task The_rename_runs_once_and_a_fresh_store_names_no_old_column()
    {
        await SessionStore.OpenAsync(_connection);
        var second = await SessionStore.OpenAsync(_connection);
        Assert.Equal("claude-code", (await second.FindAsync("abcd1234"))!.Door);

        await using var fresh = new SqliteConnection("Data Source=:memory:");
        await fresh.OpenAsync();
        var made = await SessionStore.OpenAsync(fresh);
        var chat = await made.CreateAsync(null, "Shared", "codex", DateTimeOffset.Parse("2026-09-20T10:00:00Z"), kind: SessionKind.Chat, doorVersion: "codex 0.9");
        Assert.Equal(("codex", "codex 0.9"), ((await made.FindAsync(chat.Id))!.Door, (await made.FindAsync(chat.Id))!.DoorVersion));
        await using var probe = fresh.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name IN ('adapter', 'harness_version')";
        Assert.Equal(0L, (long)(await probe.ExecuteScalarAsync())!);
    }

    private async Task<List<string>> ColumnsAsync(params string[] names)
    {
        await using var probe = _connection.CreateCommand();
        probe.CommandText = $"SELECT name FROM pragma_table_info('sessions') WHERE name IN ({string.Join(", ", names.Select(name => $"'{name}'"))}) ORDER BY name";
        var found = new List<string>();
        await using var reader = await probe.ExecuteReaderAsync();
        while (await reader.ReadAsync()) found.Add(reader.GetString(0));
        return found;
    }
}
