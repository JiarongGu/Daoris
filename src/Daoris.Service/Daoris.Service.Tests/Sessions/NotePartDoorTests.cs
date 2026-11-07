using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A session note's parts name a door by <c>door</c> (AGENTPROVIDER1t-ii): a part kept with <c>adapter</c> among its values, or the reason
/// <c>adapter</c>, has both renamed as the store opens, a part that names neither is left as kept, and a second open changes nothing.
/// </summary>
public sealed class NotePartDoorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_note_kept_with_the_older_names_reads_them_as_the_door_and_the_rename_runs_once()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var made = await sessions.CreateAsync(null, "Shared", "claude-code", DateTimeOffset.Parse("2026-09-20T10:00:00Z"), kind: SessionKind.Chat);
        const string Older = """[{"code":"resumed.opening","values":{"adapter":"codex","why":"adapter","from":"a"}},{"code":"resumed.native","values":{"adapter":"codex"}},{"words":"kept","by":"before"}]""";
        await using (var age = _connection.CreateCommand())
        {
            age.CommandText = "UPDATE sessions SET note_parts = $parts WHERE id = $id";
            age.Parameters.AddWithValue("$parts", Older);
            age.Parameters.AddWithValue("$id", made.Id);
            await age.ExecuteNonQueryAsync();
        }

        await SessionStore.OpenAsync(_connection);
        await SessionStore.OpenAsync(_connection);

        await using var probe = _connection.CreateCommand();
        probe.CommandText = "SELECT note_parts FROM sessions WHERE id = $id";
        probe.Parameters.AddWithValue("$id", made.Id);
        Assert.Equal(
            """[{"code":"resumed.opening","values":{"why":"door","from":"a","door":"codex"}},{"code":"resumed.native","values":{"door":"codex"}},{"words":"kept","by":"before"}]""",
            (string)(await probe.ExecuteScalarAsync())!);
    }
}
