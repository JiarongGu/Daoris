namespace Daoris.Knowledge;

/// <summary>What one clear takes (HIST1b, D153 point 1; the history-clearing design §1.1).</summary>
public enum HistoryUnitKind
{
    /// <summary>
    /// A closed quest's work: the quest, every question its sessions asked, applied again to what each adds, as the
    /// driver's <c>AskWork.Read</c> reads a quest's work (D132 §1), and every session record that served them.
    /// </summary>
    Quest,

    /// <summary>An ask's work: the ask, its intake, and every quest it asked with that quest's work, chain steps included.</summary>
    Ask,

    /// <summary>A closed quest's failed sessions of this machine's. The quest, its log and its other sessions stay.</summary>
    Failed,
}
