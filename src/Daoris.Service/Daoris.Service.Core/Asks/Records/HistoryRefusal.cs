namespace Daoris.Knowledge;

/// <summary>
/// Why a unit stays on this machine whole, or a piece of one is listed and kept (HIST1b, design §1.2). The service judges
/// the records' half; a tree, a landing and a process are the driver's to judge (HIST1c).
/// </summary>
public enum HistoryRefusal
{
    None,

    /// <summary>No such quest or ask on this machine.</summary>
    Unknown,

    /// <summary>A quest of the work is open or taken; or the failed sessions asked for are an open quest's strikes (D58).</summary>
    Open,

    /// <summary>A quest an ask here asked, named on its own: it is cleared with its ask, or the ask reads as a proposal again (H6).</summary>
    Asked,

    /// <summary>A session of the work still runs here, or a teammate's still reads as running.</summary>
    Live,

    /// <summary>
    /// Something of it waits on the person: a parked session, a done held for their yes, a conflict nobody dismissed, an
    /// ask they have yet to publish or close, or a rule proposal its session made that nobody settled.
    /// </summary>
    NeedsYou,

    /// <summary>Open work names it: a taken quest waits on its answer (H7), a question its session published is still open, or a chain's next step is.</summary>
    Awaited,

    /// <summary>On a wired workspace, a move or a record of it has not reached the remote yet: the team's copy still to come.</summary>
    Unpushed,

    /// <summary>A teammate's failed session, which a failed-sessions clear lists and keeps: its record is theirs.</summary>
    NotOurs,
}
