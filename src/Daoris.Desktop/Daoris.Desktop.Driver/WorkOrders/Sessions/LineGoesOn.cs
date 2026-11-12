namespace Daoris.Driver;

/// <summary>
/// Whether the line a part belongs to goes on past that part's end: a later part of it that has not ended, or a co-worker serving it that
/// has not. An end of such a part is no <i>Ended</i> or <i>Failed</i> notice of the line, which has not ended; the line's own state says
/// what it is doing (<see cref="LineStandings"/>).
/// </summary>
public static class LineGoesOn
{
    private static readonly string[] Closed = ["completed", "declined", "stooddown", "failed", "stopped"];

    /// <summary>Whether <paramref name="session"/>'s line goes on, read from the records held; false for a session none of them holds.</summary>
    public static bool Of(string session, IReadOnlyList<SessionRecord> records)
    {
        var priors = AtWorkExchangeReading.Priors(records);
        if (!priors.TryGetValue(session, out var ended)) return false;

        var key = LineKeys.Of(session, priors);
        var after = ended.Created ?? DateTimeOffset.MinValue;
        var held = records.DistinctBy(record => record.Id, StringComparer.Ordinal).ToList();
        var parts = LineKeys.Members(key, priors).Select(member => member.Session).ToHashSet(StringComparer.Ordinal);
        return held.Any(record => !Ended(record) && record.Id != session && (
            (parts.Contains(record.Id) && record.Created > after)
            || (LineStandings.Served(record, priors) is { } serves && parts.Contains(serves))));
    }

    /// <summary>
    /// Whether <paramref name="told"/> is the end of a part whose line goes on: no notice of the line. False for a park, and where the
    /// records cannot be read this moment, which tells the end as before.
    /// </summary>
    public static async Task<bool> OfNewsAsync(ServiceClient service, NewsTold told, CancellationToken ct)
    {
        if (told.Kind != AttentionKind.Ended) return false;
        try
        {
            return Of(told.Session, await service.SessionList.AllAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private static bool Ended(SessionRecord record) => Closed.Contains(record.State.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant());
}
