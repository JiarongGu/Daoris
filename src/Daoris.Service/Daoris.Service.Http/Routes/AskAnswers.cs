using Daoris.Contracts.Asks;
using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// An ask as every door answers it: the desk's outcome as the ask and its sentence, or its refusal in the desk's words, whole,
/// including a named receiver the exchange refused, whose message already says the ask is kept and where it is proposed instead.
/// </summary>
public sealed class AskAnswers(HostWiring host)
{
    private readonly QuestAnswers _quests = new(host);

    /// <summary>The outcome of a move on an ask.</summary>
    public Answer<AskActionResponse> Moved(HttpContext http, AskOutcome outcome) => outcome.Refusal switch
    {
        AskRefusal.None => new AskActionResponse(
            ToAsk(outcome.Ask!, host.Service.Files, host.MachineLocal(http)), outcome.Message,
            outcome.Quest is null ? null : QuestAnswers.ToQuest(outcome.Quest, host.Service.Files, host.MachineLocal(http), here: _quests.HereFor(http))),
        AskRefusal.UnknownWorkspace or AskRefusal.NotFound => Refusal.NotFound(outcome.Message),
        AskRefusal.Closed or AskRefusal.QuestRefused => Refusal.Conflict(outcome.Message),
        _ => Refusal.BadRequest(outcome.Message),
    };

    /// <summary>The ask's record, its kept files' paths answered to this machine only, the quest files' rule.</summary>
    public static AskResponse ToAsk(Ask a, QuestFiles? files, bool machineLocal)
    {
        var kept = files?.For(HomeEntry.AskFiles);
        return new(
            a.Id, a.Workspace, a.Sentence, a.State.ToString(), a.Tier, a.Asked, a.Updated, a.Asker, a.Note, a.Links,
            a.Attachments.Select(f => new QuestAttachmentResponse(
                f.Name, f.Sha256, f.Bytes,
                Path: machineLocal && kept is not null && kept.Has(a.Id, f) ? kept.PathOf(a.Id, f) : null)).ToList(),
            a.Proposal.Select(m => new DeclarationMatchResponse(m.Repository, m.Score, m.Matched)).ToList(),
            a.Quests, a.Intake, a.Deletable,
            // The person's words, served as the sentence is: these routes are a local host's alone.
            Words: a.Words.Select(w => new AskWordResponse(AskWord.Spell(w.Kind), w.Text, w.At, w.Session, w.Quest)).ToList(),
            WordsKeptFrom: a.WordsKeptFrom,
            GoAheads: a.GoAheads.Select(g => new GoAheadResponse(
                g.Number, g.Kind, g.On, g.Act, GoAhead.Spell(g.State),
                g.Asked.Select(r => new GoAheadRequestResponse(r.Session, r.Quest, r.At, r.Why)).ToList(),
                g.Answer is { } answer ? new GoAheadAnswerResponse(answer.Approved, answer.Words, answer.At) : null,
                g.Near)).ToList(),
            // Each of the four is absent where there is none.
            ReviewChoices: a.ReviewChoices.Count == 0
                ? null
                : a.ReviewChoices.Select(c => new AskReviewChoiceResponse(c.Choice, c.At, c.Words)).ToList(),
            ReviewProposals: a.ReviewProposals.Count == 0
                ? null
                : a.ReviewProposals.Select(p => new AskReviewProposalResponse(p.Choice, p.Reason, p.At, p.Session, p.Quest)).ToList(),
            WorkflowChoices: a.WorkflowChoices.Count == 0
                ? null
                : a.WorkflowChoices.Select(c => new AskWorkflowChoiceResponse(c.Kind, c.Workflow, c.At, c.Words)).ToList(),
            WorkflowProposals: a.WorkflowProposals.Count == 0
                ? null
                : a.WorkflowProposals.Select(p => new AskWorkflowProposalResponse(p.Kind, p.Reason, p.At, p.Session, p.Quest)).ToList(),
            Short: a.Short,
            Paths: a.Paths);
    }
}
