namespace Daoris.Driver;

/// <summary>
/// The rules a driven session waiting on its own check is read by: a session whose agent asked the broker for a check and ended its turn
/// waits on that check as on a step (<see cref="StepWait.Check"/>, <c>awaiting-step</c>), holding its tree, and the verdict is its next
/// word in the same conversation.
/// </summary>
public static class CheckWaits
{
    /// <summary>The checks <paramref name="session"/>'s agent asked that are still open: no verdict handed yet.</summary>
    public static IReadOnlyList<CheckAsk> Open(string session, IEnumerable<CheckAsk> asks, Func<string, CheckState?> state) =>
        [.. asks.Where(ask => string.Equals(ask.Session, session, StringComparison.Ordinal) && state(ask.Id)?.Handed is null)];

    /// <summary>The key a verdict's word is kept under, so it is said once however often a look asks.</summary>
    public static string Key(CheckAsk ask) => $"check/{ask.Id}";

    /// <summary>The verdict as Daoris's word to the session that asked: what ran, how it ended, and a red's log tail.</summary>
    public static string Words(CheckAsk ask, CheckState state)
    {
        if (state.State == CheckStates.Refused) return $"Daoris did not run the checks you asked for: {state.Error}";
        var what = ask.Gate is { } gate ? $"`{gate}`" : ask.Command is { } command ? $"`{HeavyRun.CommandText(command)}`" : "the repository's checks";
        const string who = "Daoris ran, as you asked,";
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
