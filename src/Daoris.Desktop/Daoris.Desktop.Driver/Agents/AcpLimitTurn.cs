using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The message the agent's last step said, kept as its chunks arrive, for the run to read at each turn's end
/// (<see cref="Ended"/>): Claude Code's adapter ends a turn on its account's limit by saying the limit's sentence as that message. A call
/// or a thought after it is a step of the agent's own, so the turn ended on no message.
/// </summary>
/// <remarks>After <c>render</c>, so a replay or a subagent's update, which the stages before it take, is never read here.</remarks>
[TurnStage("limit", TurnPhase.Update, "render")]
internal sealed class AcpLimitTurn(ITurnWiring wiring) : ITurnStage
{
    private readonly object _gate = new();
    private readonly StringBuilder _words = new();
    private string? _message;
    private bool _last;

    public ValueTask<bool> RunAsync(TurnFrame frame)
    {
        if (!frame.IsOwn(wiring.SessionId)) return ValueTask.FromResult(false);

        var kind = AcpRender.Kind(frame.Update);
        lock (_gate)
        {
            if (kind == "agent_message_chunk" && AcpRender.Map(frame.Update) is { Text: { } text, Id: var id })
            {
                if (!_last || id is not null && id != _message) _words.Clear();
                _words.Append(text);
                _message = id ?? _message;
                _last = true;
            }
            else if (kind is "tool_call" or "agent_thought_chunk" or "plan")
            {
                _last = false;
            }
        }

        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// The turn just ended on its account's limit, read by <paramref name="said"/> from the agent's last message, as the run's failure;
    /// null where no reader is handed, or the turn ended otherwise.
    /// </summary>
    public AcpTurnLimited? Ended(Func<string, bool>? said)
    {
        string? words;
        lock (_gate) words = _last ? _words.ToString() : null;
        return said is not null && words is { Length: > 0 } && said(words) ? new AcpTurnLimited(words) : null;
    }
}
