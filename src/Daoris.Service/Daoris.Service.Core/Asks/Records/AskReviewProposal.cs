namespace Daoris.Knowledge;

/// <summary>
/// A review choice an intake proposed for a chain it published (design §1.5–§1.6), with its reason: a reading
/// the person may apply, never a choice.
/// </summary>
/// <param name="Choice">What it proposes: <c>off</c>, <c>on</c> or an environment's name.</param>
/// <param name="Reason">Why, in at most <see cref="Reviews.ReasonLimit"/> characters.</param>
/// <param name="At">When it proposed it.</param>
/// <param name="Session">The intake session that proposed it: the record that names the tier.</param>
/// <param name="Quest">The quest it published the proposal with, the chain's first; null for none.</param>
public sealed record AskReviewProposal(string Choice, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);
