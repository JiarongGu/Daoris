namespace Daoris.Knowledge;

/// <summary>
/// Why a session record is not deleted — or <see cref="None"/> when it is, or would be. The
/// record's half only: whether its tree or a landing is still on the machine is the driver's to judge.
/// </summary>
public enum SessionDeleteRefusal
{
    None,

    /// <summary>No record under that id.</summary>
    NotFound,

    /// <summary>A teammate's record: it ran on their machine, and the record is theirs.</summary>
    NotOurs,

    /// <summary>It still runs or waits: stopped first, it ends.</summary>
    Live,

    /// <summary>
    /// It served a quest, and is that work's record: the strikes and the carry-ons are read from it. A chat that
    /// took one through its own connector served it too.
    /// </summary>
    ServedQuest,

    /// <summary>An ask names it as its intake, or a quest is published by it.</summary>
    Named,

    /// <summary>It went up to a workspace's remote, and a session record does not travel as a deletion.</summary>
    OnRemote,
}
