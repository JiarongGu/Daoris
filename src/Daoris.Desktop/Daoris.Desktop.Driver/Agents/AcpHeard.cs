namespace Daoris.Driver;

/// <summary>
/// The agent went on after an error answered the turn before a word (USERSHOWN1): its words, thinking, calls or plan are that word's
/// turn, so it was taken. A meter or a mode said meanwhile is nobody's answer.
/// </summary>
[TurnStage("heard", TurnPhase.Update, "watch")]
internal sealed class AcpHeard(ITurnWiring wiring) : ITurnStage
{
    public ValueTask<bool> RunAsync(TurnFrame frame)
    {
        if (frame.IsOwn(wiring.SessionId)
            && AcpRender.StringField(frame.Update, "sessionUpdate") is { } kind
            && (kind.StartsWith("agent_", StringComparison.Ordinal) || kind.StartsWith("tool_call", StringComparison.Ordinal) || kind == "plan"))
        {
            wiring.Heard();
        }

        return ValueTask.FromResult(false);
    }
}
