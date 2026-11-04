namespace Daoris.Driver;

/// <summary>The units a pass reads (<see cref="WorkUnits"/>), and each repository's checkout here, which the awaited set names.</summary>
public sealed record NodeUnitsRead(IReadOnlyList<WorkUnit> Units, IReadOnlyDictionary<string, string> Roots)
{
    /// <summary>
    /// The tip of each branch a bound quest's chain worked, as git answered this one read (<see cref="QuestClosing.TipsAsync"/>), by the
    /// branch unit's id: a commit, empty where the branch is not there, null where git did not say. What a closing quest's revision digests
    /// (<see cref="QuestClosing.Revision"/>). Null where the read asked no git at all: a close made on it waits.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Tips { get; init; }

    /// <summary>
    /// Why the work's attribution was not read: without the service, or where it did not answer, the units are the branches alone, with none
    /// of the links sessions make. Empty where it was read. A missing link is never read as unbound work.
    /// </summary>
    public IReadOnlyList<string> Unread { get; init; } = [];
}
