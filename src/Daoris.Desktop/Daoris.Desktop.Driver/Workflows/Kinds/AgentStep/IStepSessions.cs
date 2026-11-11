namespace Daoris.Driver;

/// <summary>
/// The loop's start of the session that performs a step (<see cref="StepAgents"/>): staffed as the place's other sessions are, at the work's
/// place with no tree of its own, handed its one server alone. Completes when the session has ended, however it ended.
/// </summary>
public interface IStepSessions
{
    /// <param name="tools">The step's server for the session's record once it is opened, named by its id.</param>
    Task RunAsync(StepAgentAsk ask, Func<string, ServerEntry> tools, CancellationToken ct);

    /// <summary>The person's stop of the step: its session stopped, as a running session's stop door stops it.</summary>
    void Stop(string invocation);
}
