namespace Daoris.Driver;

/// <summary>
/// Where in a driven session's run a stage runs: each phase is an anchor in the driver's run of one start, which runs that phase's
/// stages in order there. A phase is added with its anchor, so a stage can never be declared in a phase that no run runs.
/// </summary>
internal enum RunPhase
{
    /// <summary>
    /// Before anything is grown, locked or opened: the agent the start runs on, a clean checkout, the account the walk takes and the
    /// access setup. A hold here records nothing and leaves nothing to undo.
    /// </summary>
    Choose,

    /// <summary>
    /// The tree the session runs in found or grown, with the repository's trees held until the record is open: what may still hold the
    /// start there leaves the tree it grew to be let go.
    /// </summary>
    Grown,

    /// <summary>
    /// Once the tree the session runs in is open and held, before its record opens: what may still hold the start there, a steward's
    /// start outside a tree of this home, the run's workflow binding and a set-up step's procedure and tab. A hold here lets the fresh
    /// tree go.
    /// </summary>
    Placed,

    /// <summary>
    /// Once placed, before the record opens: what the tree's trust and a record the person's words wait on may still hold the start
    /// for, or move it to a new session. A hold here lets the fresh tree go only where the stage says so.
    /// </summary>
    Cleared,

    /// <summary>
    /// The record opens through the ledger and says how it was chosen: a refusal of the open is an answer, and the repository's trees
    /// are let go once it is open.
    /// </summary>
    Opened,

    /// <summary>
    /// The opened record handed to its session, which runs to its conclusion: the one stage here ends in the run's own start however the
    /// session ended, so nothing runs after it.
    /// </summary>
    Handed,
}
