namespace Daoris.Knowledge;

/// <summary>Why a delete did not delete — or <see cref="None"/> when it did (D95).</summary>
public enum QuestDeleteRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>
    /// Something stands on it — taken, done or declined, a session started for it, or a taken quest
    /// waiting on it — so its record stays.
    /// </summary>
    Kept,

    /// <summary>Another machine's take reached the remote first: the delete lost, and the quest stays, taken.</summary>
    TakenElsewhere,
}
