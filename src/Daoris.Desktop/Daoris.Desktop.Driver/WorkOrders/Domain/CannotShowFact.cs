namespace Daoris.Driver;

/// <summary>
/// Why the planner sits a review set-up step it cannot show (<see cref="StartVerdict.CannotShow"/>), as a fact the page words: a
/// code, the environment the step names, and the address where it is known.
/// </summary>
/// <param name="Code"><see cref="Unnamed"/>, <see cref="NoAddress"/> or <see cref="NoWindow"/>.</param>
/// <param name="Environment">The environment's name the step shows its work in.</param>
/// <param name="Address">Where a local environment shows, for <see cref="NoWindow"/>; null otherwise.</param>
public sealed record CannotShowFact(string Code, string Environment, string? Address = null)
{
    /// <summary>No review rule here names the environment.</summary>
    public const string Unnamed = "unnamed";

    /// <summary>A local environment that declares no address.</summary>
    public const string NoAddress = "no-address";

    /// <summary>A local environment and no window in this loop to show it in.</summary>
    public const string NoWindow = "no-window";
}

/// <summary>A sit: the fact and the sentence, both from the same branch of <see cref="SetUpSits.Sits"/>.</summary>
public sealed record CannotShowSit(CannotShowFact Fact, string Reason);
