using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Daoris.Contracts;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A widest list as its change since a mark this door answered: the list as its door makes it, and each record whose written answer
/// differs from the one at the mark. What a record answers moves with records beside it (a session's entry, delete and line's head,
/// a quest's delete and its takes), so the change is read from the answers themselves, never from which rows were written.
/// </summary>
/// <remarks>
/// The last few answers are held, each numbered as it is first asked from; the number is this process's, so a mark from before a
/// restart, or one past the few, is answered every record. The list's door keeps its answer by the store's mark, so an answer asked
/// again while nothing moved is the same answer, found here by its reference and written no second time.
/// </remarks>
/// <param name="list">The widest list, to a caller on this machine or not.</param>
/// <param name="id">A record's id, which the order names it by.</param>
public sealed class ListChanges<T>(Func<bool, CancellationToken, Task<T[]>> list, Func<T, string> id)
{
    private const int Held = 4;

    private static readonly JsonTypeInfo<T> Written = (JsonTypeInfo<T>)ContractJson.Options.GetTypeInfo(typeof(T));

    private readonly string _epoch = Guid.NewGuid().ToString("N")[..8];
    private readonly Lock _gate = new();
    private readonly Dictionary<bool, List<Made>> _made = [];
    private long _numbered;

    private sealed record Made(string Mark, T[] Answer, Dictionary<string, string> Records);

    /// <summary>The mark to ask from next, every id in the list's order, the records that moved since <paramref name="since"/>, and whether that is every record.</summary>
    public async Task<(string Mark, string[] Order, T[] Changed, bool Whole)> SinceAsync(string? since, bool local, CancellationToken ct)
    {
        var answer = await list(local, ct);
        var now = Find(local, made => ReferenceEquals(made.Answer, answer)) ?? Keep(local, answer);
        var before = since is null ? null : Find(local, made => made.Mark == since);
        var order = answer.Select(id).ToArray();
        if (before is null) return (now.Mark, order, answer, true);

        var changed = answer
            .Where(record => !before.Records.TryGetValue(id(record), out var was) || was != now.Records[id(record)])
            .ToArray();
        return (now.Mark, order, changed, false);
    }

    private Made? Find(bool local, Func<Made, bool> which)
    {
        lock (_gate) return _made.TryGetValue(local, out var made) ? made.LastOrDefault(which) : null;
    }

    private Made Keep(bool local, T[] answer)
    {
        // Each record written apart, at once: three thousand written one after another were most of a change's answer.
        var written = new string[answer.Length];
        Parallel.For(0, answer.Length, at => written[at] = JsonSerializer.Serialize(answer[at], Written));
        var records = new Dictionary<string, string>(answer.Length, StringComparer.Ordinal);
        for (var at = 0; at < answer.Length; at++) records[id(answer[at])] = written[at];
        lock (_gate)
        {
            if (!_made.TryGetValue(local, out var made)) _made[local] = made = [];
            if (made.LastOrDefault(each => ReferenceEquals(each.Answer, answer)) is { } kept) return kept;
            var fresh = new Made($"{_epoch}.{++_numbered}", answer, records);
            made.Add(fresh);
            if (made.Count > Held) made.RemoveAt(0);
            return fresh;
        }
    }
}
