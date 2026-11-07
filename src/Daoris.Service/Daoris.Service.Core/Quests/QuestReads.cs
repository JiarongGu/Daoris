using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>The reads of the quests table and the fetch cursor: one quest, a list, what waits on a question, and where a circle's fetch stands.</summary>
internal sealed class QuestReads(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// The taken quests waiting on <paramref name="question"/> — what keeps a question from being
    /// deleted: deleted, the quests waiting on it would wait on nothing for good.
    /// </summary>
    public Task<IReadOnlyList<Quest>> WaitingOnAsync(string question, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM quests WHERE awaits = $question AND status = 'Taken' ORDER BY filed";
        command.Parameters.AddWithValue("$question", question);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>Every question a taken quest waits on — what <see cref="WaitingOnAsync"/> answers, for a whole list at once.</summary>
    public Task<IReadOnlySet<string>> AwaitedAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlySet<string>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT DISTINCT awaits FROM quests WHERE awaits IS NOT NULL AND status = 'Taken'";

        var awaited = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) awaited.Add(reader.GetString(0));
        return awaited;
    }, ct);

    /// <summary>The last number this machine fetched from a workspace's remote — zero before its first fetch.</summary>
    public Task<long> CursorAsync(string workspace, CancellationToken ct = default) =>
        _db.RunAsync(() => CursorAsync(workspace, transaction: null, ct), ct);

    internal async Task<long> CursorAsync(string workspace, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command is made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT number FROM quest_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } number and not DBNull
            ? Convert.ToInt64(number)
            : 0;
    }

    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync(() => FindAsync(id, transaction: null, ct), ct);

    internal async Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command is made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT * FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Quests addressed to one repository, or every quest when none is named.
    /// </summary>
    /// <remarks>
    /// Open and taken first: what is outstanding is the question worth asking, and a finished queue
    /// pushing live work off the end is how a list stops being read. A done that a departure holds for the
    /// person's yes is outstanding too: it waits on someone, and what follows it waits with it — the
    /// driver keeps a quest waiting on it waiting because it is listed here.
    /// </remarks>
    public Task<IReadOnlyList<Quest>> ListAsync(
        string? receiver = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM quests
            WHERE ($receiver IS NULL OR receiver = $receiver)
              AND ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : "AND (status IN ('Open', 'Taken') OR held = 1)")}
            ORDER BY CASE status WHEN 'Open' THEN 0 WHEN 'Taken' THEN 1 ELSE 2 END, filed
            """;
        command.Parameters.AddWithValue("$receiver", (object?)receiver ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>
    /// Every quest one sender asked, closed ones included — or, <paramref name="startingWith"/>, every
    /// quest whose sender begins with it. What an ask's standing is read from: its quests are
    /// the ones asked BY it, chain steps included, whichever machine last moved them.
    /// </summary>
    public Task<IReadOnlyList<Quest>> FromAsync(
        string sender, bool startingWith = false, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = startingWith
            ? "SELECT * FROM quests WHERE substr(sender, 1, length($sender)) = $sender ORDER BY filed"
            : "SELECT * FROM quests WHERE sender = $sender ORDER BY filed";
        command.Parameters.AddWithValue("$sender", sender);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);
}
