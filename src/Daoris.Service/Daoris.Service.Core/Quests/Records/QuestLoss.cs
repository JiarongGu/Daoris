namespace Daoris.Knowledge;

/// <summary>What a rebase does with a pending operation that no longer applies after a fetch (D68 rule 2, sync design §8).</summary>
public enum QuestLoss
{
    /// <summary>Dropped from the log: it was never anybody's decision, or it has nothing left to say.</summary>
    Forgotten,

    /// <summary>Rewritten as a conflict on the quest, kept for a person, naming the status it attempted.</summary>
    Conflict,

    /// <summary>Left as it is: a conflict already, on a quest another machine deleted (D95).</summary>
    Kept,
}
