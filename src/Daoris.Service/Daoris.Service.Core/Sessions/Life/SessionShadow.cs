namespace Daoris.Knowledge;

/// <summary>Where the fold says other than the stored status: both sides, and the last fact the fold read.</summary>
public sealed record ShadowDifference(string Session, SessionState Stored, SessionFolded Fold, SessionFact Last);

/// <summary>
/// One audit of the fold in shadow: the generation it reads in, when (null before the first), how many it folded, its differences,
/// the journal rows it refused, and its time.
/// </summary>
public sealed record ShadowReading(
    long Generation, DateTimeOffset? Audited, int Folded, IReadOnlyList<ShadowDifference> Differences, IReadOnlyList<UnreadableFact> Unreadable,
    TimeSpan Took)
{
    /// <summary>Before any tick audited.</summary>
    public static readonly ShadowReading None = new(0, null, 0, [], [], TimeSpan.Zero);
}

/// <summary>
/// The fold in shadow beside the stored status, audited on each tick of the watch: every record the journal or the status says is
/// live is folded from its last fact against the leases the tick's markers prove, the writer's generation is marked seen, and the reading
/// is kept as the last audit the list reads (<see cref="Last"/>), so a read of the list folds nothing. Each difference is said once
/// (<see cref="Said"/>), with the session's whole journal, until the session's difference changes or ends; each row the journal holds that
/// this build cannot read is said once too (<see cref="Refused"/>). What it remembers is bounded by the sessions it folds. Nothing acts on it.
/// </summary>
public sealed class SessionShadow(SessionStore sessions, TimeProvider clock)
{
    private readonly Dictionary<string, string> _said = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private ShadowReading _last = ShadowReading.None;

    /// <summary>Told each difference the first time it is found, with the session's whole journal: the machine log's line.</summary>
    public Action<ShadowDifference, IReadOnlyList<SessionFact>>? Said { get; init; }

    /// <summary>Told each journal row this build cannot read, the first time an audit meets it.</summary>
    public Action<UnreadableFact>? Refused { get; init; }

    /// <summary>The last audit; <see cref="ShadowReading.None"/> before the first.</summary>
    public ShadowReading Last
    {
        get
        {
            lock (_gate) return _last;
        }
    }

    /// <summary>Audit every shadowed record against <paramref name="leases"/>, now, for a tick whose driver writes in <paramref name="writer"/>.</summary>
    public async Task<ShadowReading> AuditAsync(IReadOnlyList<SessionLease> leases, long writer, CancellationToken ct = default)
    {
        var began = clock.GetTimestamp();
        var now = clock.GetUtcNow();
        var (shadowed, unreadable) = await sessions.Journal.ShadowedAsync(ct).ConfigureAwait(false);
        var generation = await sessions.Generations.CurrentAsync(ct).ConfigureAwait(false);
        if (writer > 0) await sessions.Generations.SeenAsync(writer, now, ct).ConfigureAwait(false);
        var leased = leases
            .GroupBy(lease => lease.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SessionLease>)[.. group], StringComparer.Ordinal);

        var differences = new List<ShadowDifference>();
        foreach (var each in shadowed)
        {
            if (SessionFold.Of(each.Last, now, leased.GetValueOrDefault(each.Session) ?? []) is not { } folded || !SessionFold.Differs(folded, each.Stored)) continue;
            differences.Add(new ShadowDifference(each.Session, each.Stored, folded, each.Last!));
        }

        var reading = new ShadowReading(generation, now, shadowed.Count, differences, unreadable, clock.GetElapsedTime(began));
        await SayAsync(reading, ct).ConfigureAwait(false);
        lock (_gate) _last = reading;
        return reading;
    }

    /// <summary>Say what this audit found first: each new difference with its whole journal, each new unreadable row; forget the rest.</summary>
    private async Task SayAsync(ShadowReading reading, CancellationToken ct)
    {
        List<ShadowDifference> first;
        List<UnreadableFact> refused;
        lock (_gate)
        {
            first = [.. reading.Differences.Where(difference => Remember(difference.Session, Key(difference)))];
            refused = [.. reading.Unreadable.Where(row => Remember($"!{row.Session}", $"{row.Position}|{row.Column}"))];
            var kept = reading.Differences.Select(difference => difference.Session)
                .Concat(reading.Unreadable.Select(row => $"!{row.Session}"))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var gone in _said.Keys.Where(key => !kept.Contains(key)).ToList()) _said.Remove(gone);
        }

        foreach (var row in refused) Refused?.Invoke(row);
        if (Said is not { } said || first.Count == 0) return;
        var journals = await sessions.Journal.OfManyAsync([.. first.Select(difference => difference.Session)], ct).ConfigureAwait(false);
        foreach (var difference in first) said(difference, journals.Journals.GetValueOrDefault(difference.Session) ?? [difference.Last]);
    }

    private bool Remember(string key, string value)
    {
        if (_said.TryGetValue(key, out var before) && before == value) return false;
        _said[key] = value;
        return true;
    }

    private static string Key(ShadowDifference difference) =>
        $"{difference.Stored}|{difference.Fold.State}|{difference.Fold.Outcome}|{difference.Fold.Breach}|{difference.Last.Position}";
}
