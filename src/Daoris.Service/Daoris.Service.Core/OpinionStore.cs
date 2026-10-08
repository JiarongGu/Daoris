using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Second opinions, kept by the local host beside the session records (XAGENT1c, D155 point 11; the second agent design
/// §6.2): one row a pass, this machine's alone. Nothing here is a quest operation, has a revision or goes on any wire: the
/// candidate is commits only this machine holds until they land, and the reviewer names an account (D47 §4).
/// </summary>
/// <remarks>
/// <b>As blind as the session store.</b> What may be kept is judged by <see cref="OpinionDesk"/>, which calls these inside
/// <see cref="SessionStore.ExclusiveAsync{T}"/>: both stores are on one connection, so its write lock holds for both.
/// </remarks>
public sealed class OpinionStore
{
    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
    private readonly ConnectionGate _db;

    private OpinionStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static async Task<OpinionStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new OpinionStore(connection);
        await store._db.RunAsync(() => store.EnsureSchemaAsync(ct), ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        // The columns a door asks by, and JSON for the rest, read part by part so a later build's field is never a failed read.
        await using var command = _db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS opinions (
              id          TEXT PRIMARY KEY,
              repository  TEXT NOT NULL,
              working     TEXT NOT NULL,
              session     TEXT NOT NULL,
              occasion    TEXT NOT NULL,
              pass        TEXT NOT NULL,
              rechecks    TEXT NULL,
              asked       TEXT NOT NULL,
              asking      TEXT NOT NULL,
              given       TEXT NULL,
              handed_to   TEXT NULL,
              handed_word TEXT NULL,
              handed_at   TEXT NULL,
              answers     TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS opinions_working ON opinions (working);
            CREATE INDEX IF NOT EXISTS opinions_session ON opinions (session);
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Keep a new pass, as asked: nothing given, handed or answered yet.</summary>
    public Task CreateAsync(Opinion opinion, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO opinions (id, repository, working, session, occasion, pass, rechecks, asked, asking)
            VALUES ($id, $repository, $working, $session, $occasion, $pass, $rechecks, $asked, $asking)
            """;
        command.Parameters.AddWithValue("$id", opinion.Id);
        command.Parameters.AddWithValue("$repository", opinion.Candidate.Repository);
        command.Parameters.AddWithValue("$working", opinion.Working);
        command.Parameters.AddWithValue("$session", opinion.Session);
        command.Parameters.AddWithValue("$occasion", opinion.Occasion);
        command.Parameters.AddWithValue("$pass", opinion.Pass);
        command.Parameters.AddWithValue("$rechecks", (object?)opinion.Rechecks ?? DBNull.Value);
        command.Parameters.AddWithValue("$asked", opinion.Asked.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asking", Opinions.AskedJson(opinion));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>The opinion under <paramref name="id"/>, or null.</summary>
    public Task<Opinion?> FindAsync(string id, CancellationToken ct = default) => _db.RunAsync<Opinion?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM opinions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The opinions that match every filter named, oldest first: those on <paramref name="working"/>'s work, the one
    /// <paramref name="session"/> reads as reviewer, those of <paramref name="repository"/>, those a recheck
    /// <paramref name="rechecks"/>, and those handed to <paramref name="handedTo"/>. A row whose asking cannot be read is passed over.
    /// </summary>
    public Task<IReadOnlyList<Opinion>> ListAsync(
        string? working = null, string? session = null, string? repository = null, string? rechecks = null, string? handedTo = null,
        CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Opinion>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM opinions
            WHERE ($working IS NULL OR working = $working) AND ($session IS NULL OR session = $session)
              AND ($repository IS NULL OR repository = $repository) AND ($rechecks IS NULL OR rechecks = $rechecks)
              AND ($handedTo IS NULL OR handed_to = $handedTo)
            ORDER BY asked, rowid
            """;
        command.Parameters.AddWithValue("$working", (object?)working ?? DBNull.Value);
        command.Parameters.AddWithValue("$session", (object?)session ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", (object?)repository ?? DBNull.Value);
        command.Parameters.AddWithValue("$rechecks", (object?)rechecks ?? DBNull.Value);
        command.Parameters.AddWithValue("$handedTo", (object?)handedTo ?? DBNull.Value);

        var opinions = new List<Opinion>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (Read(reader) is { } opinion) opinions.Add(opinion);
        }

        return opinions;
    }, ct);

    /// <summary>Keep what the reviewer said, once: false when the opinion holds one already, or there is none.</summary>
    public Task<bool> KeepGivenAsync(string id, OpinionGiven given, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE opinions SET given = $given WHERE id = $id AND given IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$given", Opinions.GivenJson(given));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>Keep where the findings went, once: false when they went somewhere already, or there is none.</summary>
    public Task<bool> KeepHandedAsync(string id, OpinionHanded handed, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            UPDATE opinions SET handed_to = $to, handed_word = $word, handed_at = $at WHERE id = $id AND handed_to IS NULL
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$to", handed.Session);
        command.Parameters.AddWithValue("$word", handed.Word);
        command.Parameters.AddWithValue("$at", handed.At.ToString("O", CultureInfo.InvariantCulture));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>Write the answers whole: a read, then the write it decides, inside the desk's lock.</summary>
    public Task<bool> KeepAnswersAsync(string id, IReadOnlyList<OpinionAnswer> answers, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE opinions SET answers = $answers WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$answers", Opinions.AnswersJson(answers));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    private static Opinion? Read(SqliteDataReader reader)
    {
        string? Column(string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));

        if (!DateTimeOffset.TryParse(Column("asked"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var asked)) return null;
        var handed = Column("handed_to") is { } to && Column("handed_word") is { } word
            && DateTimeOffset.TryParse(Column("handed_at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                ? new OpinionHanded(to, word, at)
                : null;
        return Opinions.Read(
            Column("id")!, Column("occasion")!, Column("pass")!, Column("working")!, Column("session")!, Column("rechecks"), asked,
            Column("asking")!, Column("given"), handed, Column("answers"));
    }
}
