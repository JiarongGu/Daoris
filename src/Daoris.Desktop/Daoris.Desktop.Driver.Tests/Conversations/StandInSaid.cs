using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// A record's <c>said</c> as a stand-in (COWORK1c): each word appended in the order its keep arrives, under the record's own id, kept
/// once by its key, and taken off by id. A keep can be held after its append and before its answer (<see cref="Answering"/>), which
/// is how two writes return out of order; a take-off can be held too (<see cref="TakingOff"/>).
/// </summary>
internal sealed class StandInSaid : ISaidDoor
{
    private readonly object _gate = new();
    private int _ids;

    /// <summary>The words waiting, in the record's order.</summary>
    public List<SaidWordView> Said { get; } = [];

    /// <summary>Every id taken off, in the order the take-offs arrived.</summary>
    public List<string> TakenOff { get; } = [];

    /// <summary>Held after the word is appended and before its answer returns; null answers at once.</summary>
    public Func<ChatMessage, Task>? Answering { get; set; }

    /// <summary>Held before a take-off lands; null lands at once.</summary>
    public Func<IReadOnlyList<string>, Task>? TakingOff { get; set; }

    /// <summary>A refusal every keep is answered with; null keeps.</summary>
    public string? Refusing { get; set; }

    public IReadOnlyList<string> Ids
    {
        get
        {
            lock (_gate) return [.. Said.Select(word => word.Id!)];
        }
    }

    public async Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct)
    {
        if (Refusing is { } refusal) return new SayAnswer(false, $"refused: {refusal}") { Refusal = refusal };

        SaidWordView kept;
        lock (_gate)
        {
            var key = word.Key ?? word.Daoris?.Key;
            if (key is not null && Said.FirstOrDefault(each => each.Key == key) is { } waiting) return new SayAnswer(true, "again") { Word = waiting };

            kept = new SaidWordView($"w{++_ids}", word.Text, DateTimeOffset.UnixEpoch, [], false)
            {
                Key = key, Level = word.Level, From = word.Daoris is null ? null : DaorisWords.From,
            };
            Said.Add(kept);
        }

        if (Answering is { } answering) await answering(word).ConfigureAwait(false);
        return new SayAnswer(true, "kept") { Word = kept };
    }

    /// <summary>Every id marked attempted, in the order the marks arrived (COWORK1c).</summary>
    public List<string> Attempted { get; } = [];

    public Task<bool> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        lock (_gate)
        {
            Attempted.AddRange(ids);
            for (var at = 0; at < Said.Count; at++)
            {
                if (ids.Contains(Said[at].Id!) && Said[at].Attempt is null) Said[at] = Said[at] with { Attempt = DateTimeOffset.UnixEpoch };
            }
        }

        return Task.FromResult(true);
    }

    public async Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (TakingOff is { } taking) await taking(ids).ConfigureAwait(false);
        lock (_gate)
        {
            TakenOff.AddRange(ids);
            Said.RemoveAll(word => ids.Contains(word.Id!));
        }

        return true;
    }
}
