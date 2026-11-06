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
}
