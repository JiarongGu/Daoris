using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1 row l-i (the foundation design §4.1, §4.5): the journal's rows answered to a reader by position, so a process that reads the
/// journal from its come-up learns what moved in order. Each case runs on a database file of its own under <c>_fixtures/</c>.
/// </summary>
public sealed class JournalRowsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-30T09:00:00Z");

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "journal-rows", Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_root, "service.db");

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        SqlitePools.Release(Database);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file the scanner holds for a moment: the next prune of _fixtures takes it.
        }
    }

    private async Task<Session> EndedAsync(string code)
    {
        var record = await _sessions.CreateAsync(null, "Owner", "stub", Now, workspace: "team", kind: SessionKind.Chat);
        await _sessions.SetStateAsync(record.Id, SessionState.Working, null, null, null, Now);
        await _sessions.SetStateAsync(
            record.Id, SessionState.Completed, "ended", null, null, Now,
            noteParts: $$"""[{"code":"{{code}}","values":{},"text":"private text"}]""");
        return record;
    }

    /// <summary>Rows after a position come in order with their snapshot, the store's identity and the floor beside them, and no text.</summary>
    [Fact]
    public async Task Rows_after_a_position_come_in_order_with_the_place_and_no_text()
    {
        var record = await EndedAsync("ended.done");

        var page = await JournalRows.ReadAsync(_connection, since: 0, limit: 100);

        Assert.Equal(["opened", "state", "state"], page.Rows.Select(row => row.Change));
        Assert.Equal(page.Rows.Select(row => row.Position).Order(), page.Rows.Select(row => row.Position));
        var ended = page.Rows[^1];
        Assert.Equal((record.Id, "sessions", "Working", "Completed", "ended.done"), (ended.Record, ended.Family, ended.StateOld, ended.StateNew, ended.NoteCodes));
        Assert.Equal((1L, ended.Position), (page.Floor, page.Newest));
        Assert.False(string.IsNullOrEmpty(page.Store));
        Assert.DoesNotContain("private text", System.Text.Json.JsonSerializer.Serialize(page));
    }

    /// <summary>A reader's cursor is exclusive, a limit holds the page, and a cursor at the newest position is answered no rows.</summary>
    [Fact]
    public async Task A_cursor_is_exclusive_and_a_limit_holds_the_page()
    {
        await EndedAsync("ended.done");
        var all = await JournalRows.ReadAsync(_connection, 0, 100);

        var second = await JournalRows.ReadAsync(_connection, all.Rows[0].Position, 1);
        var none = await JournalRows.ReadAsync(_connection, all.Newest, 100);

        Assert.Equal([all.Rows[1].Position], second.Rows.Select(row => row.Position));
        Assert.Empty(none.Rows);
        Assert.Equal(all.Newest, none.Newest);
    }

    /// <summary>A database with no journal yet is answered an empty page, never an error.</summary>
    [Fact]
    public async Task A_database_with_no_journal_is_answered_an_empty_page()
    {
        var bare = Path.Combine(_root, "bare.db");
        await using var made = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = bare, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        await made.OpenAsync();
        await using (var command = made.CreateCommand())
        {
            command.CommandText = "CREATE TABLE other (x)";
            await command.ExecuteNonQueryAsync();
        }

        var page = await JournalRows.ReadAsync(made, 0, 100);

        Assert.Empty(page.Rows);
        Assert.Equal((0L, 0L), (page.Floor, page.Newest));
    }
}
