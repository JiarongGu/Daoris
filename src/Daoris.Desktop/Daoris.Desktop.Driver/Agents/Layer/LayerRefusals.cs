namespace Daoris.Driver;

/// <summary>
/// A refused model told by its whole outcome on the layer path (the agent-lyntai design §5.3, round 5): a failure the ingress built, fatal,
/// its restriction the model with an owner's action and decided by one of the door's model-refusal entries (<see cref="ModelRefusals.Refuses"/>),
/// then mapped by door and phase (<see cref="ModelRefusals.Path"/>). A subject alone is nothing: the layer scopes a per-model limit and a
/// model at capacity to the model too.
/// </summary>
/// <param name="entries">The layer's entries the door spells a refusal with, as <c>AgentFailure.By</c> has them.</param>
internal sealed class LayerRefusals(IEnumerable<string> entries)
{
    /// <summary>A door with no model-refusal entry, which refuses no model.</summary>
    public static LayerRefusals None { get; } = new([]);

    private readonly HashSet<string> _entries = [.. entries.Select(entry => LayerFailures.Mark + entry)];

    /// <summary>
    /// Whether <paramref name="failure"/>, met at <paramref name="where"/> on <paramref name="door"/>'s transport word, is the ending's refused
    /// model: the native door's failed result under today's guard, a turn not stopped that ran nothing of the session's own. A protocol open's
    /// refusal is the re-ask's, never the ending's flag.
    /// </summary>
    public bool Refused(NormalizedFailure failure, string door, string where, bool ranNothing, bool stopped) =>
        ModelRefusals.Refuses(failure.Kind, failure.By, failure.Restriction, _entries)
        && ModelRefusals.Path(door == "protocol" ? Transport.Protocol : Transport.Native, where, ranNothing, stopped) == ModelRefusalPath.Ending;
}
