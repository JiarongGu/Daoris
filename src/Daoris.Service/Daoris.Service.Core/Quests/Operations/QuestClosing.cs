namespace Daoris.Knowledge;

/// <summary>
/// Why a graph's close wrote nothing, or <see cref="None"/> when it closed every quest it named. Each
/// refusal names the quest it failed on, and nothing is written for any.
/// </summary>
public enum QuestCloseRefusal
{
    None,

    /// <summary>The call is not one: no key or token, no quest named, a quest named twice, or a submission half named.</summary>
    Malformed,

    /// <summary>No quest under a named id.</summary>
    NotFound,

    /// <summary>A named quest no closure graph holds: its taker's driver never bound it.</summary>
    NotBound,

    /// <summary>A named quest this machine does not hold: never taken here, or taken on another machine first.</summary>
    NotHeld,

    /// <summary>A named quest is already done or declined, by another close, the person, or another machine.</summary>
    Closed,

    /// <summary>The submission named is not the quest's current one, or it has none: the close's binding is stale.</summary>
    Stale,

    /// <summary>A named quest's submission is held for the person: a departure, its evidence, or its review.</summary>
    Held,

    /// <summary>A named quest's chain step is not published from its submission, so the submission is not sealed.</summary>
    Unsealed,

    /// <summary>
    /// The key is a close's already, sent with another token or other submissions: not that close again, so
    /// not answered with its outcome. A new attempt takes a new key.
    /// </summary>
    KeyReused,

    /// <summary>
    /// No close took the key before the driver asked what became of it, so its close never ran, and the
    /// question withdrew it: kept under the key like any outcome, so a send of it arriving after is refused and never runs. Kept
    /// by its name; a build before it reads the name as no refusal it knows, which it answers as a key not that close's.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// A named quest asks another repository than the quest the close is at: a close is one repository's, the chain's
    /// quests there, never another's, whose own graph closes them.
    /// </summary>
    OtherRepository,

    /// <summary>
    /// A named quest is not of the chain the quest the close is at grew from: neither that quest nor one of its ancestors,
    /// walked through its parents as the driver walks them (<c>QuestClosing.Lineage</c>).
    /// </summary>
    NotInChain,

    /// <summary>A named quest is bound to another graph than the quest the close is at: one graph's close is its own.</summary>
    OtherGraph,
}

/// <summary>
/// A close's refusal as the close door names it on the wire: the driver keeps the code beside the sentence, so it tells a key
/// gone with another close from a check a quest's state failed without reading the words. 🔴 The driver's <c>QuestCloseOutcomes.Codes</c>
/// lists each code (twins): a code it does not know leaves the close unknown, so a code added here is added there.
/// </summary>
public static class QuestCloseCodes
{
    /// <summary>The refusal's code, or null for none.</summary>
    public static string? Of(QuestCloseRefusal refusal) => refusal switch
    {
        QuestCloseRefusal.None => null,
        QuestCloseRefusal.Malformed => "malformed",
        QuestCloseRefusal.NotFound => "not-found",
        QuestCloseRefusal.NotBound => "not-bound",
        QuestCloseRefusal.NotHeld => "not-held",
        QuestCloseRefusal.Closed => "closed",
        QuestCloseRefusal.Stale => "stale",
        QuestCloseRefusal.Held => "held",
        QuestCloseRefusal.Unsealed => "unsealed",
        QuestCloseRefusal.KeyReused => "key-reused",
        QuestCloseRefusal.Withdrawn => "withdrawn",
        QuestCloseRefusal.OtherRepository => "other-repository",
        QuestCloseRefusal.NotInChain => "not-in-chain",
        QuestCloseRefusal.OtherGraph => "other-graph",
        _ => "refused",
    };
}

/// <param name="Refusal"><see cref="QuestCloseRefusal.None"/> when every named quest is Done.</param>
/// <param name="Quest">The quest a refusal failed on, or null.</param>
/// <param name="Quests">Every named quest as it now stands, in the order named; empty where one is not found.</param>
/// <param name="Replayed">
/// Whether this is the same close again, answered with its first outcome and writing nothing: a close, or a refusal
/// whose quest may have moved since.
/// </param>
/// <param name="On">The id a refusal failed on, kept with it, so a replayed refusal names it whether or not the quest is found.</param>
public sealed record QuestClosing(
    QuestCloseRefusal Refusal, Quest? Quest, IReadOnlyList<Quest> Quests, bool Replayed = false, string? On = null);
