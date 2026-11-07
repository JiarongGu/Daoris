namespace Daoris.Contracts.History;

/// <summary>
/// Why a unit stays, or a piece of it is kept: <c>Error</c> the desk's sentence, <c>Refusal</c> its word (<c>unknown</c>, <c>open</c>,
/// <c>asked</c>, <c>live</c>, <c>needs-you</c>, <c>awaited</c>, <c>unpushed</c>, <c>not-ours</c>), which the driver reads instead of the
/// sentence, and the quest, ask, session, machine or workspace it names. <c>Waits</c>, beside <c>needs-you</c> only, names what waits on
/// the person (<c>parked</c>, <c>held</c>, <c>conflict</c>, <c>ask</c>, <c>proposal</c>); <c>Stands</c>, beside <c>open</c> only, how the
/// work in progress stands (<c>open</c>, <c>taken</c>); <c>By</c>, beside <c>awaited</c> only, the open work that names it
/// (<c>question</c>, <c>asker</c>, <c>step</c>).
/// </summary>
public sealed record HistoryRefusalResponse(
    string Refusal, string Error, string? Quest = null, string? Ask = null, string? Session = null, string? Origin = null,
    string? Workspace = null, string? Waits = null, string? Stands = null, string? By = null);

/// <summary>
/// One unit (<c>Kind</c>: <c>quest</c>, <c>ask</c> or <c>failed</c>): what a clear takes, by id, and whether it may go now.
/// <c>Forgotten</c> is which of its quests a remote numbered, forgotten here rather than removed; <c>Teammates</c> this machine's copies
/// of a teammate's records; <c>Kept</c> the pieces listed and kept while the unit goes (a teammate's failed session).
/// </summary>
public sealed record HistoryUnitResponse(
    string Kind, string Id, string? Workspace, bool Clearable, IReadOnlyList<string> Quests, IReadOnlyList<string> Forgotten,
    IReadOnlyList<string> Asks, IReadOnlyList<string> Sessions, IReadOnlyList<string> Teammates,
    HistoryRefusalResponse? Refusal, IReadOnlyList<HistoryRefusalResponse> Kept);

public sealed record HistoryPlanResponse(IReadOnlyList<HistoryUnitResponse> Units);

/// <summary>The second press names its units as the first listed them.</summary>
public sealed record HistoryUnitRequest(string? Kind, string? Id);

/// <summary>
/// What the press did to one unit: the unit as judged where it is cleared, whether it went, and the desk's sentence. <c>Failed</c>,
/// only where a removal failed, names the quests and asks whose kept files the disk would not let go of, so the driver counts them
/// as failed and frees none of their bytes.
/// </summary>
public sealed record HistoryClearedResponse(HistoryUnitResponse Unit, bool Cleared, string Message, HistoryFailedResponse? Failed = null);

public sealed record HistoryFailedResponse(IReadOnlyList<string> Quests, IReadOnlyList<string> Asks);

public sealed record HistoryClearResponse(IReadOnlyList<HistoryClearedResponse> Units);
