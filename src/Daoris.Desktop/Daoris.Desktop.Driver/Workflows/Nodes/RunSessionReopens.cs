using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>What a run asks of the service for its records ended done under the older meaning: the sessions, and what they wait on.</summary>
public sealed record RunReopen(string Run, IReadOnlyList<string> Sessions, Noted Note);

/// <summary>
/// A run still going on whose work ended done under the older meaning, where the agent's done ended the session and closed the quest,
/// carries that record on under the newer one: its sessions wait on the run's next step, so the run's end ends them. Asked once a process,
/// at the run's first look; the service moves only a record ended done that never waited on a step, so a record ended since by its
/// workflow or by the person stays as it is.
/// </summary>
public static class RunSessionReopens
{
    /// <summary>
    /// Asks <paramref name="reopen"/> once for run <paramref name="run"/>, not ended, whose quests here stand done or at a submission:
    /// <paramref name="sessions"/> wait on <paramref name="next"/>, its next step where one stands. A run answered is not asked again
    /// this process (<paramref name="asked"/>); one the service could not be asked is asked at the next look.
    /// </summary>
    public static async Task AskAsync(
        Func<RunReopen, CancellationToken, Task<RunReopenResponse?>>? reopen, HashSet<string> asked, string run, RunEnd? ended,
        IEnumerable<QuestView> quests, IEnumerable<string> sessions, string? next, List<string> said, CancellationToken ct)
    {
        if (reopen is null || ended is not null || asked.Contains(run)) return;
        if (!quests.Any(quest => quest.Status == "Done" || QuestResults.Submitted(quest))) return;
        var named = sessions.Distinct(StringComparer.Ordinal).ToList();
        if (named.Count == 0) return;
        var answer = await reopen(new RunReopen(run, named, new StepWait(run, next).Note()), ct).ConfigureAwait(false);
        if (answer is null) return;
        asked.Add(run);
        if (answer.Reopened.Count > 0) said.Add($"run   {run}: {answer.Message}");
    }
}
