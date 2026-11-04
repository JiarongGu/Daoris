namespace Daoris.Knowledge;

/// <summary>Why a session did not move — or <see cref="None"/> when it did.</summary>
public enum SessionAdvanceRefusal
{
    None,

    /// <summary>Not a state this ledger knows.</summary>
    UnknownState,

    /// <summary>No session under that id.</summary>
    NotFound,

    /// <summary>
    /// The session is finished, and a finished session is a record. Records do not move — but for the one move out of an
    /// ended state, to working with the person's words waiting (MSG1a, D137 §2.3).
    /// </summary>
    Terminal,

    /// <summary>A move the lifecycle does not allow.</summary>
    InvalidMove,

    /// <summary>
    /// An ended record would go on in a tree another session now holds (MSG1a): one session per working tree (D51)
    /// holds for going on as for a start.
    /// </summary>
    Busy,

    /// <summary>
    /// A move to working whose waiting supervisor's words are not exactly the ones it names (SUPERVISOR1a-i, the supervisor design
    /// §6.3): a plan made before a release, before a take-back or beside a word Daoris cannot attribute never moves the record.
    /// </summary>
    WordsChanged,
}
