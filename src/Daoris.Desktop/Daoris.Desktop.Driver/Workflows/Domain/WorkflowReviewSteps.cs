using static Daoris.Driver.WorkflowRuns;

namespace Daoris.Driver;

/// <summary>The second opinion and the person's look of a run, each read from its gate.</summary>
internal static class WorkflowReviewSteps
{
    /// <summary>
    /// The second opinion: where Current draws one, its gate's state, the detail being the gate's own code. It is not reached while
    /// the work runs, since the gate reads the chain's work once it is done here; and a landing done is what it let go, as its landing record keeps it.
    /// </summary>
    /// <remarks>
    /// The gate's states, where they stand at a step: another agent at it (<c>not-asked</c>, which the driver's next look asks;
    /// <c>reading</c>; <c>read-again</c>) is working; the working session answering its findings is waiting on an agent; a dispute,
    /// commits no other agent read, and an absence the rule requires all wait on the person; an absence it does not require is
    /// passed over; <c>settled</c> and the person's answers (<c>anyway</c>, <c>myself</c>, <c>answered</c>) are done.
    /// </remarks>
    internal static WorkflowRunStep? Opinion(WorkflowRunFacts facts, bool workDone, WorkflowRunStep landing)
    {
        if (facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Opinion) is not { } cell) return null;
        WorkflowRunStep Step(string state, string detail) => new(cell, state, detail);

        // A landing done is what the gate let go (no door lands what it holds): its record keeps what did.
        if (landing.State == WorkflowRunStates.Done && landing.Detail is not WorkflowRunDetails.Nothing)
        {
            var kept = facts.Landings.Select(entry => entry.Opinion).LastOrDefault(opinion => opinion is not null);
            if (kept is null) return Step(WorkflowRunStates.Done, WorkflowRunDetails.Landed);
            return Step(kept.Said == OpinionGateStates.Unavailable ? WorkflowRunStates.Skipped : WorkflowRunStates.Done, kept.Said) with
            {
                Agent = kept.Reviewer, Commit = kept.Tip, At = kept.At, Words = kept.Words, Code = kept.Code, Count = kept.Disputes,
            };
        }

        if (!workDone) return Step(WorkflowRunStates.NotReached, "");
        if (facts.Opinion is not { } gate) return Step(WorkflowRunStates.NotKnown, OpinionGateStates.Unread);

