namespace Daoris.Knowledge;

/// <summary>
/// How the work in progress stands, where a unit stays for <see cref="HistoryRefusal.Open"/>: named by the desk that
/// judged it, as <see cref="HistoryWaiting"/> is, so the driver says *taken* from the refusal rather than from a second read
/// of the quest, which may have been taken or closed between the two.
/// </summary>
public enum HistoryStands
{
    /// <summary>The quest is still open.</summary>
    Open,

    /// <summary>The quest is taken.</summary>
    Taken,
}
