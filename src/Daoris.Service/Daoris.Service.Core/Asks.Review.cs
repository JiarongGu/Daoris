namespace Daoris.Knowledge;

/// <summary>
/// The review on an ask (REVIEWENV1b, D154 point 3; design §1.4–§1.6): the person's choice for every chain the ask publishes
/// that sets none, and its intake's proposals, a reading the person's press applies.
/// </summary>
public sealed partial class AskDesk
{
    /// <summary>
    /// The person sets the ask's review choice (design §1.5): <c>off</c>, <c>on</c> or an environment's name, with any words
    /// they give, kept with when; the latest stands, as a go-ahead's answer does. Applying the intake's proposal is this same
    /// press, with the proposal's choice.
    /// </summary>
    /// <remarks>
    /// <b>The person's door</b>, a local host's as every ask route is. Whether the repositories the ask reaches declare an
    /// environment is the door's to say, which reads the review rule; the service holds no rule.
    /// </remarks>
    public async Task<AskOutcome> ChooseReviewAsync(string id, string? choice, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}): nothing it publishes waits for a review.", ask);
        }

        var (chosen, unfit) = JudgeChoice(choice, words, now);
        if (unfit is not null) return new(AskRefusal.BadReview, unfit, ask);

        await asks.RecordReviewChoiceAsync(ask.Id, chosen!, now, ct).ConfigureAwait(false);
        var stands = await FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, chosen!.Choice switch
        {
            Reviews.Off => $"Ask `#{ask.Id}`: no review for the chains it publishes that choose none.",
            Reviews.On => $"Ask `#{ask.Id}`: the chains it publishes that choose none are reviewed in each repository's default environment.",
            var environment => $"Ask `#{ask.Id}`: the chains it publishes that choose none are reviewed in `{environment}`.",
        } + (chosen.Words is null ? "" : " Your words are kept with it."), stands);
    }

    /// <summary>A choice as the ask keeps it, or why not: its shape, and the person's words trimmed and bounded.</summary>
    private static (AskReviewChoice? Choice, string? Refusal) JudgeChoice(string? choice, string? words, DateTimeOffset now)
    {
        var chosen = choice?.Trim() ?? "";
        if (Reviews.JudgeChoice(chosen) is { } unfit) return (null, $"The ask's {unfit}. Nothing was kept.");
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        return said is { Length: > Reviews.WordsLimit }
            ? (null, $"Your words with a review choice are at most {Reviews.WordsLimit} characters; these are {said.Length}. Nothing was kept.")
            : (new AskReviewChoice(chosen, now, said), null);
    }

    /// <summary>
    /// An intake's proposal as the ask keeps it, or why not (design §1.5–§1.6): from an agent, with a reason of at most
    /// <see cref="Reviews.ReasonLimit"/> characters, and never beside a choice it sets on the person's words. Null for none.
    /// The session that made it is kept where the agent named one; an agent that named none is still an agent (REVIEWENV1b3).
    /// </summary>
    private static (AskReviewProposal? Proposal, string? Refusal) JudgeProposal(AskDraft? draft, string? session, bool agent, DateTimeOffset now)
    {
        if (draft?.ReviewProposal is not { } proposed) return (null, null);

        var choice = proposed.Choice?.Trim() ?? "";
        var reason = proposed.Reason?.Trim() ?? "";
        string? unfit =
            !agent ? "A review proposal is an intake's reading, kept for the person's press: the person sets their own choice instead"
            : draft.Review is not null ? "A chain takes a review choice on the person's words, or a proposal with your reason, never both"
            : Reviews.JudgeChoice(choice) is { } shape ? $"The proposal's {shape}"
            : reason.Length == 0 ? "A review proposal says why, in a sentence: the person decides on it"
            : reason.Length > Reviews.ReasonLimit ? $"A review proposal's reason is at most {Reviews.ReasonLimit} characters; this is {reason.Length}"
            : null;
        return unfit is not null
            ? (null, unfit + ". Nothing was published.")
            : (new AskReviewProposal(choice, reason, now, string.IsNullOrWhiteSpace(session) ? null : session.Trim()), null);
    }
}
