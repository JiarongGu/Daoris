namespace Daoris.Knowledge;

/// <summary>
/// One turn line of a session's usage history, as the driver hands it at the commit point (the memory coordinator design §1): its sequence,
/// when it started where the line says, when it ended, and its model, or the split's models joined.
/// </summary>
public sealed record MemoryTurn(long Seq, DateTimeOffset? Started, DateTimeOffset Ended, string Model);

/// <summary>A session's turn lines, and the instants the person asked it something, which tell a turn with no start from the one before it.</summary>
public sealed record MemoryTurns(IReadOnlyList<MemoryTurn> Turns, IReadOnlyList<DateTimeOffset> Asked);

/// <summary>Which model made an operation, read from its session's turn lines by the operation's instant (§1).</summary>
public static class MemoryModels
{
    /// <summary>What a row names where no turn line says.</summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// The models of the turn whose start and end hold <paramref name="at"/>. A first turn ending after the instant with no start is the
    /// row's only where the person asked nothing between the two: an ask there began a later turn, after a stopped one that kept no line.
    /// A pruned or dropped line holds no instant, so its rows read <see cref="Unknown"/>.
    /// </summary>
    public static string Of(MemoryTurns? turns, DateTimeOffset at)
    {
        var turn = turns?.Turns.Where(each => each.Ended >= at).OrderBy(each => each.Ended).ThenBy(each => each.Seq).FirstOrDefault();
        if (turn is null) return Unknown;
        if (turn.Started is { } started) return started <= at ? Named(turn.Model) : Unknown;
        return turns!.Asked.Any(asked => asked > at && asked <= turn.Ended) ? Unknown : Named(turn.Model);
    }

    private static string Named(string? model) => string.IsNullOrWhiteSpace(model) ? Unknown : model.Trim();
}
