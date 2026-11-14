namespace Daoris.Driver;

/// <summary>
/// A run's end ends the sessions that wait on its steps: a session is the work's passage through its workflow, so only the workflow's end
/// ends it. A run that came to its graph's end ends them completed; one that ended any other way (failed at a step, stopped) leaves its work
/// abandoned, and ends them stopped. Each move names <c>awaiting-step</c> as the state it expects, so a session that is working again, or ended
/// some other way, is never moved.
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

    /// <summary>Moves each of <paramref name="sessions"/> that waits on a step to its end through <paramref name="finish"/>, saying each it moved.</summary>
    public static async Task FinishAsync(
        Func<string, string, string, CancellationToken, Task<bool>>? finish, string run, RunEnd ended, IEnumerable<string> sessions, List<string> said,
        CancellationToken ct)
    {
        if (finish is null) return;
        var (state, note) = (StateOf(ended), NoteOf(run, ended));
        foreach (var session in sessions.Distinct(StringComparer.Ordinal))
        {
            if (await finish(session, state, note, ct).ConfigureAwait(false)) said.Add($"run   {run}: session {session} ended {state} with its workflow's run.");
        }
    }
}
