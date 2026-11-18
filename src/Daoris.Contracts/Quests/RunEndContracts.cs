using System.Text.Json;

namespace Daoris.Contracts.Quests;

// The driver's doors on a workflow run's quests, a local host's alone: the bind at a session's first start, which makes the
// agent's done a submission, and the run's end, which writes the quests' Done and ends the sessions waiting on it in one write.

/// <summary>Binds a quest to the run whose end writes its Done: once, open or taken here, before any submission.</summary>
public sealed record BindQuestToRun(string Id, string? Run) : IRoute<RunBindResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/run";
}

/// <summary>Whether the bind is made, and why not.</summary>
public sealed record RunBindResponse(bool Bound, string Message);

/// <summary>
/// A run's end: where <c>State</c> is <c>completed</c>, each quest named closed Done on its submission; then each session named that still
/// waits on a step moved to <c>State</c>. A close refused moves no session, answered with its refusal.
/// </summary>
/// <param name="Quests">The run's quests here in its chain's order, each with the submission it stands on.</param>
/// <param name="NoteParts">The note's lines by code, beside it.</param>
public sealed record EndRun(
    string Run, IReadOnlyList<RunEndQuest>? Quests, IReadOnlyList<string>? Sessions, string? State, string? Note, JsonElement? NoteParts = null)
    : IRoute<RunEndResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/runs/{run}/end";
}

/// <summary>A quest a run's end closes, with the submission (its machine and sequence) it closes on, and whether the person passed its holds.</summary>
public sealed record RunEndQuest(string Id, string Machine, long Sequence, bool Passed = false);

/// <summary>What the end writes: the refusal's name where the close is refused, the quests closed and the sessions ended.</summary>
public sealed record RunEndResponse(string? Refused, string Message, IReadOnlyList<string> Closed, IReadOnlyList<string> Ended);
