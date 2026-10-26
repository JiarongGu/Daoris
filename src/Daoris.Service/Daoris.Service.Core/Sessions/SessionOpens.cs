using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>The result kept by a keyed open, independent of the destination's waiting words.</summary>
public sealed record SessionOpenRecord(string Key, string Left, string Opened, bool Gone, IReadOnlyList<OpenTransfer> Transfer);

/// <summary>A word moved by an open, in its transfer order. Row d-i writes an empty transfer.</summary>
public sealed record OpenTransfer(string Word, string? Key, string Transfer, int Order);

/// <summary>Local open receipts, written and read inside the ledger's exclusive step (D212 §19.5).</summary>
public sealed class SessionOpens
{
    private readonly ConnectionGate _db;
    internal SessionOpens(ConnectionGate db) => _db = db;

    internal async Task EnsureAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS sessions_from ON sessions (from_session, created);
            CREATE INDEX IF NOT EXISTS sessions_quest ON sessions (quest, created);
            CREATE TABLE IF NOT EXISTS session_opens (
                key TEXT PRIMARY KEY NOT NULL,
                left_session TEXT NOT NULL,
                opened_session TEXT NOT NULL,
                gone INTEGER NOT NULL DEFAULT 0,
                transfer TEXT NOT NULL
            ) WITHOUT ROWID;
            CREATE INDEX IF NOT EXISTS session_opens_left ON session_opens (left_session);
            CREATE INDEX IF NOT EXISTS session_opens_opened ON session_opens (opened_session);
            CREATE TRIGGER IF NOT EXISTS session_opens_deleted AFTER DELETE ON sessions
            BEGIN
                DELETE FROM session_opens WHERE left_session = OLD.id;
                UPDATE session_opens SET gone = 1, transfer = '[]' WHERE opened_session = OLD.id;
            END;
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public Task<SessionOpenRecord?> FindAsync(string key, CancellationToken ct = default) => _db.RunAsync<SessionOpenRecord?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT left_session, opened_session, gone, transfer FROM session_opens WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(key, reader.GetString(0), reader.GetString(1), reader.GetInt64(2) != 0,
                JsonSerializer.Deserialize<OpenTransfer[]>(reader.GetString(3))!)
            : null;
    }, ct);

    internal Task KeepAsync(string key, string left, string opened, CancellationToken ct) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "INSERT INTO session_opens (key, left_session, opened_session, transfer) VALUES ($key, $left, $opened, '[]')";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$left", left);
        command.Parameters.AddWithValue("$opened", opened);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    internal Task<string?> LeftForAsync(string left, CancellationToken ct) => _db.RunAsync<string?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT o.opened_session FROM session_opens o
            JOIN sessions s ON s.id = o.opened_session
            WHERE o.left_session = $left AND o.gone = 0 LIMIT 1
            """;
        command.Parameters.AddWithValue("$left", left);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }, ct);
}
