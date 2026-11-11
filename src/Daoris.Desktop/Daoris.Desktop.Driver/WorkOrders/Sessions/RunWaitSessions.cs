namespace Daoris.Driver;

/// <summary>The session each run waiting on the person stands for in Sessions' groups: the newest record that served a quest of the run, whatever its state.</summary>
internal static class RunWaitSessions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PersonWait>> None = new Dictionary<string, IReadOnlyList<PersonWait>>(StringComparer.Ordinal);

    /// <summary>Each such session's id, with its run's waits in the order the look read them.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PersonWait>> Of(SessionLook look)
    {
        if (look.RunWaits.Count == 0) return None;
        var byRun = look.RunWaits.GroupBy(wait => wait.Run, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PersonWait>)[.. group], StringComparer.Ordinal);
        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var standing = new Dictionary<string, (SessionRecord Record, IReadOnlyList<PersonWait> Waits)>(StringComparer.Ordinal);
        foreach (var record in look.Records.Where(record => !record.Teammate && record.Quest is { Length: > 0 }))
        {
            var run = quests.TryGetValue(record.Quest!, out var quest)
                ? QuestRuns.Of(quest, id => quests.GetValueOrDefault(id.TrimStart('#')))
                : record.Quest!;
            if (!byRun.TryGetValue(run, out var waits)) continue;
            // A step a run ended failed at is work left only for a quest that closed done: a declined or abandoned one left nothing to finish.
            waits = [.. waits.Where(wait => wait.Why != PersonWaits.Left || quest is { Status: "Done" })];
            if (waits.Count == 0) continue;
            if (!standing.TryGetValue(run, out var newest) || Newer(record, newest.Record)) standing[run] = (record, waits);
        }

        return standing.ToDictionary(pair => pair.Value.Record.Id, pair => pair.Value.Waits, StringComparer.Ordinal);
    }

    private static bool Newer(SessionRecord record, SessionRecord than) =>
        record.Created > than.Created || (record.Created == than.Created && string.CompareOrdinal(record.Id, than.Id) > 0);
}
