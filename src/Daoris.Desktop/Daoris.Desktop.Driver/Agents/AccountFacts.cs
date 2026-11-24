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
/// <param name="SignIn">Its sign-in state (<see cref="SignInStates"/>): one whose check did not answer goes last.</param>
/// <param name="Plan">The plan its agent last named for it (<c>max_20x</c>, <c>team</c>), as the agent said it; null where none did.</param>
/// <param name="Size">The plan's size in its agent's base plans (<see cref="PoolFacts.PlanSizes"/>); null where none is declared.</param>
public sealed record AccountFacts(
    int Running = 0, DateTimeOffset? LastStarted = null, long? Chosen = null, AccountSaid? Said = null, AccountProjection? Projected = null,
    SignInState? SignIn = null, string? Plan = null, double? Size = null)
{
    /// <summary>What the walk multiplies a window's room by, to weigh it in the plan's units: the size, else one (the base plan).</summary>
    public double Units => Size is { } size and > 0 ? size : 1;
}
