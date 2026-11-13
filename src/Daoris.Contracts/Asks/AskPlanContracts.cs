namespace Daoris.Contracts.Asks;

// An ask's plan as the page reads it: its parts with the quest each is, the edges between them and what the judge found. The plan is
// written through the connector's `team_plan` door, by the lead, by Ask Daoris or by a person's hand; the person's own two acts on its
// delivery, an answer and a waiver, are `SettleAskPlan`. Local hosts only.

/// <summary>
/// An ask's plan: the newest revision of its own plan, then of each split a worker made of its own part. A read, sent as a POST for the
/// blobs the reader holds: an order whose evidence holds another blob there is named stale, and asked about.
/// </summary>
/// <param name="Seen">The blob id each evidence place (<c>repository:file</c>) holds now, for the places the reader looked at; absent where it looked at none.</param>
public sealed record ReadAskPlan(string Id, IReadOnlyDictionary<string, string>? Seen = null) : IRoute<AskPlanResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/plan";
}

/// <summary>
/// The person's answer to one open question of an ask's delivery plan (<c>Key</c> and <c>Words</c>), or their waiver of one release order
/// (<c>Deployable</c> after <c>On</c>, each <c>repository/id</c>, with their <c>Reason</c>). Either is kept on the plan's next revision.
/// Answers the plan as it then reads.
/// </summary>
public sealed record SettleAskPlan(
    string Id, string? Key = null, string? Words = null, string? Deployable = null, string? On = null, string? Reason = null) : IRoute<AskPlanResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/plan/settle";
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

/// <summary>What the person said to a question of the delivery, which settled it.</summary>
public sealed record AskPlanAnswerResponse(string Key, string Words, DateTimeOffset At);

/// <summary>The place a release order's reason stands on: the file in its repository, the range read and the blob it was read at.</summary>
public sealed record AskPlanEvidenceResponse(string Repository, string File, string Range, string Blob);

/// <summary>What a repository releases as its declaration reads: <c>Provides</c> and <c>Consumes</c> each as <c>resource (mode)</c>.</summary>
public sealed record AskPlanDeployableResponse(string Repository, string Id, IReadOnlyList<string> Provides, IReadOnlyList<string> Consumes);

/// <summary>
/// A release order: <c>Deployable</c> after <c>On</c> (each <c>repository/id</c>), why, and the <c>Source</c> it stands on: <c>declaration</c>,
/// <c>stage-condition</c>, <c>contract</c>, <c>code-read</c>, <c>policy</c> or <c>person</c>. <c>Evidence</c> names the place it stands on, null for an order
/// with none. <c>Waived</c> is the person's reason where they waived it, <c>Stale</c> whether the evidence holds another blob where the reader looked.
/// </summary>
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
