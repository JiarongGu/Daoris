namespace Daoris.Driver;

/// <summary>
/// The loop's start of the session that performs a step (<see cref="StepAgents"/>): staffed as the place's other sessions are, at the work's
/// place with no tree of its own, handed its one server alone. Completes when the session has ended, however it ended, saying whether
/// the end was an account's sign-in.
/// </summary>
public interface IStepSessions
{
    /// <param name="movedOff">The account a run before this one was refused on for its sign-in: a walk that gives it again runs nothing.</param>
    Task<StepSessionEnd> RunAsync(StepAgentAsk ask, Func<string, ServerEntry> tools, string? movedOff, CancellationToken ct);

    /// <summary>The person's stop of the step: its session stopped, as a running session's stop door stops it.</summary>
    void Stop(string invocation);
}
