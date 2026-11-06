using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// What became of a prompt's words, read from what its own turn showed after the write (the agent-lyntai design §5.3, LQ3's per-prompt
/// half): <em>heard</em> is Daoris's classification, not the layer's, so the ingress makes it from the observation the layer states. A tool
/// step shown is uncertain; the agent's words, thinking or a plan, taken; a meter, a background task or nothing, never sent. A session's
/// start the agent announced before the write is none of these, as the layer now states it.
/// </summary>
internal static class LayerDelivery
{
    /// <summary>
    /// The delivery <paramref name="turn"/> shows; null where the layer states none (a terminal built before any turn ran), when the layer's
    /// own stamp stands. A write that did not complete shows nothing after it, so it reads never sent: that the runner saw no line of the
    /// prompt written is the runner's to say.
    /// </summary>
    public static FailureDelivery? Of(AgentTurnEvidence? turn)
    {
        if (turn is null) return null;
        if (turn.Shown.Any(kind => kind is AgentShownKind.ToolCall or AgentShownKind.ToolResult)) return FailureDelivery.Uncertain;
        return turn.Shown.Any(kind => kind is AgentShownKind.Text or AgentShownKind.Thinking or AgentShownKind.Plan)
            ? FailureDelivery.Taken
            : FailureDelivery.NeverSent;
    }
}
