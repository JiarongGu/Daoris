namespace Daoris.Contracts.Asks;

// An ask's plan as the page reads it: its parts with the quest each is, the edges between them and what the judge found. Read-only: the
// plan is written through the connector's `team_plan` door, by the lead, by Ask Daoris or by a person's hand. Local hosts only.

/// <summary>An ask's plan: the newest revision of its own plan, then of each split a worker made of its own part.</summary>
public sealed record ReadAskPlan(string Id) : IRoute<AskPlanResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/asks/{id}/plan";
}

/// <summary>The plans an ask holds, the ask's own first; none before anyone wrote one.</summary>
public sealed record AskPlanResponse(string Ask, IReadOnlyList<AskPlanRevisionResponse> Plans);

/// <summary>One plan at its newest revision. <c>Scope</c> is the part a split is of, or null for the ask's own plan.</summary>
public sealed record AskPlanRevisionResponse(
    string? Scope, int Revision, int Depth, string Why, IReadOnlyList<AskPlanPartResponse> Parts,
    IReadOnlyList<AskPlanEdgeResponse> Edges, IReadOnlyList<AskPlanFlagResponse> Flags, IReadOnlyList<AskPlanDeployableResponse> Deployables,
    IReadOnlyList<AskPlanReleaseResponse> Releases, IReadOnlyList<AskPlanQuestionResponse> Questions);

/// <summary>What a repository releases as its declaration reads: <c>Provides</c> and <c>Consumes</c> each as <c>resource (mode)</c>.</summary>
public sealed record AskPlanDeployableResponse(string Repository, string Id, IReadOnlyList<string> Provides, IReadOnlyList<string> Consumes);

/// <summary>
/// A release order: <c>Deployable</c> after <c>On</c> (each <c>repository/id</c>), why, and the <c>Source</c> it stands on: <c>declaration</c>,
/// <c>stage-condition</c>, <c>contract</c>, <c>code-read</c>, <c>policy</c> or <c>person</c>. <c>Evidence</c> names the place it stands on, null for an order
/// with none.
/// </summary>
public sealed record AskPlanReleaseResponse(string Deployable, string On, string Why, string Source, string? Evidence);

/// <summary>A question for the person about the delivery, asked once under its <c>Key</c>: what yes does, what waits, and the default.</summary>
public sealed record AskPlanQuestionResponse(string Key, string Kind, string Text, string Yes, string Blocks, string Default);

/// <summary>One part: where it is done, what a person sees working when it is, and the quest it became, null until it is published.</summary>
public sealed record AskPlanPartResponse(
    string Id, string Repository, string? Quest, string Outcome, IReadOnlyList<string> Lanes, IReadOnlyList<string> Claims);

/// <summary>An order between two parts: <c>Kind</c> is <c>after</c> (a build dependency) or <c>orders</c> (a landing order only).</summary>
public sealed record AskPlanEdgeResponse(string Part, string On, string Kind, string Why);

/// <summary>What the plan's judge found, a sentence the supervisor reads: <c>Kind</c> names it.</summary>
public sealed record AskPlanFlagResponse(string Kind, string? Part, string Text);
