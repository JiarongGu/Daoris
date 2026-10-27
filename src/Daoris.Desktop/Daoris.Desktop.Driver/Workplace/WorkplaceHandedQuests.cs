namespace Daoris.Driver;

/// <summary>
/// The quests held on another agent, as history clearing reads and lets them go (MIXAGENT1a-ii): Course keeps the store, so the
/// workplace names only these two operations, which the store implements directly.
/// </summary>
internal interface IWorkplaceHandedQuests
{
    /// <summary>Every quest held on another agent that reads, by id, in the store's order.</summary>
    IEnumerable<string> Readable();

    /// <summary>Let the quest go back to the machine's agent; nothing to clear is no failure.</summary>
    void Clear(string quest);
}

/// <summary>Course constructs the store over a home; the workplace holds that construction for history clearing's config step.</summary>
internal interface IWorkplaceHandedQuestStores
{
    IWorkplaceHandedQuests Open(string home);
}

internal static class WorkplaceHandedQuests
{
    private static IWorkplaceHandedQuestStores? _current;

    internal static IWorkplaceHandedQuestStores Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace handed-quest store factory is installed: Course installs it when the driver loads");
        set => _current = value;
    }
}
