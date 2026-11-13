namespace Daoris.Driver;

/// <summary>The part a scout orients, as the lead's ask names it and the service judged it against the plan.</summary>
/// <param name="Ask">The ask whose plan holds the part.</param>
/// <param name="Part">The part's id.</param>
/// <param name="Repository">The repository the part changes, where the scout reads.</param>
public sealed record HelperScoutPart(string Ask, string Part, string Repository);
