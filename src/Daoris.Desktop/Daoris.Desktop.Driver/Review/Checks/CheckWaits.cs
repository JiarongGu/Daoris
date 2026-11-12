namespace Daoris.Driver;

/// <summary>
/// The rules a driven session waiting on its own check is read by, the twin of <see cref="HelperWaits"/>: a session whose agent asked the
/// broker for a check and ended its turn waits on that check, not on the person. It ends <c>completed</c> by
/// <see cref="EndNoteCodes.EndedAwaitsCheck"/> holding its tree, and the verdict is its next word in the same conversation.
/// </summary>
public static class CheckWaits
{
    /// <summary>The checks <paramref name="session"/>'s agent asked that are still open: no verdict handed yet. A check Daoris asked is never the agent's wait.</summary>
    public static IReadOnlyList<CheckAsk> Open(string session, IEnumerable<CheckAsk> asks, Func<string, CheckState?> state) =>
        [.. asks.Where(ask => string.Equals(ask.Session, session, StringComparison.Ordinal) && ask.By == CheckAsk.ByAgent && state(ask.Id)?.Handed is null)];

    /// <summary>Whether a run's conclusion is the wait on its own check: its note's code.</summary>
    public static bool Ended(IReadOnlyList<NotePart>? parts) => HelperWaitTrees.Carries(parts, EndNoteCodes.EndedAwaitsCheck);

    /// <summary>Whether <paramref name="record"/> ended waiting on its own check with nothing waiting on it yet: the verdict's word is what goes on with it.</summary>
    public static bool Waits(PriorSession record) =>
        !record.Teammate && string.Equals(record.State, "completed", StringComparison.OrdinalIgnoreCase) && Ended(record.NoteParts) && !record.WordsWaiting;

    /// <summary>The key a verdict's word is kept under, so it is said once however often a look asks.</summary>
    public static string Key(CheckAsk ask) => $"check/{ask.Id}";

    /// <summary>The verdict as Daoris's word to the session that asked: what ran, how it ended, and a red's log tail.</summary>
    public static string Words(CheckAsk ask, CheckState state)
    {
        var what = ask.Gate is { } gate ? $"`{gate}`" : ask.Command is { } command ? $"`{HeavyRun.CommandText(command)}`" : "the heavy gates";
        var who = ask.By == CheckAsk.ByDaoris ? "Daoris ran" : "Daoris ran, as you asked,";
        var head = state.State switch
        {
            CheckStates.Reached => $"{who} {what} in your tree and it reached.",
            CheckStates.Stopped => $"{who} {what} in your tree and the run was stopped before it ended.",
            _ => $"{who} {what} in your tree and it failed" + (state.Exit is { } exit ? $" (exit {exit})." : "."),
        };
        return state.State == CheckStates.Reached || state.Tail is not { Length: > 0 } tail
            ? head
            : $"{head} The end of its log:\n\n{tail.TrimEnd()}";
    }
}
