namespace Daoris.Knowledge;

/// <summary>Where a quest is in its life. Four states, because anything finer is status for its own sake.</summary>
public enum QuestStatus
{
    /// <summary>Published, nobody has taken it.</summary>
    Open,

    /// <summary>A repository's agent has accepted it.</summary>
    Taken,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Turned down — a real answer, and the reason is the part the asker can act on.</summary>
    Declined,
}
