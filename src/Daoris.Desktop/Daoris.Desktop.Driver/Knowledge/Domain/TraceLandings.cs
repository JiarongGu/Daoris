namespace Daoris.Driver;

/// <summary>The landing link of a chain: the branches a landing made of a session, from <c>landings.json</c>.</summary>
internal static class TraceLandings
{
    /// <summary>
    /// Where its work landed, from <c>landings.json</c> (WSR5): each branch a landing made of it, standing or gone. A landing
    /// into the line records no branch, so where none is, the acceptance its record keeps is pointed to, or said absent.
    /// </summary>
    internal static TraceLanding Landing(TracedSession session, TraceFacts facts, TraceEvents? events)
    {
        if (facts.LandingsUnread is not null) return new TraceLanding { Missing = TraceLandingGaps.Unread };

        // FLOWCHART1c: every landing that names it, the one made for it and each it moved on, so a shared branch is read from each.
        var branches = facts.Landings.Where(each => each.Names(session.Id)).Select(Branch).ToList();
        if (branches.Count > 0) return new TraceLanding { Branches = branches };

        return new TraceLanding
        {
            Missing = events is null || events.Missing is not null ? TraceLandingGaps.Unknown
                : events.Accepted.Count == 0 ? TraceLandingGaps.None
                : TraceLandingGaps.MergeAccepted,
        };
    }

    /// <summary>A landing whose session the service holds no record of: what the landing alone says.</summary>
    internal static TraceUnrecordedLink Unrecorded(string session, TraceFacts facts) =>
        new(session) { Branches = [.. facts.Landings.Where(each => each.Names(session)).Select(Branch)] };

    private static TraceBranch Branch(LandedBranch landing) => new(landing.Branch, landing.Tip, landing.LandedAt)
    {
        Line = landing.Line,
        From = landing.From,
        Plugin = landing.Plugin,
        Pushed = landing.Pushed,
        PullRequest = landing.PullRequest,
        PullRequestState = landing.PullRequestState,
        PullRequestAskFailed = landing.PullRequestAskFailed,
        Gone = landing.GoneAt,
        RemovedAs = landing.GoneAt is null ? null : landing.RemovedAs,
        RemovedOn = landing.GoneAt is null || landing.RemovedAs is null ? null : landing.RemovedOn,
        AcceptedBy = landing.AcceptedBy,
        Rule = landing.Rule is { } rule ? new TraceRule(rule.Plugin, rule.AutoAccept, rule.Source) { Access = rule.Access } : null,
        Review = landing.Review,
        Opinion = landing.Opinion,
        Workflow = landing.Workflow,
        Session = landing.Session,
        Advances =
        [
            .. landing.Advances.Select(advance => new TraceAdvance(advance.From, advance.To, advance.At, advance.Session)
            {
                AcceptedBy = advance.AcceptedBy,
                Review = advance.Review,
                Opinion = advance.Opinion,
                Workflow = advance.Workflow,
                Quest = advance.Quest,
                Unit = advance.Unit,
            }),
        ],
        Units = landing.Units,
    };
}
