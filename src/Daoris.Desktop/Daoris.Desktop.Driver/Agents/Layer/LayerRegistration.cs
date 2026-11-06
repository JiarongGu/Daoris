using Lyntai;

namespace Daoris.Driver;

/// <summary>
/// One layer-run door's registration as the layer's builder takes it (the agent-lyntai design §4.2, §5.5): the door's id as Daoris's
/// registry spells it, which is the layer provider's id, and the registration's call, its pinned command among it.
/// </summary>
internal sealed record LayerRegistration(string Door, Action<LyntaiBuilder> Register)
{
    /// <summary>The layer's entries (<c>AgentFailure.By</c>) this door spells a refused model with: the door's model-refusal entries (§5.3, round 5).</summary>
    public IReadOnlySet<string> ModelRefusals { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The door's own reading of a tool call, its title and kind as the door's native reader shows the same call; null shows a call by its
    /// name alone, as a door with no vocabulary of its tools.
    /// </summary>
    public Func<Lyntai.Agents.ToolCall, SessionEvent>? Steps { get; init; }

    /// <summary>
    /// The door's own options type over the turn's prompt, holding what the start's carriers hand it (its rules file and its servers file,
    /// §5.6); null takes the layer's own options, carrying neither.
    /// </summary>
    public Func<string, RunCarriers?, Lyntai.Agents.AgentSessionOptions>? Options { get; init; }
}
