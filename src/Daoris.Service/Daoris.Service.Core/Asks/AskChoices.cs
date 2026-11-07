namespace Daoris.Knowledge;

/// <summary>The person's review choice, kind and workflow on an ask, and the intake's proposals beside them.</summary>
internal sealed class AskChoices(AskStore asks, AskStandings standings)
{
    /// <summary>
    /// The person sets the ask's review choice: <c>off</c>, <c>on</c> or an environment's
    /// name, with any words they give, kept with when; the latest stands, as a go-ahead's answer does. Applying the intake's
    /// proposal is this same press, with the proposal's choice.
    /// </summary>
    /// <remarks>
    /// <b>The person's door</b>, a local host's as every ask route is. Whether the repositories the ask reaches declare an
    /// environment is the door's to say, which reads the review rule; the service holds no rule.
    /// </remarks>
    public async Task<AskOutcome> ChooseReviewAsync(string id, string? choice, string? words, DateTimeOffset now, CancellationToken ct)
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
        var stands = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, chosen!.Choice switch
        {
            Reviews.Off => $"Ask `#{ask.Id}`: no review for the chains it publishes that choose none.",
            Reviews.On => $"Ask `#{ask.Id}`: the chains it publishes that choose none are reviewed in each repository's default environment.",
            var environment => $"Ask `#{ask.Id}`: the chains it publishes that choose none are reviewed in `{environment}`.",
        } + (chosen.Words is null ? "" : " Your words are kept with it."), stands);
    }

    /// <summary>A choice as the ask keeps it, or why not: its shape, and the person's words trimmed and bounded.</summary>
    public static (AskReviewChoice? Choice, string? Refusal) JudgeChoice(string? choice, string? words, DateTimeOffset now)
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
    /// The session that made it is kept where the agent named one; an agent that named none is still an agent.
    /// </summary>
    public static (AskReviewProposal? Proposal, string? Refusal) JudgeProposal(AskDraft? draft, string? session, bool agent, DateTimeOffset now)
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

    /// <summary>
    /// The person sets the ask's kind and workflow: a kind, a workflow, both, or neither to
    /// clear them, with any words they give, kept with when; the latest stands, as a review choice does. It reaches each chain the
    /// ask publishes whose work has not started: a run is bound at its first start.
    /// </summary>
    /// <remarks>
    /// <b>The person's door</b>, a local host's as every ask route is: an agent proposes a kind, the intake's,
    /// and never sets one. Whether the ask's workspace declares the kind and the workflow reads here is the driving machine's to
    /// say; the service holds neither.
    /// </remarks>
    public async Task<AskOutcome> ChooseWorkflowAsync(
        string id, string? kind, string? workflow, string? words, DateTimeOffset now, CancellationToken ct)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}): nothing it publishes waits for a workflow.", ask);
        }

        var (chosen, unfit) = JudgeWorkflowChoice(kind, workflow, words, now);
        if (unfit is not null) return new(AskRefusal.BadWorkflow, unfit, ask);

        await asks.RecordWorkflowChoiceAsync(ask.Id, chosen!, now, ct).ConfigureAwait(false);
        var stands = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, WorkflowSaid(ask.Id, chosen!) + (chosen!.Words is null ? "" : " Your words are kept with it."), stands);
    }

    /// <summary>A choice as the ask keeps it, or why not: each blank one none, its shape, and the person's words trimmed and bounded.</summary>
    public static (AskWorkflowChoice? Choice, string? Refusal) JudgeWorkflowChoice(string? kind, string? workflow, string? words, DateTimeOffset now)
    {
        var namedKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var named = string.IsNullOrWhiteSpace(workflow) ? null : workflow.Trim();
        if (WorkflowChoices.Judge(namedKind, named) is { } unfit) return (null, $"The ask's {unfit}. Nothing was kept.");
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        return said is { Length: > WorkflowChoices.WordsLimit }
            ? (null, $"Your words with a kind or a workflow are at most {WorkflowChoices.WordsLimit} characters; these are {said.Length}. Nothing was kept.")
            : (new AskWorkflowChoice(namedKind, named, now, said), null);
    }

    /// <summary>
    /// An intake's kind proposal as the ask keeps it, or why not (design §4.3): from an agent only, a kind's shape, a
    /// reason of at most <see cref="WorkflowChoices.ReasonLimit"/> characters. Null for none. Whether the reason quotes the person is
    /// the exchange's, which holds the ask's words; the session that made it is kept where the agent named one.
    /// </summary>
    public static (AskWorkflowProposal? Proposal, string? Refusal) JudgeWorkflowProposal(AskDraft? draft, string? session, bool agent, DateTimeOffset now)
    {
        if (draft?.WorkflowProposal is not { } proposed) return (null, null);

        var unfit = !agent
            ? "A kind proposal is an intake's reading, kept for the person's press: the person sets their own kind instead"
            : WorkflowChoices.JudgeProposal(proposed.Kind, proposed.Reason);
        return unfit is not null
            ? (null, unfit + ". Nothing was published.")
            : (new AskWorkflowProposal(proposed.Kind!.Trim(), proposed.Reason!.Trim(), now, string.IsNullOrWhiteSpace(session) ? null : session.Trim()), null);
    }

    /// <summary>What a choice kept says: the kind, the workflow, both, or neither, for the chains the ask publishes.</summary>
    private static string WorkflowSaid(string id, AskWorkflowChoice chosen)
    {
        var follows = chosen.Workflow switch
        {
            null => null,
            "current" => "follow Current, each repository's rules as they stand,",
            var workflow => $"follow `{workflow}`",
        };
        return (chosen.Kind, follows) switch
        {
            (null, null) => $"Ask `#{id}`: no kind and no workflow; each repository's choice decides the workflow the chains it publishes follow.",
            ({ } kind, null) => $"Ask `#{id}`: the chains it publishes are of kind `{kind}`; each repository's choice for it decides their workflow.",
            (null, { } named) => $"Ask `#{id}`: the chains it publishes {named} in every repository they reach.",
            ({ } kind, { } named) => $"Ask `#{id}`: the chains it publishes are of kind `{kind}`, and {named} in every repository they reach.",
        } + " Work already started keeps the workflow it bound.";
    }
}
