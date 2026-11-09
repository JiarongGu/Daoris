namespace Daoris.Driver;

/// <summary>
/// The quests an open integration request names that the open list lacks: a unit closed done is on no open list, and its request
/// still holds an overlapping start. The quests are read only where one waits.
/// </summary>
internal static class IntegratingQuests
{
    public static async Task<IReadOnlyList<QuestView>> ReadAsync(
        Snapshot snapshot, IReadOnlySet<string> integrating, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> every, CancellationToken ct = default)
    {
        var missing = integrating.Where(id => !snapshot.Quests.Any(quest => string.Equals(quest.Id, id, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count == 0) return [];
        return [.. (await every(ct).ConfigureAwait(false)).Where(quest => missing.Contains(quest.Id, StringComparer.OrdinalIgnoreCase))];
    }
}
