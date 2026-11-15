namespace Daoris.Driver;

/// <summary>
/// The session a landing supervisor's decision on a step is kept against: the newest of the step's run's work in its repository. A chain's run is
/// served by the sessions of its quests; an ask's delivery run, which has no quest of its own, by those of the quests its ask gave that
/// repository, its review's handbacks (<see cref="AskRunScopes"/>), so an appointed supervisor answers the ask's one review as it answers a
/// chain's (D296).
/// </summary>
public static class LandingSessions
{
    /// <summary>
    /// Whether a quest's work serves a run: its chain's run, read up its parents among <paramref name="quests"/>, or the delivery run in its
    /// repository of the ask that published it. Where the quests are not read, or the quest is not among them, by the quest's own id alone.
    /// </summary>
    public static Func<string, string, bool> Serves(IReadOnlyList<QuestView>? quests)
    {
        var byId = (quests ?? []).GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return (quest, run) =>
        {
            if (!byId.TryGetValue(quest.TrimStart('#'), out var view)) return string.Equals(quest, run, StringComparison.Ordinal);
            if (string.Equals(QuestRuns.Of(view, id => byId.GetValueOrDefault(id.TrimStart('#'))), run, StringComparison.Ordinal)) return true;
            return AskWords.AskOf(view.From) is { } ask && string.Equals(AskRunKey.Of(ask, view.To), run, StringComparison.Ordinal);
        };
    }

    /// <summary>The newest session, never a teammate's, whose quest serves the step's run in the step's repository; null where none does.</summary>
    public static PriorSession? Of(IReadOnlyList<PriorSession> records, LandingStep step, Func<string, string, bool> serves) =>
        records.LastOrDefault(record => !record.Teammate && record.Quest is { Length: > 0 } quest && serves(quest, step.Run)
                                        && string.Equals(record.Repository, step.Repository, StringComparison.OrdinalIgnoreCase));
}
