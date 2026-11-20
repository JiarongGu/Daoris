using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountIdJson;

namespace Daoris.Driver;

/// <summary>
/// The usage history (<see cref="UsageHistory"/>): each account's epoch in <c>epochs.json</c>, then each month's readings and turns,
/// one object a line with its <c>agent</c> and <c>account</c>. A line that names no moving id is kept byte for byte.
/// </summary>
internal sealed class AccountIdHistoryStore : IAccountIdStore
{
    private static readonly JsonSerializerOptions Line = new(JsonFile.Options) { WriteIndented = false };

    private readonly AccountIdKeyedStore _epochs = new(home => DaorisHome.Entry(home, HomeEntry.UsageHistory, "epochs.json"));

    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write)
    {
        var found = _epochs.Moved(at, renames, write).ToList();
        var folder = DaorisHome.Entry(at.Home, HomeEntry.UsageHistory);
        if (!Directory.Exists(folder)) return found;
        foreach (var path in Directory.EnumerateFiles(folder, "*.jsonl").Order(StringComparer.Ordinal))
        {
            var counts = new Dictionary<AccountIdRename, int>();
            var text = AtomicFile.ReadText(path);
            var lines = text.Split('\n');
            for (var each = 0; each < lines.Length; each++)
            {
                if (Parsed(lines[each]) is not { } line || Of(renames, Json.Text(line, "agent"), Json.Text(line, "account")) is not { } rename) continue;
                line["account"] = rename.To;
                lines[each] = line.ToJsonString(Line);
                counts[rename] = counts.GetValueOrDefault(rename) + 1;
            }

            if (write && counts.Count > 0) AtomicFile.WriteText(path, string.Join('\n', lines));
            found.AddRange(renames.Where(counts.ContainsKey).Select(rename => new AccountIdReference(rename, at.Place(path), Counted(counts[rename], "line"))));
        }

        return found;
    }

    private static JsonObject? Parsed(string line)
    {
        if (line.Trim().Length == 0) return null;
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The sessions' records: the service's <c>sessions.profile</c> in the knowledge store, each session's agent its <c>door</c>'s, and the
/// desktop store's <c>binding_picks.account</c>, each pick's agent its session's door, else its binding's, else the one agent whose id it is.
/// </summary>
internal sealed class AccountIdSessionStores : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write)
    {
        var knowledge = at.KnowledgeDb is { Length: > 0 } wired ? Path.GetFullPath(wired) : DaorisHome.Entry(at.Home, HomeEntry.Knowledge);
        var desktop = DaorisHome.Entry(at.Home, HomeEntry.DesktopDb);
        var doors = new Dictionary<string, string>(StringComparer.Ordinal);
        var found = new List<AccountIdReference>();

        if (Has(knowledge, "sessions", "door", "profile"))
        {
            using var connection = Open(knowledge, write);
            foreach (var (session, door) in Rows(connection, "SELECT id, door FROM sessions")) doors[session] = door;
            found.AddRange(Move(at, connection, knowledge, "sessions", "id", "profile", renames, doors, write));
        }

        if (Has(desktop, "binding_picks", "session", "account"))
        {
            using var connection = Open(desktop, write);
            if (Has(desktop, "bindings", "session", "adapter"))
            {
                foreach (var (session, adapter) in Rows(connection, "SELECT session, adapter FROM bindings")) doors.TryAdd(session, adapter);
            }

            found.AddRange(Move(at, connection, desktop, "binding_picks", "session", "account", renames, doors, write));
        }

        return found;
    }

    /// <summary>Each row of <paramref name="table"/> whose account is a moving id of its session's agent, moved in one transaction.</summary>
    private static IEnumerable<AccountIdReference> Move(
        AccountIdHome at, DbConnection connection, string path, string table, string key, string column, IReadOnlyList<AccountIdRename> renames,
        IReadOnlyDictionary<string, string> doors, bool write)
    {
        var moving = new List<(string Session, AccountIdRename Rename)>();
        foreach (var (session, account) in Rows(connection, $"SELECT {key}, {column} FROM {table} WHERE {column} IS NOT NULL"))
        {
            var rename = doors.TryGetValue(session, out var door) ? Of(renames, at.Doors.Of(door), account) : Only(renames, account);
            if (rename is not null) moving.Add((session, rename));
        }

        if (write && moving.Count > 0)
        {
            using var transaction = connection.BeginTransaction();
            foreach (var (session, rename) in moving)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {table} SET {column} = $to WHERE {key} = $key";
                Parameter(command, "$to", rename.To);
                Parameter(command, "$key", session);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        return
        [
            .. moving.GroupBy(each => each.Rename).Select(group =>
                new AccountIdReference(group.Key, at.Place(path), $"{table}.{column} {Counted(group.Count(), "row")}")),
        ];
    }

    private static DbConnection Open(string path, bool write)
    {
        var connection = StoreEngineSource.Connect(path, write ? DesktopOpen.ReadWrite : DesktopOpen.ReadOnly);
        if (connection.State != ConnectionState.Open) connection.Open();
        return connection;
    }

    /// <summary>Whether the store is there and its table has the columns a move reads.</summary>
    private static bool Has(string path, string table, params string[] columns)
    {
        if (!System.IO.File.Exists(path)) return false;
        using var connection = Open(path, write: false);
        var present = Rows(connection, $"SELECT name, name FROM pragma_table_info('{table}')").Select(row => row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return columns.All(present.Contains);
    }

    private static List<KeyValuePair<string, string>> Rows(DbConnection connection, string query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = query;
        using var reader = command.ExecuteReader();
        var rows = new List<KeyValuePair<string, string>>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1)) rows.Add(KeyValuePair.Create(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static void Parameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
