namespace Daoris.Knowledge;

/// <summary>What became of an ask.</summary>
public enum AskState
{
    /// <summary>Asked, and nothing has answered yet.</summary>
    Open,

    /// <summary>A tier proposed where it belongs — possibly nowhere — and nothing is published.</summary>
    Proposed,

    /// <summary>At least one quest carries it.</summary>
    Published,

    /// <summary>The person closed it, with a reason.</summary>
    Closed,

    /// <summary>
    /// Its work is finished: it became at least one quest, and none of the quests asked by it is
    /// open or taken — chain steps included, since a step is asked by the ask too. Never stored: the
    /// desk derives it from the quests on every read, so a quest closed on another machine and synced
    /// in is reflected with no write to the ask. A closed ask stays closed, because that is the
    /// person's word on it.
    /// </summary>
    Done,
}
