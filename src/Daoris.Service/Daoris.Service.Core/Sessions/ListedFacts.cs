using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <summary>
/// The facts a list's records are answered by, their entry keys and their lines' heads, read for all of them at once. Who continues
/// each record and each quest's last run here are each one read of the table, by the same filter and the same order the store's
/// single reads take; a record the list holds is not read again; and a quest, its chain, a mirror or the conversation that took a
/// quest is read once however many records ask it.
/// </summary>
/// <remarks>
/// A list of three thousand asked those reads one record at a time, some ten thousand of them through the store's one connection,
/// and every other door waited its turn behind them. The walk and the entry's rule are the same ones (<see cref="Lines"/>,
/// <see cref="SessionEntries"/>); only where their facts come from differs.
/// </remarks>
public sealed class ListedFacts : ILineFacts, ISessionEntryFacts
{
    private readonly SessionStore _sessions;
    private readonly QuestStore _quests;
    private readonly IReadOnlyDictionary<string, string> _continuedBy;
    private readonly IReadOnlyDictionary<string, string> _lastRun;
    private readonly ConcurrentDictionary<string, Task<Session?>> _found = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<Session?>> _mirrored = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<Session?>> _took = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<Quest?>> _quest = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<Quest>>> _chains = new(StringComparer.Ordinal);

    private ListedFacts(
        SessionStore sessions, QuestStore quests, IReadOnlyDictionary<string, string> continuedBy, IReadOnlyDictionary<string, string> lastRun,
        IEnumerable<Session> listed)
    {
        (_sessions, _quests, _continuedBy, _lastRun) = (sessions, quests, continuedBy, lastRun);
        foreach (var record in listed) _found.TryAdd(record.Id, Task.FromResult<Session?>(record));
    }

    /// <summary>The facts for <paramref name="listed"/>, the records a list holds: who continues each, and each quest's last run, read whole.</summary>
    public static async Task<ListedFacts> ReadAsync(SessionStore sessions, QuestStore quests, IEnumerable<Session> listed, CancellationToken ct = default)
    {
        var continuedBy = await NewestAsync(sessions, "from_session", ct).ConfigureAwait(false);
        var lastRun = await NewestAsync(sessions, "quest", ct).ConfigureAwait(false);
        return new ListedFacts(sessions, quests, continuedBy, lastRun, listed);
    }

    // The newest local record by each value of the column: ContinuedByAsync's and LastOwnIdForQuestAsync's filter and order, read whole.
    private static Task<IReadOnlyDictionary<string, string>> NewestAsync(SessionStore sessions, string column, CancellationToken ct) =>
        sessions.Db.RunAsync<IReadOnlyDictionary<string, string>>(async () =>
        {
            await using var command = sessions.Db.Command();
            command.CommandText = $"SELECT {column}, id FROM sessions WHERE {column} IS NOT NULL AND origin IS NULL ORDER BY created DESC, rowid DESC";
            var newest = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) newest.TryAdd(reader.GetString(0), reader.GetString(1));
            return newest;
        }, ct);

    public async Task<Session?> ContinuedByAsync(string id, CancellationToken ct) =>
        _continuedBy.TryGetValue(id, out var next) ? await FindAsync(next, ct).ConfigureAwait(false) : null;

    public Task<string?> LastOwnIdForQuestAsync(string quest, CancellationToken ct) =>
        Task.FromResult(_lastRun.TryGetValue(quest, out var last) ? last : null);

    public Task<Session?> FindAsync(string id, CancellationToken ct) => _found.GetOrAdd(id, named => _sessions.FindAsync(named, ct));

    public Task<IReadOnlyList<Quest>> ChainAsync(string quest, CancellationToken ct) => _chains.GetOrAdd(quest, named => _quests.ChainAsync(named, ct));

    public Task<Quest?> QuestAsync(string id, CancellationToken ct = default) => _quest.GetOrAdd(id, named => _quests.FindAsync(named, ct));

    // A record of this machine's with the id is the store's first answer, since its order puts mirrors last and an id is one record.
    public Task<Session?> SessionAsync(string id, CancellationToken ct = default) =>
        _found.TryGetValue(id, out var held) && held.IsCompletedSuccessfully && held.Result is { Origin: null }
            ? held
            : _mirrored.GetOrAdd(id, named => _sessions.FindOrMirrorAsync(named, ct));

    public Task<Session?> ChatTookAsync(string questId, CancellationToken ct = default) =>
        _took.GetOrAdd(questId, named => _sessions.FindChatTookAsync(named, ct));
}
