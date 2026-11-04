namespace Daoris.Driver;

/// <summary>
/// Where in the protocol door's reading of a frame a stage runs: each phase is an anchor in the session's dispatch, which runs that
/// phase's stages in order there. A phase is added with its anchor, so a stage can never be declared in a phase no frame reaches.
/// </summary>
internal enum TurnPhase
{
    /// <summary>
    /// Each <c>session/update</c> of the session's own, once a load's replay is passed over, the background work and the wake have read
    /// it and a subagent's is routed to its own stream: what the agent says of itself, then the update rendered for the console and
    /// the record.
    /// </summary>
    Update,

    /// <summary>
    /// Each request the agent makes of the client, answered by the stage that takes its method. One no stage takes is answered as a
    /// method this client does not implement: an unanswered request is a turn hung for ever.
    /// </summary>
    Request,
}
