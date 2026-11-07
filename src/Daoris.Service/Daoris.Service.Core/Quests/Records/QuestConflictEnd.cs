namespace Daoris.Knowledge;

/// <summary>How a settled conflict ended.</summary>
public enum QuestConflictEnd
{
    /// <summary>A plain dismissal: the person dropped their machine's work, which stays where it is.</summary>
    Dropped,

    /// <summary>A dismissal marked with the fresh take it made: the person brought their machine's work forward.</summary>
    BroughtForward,
}
