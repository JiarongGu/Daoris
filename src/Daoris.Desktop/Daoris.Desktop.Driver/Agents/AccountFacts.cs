namespace Daoris.Driver;

/// <summary>
/// What Daoris knows of one account (TOOL6b, D130 §16.4), and what its agent last said about its windows, where it said
/// anything (TOOL6c, §5.2): null is unknown, never spent and never fresh.
/// </summary>
/// <remarks>
/// <para>It holds no weekly reset a limit told (USAGERESET1): that says when a week turns over and not how much of it would lapse,
/// which only a reading of its use says (<see cref="UsageCurves.Lapsing"/>).</para>
/// <para><see cref="Projected"/> is its use projected to now (USAGEPROBE1), set only for the start's line, which says it as
/// projected: the walk ranks on <see cref="Said"/> alone, as near and over are a reading's (D130 point 9, D179 point 7).</para>
/// </remarks>
public sealed record AccountFacts(
    int Running = 0, DateTimeOffset? LastStarted = null, long? Chosen = null, AccountSaid? Said = null, AccountProjection? Projected = null);
