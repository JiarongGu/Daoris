using static Daoris.Driver.WorkflowRuns;

namespace Daoris.Driver;

/// <summary>The landing of a run's work and the pull request that follows it.</summary>
internal static class WorkflowLandingSteps
{
    /// <summary>
    /// The landing: reached once the work is done, read on the newest of its sessions that has work
    /// to land. Done where this machine's record names it, a branch or the line; else it waits for the person's <i>Accept…</i>,
    /// or for Daoris's next look where the rule accepts automatically.
    /// </summary>
    internal static WorkflowRunStep Landing(WorkflowStep cell, bool workDone, IReadOnlyList<TracedSession> sessions, WorkflowRunFacts facts)
    {
        var ended = sessions.Where(session => !Live(session)).ToList();
        // A session whose evidence says it made no commits has nothing to land; one whose evidence is unread may have.
        var landable = ended.Where(session => CommitsOf(session) != 0).ToList();
        if (!workDone)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        }

        if (landable.LastOrDefault() is not { } newest)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.Done, WorkflowRunDetails.Nothing) { Session = ended.LastOrDefault()?.Id };
        }

        var on = new WorkflowRunStep(cell, "", "") { Session = newest.Id };
        // Another machine ran it, and keeps its landing: nothing here can say.
        if (newest.Teammate) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Elsewhere };

        if (facts.Landings.LastOrDefault(entry => entry.Names(newest.Id)) is { } landed)
        {
            return on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Branch, Branch = landed.Branch, At = landed.AcceptedAt(newest.Id),
                Code = landed.AcceptedByOf(newest.Id), Plugin = landed.Plugin,
            };
        }

        // Brought into another session's landing, read from its operation, as a left-out part's note is.
        if (facts.Brought.TryGetValue(newest.Id, out var brought))
        {
            return on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.BroughtInto, Branch = brought.Into, At = brought.At,
                Code = brought.Line ? "line" : null,
            };
        }

        if (facts.Accepted.TryGetValue(newest.Id, out var accepted))
        {
            return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Merge, At = accepted };
        }

        // Accepted automatically: the run's `land` lands it.
        if (cell.Participation == WorkflowParticipation.Automatic)
        {
            return on with { State = WorkflowRunStates.Working, Detail = WorkflowRunDetails.Automatic };
        }

        return on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Accept, At = newest.Updated };
    }

    /// <summary>
    /// The pull request: opened by the plugin that pushed the landed branch; its state is
    /// the answer its plugin last gave, kept on the landing record, which Current asks only when branches are cleaned up.
    /// </summary>
    internal static WorkflowRunStep PullRequest(WorkflowStep cell, WorkflowRunStep landing, WorkflowRunFacts facts)
    {
        if (landing.State != WorkflowRunStates.Done) return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        if (landing.Detail == WorkflowRunDetails.Nothing) return new WorkflowRunStep(cell, WorkflowRunStates.Skipped, WorkflowRunDetails.Nothing);
        if (landing.Detail != WorkflowRunDetails.Branch || landing.Session is not { } session
            || facts.Landings.LastOrDefault(entry => entry.Names(session)) is not { } entry)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.Skipped, WorkflowRunDetails.NoBranch);
        }

        var on = new WorkflowRunStep(cell, "", "")
        {
            Session = session, Branch = entry.Branch, Plugin = entry.Plugin ?? TextOf(cell, "plugin"),
            PullRequest = entry.PullRequest ?? entry.PullRequestState?.PullRequest,
        };
        var kept = entry.PullRequestState;
        if (kept?.State == PullRequestStates.Completed)
        {
            return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Merged, At = kept.At ?? kept.AskedAt };
        }

        if (kept?.State == PullRequestStates.Abandoned)
        {
            return on with { State = WorkflowRunStates.Stopped, Detail = WorkflowRunDetails.Abandoned, At = kept.At ?? kept.AskedAt };
        }

        if (!entry.Pushed) return on with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.NotPushed };

        // A failure never overwrites an answer: the newer of the two is what is known now.
        if (entry.PullRequestAskFailed is { } failed && failed.At > (kept?.AskedAt ?? DateTimeOffset.MinValue))
        {
            return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.AskFailed, Code = failed.Code, Plugin = failed.Plugin, At = failed.At };
        }

        return on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Merging, Code = kept?.State, At = kept?.AskedAt };
    }
}
