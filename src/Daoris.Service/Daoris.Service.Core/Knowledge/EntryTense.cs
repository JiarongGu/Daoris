namespace Daoris.Knowledge;

/// <summary>
/// Whether an entry says how something works now, what held on its date, or how it came to be, which
/// <see cref="PresentFirst"/> reads.
/// </summary>
public enum EntryTense
{
    /// <summary>The rules, knowledge, skills, index rows and the documents named for no date: what is true now.</summary>
    Present,

    /// <summary>A document named for its date (a design, a study, a plan, its evidence) and not filed.</summary>
    Dated,

    /// <summary>Decisions, fixes, finished tasks, finished trees' records and the documents filed in an archive folder.</summary>
    History,
}
