namespace Daoris.Driver;

/// <summary>
/// What the card of a place's supervisor rules shows: the place and each rule before and after, as
/// the rules' own fields, so the page says them in its catalogue's words in the person's language.
/// </summary>
public sealed record HelpRulesView(string Place, IReadOnlyList<SupervisorRule> Before, IReadOnlyList<SupervisorRule> After);
