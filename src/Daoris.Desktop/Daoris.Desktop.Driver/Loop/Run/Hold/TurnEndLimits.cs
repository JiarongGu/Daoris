namespace Daoris.Driver;

/// <summary>
/// A protocol turn's ending read for its account's limit by the one reader (<see cref="AgentErrors.Read"/>), as a refused prompt's words
/// are: Claude Code's adapter ends a turn <c>end_turn</c> with the limit said as its agent's last message, carrying no failure, so that
/// message stands as the door's words. Only a limit counts; any other reading leaves the turn's end alone.
/// </summary>
internal static class TurnEndLimits
{
    /// <summary>Whether the last message is the account's limit, told to <paramref name="read"/> where it is.</summary>
    /// <param name="clock">When the turn ended, which a reset is read from.</param>
    /// <param name="zone">This machine's zone, for a reset that names none.</param>
    public static Func<string, bool> For(
        ProviderRegistry adapters, AgentDoor adapter, Func<DateTimeOffset> clock, TimeZoneInfo zone, Action<AgentError>? read = null) =>
        words =>
        {
            var error = AgentErrors.Read(adapters, adapter, DoorFailures.OfTurnEnd(words), clock(), zone);
            if (error.Kind != AgentErrorKind.Limit) return false;
            read?.Invoke(error);
            return true;
        };
}
