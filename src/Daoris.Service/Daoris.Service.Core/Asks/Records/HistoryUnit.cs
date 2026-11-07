namespace Daoris.Knowledge;

/// <summary>
/// One unit as the desk judged it: what it holds and would take, and whether it may go (design §6.3). Listed by the first
/// press and judged again by the second.
/// </summary>
/// <param name="Workspace">The workspace its records are filed under; null for a unit this machine does not hold.</param>
public sealed record HistoryUnit(HistoryUnitKind Kind, string Id, string? Workspace)
{
    /// <summary>Every quest it takes, the one it is read from first; none for a failed-sessions clear.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>Of <see cref="Quests"/>, those a remote numbered: forgotten here and kept there, not simply removed (design §3.2).</summary>
    public IReadOnlyList<string> Forgotten { get; init; } = [];

    /// <summary>The ask it takes, for an ask's work.</summary>
    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>This machine's session records it takes, intakes included.</summary>
    public IReadOnlyList<string> Sessions { get; init; } = [];

    /// <summary>This machine's copies of a teammate's records it takes, keyed <c>origin/id</c>.</summary>
    public IReadOnlyList<string> Teammates { get; init; } = [];

    /// <summary>Why the whole unit stays; null when it may go.</summary>
    public HistoryKept? Refusal { get; init; }

    /// <summary>Pieces listed and kept while the unit goes: a teammate's failed session (<see cref="HistoryRefusal.NotOurs"/>).</summary>
    public IReadOnlyList<HistoryKept> Kept { get; init; } = [];

    /// <summary>Whether it may go now. Nothing to clear is no refusal: a unit that takes nothing may still be pressed.</summary>
    public bool Clearable => Refusal is null;
}
