namespace Daoris.Driver;

/// <summary>Who holds the writer's lease of the place its session works in, as the ledger reads it.</summary>
public static class PlaceLeases
{
    /// <summary>
    /// Whether <paramref name="session"/> holds its place's lease: an active record that is no ask, opinion, service or decision, and not
    /// parked unless it holds uncommitted paths of its own.
    /// </summary>
    public static bool Holds(SessionView session) =>
        session is { Ask: null, Opinion: null, Serves: null, Decides: null }
        && (!SessionStates.IsParked(session.State) || session.HoldsPlace is { Count: > 0 });
}
