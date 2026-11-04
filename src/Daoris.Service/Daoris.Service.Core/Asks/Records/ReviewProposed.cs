namespace Daoris.Knowledge;

/// <summary>An intake's review proposal for the chain it publishes (design §1.5): a choice and its reason.</summary>
/// <param name="Choice"><c>off</c>, <c>on</c> or an environment's name.</param>
/// <param name="Reason">Why, in at most <see cref="Reviews.ReasonLimit"/> characters.</param>
public sealed record ReviewProposed(string? Choice, string? Reason);
