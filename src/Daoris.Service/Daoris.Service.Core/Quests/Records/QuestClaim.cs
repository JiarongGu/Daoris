namespace Daoris.Knowledge;

/// <summary>Where this machine's claim on a quest stands (D68 §4, D69).</summary>
public enum QuestClaim
{
    /// <summary>This machine never took it.</summary>
    None,

    /// <summary>A remote numbered this machine's take: the quest is this machine's, everywhere.</summary>
    Held,

    /// <summary>The take is only here — no remote answered yet, or there is none.</summary>
    Unconfirmed,

    /// <summary>Another machine's take reached the remote first; this one is a conflict on the quest.</summary>
    Lost,
}
