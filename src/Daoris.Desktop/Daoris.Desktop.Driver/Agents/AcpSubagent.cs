namespace Daoris.Driver;

/// <summary>
/// Routes an update by whose it is (CONSOLE2): a subagent speaks under its own session id, and a client that reads every update as
/// the session's merges the child into the parent. A subagent's update is taken here and goes no further.
/// </summary>
[TurnStage("subagent", TurnPhase.Update, "heard")]
internal sealed class AcpSubagent(ITurnWiring wiring) : ITurnStage
{
    public ValueTask<bool> RunAsync(TurnFrame frame) =>
        ValueTask.FromResult(wiring.Beside is { } beside && beside.Take(frame.From, wiring.SessionId, frame.Update));
}
