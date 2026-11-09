namespace Daoris.Driver;

/// <summary>
/// How a handed session's run ends when it does not conclude: the driver closing, a wire that broke after a stop, any other failure,
/// and what every end lets go of. The record must say what the driver saw, even when what it saw is its own failure: an abandoned
/// <c>starting</c> row reads as a session that never ends.
/// </summary>
internal sealed class RunEndings(IRunWiring wiring, RunRound run, string sessionId, string agent, string workTree, bool locksTree)
{
    /// <summary>
    /// A throw between the record's open and the hand, whose own endings are not armed yet: the record ends as the hand's would end it,
    /// closed where the driver is closing, failed otherwise, and its tree lock let go.
    /// </summary>
    public static async Task<StartRun> BeforeHandAsync(IRunWiring wiring, RunRound run, string sessionId, Exception error, CancellationToken ct)
    {
        var tree = run.State.WorkTree;
        var endings = new RunEndings(wiring, run, sessionId, run.State.Agent, tree, wiring.Context.Trees.Holds(tree));
        try
        {
            return error is OperationCanceledException && ct.IsCancellationRequested
                ? await endings.ClosedAsync().ConfigureAwait(false)
                : await endings.FailedAsync(error).ConfigureAwait(false);
        }
        finally
        {
            await endings.LetGoAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The stop made of the session before a close cut its conclusion off, told as its run lets go.</summary>
    public SessionStop? StoppedFirst { get; set; }

    /// <summary>Whether its record still holds its tree as the run ends, waiting on the person or going on at the next look.</summary>
    public bool HoldsTree { get; set; }

    /// <summary>
    /// The person is closing the driver. The process tree is already ended, so the record says so rather than sit at "working" forever;
    /// the write rides an unbound token, since the cancelled one would refuse the very report it caused. Closing is not a decision about
    /// this quest, so a take the session held is carried on at the next start, as a cut-off is; unless a stop came first, which decided
    /// the end and is never interrupted.
    /// </summary>
    public async Task<StartRun> ClosedAsync()
    {
        var quest = run.Quest;
        var (closed, interrupted) = Observation.Closed(StoppedFirst);
        try
        {
            await wiring.Context.Service.AdvanceAsync(
                sessionId, closed.State,
                StoppedFirst is null && wiring.Context.Processes.Close.Build is { } build ? ShellClose.Closed(build) : closed.AsNoted(),
                ct: CancellationToken.None, interrupted: interrupted).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort by construction: the host may already be gone on the same shutdown.
        }

        if (StoppedFirst is { } first)
        {
            return new StartRun(
                $"{closed.State}  session {sessionId} (#{quest.Id} → {quest.To}): {closed.Note}",
                true,
                new SessionEnded(sessionId, quest.To, closed.State, ByPerson: first.ByPerson, closed.Note, Quest: quest.Id, Adapter: agent));
        }

        // The person is closing the driver, so this ending is theirs: no interruption is owed for something they are in the middle of doing.
        return new StartRun(
            $"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.",
            true,
            new SessionEnded(sessionId, quest.To, "stopped", ByPerson: true, Quest: quest.Id, Adapter: agent));
    }

    /// <summary>
    /// A wire that broke after a stop (the process killed under a handshake's write) is that stop's end, not a failure: the stop decided
    /// it, as its conclusion would have, and the person's is never interrupted.
    /// </summary>
    public async Task<StartRun> StoppedAsync()
    {
        var quest = run.Quest;
        var stopped = await wiring.EndStoppedAsync(sessionId, StoppedFirst!).ConfigureAwait(false);

        return new StartRun(
            $"{stopped.State}  session {sessionId} (#{quest.Id} → {quest.To}): {stopped.Note}",
            true,
            new SessionEnded(sessionId, quest.To, stopped.State, ByPerson: StoppedFirst!.ByPerson, stopped.Note, Quest: quest.Id, Adapter: agent));
    }

    /// <summary>
    /// Everything but the shutdown, which includes a client timeout: HttpClient's is an <see cref="OperationCanceledException"/> with
    /// nobody having cancelled, and filtering it out let it escape the tick and leave the record at <c>starting</c>.
    /// </summary>
    public async Task<StartRun> FailedAsync(Exception error)
    {
        var quest = run.Quest;
        try
        {
            await wiring.Context.Service.AdvanceAsync(
                sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The terminal write is best-effort by construction: the first failure is the report, and a host that is gone or slow here
            // must not turn that report into a second throw.
        }

        return new StartRun(
            $"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}",
            true,
            new SessionEnded(sessionId, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id, Adapter: agent));
    }

    /// <summary>
    /// However this ended, the stream is over. The buffer stays readable so whoever is watching can see how it finished, and the
    /// session lets go of its tree before any clean-up of Daoris's removes it; on no token, since a close is one of the ways.
    /// </summary>
    public async Task LetGoAsync()
    {
        wiring.Context.Output?.Close(sessionId);
        if (locksTree && !HoldsTree) await SessionLocks.LetGoAsync(workTree, sessionId, CancellationToken.None).ConfigureAwait(false);
    }
}
