namespace Daoris.Knowledge;

/// <summary>An intake's kind proposal for the chain it publishes (design §4.3): a kind and its reason.</summary>
/// <param name="Kind">A kind's id, which the ask's workspace declares on the machine that drives it.</param>
/// <param name="Reason">The person's own words it rests on, copied exactly, in at most <see cref="WorkflowChoices.ReasonLimit"/> characters.</param>
public sealed record WorkflowProposed(string? Kind, string? Reason);
