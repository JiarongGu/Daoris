using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The upgrade that lets a session have no quest.
/// </summary>
/// <remarks>
/// SQLite cannot drop a NOT NULL constraint, so the table is rebuilt — and the rows are COPIED, not
/// discarded. The entry store may rebuild by discarding because its contents are derived; a session
/// record is the reviewable trace of work that actually happened, and nothing can re-derive it.
/// </remarks>
public sealed class SessionSchemaUpgradeTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        // The table exactly as it stood before chats: quest NOT NULL, no kind column.
        await using var old = _connection.CreateCommand();
        old.CommandText = """
            CREATE TABLE sessions (
              id         TEXT PRIMARY KEY,
              quest      TEXT NOT NULL,
              repository TEXT NOT NULL,
              adapter    TEXT NOT NULL,
              state      TEXT NOT NULL,
              note       TEXT NULL,
              evidence   TEXT NULL,
              transcript TEXT NULL,
              created    TEXT NOT NULL,
              updated    TEXT NOT NULL,
              workspace  TEXT NOT NULL DEFAULT 'default'
            );
            INSERT INTO sessions (id, quest, repository, adapter, state, evidence, created, updated, workspace)
            VALUES ('old12345', 'abc123', 'Elder', 'claude-code', 'Completed', 'commit deadbee',
                    '2026-09-18T10:00:00+00:00', '2026-09-18T11:00:00+00:00', 'aurora');
            """;
        await old.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task The_upgrade_keeps_every_record_and_lets_a_chat_open()
    {
        var sessions = await SessionStore.OpenAsync(_connection);

        var elder = await sessions.FindAsync("old12345");
        Assert.NotNull(elder);
        Assert.Equal("abc123", elder!.Quest);
        Assert.Equal("commit deadbee", elder.Evidence);
        Assert.Equal("aurora", elder.Workspace);
        // Everything that predates kinds is driven.
        Assert.Equal(SessionKind.Driven, elder.Kind);
        // …and everything that predates trees names no tree, which the lock reads as "possibly any of
        // them". A record of work that happened survives every column that arrives after it.
        Assert.Null(elder.Tree);
        // …and a record from before the mark says nothing about being interrupted: a stop
        // is the person's.
        Assert.False(elder.Interrupted);
        // …nor about an account's limit: a failure like any other.
        Assert.False(elder.Limit);
        // …and it names no second opinion: a session that reads nobody's work for one.
        Assert.Null(elder.Opinion);
        // …nor a session it continues: a session that goes on from none.
        Assert.Equal(((string?)null, (string?)null, (string?)null), (elder.From, elder.FromWhy, elder.FromBy));
        // …nor the access setup it started under: a start before the setup runs as today's.
        Assert.Null(elder.Access);
        Assert.Null(elder.Actor);
        Assert.Null(elder.Choice);
        Assert.Null(elder.Asked);
        Assert.Null(elder.Override);
        Assert.Null(elder.Ran);
        Assert.Null(elder.Chosen);
        // …and it is answered its work from its marks by a host that has the reading: a quest's driven session is build.
        Assert.Equal(ParticipantWork.Build, ParticipantWork.Of(elder));

        var chat = await sessions.CreateAsync(
            null, "Elder", "stub", Now, workspace: null, kind: SessionKind.Chat);
        Assert.Null((await sessions.FindAsync(chat.Id))!.Quest);
    }

    /// <summary>
    /// The rebuild is one step: where it fails after the old table is dropped, the open leaves the store as it found it, so the next
    /// open rebuilds and keeps every record, instead of finding the rows in the half-made table or its name taken.
    /// </summary>
    [Fact]
    public async Task A_rebuild_that_fails_part_way_leaves_the_store_as_it_found_it()
    {
        // A view over the table names a table the drop removes, which the rename that follows refuses.
        await ExecuteAsync("CREATE VIEW session_names AS SELECT id FROM sessions");

        await Assert.ThrowsAnyAsync<Exception>(() => SessionStore.OpenAsync(_connection));

        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sessions WHERE id = 'old12345'"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sessions_relaxed'"));
        await ExecuteAsync("DROP VIEW session_names");
        Assert.NotNull(await (await SessionStore.OpenAsync(_connection)).FindAsync("old12345"));
    }

    /// <summary>
    /// A column and the backfill that fills it are one step: where the backfill fails the column goes with it, so the next open adds
    /// both, instead of finding the column and leaving its rows unfilled for good.
    /// </summary>
    [Fact]
    public async Task A_backfill_that_fails_leaves_no_column_to_skip_it_for_good()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        await sessions.AdvanceCursorAsync("aurora", pushed: 1_000);
        await ExecuteAsync("ALTER TABLE sessions DROP COLUMN pushed");
        await ExecuteAsync("CREATE TRIGGER pushed_refused BEFORE UPDATE OF pushed ON sessions BEGIN SELECT RAISE(ABORT, 'planted'); END");

        await Assert.ThrowsAnyAsync<Exception>(() => SessionStore.OpenAsync(_connection));

        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name = 'pushed'"));
        await ExecuteAsync("DROP TRIGGER pushed_refused");
        var upgraded = await SessionStore.OpenAsync(_connection);
        Assert.True((await upgraded.FindAsync("old12345"))!.Pushed);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>Opening twice must not rebuild twice — a guard that ran every time would be a rewrite
    /// of the whole table on every start, and the second one would find nothing to relax.</summary>
    [Fact]
    public async Task The_rebuild_runs_once()
    {
        await SessionStore.OpenAsync(_connection);
        var second = await SessionStore.OpenAsync(_connection);

        Assert.NotNull(await second.FindAsync("old12345"));
    }

    /// <summary>
    /// A store from before the pushed mark derives it once, from its push cursor. A record of this
    /// machine's at or before what its workspace pushed went up as far as anything here can tell, so it is marked, which
    /// errs toward refusing a delete; one written after, a teammate's and one of a workspace never pushed are not.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_pushed_mark_derives_it_from_its_cursor()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var earlier = await sessions.CreateAsync(null, "Elder", "stub", Now, kind: SessionKind.Chat);
        await sessions.AdvanceCursorAsync("aurora", pushed: (await sessions.OwnChangedSinceAsync(0, "aurora")).Max(change => change.Revision));
        await sessions.AdvanceCursorAsync(
            Workspaces.Default, pushed: (await sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Max(change => change.Revision));
        var later = await sessions.CreateAsync(null, "Elder", "stub", Now, workspace: "aurora", kind: SessionKind.Chat);
        var elsewhere = await sessions.CreateAsync(null, "Elder", "stub", Now, workspace: "borealis", kind: SessionKind.Chat);
        await sessions.MirrorAsync(new Session("bob/fe12dc34", null, "Elder", "stub", SessionState.Completed, null, null, null, Now, Now));
        await using (var drop = _connection.CreateCommand())
        {
            drop.CommandText = "ALTER TABLE sessions DROP COLUMN pushed";
            await drop.ExecuteNonQueryAsync();
        }

        var upgraded = await SessionStore.OpenAsync(_connection);

        Assert.True((await upgraded.FindAsync("old12345"))!.Pushed);
        Assert.True((await upgraded.FindAsync(earlier.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync(later.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync(elsewhere.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync("bob/fe12dc34"))!.Pushed);
    }
}
