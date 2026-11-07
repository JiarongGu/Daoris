namespace Daoris.Knowledge;

/// <summary>
/// Where a driver-started session is in its life. Observed, never self-reported: the driver moves a
/// session by what it can see — process lifetime, and the quest's own transitions.
/// </summary>
public enum SessionState
{
    /// <summary>The driver decided to start this; waiting on the repository's slot, hold, or tree.</summary>
    Queued,

    /// <summary>The process is being spawned.</summary>
    Starting,

    /// <summary>The process is alive.</summary>
    Working,

    /// <summary>Parked at a checkpoint only the person can clear. Still holds its repository.</summary>
    AwaitingPerson,

    /// <summary>Exited; its quest reached Done.</summary>
    Completed,

    /// <summary>Exited; its quest reached Declined, with the reason on the quest.</summary>
    Declined,

    /// <summary>Exited without working: the quest is already taken or closed by someone else.</summary>
    StoodDown,

    /// <summary>The process died, or exited with the quest unexplained.</summary>
    Failed,

    /// <summary>
    /// The person cancelled it — or, where the record says <see cref="Session.Interrupted"/>, the orphan
    /// sweep or the driver's shutdown ended it.
    /// </summary>
    Stopped,
}
