namespace Daoris.Contracts.Asks;

// An ask's plan as the page reads it: its parts with the quest each is, the edges between them and what the judge found. Read-only: the
// plan is written through the connector's `team_plan` door, by the lead, by Ask Daoris or by a person's hand. Local hosts only.

/// <summary>An ask's plan: the newest revision of its own plan, then of each split a worker made of its own part.</summary>
public sealed record ReadAskPlan(string Id) : IRoute<AskPlanResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/asks/{id}/plan";
}

/// <summary>The plans an ask holds, the ask's own first; none before anyone wrote one. <c>Team</c> is who serves it, <c>Decisions</c> what was decided, oldest first.</summary>
public sealed record AskPlanResponse(
    string Ask, IReadOnlyList<AskPlanRevisionResponse> Plans, IReadOnlyList<AskPlanMemberResponse> Team, IReadOnlyList<AskPlanDecisionResponse> Decisions);

/// <summary>A session on the ask's team: its <c>Role</c> (<c>lead</c>), the part it serves (null for the ask's own plan) and the newest revision it wrote.</summary>
public sealed record AskPlanMemberResponse(string? Part, string Role, string Session, int Revision);

/// <summary>What was decided about the plan at a revision, by whom, and what followed. <c>Scope</c> is the part a split is of, or null for the ask's own plan.</summary>
public sealed record AskPlanDecisionResponse(string? Scope, int Revision, string Decision, string Reason, string Author, string Act);

/// <summary>One plan at its newest revision. <c>Scope</c> is the part a split is of, or null for the ask's own plan.</summary>
public sealed record AskPlanRevisionResponse(
    string? Scope, int Revision, int Depth, string Why, IReadOnlyList<AskPlanPartResponse> Parts,
    IReadOnlyList<AskPlanEdgeResponse> Edges, IReadOnlyList<AskPlanFlagResponse> Flags);

/// <summary>One part: where it is done, what a person sees working when it is, and the quest it became, null until it is published.</summary>
public sealed record AskPlanPartResponse(
    string Id, string Repository, string? Quest, string Outcome, IReadOnlyList<string> Lanes, IReadOnlyList<string> Claims);

/// <summary>An order between two parts: <c>Kind</c> is <c>after</c> (a build dependency) or <c>orders</c> (a landing order only).</summary>
public sealed record AskPlanEdgeResponse(string Part, string On, string Kind, string Why);

/// <summary>What the plan's judge found, a sentence the supervisor reads: <c>Kind</c> names it.</summary>
public sealed record AskPlanFlagResponse(string Kind, string? Part, string Text);
