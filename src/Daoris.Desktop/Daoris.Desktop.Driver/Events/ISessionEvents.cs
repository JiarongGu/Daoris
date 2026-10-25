namespace Daoris.Driver;

/// <summary>
/// A session's events as the events' own watches read them (FOUNDATION1 mod-vii, the design §14.3): told of each one as it is written, and
/// handed one to keep. The record that writes them, <see cref="SessionEvents"/>, is the context's and answers this port, so the watches
/// of calls, held permissions, quiet and the machine log name no type above the events, and a watch keys what it keeps by the record's
/// instance, whichever record it is.
/// </summary>
public interface ISessionEvents
{
    /// <summary>
    /// Raised for every event, after it is written. The shell turns it into its IPC event; a handler that throws costs its own view,
    /// never the record or the session.
    /// </summary>
    event Action<string, SessionEvent>? Evented;

    /// <summary>
    /// Record one event, and a failure to write it costs its caller a console line, never the session: <paramref name="say"/> is where the
    /// reason goes, or null to drop it.
    /// </summary>
    void Keep(string sessionId, SessionEvent e, Action<string>? say);
}
