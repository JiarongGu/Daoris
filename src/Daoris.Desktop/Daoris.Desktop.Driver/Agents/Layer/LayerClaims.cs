using Lyntai.Agents;
using LyntaiDoor = Lyntai.Agents.AgentDoor;

namespace Daoris.Driver;

/// <summary>
/// A layer door's claims read into Daoris's <see cref="Known{T}"/> (the agent-lyntai design §4.3), never declared beside it: Daoris reads
/// silence as today's behaviour and the layer as not promised, so an unknown claim is stated absent and its fallback is the door's.
/// </summary>
internal static class LayerClaims
{
    /// <summary>
    /// <paramref name="ability"/> as <paramref name="door"/> claims it: supported true and unsupported false, with the claim's source and its
    /// evidence verbatim, applying to <paramref name="doorId"/> (the Daoris door) at the one version the claim names; unknown, or never
    /// named, stated absent.
    /// </summary>
    public static Known<bool> Of(LyntaiDoor door, string doorId, string ability) =>
        door.Abilities.FirstOrDefault(each => string.Equals(each.Name, ability, StringComparison.Ordinal)) is { } claim
        && claim.Support is AgentSupport.Supported or AgentSupport.Unsupported
            ? new Known<bool>(
                claim.Support == AgentSupport.Supported, ByOf(claim.Source), claim.Evidence ?? string.Empty,
                claim.AppliesTo is { Length: > 0 } version ? $"{doorId} {version}" : doorId, IsKnown: true)
            : Known<bool>.Absent;

    private static KnownBy ByOf(AgentAbilitySource source) => source switch
    {
        AgentAbilitySource.Negotiated => KnownBy.Negotiated,
        AgentAbilitySource.Measured => KnownBy.Measured,
        _ => KnownBy.Declared,
    };
}
