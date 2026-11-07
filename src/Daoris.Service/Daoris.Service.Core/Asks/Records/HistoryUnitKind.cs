namespace Daoris.Knowledge;

/// <summary>What one clear takes.</summary>
public enum HistoryUnitKind
{
    /// <summary>
    /// A closed quest's work: the quest, every question its sessions asked, applied again to what each adds, as the
    /// driver's <c>AskWork.Read</c> reads a quest's work, and every session record that served them.
    /// </summary>
    Quest,

    /// <summary>An ask's work: the ask, its intake, and every quest it asked with that quest's work, chain steps included.</summary>
    Ask,

    /// <summary>A closed quest's failed sessions of this machine's. The quest, its log and its other sessions stay.</summary>
    Failed,
}