        var opinion = gate.Opinion;
        var on = Step("", gate.State) with
        {
            Session = opinion?.Session ?? gate.Session,
            Agent = opinion?.Who,
            Commit = gate.Recheck is { Given: true } recheck ? recheck.Tip : opinion?.Tip,
            Code = gate.Code,
        };
        return gate.State switch
        {
            OpinionGateStates.None => on with { State = WorkflowRunStates.Skipped },
            OpinionGateStates.WaitsChain => on with { State = WorkflowRunStates.NotReached, Quest = gate.Later },
            OpinionGateStates.NotAsked or OpinionGateStates.Reading or OpinionGateStates.ReadAgain =>
                on with { State = WorkflowRunStates.Working, Session = gate.State == OpinionGateStates.ReadAgain ? gate.Recheck?.Session ?? on.Session : on.Session },
            OpinionGateStates.WithSession => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Session = opinion?.HandedTo ?? opinion?.Working ?? gate.Session,
                Count = opinion?.Findings?.Count,
            },
            OpinionGateStates.Disputed => on with { State = WorkflowRunStates.WaitingOnYou, Session = gate.Session, Count = gate.Disputes?.Count },
            OpinionGateStates.CommitsSince => on with { State = WorkflowRunStates.WaitingOnYou, Session = gate.Session, Count = gate.Since },
            OpinionGateStates.Unavailable => on with
            {
                State = gate.Required ? WorkflowRunStates.WaitingOnYou : WorkflowRunStates.Skipped, Session = gate.Session, At = gate.Until,
            },
            OpinionGateStates.Settled => on with { State = WorkflowRunStates.Done, Count = opinion?.Findings?.Count },
            OpinionGateStates.Anyway or OpinionGateStates.Myself or OpinionGateStates.Answered => on with
            {
                State = WorkflowRunStates.Done, At = gate.Person?.At, Words = gate.Person?.Words, Code = gate.Person?.Said,
            },
            // A named workflow's opinion step that cannot start: it sits, saying why.
            OpinionGateStates.CannotStart => on with { State = WorkflowRunStates.CannotStart, Session = gate.Session, Words = gate.Problem },
            _ => on with { State = WorkflowRunStates.NotKnown, Detail = OpinionGateStates.Unread, Words = gate.Problem },
        };
    }

    /// <summary>
    /// The person's look: where Current draws one, or where the work's own choice asks for one
    /// (<i>a look added for this work</i>). Its state is the review's gate's; a landing done is what it let go.
    /// </summary>
    internal static WorkflowRunStep? Look(WorkflowRunFacts facts, bool workDone, WorkflowRunStep landing)
    {
        var cell = facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Look);
        var gate = facts.Review;
        var decision = gate?.Decision;
        var asks = decision?.Reviews == true;
        if (cell is null && !asks) return null;

        var environment = decision?.Environment ?? (cell is null ? null : TextOf(cell, "environment"));
        var id = WorkflowKinds.Look;
        while (cell is null && facts.Current.Steps.Any(each => each.Id == id)) id = "added-" + id;
        var step = cell ?? new WorkflowStep(
            id, WorkflowKinds.Look, WorkflowParticipation.AgentAndYou, WorkflowExecutor.Agent, WorkflowPress.Reviewed,
            new WorkflowSource(WorkflowRule.Review, LandingSource.Default), [new("environment", environment)],
            WorkflowRuntime.Partial, WorkflowLimits.LookPartial);
        var on = new WorkflowRunStep(step, "", "")
        {
            Added = cell is null ? decision!.Level : null,
            Environment = environment,
            Quest = decision?.SetUpStep?.Id,
        };

        if (gate is null) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread };
        if (gate.State == ReviewStates.Unread) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread, Words = gate.Problem };

        // The person's skip stands whenever it is given.
        if (gate.State == ReviewStates.Skipped)
        {
            return on with
            {
                State = WorkflowRunStates.Skipped, Detail = WorkflowRunDetails.Skip, Quest = decision!.SkippedOn, Words = decision.Words, At = decision.Skip?.At,
            };
        }

        // Current draws a look, and the chain's or the ask's choice says none for this work.
        if (!asks)
        {
            return on with { State = WorkflowRunStates.Skipped, Detail = WorkflowRunDetails.Off, Code = decision!.Level, Words = decision.Choice };
        }

        // A landing done is what the look let go: no door lands work the gate holds, so the work stood reviewed then. Its
        // record keeps the commit where it is a branch; a tree gone since cannot show what it held.
        if (landing.State == WorkflowRunStates.Done && landing.Detail is not WorkflowRunDetails.Nothing)
        {
            var kept = facts.Landings.Select(entry => entry.Review).LastOrDefault(review => review?.Said == ReviewVerdicts.Reviewed);
            return on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Reviewed,
                Commit = kept?.Commit ?? gate.SetUp?.Commit, At = kept?.At ?? gate.Verdict?.At,
            };
        }

        if (!workDone) return on with { State = WorkflowRunStates.NotReached, Detail = "" };

        var setUp = gate.SetUp;
        var setUpSession = setUp?.Session ?? facts.Sessions
            .Where(session => decision!.SetUpStep is { } stepQuest && Same(session.Quest, stepQuest.Id))
            .MaxBy(session => session.Created ?? DateTimeOffset.MinValue)?.Id;
        return gate.State switch
        {
            ReviewStates.Reviewed => on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Reviewed, Commit = setUp?.Commit, At = gate.Verdict?.At, Session = setUpSession,
            },
            ReviewStates.NotShown => on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.NotShown },
            ReviewStates.CannotStart => on with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.Unread, Words = gate.Says },
            ReviewStates.BeingSetUp => on with { State = WorkflowRunStates.Working, Detail = WorkflowRunDetails.BeingSetUp, Session = setUpSession },
            ReviewStates.Shown => on with
            {
                State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Shown, Commit = setUp?.Commit, At = setUp?.At, Session = setUpSession,
            },
            ReviewStates.NotYet => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Detail = WorkflowRunDetails.NotYet, Commit = setUp?.Commit, Words = gate.Verdict?.Words,
                At = gate.Verdict?.At, Session = setUpSession,
            },
            ReviewStates.NotHeld => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Detail = WorkflowRunDetails.NotHeld, Commit = setUp?.Commit, Session = setUpSession,
            },
            _ => on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread, Words = gate.Says },
        };
    }

    /// <summary>
    /// The look a work's own choice adds to a run bound to a graph version: the look's
    /// hold at every landing door, drawn beside the graph and never one of its nodes, where it stands as a steps run's added look stands.
    /// Null where the work's choice adds none. <paramref name="facts"/>' Current holds the graph's nodes as steps and no look, so the added
    /// look takes an id none of them has.
    /// </summary>
    /// <param name="landed">Whether the graph's landing (<c>handed</c>) is reached: what the look let go.</param>
    internal static WorkflowRunStep? AddedLook(WorkflowRunFacts facts, bool workDone, bool landed)
    {
        var cell = new WorkflowStep(WorkflowKinds.Landing, WorkflowKinds.Landing, WorkflowParticipation.You, WorkflowExecutor.Daoris, WorkflowPress.Accept,
            new WorkflowSource(null, LandingSource.Default), [], WorkflowRuntime.Built, null);
        var landing = landed ? new WorkflowRunStep(cell, WorkflowRunStates.Done, WorkflowRunDetails.Branch) : new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        return Look(facts, workDone, landing) is { Added: not null } added ? added : null;
    }
}
