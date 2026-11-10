using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// The delegate keys of the sessions this process started, as hashes in memory by session: one drawn at each start that names the
/// session's record (<see cref="Hand"/>, where its id is set), never written anywhere, and forgotten as the session's process ends
/// (<see cref="SessionProcesses"/>) or its access changes. A session may hold several at once, each start's own: a run composes its start
/// once to spawn and once to read what it asked, and a start beside a live one keeps both.
/// </summary>
public sealed class SessionDelegates
{
    private readonly ConcurrentDictionary<string, ImmutableList<(byte[] Hash, long Drawn)>> _held = new(StringComparer.OrdinalIgnoreCase);
    private long _drawn;

    /// <summary>How many keys this process has drawn: a process tracked at this count ends the keys drawn up to it (<see cref="Forget(string, long)"/>).</summary>
    public long Drawn => Interlocked.Read(ref _drawn);

    /// <summary>A new key for <paramref name="session"/>, in hex, its hash kept for it.</summary>
    public string Draw(string session)
    {
        var key = HolderKey.Draw();
        var held = (HolderKey.Hash(key)!, Interlocked.Increment(ref _drawn));
        _held.AddOrUpdate(session, _ => [held], (_, kept) => kept.Add(held));
        return key;
    }

    /// <summary>
    /// The start's identity handed its key: where it names its session's record, a key drawn for that session beside it; one that names
    /// none is handed none.
    /// </summary>
    public void Hand(IDictionary<string, string?> identity)
    {
        if (identity.TryGetValue(ChildMarks.SessionVariable, out var session) && session is { Length: > 0 }) identity[DelegateKey.Variable] = Draw(session);
    }

    /// <summary>The hashes held for <paramref name="session"/>: none where it runs nothing here, or its keys were forgotten.</summary>
    public IReadOnlyList<byte[]> Of(string session) =>
        _held.TryGetValue(session, out var held) ? [.. held.Select(each => each.Hash)] : [];

    /// <summary>Every key of <paramref name="session"/> forgotten: its access changed, so its next start is handed a new one.</summary>
    public void Forget(string session) => _held.TryRemove(session, out _);

    /// <summary>
    /// The keys of <paramref name="session"/> drawn up to <paramref name="upTo"/> forgotten, as its last process ends; one drawn since that
    /// process is tracked is a start still coming, and is kept.
    /// </summary>
    public void Forget(string session, long upTo)
    {
        while (_held.TryGetValue(session, out var held))
        {
            var left = held.RemoveAll(each => each.Drawn <= upTo);
            if (left.Count == 0 ? _held.TryRemove(new KeyValuePair<string, ImmutableList<(byte[], long)>>(session, held)) : _held.TryUpdate(session, left, held)) return;
        }
    }
}
