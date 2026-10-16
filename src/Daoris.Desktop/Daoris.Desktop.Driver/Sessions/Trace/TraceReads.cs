namespace Daoris.Driver;

/// <summary>
/// The files a trace opens on this machine (TRACE1, D143): a session's events and the rules file the driver wrote for it.
/// They are read here and handed to <see cref="TraceChains"/> as <see cref="TraceHere"/>'s values, so the chain, which is a
/// Domain, opens none (STRUCT3g-i). Nothing here writes.
/// </summary>
internal static class TraceReads
{
    /// <summary>This machine's side of a trace: the settings and a read of each store under the home.</summary>
    internal static TraceHere Here(TraceSources sources) =>
        new(sources.Config, id => Events(sources.Home, id), id => Rules(sources.Home, id));

    private static TraceEventsRead Events(string home, string sessionId)
    {
        var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
        try
        {
            return File.Exists(events.PathOf(sessionId))
                ? new TraceEventsRead(events.After(sessionId, 0).Events)
                : new TraceEventsRead(null, TraceEventGaps.NoneHere);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return new TraceEventsRead(null, TraceEventGaps.Unread, error.Message);
        }
    }

    private static TraceRulesRead Rules(string home, string sessionId)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Spawn, $"{sessionId}.settings.json");
        if (!File.Exists(path)) return new TraceRulesRead(null);

        try
        {
            return new TraceRulesRead(File.ReadAllText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new TraceRulesRead(null, error.Message);
        }
    }
}
