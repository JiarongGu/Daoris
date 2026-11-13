namespace Daoris.Knowledge;

/// <summary>
/// The part a helper's ask is to orient, which makes the helper a scout: written on the ask by the lead's <c>helper_ask</c>, read by the driver
/// to start the pass under the Scout role in the part's repository, and by the answer door to keep its pack with the plan.
/// </summary>
/// <param name="Ask">The ask whose plan holds the part.</param>
/// <param name="Part">The part to orient.</param>
/// <param name="Repository">The repository the part changes, where the scout reads.</param>
public sealed record HelperScout(string Ask, string Part, string Repository);
