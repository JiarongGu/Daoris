using static Daoris.Driver.WorkflowReviewSteps;
using static Daoris.Driver.WorkflowRuns;

namespace Daoris.Driver;

/// <summary>The steps of a chat run, read by its candidate's readiness and the landing kept for the tip its tree stands at.</summary>
internal static class WorkflowChatSteps
{
    /// <summary>
    /// The landing kept for the tip a chat's tree stands at, or null. A branch landing
    /// is kept for a tip where an entry naming a record of the line holds it as its own <c>Squashed ?? Tip</c>, or an advance's
    /// <c>Squashed ?? To</c>. A merge acceptance keeps no tip, so it is read from the line: kept for the tip the tree stands at where nothing
    /// stands beyond the line, since the line then holds that tip's work. A tree that does not stand reads its line's newest landing.
    /// </summary>
    internal static ChatLanded? ChatLandedAt(WorkflowRunFacts facts, ChatRunFacts chat)
    {
        var ids = new HashSet<string>(facts.Sessions.Select(session => session.Id), StringComparer.OrdinalIgnoreCase);
        var kept = new List<(string Tip, ChatLanded Landed)>();
        foreach (var entry in facts.Landings)
        {
            if (ids.Contains(entry.Session))
            {
                kept.Add((entry.Squashed ?? entry.Tip, new ChatLanded(entry.Session, WorkflowRunDetails.Branch, entry.LandedAt, entry, entry.Opinion, entry.AcceptedBy)));
            }

            foreach (var advance in entry.Advances.Where(advance => ids.Contains(advance.Session)))
            {
                kept.Add((advance.Squashed ?? advance.To, new ChatLanded(advance.Session, WorkflowRunDetails.Branch, advance.At, entry, advance.Opinion, advance.AcceptedBy)));
            }
        }

        var accepted = facts.Accepted.Where(pair => ids.Contains(pair.Key)).OrderBy(pair => pair.Value).ToList();
        var merge = accepted.Count == 0 ? null : new ChatLanded(accepted[^1].Key, WorkflowRunDetails.Merge, accepted[^1].Value, null, null, null);
        if (chat.Tip is not { } tip)
        {
            return chat.Beyond is null ? kept.Select(each => each.Landed).Append(merge).OfType<ChatLanded>().MaxBy(each => each.At) : null;
        }

        if (kept.Where(each => SameCommit(each.Tip, tip)).Select(each => each.Landed).MaxBy(each => each.At) is { } branch) return branch;
        return chat.Beyond == 0 ? merge : null;
    }

    /// <summary>
    /// A chat run's candidate: commits beyond the line, between turns or ended; or a landing kept for the tip the
    /// tree stands at, which it stays while that reads done; or an opinion a press asked on a tip of the line, asked or read,
    /// so it stays visible while an answering turn runs.
    /// </summary>
    internal static bool Candidate(WorkflowRunFacts facts, ChatRunFacts chat, ChatLanded? landed) =>
        landed is not null || (chat.Beyond > 0 && facts.Sessions.LastOrDefault() is { } newest && (!Live(newest) || chat.Between))
        || (chat.Beyond > 0 && facts.Opinion is { } gate && (gate.Asked || gate.Opinion is not null));

    /// <summary>
    /// A chat run's work: done once a landing is kept for the tip its tree stands at, read first; an agent at it
    /// while a turn runs or words wait; the person's <i>Accept…</i> between turns, or ended, with commits beyond the line; their turn to
    /// talk between turns with nothing beyond it; stopped once the conversation ended with nothing beyond it.
    /// </summary>
    internal static WorkflowRunStep ChatWork(WorkflowStep cell, WorkflowRunFacts facts, ChatRunFacts chat, ChatLanded? landed)
    {
        if (facts.Sessions.LastOrDefault() is not { } newest) return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        var on = new WorkflowRunStep(cell, "", "") { Session = newest.Id, Agent = newest.Door };
        if (landed is not null) return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Landed, Session = landed.Session, At = landed.At };
        var live = Live(newest);
        if (live && !chat.Between) return on with { State = WorkflowRunStates.Working, Detail = WorkflowRunDetails.Agent, At = newest.Created };
        if (chat.Beyond > 0) return on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Land, Count = chat.Beyond, At = newest.Updated };
        return live
            ? on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Talk, At = newest.Updated }
            : on with { State = WorkflowRunStates.Stopped, Detail = WorkflowRunDetails.Stopped, At = newest.Updated };
    }

    /// <summary>
    /// A chat run's landing: done for the landing kept for the tip its tree stands at; else waiting on the
    /// person's <i>Accept…</i> while it is a candidate, on the line's newest record, live or ended; not reached before.
    /// </summary>
    internal static WorkflowRunStep ChatLanding(WorkflowStep cell, bool candidate, WorkflowRunFacts facts, ChatLanded? landed)
    {
        if (landed is not null)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.Done, landed.Detail)
            {
                Session = landed.Session, Branch = landed.Entry?.Branch, At = landed.At, Code = landed.AcceptedBy, Plugin = landed.Entry?.Plugin,
            };
        }

        if (!candidate || facts.Sessions.LastOrDefault() is not { } newest) return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        return new WorkflowRunStep(cell, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept) { Session = newest.Id, At = newest.Updated };
    }

    /// <summary>
    /// A chat run's second opinion: the one a landing of the tip the tree stands at kept, never an
    /// earlier tip's; else, once the work is a candidate, its gate as a quest's reads, but the opinion no press has asked waits on the person's
    /// <i>Accept…</i>, which asks it (<c>chat-at-accept</c>), naming the reviewer it asks.
    /// </summary>
    internal static WorkflowRunStep? ChatOpinion(WorkflowRunFacts facts, bool candidate, WorkflowRunStep landing, ChatLanded? landed)
    {
        if (facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Opinion) is not { } cell) return null;
        if (landed?.Opinion is { } kept)
        {
            return new WorkflowRunStep(cell, kept.Said == OpinionGateStates.Unavailable ? WorkflowRunStates.Skipped : WorkflowRunStates.Done, kept.Said)
            {
                Agent = kept.Reviewer, Commit = kept.Tip, At = kept.At, Words = kept.Words, Code = kept.Code, Count = kept.Disputes,
            };
        }

        if (landed is not null) return new WorkflowRunStep(cell, WorkflowRunStates.Done, WorkflowRunDetails.Landed);
        if (!candidate) return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        if (facts.Opinion is { State: OpinionGateStates.NotAsked, Asked: false } waits)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.ChatAtAccept)
            {
                Session = facts.Sessions.LastOrDefault()?.Id, Agent = waits.Rule?.Rule.Reviewers.FirstOrDefault(),
            };
        }

        return Opinion(facts, candidate, landing);
    }
}
