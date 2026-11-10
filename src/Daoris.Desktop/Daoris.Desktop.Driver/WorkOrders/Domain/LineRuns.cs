namespace Daoris.Driver;

/// <summary>
/// The earlier parts of a line that are a new quest's own run (<see cref="QuestRuns"/>), read from the quests the look holds, open and closed:
/// the only parts whose tree the quest may go into, so two runs never work one branch. A part whose quest the look does not hold is a run of
/// its own.
/// </summary>
internal static class LineRuns
{
    /// <summary>The parts of <paramref name="parts"/> that are <paramref name="quest"/>'s run, in their order.</summary>
    public static IReadOnlyList<PriorSession> Own(QuestView quest, IReadOnlyList<PriorSession> parts, Snapshot snapshot)
    {
        var quests = snapshot.Quests.Concat(snapshot.Closed).Append(quest)
            .GroupBy(each => each.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(same => same.Key, same => same.First(), StringComparer.OrdinalIgnoreCase);
        QuestView? Find(string id) => quests.GetValueOrDefault(id.TrimStart('#'));
        var run = QuestRuns.Of(quest, Find);
        return [.. parts.Where(part => part.Quest is { Length: > 0 } id && Find(id) is { } view
            && string.Equals(QuestRuns.Of(view, Find), run, StringComparison.OrdinalIgnoreCase))];
    }
}
