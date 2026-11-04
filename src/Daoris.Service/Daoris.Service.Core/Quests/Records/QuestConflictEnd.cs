namespace Daoris.Knowledge;

/// <summary>How a settled conflict ended (SYNCMERGE1b0, D178 point 7).</summary>
public enum QuestConflictEnd
{
    /// <summary>A plain dismissal (SYNC6c): the person dropped their machine's work, which stays where it is.</summary>
    Dropped,

    /// <summary>A dismissal marked with the fresh take it made (SYNCMERGE1a): the person brought their machine's work forward.</summary>
    BroughtForward,
}
