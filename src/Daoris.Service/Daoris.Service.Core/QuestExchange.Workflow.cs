namespace Daoris.Knowledge;

/// <summary>
/// An intake's kind proposal at publish (WORKFLOW1i, D157; the workflow design §4.3): its shape and its reason, which must quote the
/// person's words on the ask, as a review choice's words do (REVIEWENV1b3). The exchange only judges; the ask's desk keeps the
/// proposal and the person's press applies it. Version 1 only proposes: whether a kind lowers the person's part is the driving
/// machine's table (WORKFLOW1i2), which the service does not hold.
/// </summary>
public sealed partial class QuestExchange
{
    /// <summary>
    /// Why a kind proposal is refused, or null where there is none or it stands: a kind that is not a kind's id, a reason that is
    /// missing or past its bound, one on no ask held here, or a reason that is not the person's words, verbatim.
    /// </summary>
    private async Task<(QuestPublishRefusal Refusal, string Message)?> JudgeWorkflowProposalAsync(string from, QuestAsk ask, CancellationToken ct)
    {
        if (ask.WorkflowProposal is not { } proposed) return null;

        var reason = proposed.Reason?.Trim() ?? "";
        if (WorkflowChoices.JudgeProposal(proposed.Kind?.Trim(), reason) is { } unfit)
        {
            return (QuestPublishRefusal.BadWorkflow, $"{unfit}. Nothing was published.");
        }

        if (AskDesk.AskOf(from) is not { } askId || asks is null || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held)
        {
            return (QuestPublishRefusal.BadWorkflow,
                $"A kind proposal quotes the person's words on the ask a quest is asked by, and this quest is asked by `{from}`, "
                + "on no ask held here — so there are no words of theirs to quote. Nothing was published.");
        }

        return held.Words.Any(word => QuestRequirement.QuotedIn(reason, word.Text))
            ? null
            : (QuestPublishRefusal.NotQuoted,
                $"The kind `{proposed.Kind?.Trim()}` is proposed on words the person never said on ask `#{held.Id}`: \"{Clip(reason)}\". "
                + "The reason is their own words, verbatim (whitespace and case aside); the person decides on a proposal. Nothing was published.");
    }
}
