namespace Daoris.Knowledge;

/// <summary>How a session was entered — the one thing that differs between them (D49 §3).</summary>
public enum SessionKind
{
    /// <summary>Planned from a quest by the driver: the loop D46 built.</summary>
    Driven,

    /// <summary>
    /// Opened by a person, to talk. It may serve no quest at all, may take one mid-conversation
    /// through its own connector, or may end by publishing new ones — the quest system is where work
    /// lands, not the toll to start talking.
    /// </summary>
    Chat,
}
