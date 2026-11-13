namespace Daoris.Contracts.Asks;

/// <summary>The person's answer to an open question (<c>Key</c>, <c>Words</c>) or waiver of a release order (<c>Deployable</c> after <c>On</c>, each <c>repository/id</c>, with <c>Reason</c>); answers the plan as it then reads.</summary>
public sealed record SettleAskPlan(
    string Id, string? Key = null, string? Words = null, string? Deployable = null, string? On = null, string? Reason = null) : IRoute<AskPlanResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/plan/settle";
}

/// <summary>What the person said to a question of the delivery, which settled it.</summary>
public sealed record AskPlanAnswerResponse(string Key, string Words, DateTimeOffset At);

/// <summary>The place a release order's reason stands on: a file, the range and the blob the lead read.</summary>
public sealed record AskPlanEvidenceResponse(string Repository, string File, string Range, string Blob);
