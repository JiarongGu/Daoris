using static Daoris.Knowledge.SessionRowCodec;

namespace Daoris.Knowledge;

/// <summary>
/// The writer's lease on a place: the one in-place record of this machine's that holds a repository's own checkout. A record holds
/// it while its turn runs (queued, starting or working there), and in a git place also while it is parked or waiting with
/// uncommitted paths of its own marked (<see cref="Session.HoldsPlace"/>). The passes (a helper, a partner, a second opinion, a
/// supervisor's turn) and an intake write nothing in the place and hold none. The ledger reads it in the write that opens a record or
/// moves one into a running state, and writes the mark in the move out of one; nothing renews it on a clock.
/// </summary>
internal sealed class SessionPlaceLease(SessionStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// The record holding <paramref name="repository"/>'s place at <paramref name="root"/>, or null. A null root (nothing
    /// registered) is answered by any record of the repository, and a stored row with no tree holds every place, as the tree lock errs.
    /// </summary>
    public Task<Session?> HolderAsync(string repository, string? root, CancellationToken ct, string? except = null) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions
            WHERE repository = $repository AND origin IS NULL
              AND ($root IS NULL OR tree IS NULL OR tree = $root)
              AND ask IS NULL AND opinion IS NULL AND serves IS NULL AND decides IS NULL
              AND ($except IS NULL OR id <> $except)
              AND (state IN ('Queued', 'Starting', 'Working')
                   OR (holds_place IS NOT NULL AND state IN ('AwaitingPerson', 'Completed')))
            ORDER BY created
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$root", (object?)Trees.Normalize(root) ?? DBNull.Value);
        command.Parameters.AddWithValue("$except", (object?)except ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// Write <paramref name="paths"/> as the record's mark, or clear it where none or empty: machine-local, with no sync revision.
    /// </summary>
    /// <returns>Whether a record of this machine's had that id.</returns>
    public Task<bool> MarkAsync(string id, IReadOnlyList<string>? paths, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET holds_place = $paths WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$paths", paths is { Count: > 0 } kept ? JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var path in kept) writer.WriteStringValue(path);
            writer.WriteEndArray();
        }) : DBNull.Value);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// The busy sentence of a place: whose it is and why, never its path (a refusal is rendered verbatim wherever it lands).
    /// </summary>
    public static string Busy(string repository, Session holder)
    {
        var what = $"{Session.Spell(holder.State)}{(holder.Quest is null ? ", a chat" : $", quest `#{holder.Quest}`")}";
        return holder.HoldsPlace is { Count: > 0 } paths && holder.State is not (SessionState.Queued or SessionState.Starting or SessionState.Working)
            ? $"`{repository}` is held by session `{holder.Id}` ({what}), which keeps {paths.Count} uncommitted "
              + $"path{(paths.Count == 1 ? "" : "s")} of its own there until it commits them, they are discarded or the person lets others "
              + "work here. A place has one writer at a time: one session per working tree."
            : $"`{repository}` is held by session `{holder.Id}` ({what}), whose turn runs there; this starts when its turn ends. "
              + "A place has one writer at a time: one session per working tree.";
    }
}
