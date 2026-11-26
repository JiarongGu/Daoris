namespace Daoris.Driver;

/// <summary>
/// A taken quest whose submitted work waits at its landing, which is the run's own act and no session's: planned as a sit, so the start stage
/// begins nothing on it at each look and its place under the cap is not spent. A session that failed, or the person's words, still go on.
/// </summary>
internal static class LandingSits
{
    internal static Consideration? Of(QuestView quest, Snapshot snapshot) =>
        QuestSubmissions.Current(quest)
        && snapshot.OwedLandings.Contains(quest.Id)
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && last is { Teammate: false, PersonSaid: false } && !string.Equals(last.State, "failed", StringComparison.OrdinalIgnoreCase)
            ? new(quest, StartVerdict.RepositoryBusy, $"`#{quest.Id}`'s submitted work waits at its landing, which tries again by itself: no session is started for it.")
            {
                Said = new(SittingFact.LandingOwed),
            }
            : null;
}
