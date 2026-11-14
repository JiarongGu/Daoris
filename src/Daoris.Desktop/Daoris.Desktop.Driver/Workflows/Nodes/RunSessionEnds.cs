using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// What a run's end writes in one step: the Done of each of its quests standing at a submission, where the run came to its graph's end, and
/// the end of the sessions waiting on it.
/// </summary>
/// <param name="Quests">The run's quests here closed Done, in its chain's order; none where it ended any other way.</param>
public sealed record RunFinish(string Run, IReadOnlyList<RunEndQuest> Quests, IReadOnlyList<string> Sessions, string State, string Note);

/// <summary>
/// A run's end ends the sessions that wait on its steps, and writes its quests' Done: a session is the work's passage through its workflow,
/// so only the workflow's end ends it, and the agent's done is its submission. A run that came to its graph's end closes its submitted
/// quests and ends its sessions completed; one that ended any other way (failed at a step, stopped) leaves its work abandoned, its quests
/// taken, and ends them stopped. The service moves only a session still awaiting a step, so one working again is never moved.
/// </summary>
public static class RunSessionEnds
{
    /// <summary>The state a run's end moves its waiting sessions to.</summary>
    public static string StateOf(RunEnd ended) => ended.Outcome == RunOutcomes.Completed ? "completed" : "stopped";

    /// <summary>The note it moves them with: the run's end, in the run's words.</summary>
    public static string NoteOf(string run, RunEnd ended) =>
        ended.Outcome == RunOutcomes.Completed
            ? $"Its workflow's run `{run}` came to its end, and the session with it."
            : $"Its workflow's run `{run}` ended {ended.Outcome}{(ended.Node is { } node ? $" at `{node}`" : "")}, so the session ended with it.";

    /// <summary>The quests a completed run's end closes: each of <paramref name="quests"/> standing at its submission, in order.</summary>
    public static IReadOnlyList<RunEndQuest> QuestsOf(RunEnd ended, IEnumerable<QuestView> quests) =>
        ended.Outcome != RunOutcomes.Completed
            ? []
            : [.. quests.Where(quest => QuestResults.Submitted(quest)).Select(quest => new RunEndQuest(quest.Id, quest.Submission!.Machine, quest.Submission.Sequence))];

    /// <summary>Sends the run's end through <paramref name="finish"/> once, saying what it wrote or why it wrote nothing.</summary>
    public static async Task FinishAsync(
        Func<RunFinish, CancellationToken, Task<RunEndResponse?>>? finish, string run, RunEnd ended, IEnumerable<QuestView> quests,
        IEnumerable<string> sessions, List<string> said, CancellationToken ct)
    {
        if (finish is null) return;
        var end = new RunFinish(run, QuestsOf(ended, quests), [.. sessions.Distinct(StringComparer.Ordinal)], StateOf(ended), NoteOf(run, ended));
        if (end.Quests.Count == 0 && end.Sessions.Count == 0) return;
        var answer = await finish(end, ct).ConfigureAwait(false);
        said.Add(answer switch
        {
            null => $"run   {run}: its end was not written, so its quests stay taken and its sessions wait; the person's done closes them.",
            { Refused: { } refused } => $"run   {run}: its quests were not closed ({refused}), so its sessions wait: {answer.Message}",
            _ => $"run   {run}: {answer.Message}",
        });
    }
}
