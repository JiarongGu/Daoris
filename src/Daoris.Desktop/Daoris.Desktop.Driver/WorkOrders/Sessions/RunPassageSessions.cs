namespace Daoris.Driver;

/// <summary>One step of a run's bound graph version as its session's row draws it: passed, current, to come, or failed.</summary>
/// <param name="Mark">One of the run passage reader's marks: <c>passed</c>, <c>current</c>, <c>next</c> or <c>failed</c>.</param>
public sealed record PassageStep(string Node, string Kind, string Mark);

/// <summary>A run's passage through its bound workflow version: its steps in the graph's order, each with its mark.</summary>
public sealed record RunPassage(string Run, string Workflow, int? Version, IReadOnlyList<PassageStep> Steps);

/// <summary>The session each run's passage is drawn on in Sessions: the newest record that served a quest of the run, as its waits are.</summary>
internal static class RunPassageSessions
{
    /// <summary>Each such session's id, with its run's passage.</summary>
    public static IReadOnlyDictionary<string, RunPassage> Of(SessionLook look)
    {
        var drawn = new Dictionary<string, RunPassage>(StringComparer.Ordinal);
        if (look.Passages.Count == 0) return drawn;
        var byRun = look.Passages.GroupBy(passage => passage.Run, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var newest = new Dictionary<string, SessionRecord>(StringComparer.Ordinal);
        foreach (var record in look.Records.Where(record => !record.Teammate && record.Quest is { Length: > 0 }))
        {
            var run = quests.TryGetValue(record.Quest!, out var quest)
                ? QuestRuns.Of(quest, id => quests.GetValueOrDefault(id.TrimStart('#')))
                : record.Quest!;
            if (!byRun.ContainsKey(run)) continue;
            if (!newest.TryGetValue(run, out var kept) || record.Created > kept.Created
                || (record.Created == kept.Created && string.CompareOrdinal(record.Id, kept.Id) > 0))
            {
                newest[run] = record;
            }
        }

        foreach (var (run, record) in newest) drawn[record.Id] = byRun[run];
        return drawn;
    }
}
