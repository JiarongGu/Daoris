using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The closed quests from before an id had twelve characters (ONEWAY1x): a Done or Declined quest with a six-character id is finished
/// history nothing reads, and the lookups that read it under either width are gone. Where a remote numbered one of its operations it may
/// be held elsewhere, so its row goes and a <c>deleted</c> tombstone is appended to its log as the service's remover of a closed quest
/// appends it, for the next sync to carry; any other goes with its whole log. An open or taken one stays, to close first, and so does a
/// numbered one on a store with no machine row, which has nothing to stamp the tombstone with.
/// </summary>
public sealed class ClosedQuestsStep : IHomeMigrationStep
{
    private const int LegacyIdLength = 6;

    public string Name => "closed-quests";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Knowledge);
        if (!File.Exists(path)) return;
        run.Keep(path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        foreach (var table in new[] { "quests", "quest_log" })
        {
            using var probe = connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            probe.Parameters.AddWithValue("$name", table);
            if (Convert.ToInt32(probe.ExecuteScalar(), CultureInfo.InvariantCulture) == 0) return;
        }

        using var transaction = connection.BeginTransaction();
        var closed = new List<string>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT id FROM quests WHERE length(id) = $length AND status IN ('Done', 'Declined')";
            select.Parameters.AddWithValue("$length", LegacyIdLength);
            using var reader = select.ExecuteReader();
            while (reader.Read()) closed.Add(reader.GetString(0));
        }

        if (closed.Count == 0) return;
        var machine = Scalar(connection, transaction, "SELECT id FROM quest_machine WHERE one = 1", []) as string;
        var forwards = Scalar(connection, transaction, "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'quest_forwards'", []) is not null;
        var removed = 0;
        foreach (var id in closed)
        {
            var numbered = Convert.ToInt32(
                Scalar(connection, transaction, "SELECT EXISTS (SELECT 1 FROM quest_log WHERE quest = $id AND remote IS NOT NULL)", [("$id", id)]),
                CultureInfo.InvariantCulture) != 0;
            if (numbered && machine is null) continue;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = (numbered ? "" : "DELETE FROM quest_log WHERE quest = $id; ")
                                  + (!numbered && forwards ? "DELETE FROM quest_forwards WHERE quest = $id; " : "")
                                  + "DELETE FROM quests WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
            if (numbered) Tombstone(connection, transaction, id, machine!);
            removed++;
        }

        transaction.Commit();
        if (removed > 0) run.Said(path);
    }

    /// <summary>A <c>deleted</c> operation after the machine's highest sequence, as the service's append numbers it.</summary>
    private static void Tombstone(SqliteConnection connection, SqliteTransaction transaction, string id, string machine)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload)
            VALUES ($quest, 'deleted', $machine,
                    (SELECT MAX(
                       COALESCE((SELECT MAX(sequence) FROM quest_log WHERE machine = $machine), 0),
                       COALESCE((SELECT sequence FROM quest_machine WHERE one = 1 AND id = $machine), 0)) + 1),
                    $at, '{}')
            """;
        command.Parameters.AddWithValue("$quest", id);
        command.Parameters.AddWithValue("$machine", machine);
        command.Parameters.AddWithValue("$at", Moments.Write(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, SqliteTransaction transaction, string sql, (string Name, string Value)[] parameters)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            return command.ExecuteScalar();
        }
        catch (SqliteException) when (sql.Contains("quest_machine", StringComparison.Ordinal))
        {
            return null;
        }
    }
}
