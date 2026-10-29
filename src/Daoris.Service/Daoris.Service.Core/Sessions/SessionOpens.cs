using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>The result kept by a keyed open, independent of the destination's waiting words.</summary>
public sealed record SessionOpenRecord(string Key, string Left, string Opened, bool Gone, IReadOnlyList<OpenTransfer> Transfer);

/// <summary>
/// A word moved by an open, in its transfer order (row d-ii part 3, the design §3.3): <paramref name="Word"/>, its id on the record it left;
/// <paramref name="Key"/>, its producer's key, or none; <paramref name="Transfer"/>, the open's key and that id, so a word with no key has one
/// too; <paramref name="Order"/>, its place; and <paramref name="Id"/>, its id on the record opened, which a moved word keeps, so the two are
/// equal in this build and a reader takes the one it acts on by name. Row d-i wrote an empty transfer.
/// </summary>
public sealed record OpenTransfer(string Word, string? Key, string Transfer, int Order, string Id);

/// <summary>
/// What a keyed open moves (FOUNDATION1 row d-ii part 3; the design §3.4 rule 3): the record the words move from, <paramref name="From"/>, the
/// record the open leaves or, for a start from another's words, the source of a record that names no <c>from</c>; the ids the course named
/// (<c>Carries</c>, judged in .NET), <paramref name="Words"/>; and the ids it saw waiting there when it read, <paramref name="Read"/>, so every
/// word of the person's kept there since, which none of those names, is told apart and moves too. The service judges no word's carriage
/// itself: a word the course named moves, waiting, whoever said it.
/// </summary>
/// <remarks>
/// <b>The read mark is the ids seen</b>, not a position or a moment: a word's id is the one thing of it the course holds that a later write
/// cannot change. A list's positions shift whenever a word is taken off it, and a word's moment is its caller's clock, so neither tells a
/// word kept after the read from one before it.
/// </remarks>
public sealed record OpenMoves(string From, IReadOnlyList<string> Words, IReadOnlyList<string> Read)
{
    /// <summary>The refusal of moves that do not read, beside the open's <paramref name="from"/>; nothing is opened where it throws.</summary>
    /// <exception cref="ArgumentException">A blank source, a source other than the record the open leaves, a missing list or a blank id.</exception>
    internal void Judge(string? from)
    {
        if (string.IsNullOrWhiteSpace(From))
            throw new ArgumentException("`moves` names no record its words move from (`moves.from`), so nothing was opened.");
        if (from is not null && !string.Equals(from, From, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Words move from the record the open leaves, `{from}`, and `moves.from` names `{From}`, so nothing was opened.");
        if (Words is null || Read is null)
            throw new ArgumentException(
                "`moves` names the words the course named (`moves.words`) and the words it read (`moves.read`), each a list even where empty, "
                + "so nothing was opened.");
        if (Words.Concat(Read).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("`moves` names a blank word id, which no word has, so nothing was opened.");
    }
}

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
            ? new(key, reader.GetString(0), reader.GetString(1), reader.GetInt64(2) != 0, Transfer(reader.GetString(3)))
            : null;
    }, ct);

    /// <summary>
    /// A receipt's transfer as stored. An element that names no id on the record opened reads as its word's own, which a moved word keeps
    /// (row d-ii part 3): the element as row d-i declared it, which no open of that row wrote.
    /// </summary>
    private static OpenTransfer[] Transfer(string json) =>
        [.. JsonSerializer.Deserialize<OpenTransfer[]>(json)!.Select(word => string.IsNullOrEmpty(word.Id) ? word with { Id = word.Word } : word)];

    /// <summary>The receipt, with every word the open moved (row d-ii part 3), written in the open's own step, so a failed write rolls the open back.</summary>
    internal Task KeepAsync(string key, string left, string opened, IReadOnlyList<OpenTransfer> transfer, CancellationToken ct) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "INSERT INTO session_opens (key, left_session, opened_session, transfer) VALUES ($key, $left, $opened, $transfer)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$left", left);
        command.Parameters.AddWithValue("$opened", opened);
        command.Parameters.AddWithValue("$transfer", JsonSerializer.Serialize(transfer));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// The standing destination a keyed open made in <paramref name="left"/>'s line, which holds that record from moving out of an ended or
    /// parked state. Only a destination that continues it (its <c>from</c> the record left) does: a start from another's words takes them
    /// into a record of a line of its own (row d-ii part 3), and the record they came from stays its own line's head.
    /// </summary>
    internal Task<string?> LeftForAsync(string left, CancellationToken ct) => _db.RunAsync<string?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT o.opened_session FROM session_opens o
            JOIN sessions s ON s.id = o.opened_session AND s.from_session = o.left_session
            WHERE o.left_session = $left AND o.gone = 0 LIMIT 1
            """;
        command.Parameters.AddWithValue("$left", left);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }, ct);
}
