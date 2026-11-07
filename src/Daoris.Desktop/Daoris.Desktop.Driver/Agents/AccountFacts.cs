namespace Daoris.Driver;

/// <summary>
/// What Daoris knows of one account, and what its agent last said about its windows, where it said
/// anything: null is unknown, never spent and never fresh.
/// </summary>
/// <remarks>
/// <para>It holds no weekly reset a limit told: that says when a week turns over and not how much of it would lapse,
/// which only a reading of its use says (<see cref="UsageCurves.Lapsing"/>).</para>
/// <para><see cref="Projected"/> is its use projected to now, set only for the start's line, which says it as
/// projected: the walk ranks on <see cref="Said"/> alone, as near and over are a reading's.</para>
/// </remarks>
public sealed record AccountFacts(
    int Running = 0, DateTimeOffset? LastStarted = null, long? Chosen = null, AccountSaid? Said = null, AccountProjection? Projected = null);
