using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Daoris.Contracts;
using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The widest list as its change since a mark this door answered: the list made as <see cref="SessionListAnswers"/> makes it, and each
/// record whose written answer differs from the one at the mark. A record's entry, its delete and its line's head move with records
/// beside it, so the change is read from the answers themselves, never from which rows were written.
/// </summary>
/// <remarks>
/// The last few answers are held, each numbered as it is first asked from; the number is this process's, so a mark from before a
/// restart, or one past the few, is answered every record.
/// </remarks>
public sealed class SessionListChanges(SessionListAnswers list)
{
    private const int Held = 4;

    private static readonly JsonTypeInfo<SessionResponse> Written =
        (JsonTypeInfo<SessionResponse>)ContractJson.Options.GetTypeInfo(typeof(SessionResponse));

    private readonly string _epoch = Guid.NewGuid().ToString("N")[..8];
    private readonly Lock _gate = new();
    private readonly Dictionary<bool, List<Made>> _made = [];
    private long _numbered;

    private sealed record Made(string Mark, SessionResponse[] Answer, Dictionary<string, string> Records);

    public async Task<SessionChangesResponse> SinceAsync(string? since, bool local, CancellationToken ct)
    {
        var answer = await list.ListAsync(null, true, null, local, ct);
        var now = Find(local, made => ReferenceEquals(made.Answer, answer)) ?? Keep(local, answer);
        var before = since is null ? null : Find(local, made => made.Mark == since);
        var order = answer.Select(record => record.Id).ToArray();
        if (before is null) return new SessionChangesResponse(now.Mark, order, answer, Whole: true);

        var changed = answer
            .Where(record => !before.Records.TryGetValue(record.Id, out var was) || was != now.Records[record.Id])
            .ToArray();
        return new SessionChangesResponse(now.Mark, order, changed, Whole: false);
    }

    private Made? Find(bool local, Func<Made, bool> which)
    {
        lock (_gate) return _made.TryGetValue(local, out var made) ? made.LastOrDefault(which) : null;
    }

    private Made Keep(bool local, SessionResponse[] answer)
    {
        // Each record written apart, at once: three thousand written one after another were most of a change's answer.
        var written = new string[answer.Length];
        Parallel.For(0, answer.Length, at => written[at] = JsonSerializer.Serialize(answer[at], Written));
        var records = new Dictionary<string, string>(answer.Length, StringComparer.Ordinal);
        for (var at = 0; at < answer.Length; at++) records[answer[at].Id] = written[at];
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
