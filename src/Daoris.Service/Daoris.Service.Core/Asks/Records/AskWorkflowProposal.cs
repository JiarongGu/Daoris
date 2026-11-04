namespace Daoris.Knowledge;

/// <summary>
/// A kind an intake proposed for the chains it published (design §4.3), with its reason: a reading the person may
/// apply, never a choice. A kind only: the workflow a kind gives is the driving machine's.
/// </summary>
/// <param name="Kind">The kind it proposes, by a kind's id shape.</param>
/// <param name="Reason">Why, in at most <see cref="WorkflowChoices.ReasonLimit"/> characters: the person's own words it quotes.</param>
/// <param name="At">When it proposed it.</param>
/// <param name="Session">The intake session that proposed it: the record that names the tier.</param>
/// <param name="Quest">The quest it published the proposal with, the chain's first; null for none.</param>
public sealed record AskWorkflowProposal(string Kind, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);
