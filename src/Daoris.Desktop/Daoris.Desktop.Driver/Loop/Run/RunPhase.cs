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
}
