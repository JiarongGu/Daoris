namespace Daoris.Driver;

/// <summary>
/// The loop's start of the session that performs a step (<see cref="StepAgents"/>), staffed as the place's other sessions, its one server alone,
/// never on <c>movedOff</c>, the account a run before ended refused on for its sign-in; it completes at the session's end, saying any sign-in.
/// </summary>
public interface IStepSessions
{
    Task<StepSessionEnd> RunAsync(StepAgentAsk ask, Func<string, ServerEntry> tools, string? movedOff, CancellationToken ct);

    /// <summary>The person's stop of the step: its session stopped, as a running session's stop door stops it.</summary>
    void Stop(string invocation);
}
