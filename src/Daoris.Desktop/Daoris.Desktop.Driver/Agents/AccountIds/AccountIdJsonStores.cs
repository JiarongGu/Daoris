using System.Text.Json.Nodes;
using static Daoris.Driver.AccountIdJson;

namespace Daoris.Driver;

/// <summary>
/// <c>accounts.json</c>: each moving id's entry under its new id, and an id that was the person's word kept as the account's name where
/// it has none (<see cref="AccountIdForm.WasAWord"/>). An entry named like an account that is none (a lock's) is a stray, dropped.
/// </summary>
internal sealed class AccountIdNamesStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write) =>
        File(at, AccountNames.PathOf(at.Home), write, (root, place) =>
        {
            var found = new List<AccountIdReference>();
            foreach (var rename in renames)
            {
                var held = AccountFileNodes.Child(root, rename.Agent);
                var key = held is null ? null : AccountFileNodes.Key(held, rename.From);
                var word = AccountIdForm.WasAWord(rename.From) && Nameable(held, rename.From);
                if (key is null && !word) continue;

                held ??= AccountFileNodes.Made(root, rename.Agent);
                if (key is not null) RenameKey(held, key, rename.To);
                else held[rename.To] = new JsonObject();
                if (word && held[rename.To] is JsonObject entry && Json.Text(entry, "name") is not { Length: > 0 }) entry["name"] = rename.From;
                found.Add(new AccountIdReference(rename, place, $"{rename.Agent}.{rename.From}"));
            }

            return found;
        }, make: renames.Any(rename => AccountIdForm.WasAWord(rename.From)));

    /// <summary>Each entry named like an account that is none, by agent, in the file's order.</summary>
    public static IReadOnlyList<AccountIdStray> Strays(string home)
    {
        var (root, _) = AccountFileNodes.Read(AccountNames.PathOf(home));
        return
        [
            .. root.Where(agent => agent.Value is JsonObject)
                .SelectMany(agent => agent.Value!.AsObject().Select(entry => entry.Key).Where(HarnessAccountFolders.NotAnAccount)
                    .Select(entry => new AccountIdStray(agent.Key, entry))),
        ];
    }

    /// <summary>Each stray entry taken out, and an agent left with none.</summary>
    public static void Drop(string home, IReadOnlyList<AccountIdStray> strays)
    {
        if (strays.Count == 0) return;
        var path = AccountNames.PathOf(home);
        using var held = KeepLock.Take(path);
        var (root, unreadable) = AccountFileNodes.Read(path);
        if (unreadable) return;
        foreach (var stray in strays)
        {
            if (root[stray.Agent] is not JsonObject entries) continue;
            entries.Remove(stray.Entry);
            if (entries.Count == 0) root.Remove(stray.Agent);
        }

        JsonFile.Write(path, root);
    }

    /// <summary>Whether a word is a name a terminal can type and no other account of the agent is called.</summary>
    private static bool Nameable(JsonObject? held, string word) =>
        word.Length <= AccountNames.Longest && !word.StartsWith('-') && !word.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '`')
        && (held is null || !held.Any(entry => entry.Value is JsonObject other && Same(Json.Text(other, "name"), word)));
}

/// <summary>A store keyed by agent, then account: <c>cooling.json</c>, <c>windows.json</c>, <c>keys.json</c>, the history's <c>epochs.json</c>.</summary>
internal sealed class AccountIdKeyedStore(Func<string, string> path) : IAccountIdStore
{
    public AccountIdKeyedStore(HomeEntry entry) : this(home => DaorisHome.Entry(home, entry))
    {
    }

    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write) =>
        File(at, path(at.Home), write, (root, place) => [.. renames.SelectMany(rename => Keyed(root, rename, place, ""))]);

    /// <summary>The moving id's key under each of the agent's objects in <paramref name="root"/>, <paramref name="under"/> the agent where named.</summary>
    internal static IEnumerable<AccountIdReference> Keyed(JsonObject root, AccountIdRename rename, string place, string under)
    {
        var found = new List<AccountIdReference>();
        foreach (var agent in Agents(root, rename.Agent))
        {
            var held = under.Length == 0 ? root[agent] as JsonObject : AccountFileNodes.Child(root[agent] as JsonObject, under);
            if (held is null || AccountFileNodes.Key(held, rename.From) is not { } key) continue;
            RenameKey(held, key, rename.To);
            found.Add(new AccountIdReference(rename, place, under.Length == 0 ? $"{agent}.{rename.From}" : $"{agent}.{under}.{rename.From}"));
        }

        return found;
    }
}

/// <summary><c>reads.json</c>: each agent's readings of its accounts, under <c>accounts</c>.</summary>
internal sealed class AccountIdReadsStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write) =>
        File(at, DaorisHome.Entry(at.Home, HomeEntry.Reads), write,
            (root, place) => [.. renames.SelectMany(rename => AccountIdKeyedStore.Keyed(root, rename, place, "accounts"))]);
}

/// <summary><c>agent-offers.json</c>: each reading keyed <c>&lt;agent&gt;|&lt;account&gt;</c>, its <c>account</c> field beside.</summary>
internal sealed class AccountIdOffersStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write) =>
        File(at, DaorisHome.Entry(at.Home, HomeEntry.AgentOffers), write, (root, place) =>
        {
            var found = new List<AccountIdReference>();
            if (root["readings"] is not JsonObject readings) return found;
            foreach (var key in readings.Select(pair => pair.Key).ToList())
            {
                if (key.Split('|') is not [var agent, var account] || Of(renames, agent, account) is not { } rename) continue;
                RenameKey(readings, key, $"{agent}|{rename.To}");
                if (readings[$"{agent}|{rename.To}"] is JsonObject reading && Names(reading["account"], rename.From)) reading["account"] = rename.To;
                found.Add(new AccountIdReference(rename, place, $"readings.{key}"));
            }

            return found;
        });
}

/// <summary><c>usage.json</c>: each session's reading, its account in <c>profile</c> and its agent its <c>door</c>'s.</summary>
internal sealed class AccountIdUsageStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write)
    {
        var path = DaorisHome.Entry(at.Home, HomeEntry.Usage);
        if (!System.IO.File.Exists(path)) return [];
        using var held = write ? KeepLock.Take(path) : null;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(AtomicFile.ReadText(path));
        }
        catch (System.Text.Json.JsonException)
        {
            root = null;
        }

        if (root is not JsonArray sessions) throw new DriverException($"{at.Place(path)} does not read, so no account id was moved: mend or remove it, then run this again.");
        var counts = new Dictionary<AccountIdRename, int>();
        foreach (var session in sessions.OfType<JsonObject>())
        {
            if (Json.Text(session, "door") is not { } door || Of(renames, at.Doors.Of(door), Json.Text(session, "profile")) is not { } rename) continue;
            session["profile"] = rename.To;
            counts[rename] = counts.GetValueOrDefault(rename) + 1;
        }

        if (write && counts.Count > 0) JsonFile.Write(path, sessions);
        return [.. renames.Where(counts.ContainsKey).Select(rename => new AccountIdReference(rename, at.Place(path), Counted(counts[rename], "session")))];
    }
}
