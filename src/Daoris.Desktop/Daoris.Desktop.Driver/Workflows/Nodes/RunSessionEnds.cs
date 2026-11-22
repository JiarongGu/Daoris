using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// What a run's end writes in one step: the Done of each of its quests standing at a submission, where the run came to its graph's end, and
/// the end of the sessions waiting on it.
/// </summary>
/// <param name="Quests">The run's quests here closed Done, in its chain's order; none where it ended any other way.</param>
/// <param name="Sessions">The sessions it ends: those found waiting on the run at the write (<see cref="RunWaiters"/>).</param>
public sealed record RunFinish(string Run, IReadOnlyList<RunEndQuest> Quests, IReadOnlyList<string> Sessions, string State, string Note);

/// <summary>
/// A run's end ends the sessions that wait on its steps, and writes its quests' Done: a session is the work's passage through its workflow,
/// so only the workflow's end ends it, and the agent's done is its submission. A run that came to its graph's end closes its submitted
/// quests (<see cref="RunEndQuests"/>) and ends its sessions completed; one stopped leaves its work abandoned, its quests taken for the
/// person's word, and ends them stopped; one failed at a step ends nothing, since its holder may still settle the step (<see cref="Ends"/>). The service moves only a session still awaiting a step, so one working again is
/// never moved.
/// </summary>
public static class RunSessionEnds
{
    public static bool Ends(RunEnd ended) => ended.Outcome != RunOutcomes.Failed;

    /// <summary>The state a run's end moves its waiting sessions to.</summary>
    public static string StateOf(RunEnd ended) => ended.Outcome == RunOutcomes.Completed ? "completed" : "stopped";

    /// <summary>The note it moves them with: the run's end, in the run's words.</summary>
    public static string NoteOf(string run, RunEnd ended) =>
        ended.Outcome == RunOutcomes.Completed
            ? $"Its workflow's run `{run}` came to its end, and the session with it."
            : ended.Why == QuestDoneRuns.Why
                ? $"Its quest was closed Done outside its workflow's run `{run}`, so Daoris stopped the run{(ended.Node is { } at ? $" at `{at}`" : "")}; the session ended with it, its tree and branch kept."
            : ended.By == NodeSource.Daoris
                ? $"Daoris stopped its workflow's run `{run}`{(ended.Node is { } held ? $" at `{held}`" : "")}: its quest is closed, so what the step waits on can no longer come; the session ended with it."
            : $"Its workflow's run `{run}` ended {ended.Outcome}{(ended.Node is { } node ? $" at `{node}`" : "")}, so the session ended with it.";

    /// <summary>
    /// Sends the run's end through the ports' write, saying what it wrote or why it wrote nothing; one that closes and ends nothing is said
    /// once in the machine log (<see cref="EmptyRunEnds"/>). One the service could not take is kept owed, and asked again at each look until it
    /// is (<see cref="AgainAsync"/>).
    /// </summary>
    /// <param name="passed">Whether the person passed over the run's holds (<see cref="RunEndQuests.Passed"/>).</param>
    public static async Task FinishAsync(
        RunEndPorts ports, string run, RunEnd ended, IEnumerable<QuestView> quests, bool passed, List<string> said, CancellationToken ct)
    {
        if (ports.Finish is null)
        {
            Quiet(ports, run, EmptyRunEnds.NoDoor);
            return;
        }

        // A failure is no end of the work: its step's holder may still settle it, which governs the run again, so nothing is written.
        if (!Ends(ended))
        {
            Quiet(ports, run, EmptyRunEnds.FailedHeld);
            return;
        }

        await SendAsync(ports, new RunFinish(run, RunEndQuests.Of(ended, quests, passed), [], StateOf(ended), NoteOf(run, ended)), said, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Each run's end the service could not take, sent again; one it takes now is owed no more, one it cannot stays owed.</summary>
    public static async Task AgainAsync(RunEndPorts ports, List<string> said, CancellationToken ct)
    {
        if (ports.Finish is null || ports.Owed is null) return;
        foreach (var end in ports.Owed.Read()) await SendAsync(ports, end, said, ct, again: true).ConfigureAwait(false);
    }

    // The sessions are found at each write, the first and every one asked again, by the run each waits on, so a session that came to wait
    // on the run since is ended with it.
    private static async Task SendAsync(RunEndPorts ports, RunFinish end, List<string> said, CancellationToken ct, bool again = false)
    {
        var records = ports.Sessions is null ? [] : await ports.Sessions(ct).ConfigureAwait(false);
        var answer = null as RunEndResponse;
        if (records is not null)
        {
            end = end with { Sessions = RunWaiters.Of(end.Run, records) };
            if (end.Quests.Count == 0 && end.Sessions.Count == 0)
            {
                ports.Owed?.Paid(end.Run);
                Quiet(ports, end.Run, EmptyRunEnds.Nothing);
                return;
            }

            answer = await ports.Finish!(end, ct).ConfigureAwait(false);
        }

        if (answer is null)
        {
            ports.Owed?.Owe(end);
            if (!again) said.Add($"run   {end.Run}: its end was not taken, so its quests stay taken and its sessions wait; it is asked again at the next look.");
            return;
        }

        ports.Owed?.Paid(end.Run);
        if (answer.Refused is { } refusal) Quiet(ports, end.Run, EmptyRunEnds.Refused, refusal);
        else if (answer.Closed.Count == 0 && answer.Ended.Count == 0) Quiet(ports, end.Run, EmptyRunEnds.Unmoved);
        said.Add(answer.Refused is { } refused
            ? $"run   {end.Run}: its quests were not closed ({refused}), so its sessions wait: {answer.Message}"
            : $"run   {end.Run}: {answer.Message}");
    }

    internal static void Quiet(RunEndPorts ports, string run, string reason, string? detail = null)
    {
        if (EmptyRunEnds.Changed(ports.Home, run, reason)) ports.Log?.Invoke(EmptyRunEnds.Event, EmptyRunEnds.Sentence(run, reason, detail));
    }
}
