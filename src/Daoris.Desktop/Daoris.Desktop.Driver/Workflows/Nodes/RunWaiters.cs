namespace Daoris.Driver;

/// <summary>
/// The sessions waiting on a run's steps, the ones its end ends: this machine's records awaiting a step whose note names the run
/// (<see cref="StepWaits.From"/>). Found by the run they wait on, not by the quest or branch they served, so a part whose quest is no
/// longer the run's newest, or whose branch the line holds, is still the run's; a teammate's record is another machine's to end.
/// </summary>
public static class RunWaiters
{
    /// <summary>The ids of <paramref name="records"/> waiting on <paramref name="run"/>, in their order.</summary>
    public static IReadOnlyList<string> Of(string run, IEnumerable<SessionRecord> records) =>
        [.. records.Where(record => !record.Teammate && record.State == SessionStates.AwaitingStep
                && string.Equals(StepWaits.From(record.NoteParts)?.Run, run, StringComparison.Ordinal))
            .Select(record => record.Id).Distinct(StringComparer.Ordinal)];
}
