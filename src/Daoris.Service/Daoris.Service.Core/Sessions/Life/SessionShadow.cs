namespace Daoris.Knowledge;

/// <summary>A process proved alive for a session, as its machine's markers name it: its session and when its process started.</summary>
public sealed record ShadowLease(string Session, DateTimeOffset Started);

/// <summary>Where the fold says other than the stored status: both sides, and the facts the fold read.</summary>
public sealed record ShadowDifference(string Session, SessionState Stored, SessionFolded Fold, IReadOnlyList<SessionFact> Facts);

/// <summary>What one shadow fold read and found: the generation it folded in, how many it folded, its differences, and its time.</summary>
public sealed record ShadowReading(long Generation, int Folded, IReadOnlyList<ShadowDifference> Differences, TimeSpan Took);

/// <summary>
/// The fold in shadow beside the stored status: each read folds every record the journal or the status says is live
/// (<see cref="SessionJournal"/>), against the leases the caller proved alive, and answers where the two differ. Each difference is
/// said once per process and per pair of sides (<see cref="Said"/>), with both sides and the facts. Nothing acts on it.
/// </summary>
public sealed class SessionShadow(SessionStore sessions, TimeProvider clock)
{
    private readonly TimeProvider _clock = clock;
    private readonly HashSet<string> _said = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Told each difference the first time it is found: the machine log's line.</summary>
    public Action<ShadowDifference>? Said { get; init; }

    /// <summary>Fold every shadowed record against <paramref name="leases"/>, now.</summary>
    public async Task<ShadowReading> ReadAsync(IReadOnlyList<ShadowLease> leases, CancellationToken ct = default)
    {
        var began = _clock.GetTimestamp();
        var now = _clock.GetUtcNow();
        var shadowed = await sessions.Journal.ShadowedAsync(ct).ConfigureAwait(false);
        var journals = await sessions.Journal.OfManyAsync([.. shadowed.Select(each => each.Session)], ct).ConfigureAwait(false);
        var generations = await sessions.Generations.AllAsync(ct).ConfigureAwait(false);
        var generation = generations.Count == 0 ? 0 : generations[^1].Number;
        var leased = leases
            .GroupBy(lease => lease.Session, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SessionLease>)[.. group.Select(lease => new SessionLease(lease.Session, AppGenerations.Of(lease.Started, generations)))],
                StringComparer.Ordinal);

        var differences = new List<ShadowDifference>();
        foreach (var (session, stored) in shadowed)
        {
            var facts = journals.GetValueOrDefault(session) ?? [];
            if (SessionFold.Of(facts, now, leased.GetValueOrDefault(session) ?? [], generation) is not { } folded) continue;
            if (!SessionFold.Differs(folded, stored)) continue;
            var difference = new ShadowDifference(session, stored, folded, facts);
            differences.Add(difference);
            if (Said is { } said && FirstTime(difference)) said(difference);
        }

        return new(generation, shadowed.Count, differences, _clock.GetElapsedTime(began));
    }

    private bool FirstTime(ShadowDifference difference)
    {
        var key = $"{difference.Session}|{difference.Stored}|{difference.Fold.State}|{difference.Fold.Outcome}|{difference.Fold.Breach}";
        lock (_gate) return _said.Add(key);
    }
}
