namespace Daoris.Driver;

/// <summary>
/// Counts each update that is not a replay, and lets the session's own background work and the turns its harness wakes when
/// that work ends read it, before the console takes the same frames for its streams.
/// </summary>
[TurnStage("watch", TurnPhase.Update, "replay")]
internal sealed class AcpWatch(ITurnWiring wiring) : ITurnStage
{
    public ValueTask<bool> RunAsync(TurnFrame frame)
    {
        wiring.Counted();
        if (frame.IsOwn(wiring.SessionId))
        {
            wiring.Background.Take(frame.Update);
            wiring.Wake.Take(frame.Update, wiring.Prompted());
        }

        return ValueTask.FromResult(false);
    }
}
