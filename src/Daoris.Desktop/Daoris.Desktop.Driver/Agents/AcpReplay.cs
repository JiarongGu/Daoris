namespace Daoris.Driver;

/// <summary>
/// A load's replay of the history (ANSWER1a), passed over: the record already holds it, so it is not kept a second time.
/// </summary>
[TurnStage("replay", TurnPhase.Update)]
internal sealed class AcpReplay(ITurnWiring wiring) : ITurnStage
{
    public ValueTask<bool> RunAsync(TurnFrame frame) =>
        ValueTask.FromResult(frame.Frame.TryGetProperty("params", out var parameters) && wiring.Replayed(parameters));
}
