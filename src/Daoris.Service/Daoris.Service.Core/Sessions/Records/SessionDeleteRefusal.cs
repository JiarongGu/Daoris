namespace Daoris.Knowledge;

/// <summary>
/// Why a session record was not deleted — or <see cref="None"/> when it was, or would be (SESSUX1f, D126 §5.4). The
/// record's half only: whether its tree or a landing is still on the machine is the driver's to judge.
/// </summary>
public enum SessionDeleteRefusal
{
    None,

    /// <summary>No record under that id.</summary>
    NotFound,

    /// <summary>A teammate's record (SYNC4): it ran on their machine, and the record is theirs.</summary>
    NotOurs,

    /// <summary>It still runs or waits: stopped first, it ends.</summary>
    Live,

    /// <summary>
    /// It served a quest, and is that work's record: the strikes and the carry-ons are read from it (D58, D80). A chat that
    /// took one through its own connector served it too (CHATTAKE1).
    /// </summary>
    ServedQuest,

    /// <summary>An ask names it as its intake, or a quest was published by it (SESS1).</summary>
    Named,

    /// <summary>It went up to a workspace's remote, and a session record does not travel as a deletion.</summary>
    OnRemote,
}
