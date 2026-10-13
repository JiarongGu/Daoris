namespace Daoris.Driver;

/// <summary>A unit as a press names it (HIST1b's door): its kind's word (<c>quest</c>, <c>ask</c> or <c>failed</c>) and its id.</summary>
public sealed record HistoryUnitName(string Kind, string Id);

/// <summary>
/// Why the service keeps a unit, or a piece of one, as its history door says it (HIST1b): its word, which the driver acts on,
/// the desk's sentence, which only a terminal prints, and what the word names.
/// </summary>
/// <param name="Word"><c>unknown</c>, <c>open</c>, <c>asked</c>, <c>live</c>, <c>needs-you</c>, <c>awaited</c>, <c>unpushed</c> or <c>not-ours</c>.</param>
public sealed record HistoryRefusalView(string Word, string Message)
{
    public string? Quest { get; init; }

    public string? Ask { get; init; }

    public string? Session { get; init; }

    /// <summary>The machine a teammate's record ran on.</summary>
    public string? Origin { get; init; }

    public string? Workspace { get; init; }

    /// <summary>
    /// What waits on the person, beside <c>needs-you</c> only (HIST1l): one of <see cref="HistoryWaits"/>. Null from a host before
    /// it, which names nothing waiting.
    /// </summary>
    public string? Waits { get; init; }

    /// <summary>
    /// How the work in progress stands, beside <c>open</c> only (HIST1m): one of <see cref="HistoryStands"/>. Null from a host
    /// before it.
    /// </summary>
    public string? Stands { get; init; }

    /// <summary>
    /// The open work that names the unit, beside <c>awaited</c> only (HIST1m): one of <see cref="HistoryAwaitedBy"/>. Null from a
    /// host before it.
    /// </summary>
    public string? By { get; init; }
}

/// <summary>One unit as the service's history door judged it (HIST1b's <c>HistoryDesk</c>, the history-clearing design §6.3).</summary>
/// <param name="Workspace">Its records' workspace; null for a unit this machine does not hold.</param>
public sealed record HistoryUnitView(string Kind, string Id, string? Workspace)
{
    public bool Clearable { get; init; }

    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>Of <see cref="Quests"/>, those a remote numbered: forgotten here, and kept there.</summary>
    public IReadOnlyList<string> Forgotten { get; init; } = [];

    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>This machine's session records it takes.</summary>
    public IReadOnlyList<string> Sessions { get; init; } = [];

    /// <summary>This machine's copies of a teammate's records, keyed <c>origin/id</c>.</summary>
    public IReadOnlyList<string> Teammates { get; init; } = [];

    /// <summary>Why the whole unit stays; null when the service would clear it.</summary>
    public HistoryRefusalView? Refusal { get; init; }

    /// <summary>Pieces listed and kept while the unit goes: a teammate's failed session.</summary>
    public IReadOnlyList<HistoryRefusalView> Kept { get; init; } = [];

    public HistoryUnitName Name => new(Kind, Id);
}

/// <summary>What the service's press did to one unit: the unit as judged where it was cleared, whether it went, and its sentence.</summary>
public sealed record HistoryClearedView(HistoryUnitView Unit, bool Cleared, string Message)
{
    /// <summary>The quests whose kept files the service could not remove (HIST1j's <c>failed</c>); none from a host before it.</summary>
    public IReadOnlyList<string> FailedQuests { get; init; } = [];

    /// <summary>The asks whose kept files the service could not remove; none from a host before it.</summary>
    public IReadOnlyList<string> FailedAsks { get; init; } = [];
}
