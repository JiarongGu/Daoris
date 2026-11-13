namespace Daoris.Driver;

/// <summary>
/// Whether a turn ended in a record after a reading of its first events: a move reads the refused turns' words, then waits on the store
/// before it holds the turns, and a word said meanwhile may run and end in between, judged while the move stood and so moving nothing.
/// </summary>
internal static class TurnsSince
{
    /// <summary>Whether <paramref name="events"/> hold a turn's end after their first <paramref name="seen"/>.</summary>
    public static bool Ended(IReadOnlyList<SessionEvent> events, int seen) => events.Skip(seen).Any(e => e.Kind == SessionEventKind.Turn);
}
