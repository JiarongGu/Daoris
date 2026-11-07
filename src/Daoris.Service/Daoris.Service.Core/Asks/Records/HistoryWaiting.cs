namespace Daoris.Knowledge;

/// <summary>
/// Which thing waits on the person, where a unit stays for <see cref="HistoryRefusal.NeedsYou"/>: named by the desk
/// that judged it, so the driver says the sentence meant from the refusal itself rather than from a second read of the
/// records, which may have moved between the two.
/// </summary>
public enum HistoryWaiting
{
    /// <summary>A session of the work parked to ask the person.</summary>
    Parked,

    /// <summary>A done held for the person's yes.</summary>
    Held,

    /// <summary>A conflict on a quest of the work that nobody dismissed.</summary>
    Conflict,

    /// <summary>The ask, proposed or open: the person has yet to publish or close it.</summary>
    Ask,

    /// <summary>A rule proposal nobody settled: from a session of the work where it names one, else for the ask.</summary>
    Proposal,
}
