namespace Daoris.Contracts.Asks;

// An ask's plan as the page reads it, read-only: it is written through the connector's `team_plan` door. Local hosts only.

/// <summary>An ask's plan: the newest revision of its own plan, then of each split a worker made of its own part. <c>Seen</c> is the blob each <c>repository:file</c> of evidence holds where the reader looked.</summary>
public sealed record ReadAskPlan(string Id, IReadOnlyDictionary<string, string>? Seen = null) : IRoute<AskPlanResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/plan";
}

/// <summary>The plans an ask holds, the ask's own first; none before anyone wrote one. <c>Team</c> is who serves it, <c>Decisions</c> the choices taken, oldest first.</summary>
public sealed record AskPlanResponse(
    string Ask, IReadOnlyList<AskPlanRevisionResponse> Plans, IReadOnlyList<AskPlanMemberResponse> Team, IReadOnlyList<AskPlanDecisionResponse> Decisions);

/// <summary>A session on the ask's team: its <c>Role</c> (<c>lead</c>), the part it serves (null for the ask's own plan) and the newest revision it wrote.</summary>
public sealed record AskPlanMemberResponse(string? Part, string Role, string Session, int Revision);

/// <summary>A choice taken about the plan at a revision, by whom, and what followed. <c>Scope</c> is the part a split is of, or null for the ask's own plan.</summary>
public sealed record AskPlanDecisionResponse(string? Scope, int Revision, string Decision, string Reason, string Author, string Act);

/// <summary>One plan at its newest revision. <c>Scope</c> is the part a split is of, or null for the ask's own plan.</summary>
public sealed record AskPlanRevisionResponse(
    string? Scope, int Revision, int Depth, string Why, IReadOnlyList<AskPlanPartResponse> Parts,
    IReadOnlyList<AskPlanEdgeResponse> Edges, IReadOnlyList<AskPlanFlagResponse> Flags, IReadOnlyList<AskPlanDeployableResponse> Deployables,
    IReadOnlyList<AskPlanReleaseResponse> Releases, IReadOnlyList<AskPlanQuestionResponse> Questions, IReadOnlyList<AskPlanAnswerResponse> Answers);

/// <summary>What a repository releases as its declaration reads: <c>Provides</c> and <c>Consumes</c> each as <c>resource (mode)</c>.</summary>
public sealed record AskPlanDeployableResponse(string Repository, string Id, IReadOnlyList<string> Provides, IReadOnlyList<string> Consumes);

/// <summary>A release order: <c>Deployable</c> after <c>On</c> (each <c>repository/id</c>), why, its <c>Source</c> (<c>declaration</c>, <c>stage-condition</c>, <c>contract</c>, <c>code-read</c>, <c>policy</c> or <c>person</c>), the <c>Evidence</c> it stands on (null with none), the person's <c>Waived</c> reason, and whether the evidence moved (<c>Stale</c>).</summary>
public sealed record AskPlanReleaseResponse(
    string Deployable, string On, string Why, string Source, AskPlanEvidenceResponse? Evidence, string? Waived = null, bool Stale = false);

/// <summary>A question for the person about the delivery, asked once under its <c>Key</c>: what yes does, what waits, and the default.</summary>
public sealed record AskPlanQuestionResponse(string Key, string Kind, string Text, string Yes, string Blocks, string Default);

/// <summary>One part: where it is done, what a person sees working when it is, and the quest it became, null until it is published.</summary>
public sealed record AskPlanPartResponse(
    string Id, string Repository, string? Quest, string Outcome, IReadOnlyList<string> Lanes, IReadOnlyList<string> Claims);

/// <summary>An order between two parts: <c>Kind</c> is <c>after</c> (a build dependency) or <c>orders</c> (a landing order only).</summary>
public sealed record AskPlanEdgeResponse(string Part, string On, string Kind, string Why);

/// <summary>What the plan's judge found, a sentence the supervisor reads: <c>Kind</c> names it.</summary>
public sealed record AskPlanFlagResponse(string Kind, string? Part, string Text);
