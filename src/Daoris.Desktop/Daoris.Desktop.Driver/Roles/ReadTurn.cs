namespace Daoris.Driver;

/// <summary>A call of Daoris's own connector as its line in the session's <c>lineage.jsonl</c> keeps it, with the act its door words it.</summary>
/// <param name="Act"><c>search</c>, <c>read</c>, <c>propose</c> or <c>press</c>; null on a line that names none, which is read as unknown.</param>
/// <param name="ToolUse">The harness's id of the call where its door carries one (Claude Code's); null where it carries none (Codex's).</param>
public sealed record ConnectorCall(DateTimeOffset At, string? Tool, string? Act, string? ToolUse = null);

/// <summary>A press of Ask Daoris's this run, as its press record holds it: <paramref name="Asked"/> where its card asked the person.</summary>
public sealed record TurnPress(DateTimeOffset At, string Route, string? Place, string Outcome, bool Asked);

/// <summary>What a turn is read from: its session's events, its connector's lines and its presses, with the calls the person answered when held.</summary>
public sealed record TurnEvidence(IReadOnlyList<SessionEvent> Events, IReadOnlyList<ConnectorCall> Calls, IReadOnlyList<TurnPress> Presses)
{
    /// <summary>The ids of the tool calls held for the person and answered: their yes, not a check.</summary>
    public IReadOnlySet<string> Held { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>A delegated child's events by its stream, where its door keeps them; null where it does not.</summary>
    public Func<string, IReadOnlyList<SessionEvent>?>? Streams { get; init; }
}

/// <summary>Why a call is read: an acting kind, a press of the connector, a call Daoris cannot place, or work it delegated.</summary>
public static class SelectedWhy
{
    public const string Act = "act";
    public const string Press = "press";
    public const string Unknown = "unknown";
    public const string Delegated = "delegated";
}

/// <summary>What a reading of a turn is not shown: a cut input or output, a child not kept, a call matched by count, a turn with no end, a turn past the bound.</summary>
public static class ReadLimit
{
    public const string Cut = "cut";
    public const string Delegated = "delegated";
    public const string ByCount = "by-count";
    public const string Interrupted = "interrupted";
    public const string OverBound = "over-bound";
    public const string PacketCut = "packet-cut";
}

/// <summary>One limit of a reading, naming the call it is of where it is one call's.</summary>
public sealed record ReadLimitAt(string Code, string? Call = null);

/// <summary>A call the second check reads: its id, kind, title, tool where known, status, locations, and its input and output as the record holds them.</summary>
/// <param name="Cut">Which of <c>input</c> and <c>output</c> the record cut.</param>
public sealed record SelectedCall(
    string Id, string Why, string? Kind, string? Title, string? Tool, string? Status, IReadOnlyList<string> Locations, string? Input, string? Output,
    IReadOnlyList<string> Cut);

/// <summary>A press in the turn, with its route, place and outcome: <paramref name="Yours"/> where the person said yes to it on a card.</summary>
public sealed record SelectedPress(string Route, string? Place, string Outcome, bool Yours);

/// <summary>
/// One turn as the second check selects it: from the person's words where the session took them to its end, the calls it reads, the
/// presses, what the person said yes to, the known reads counted by kind and never quoted, and the limits of what it is shown.
/// </summary>
public sealed record ReadTurn(
    string Id, long From, long To, DateTimeOffset At, DateTimeOffset Ended, bool Interrupted, string? Words, IReadOnlyList<SelectedCall> Calls,
    IReadOnlyList<SelectedPress> Presses, IReadOnlyList<string> Yours, IReadOnlyDictionary<string, int> Reads, IReadOnlyList<ReadLimitAt> Limits)
{
    /// <summary>Whether anything in it ran with no yes of the person's: a call to read or a press not asked on their card.</summary>
    public bool Selected => Calls.Count > 0 || Presses.Any(press => !press.Yours);

    /// <summary>About how many characters its evidence takes in a packet: its words, and each call's identity, input and output's head.</summary>
    public int Size => Math.Min(Words?.Length ?? 0, SessionEventText.RawLimit)
        + Calls.Sum(call => 200 + (call.Input?.Length ?? 0) + Math.Min(call.Output?.Length ?? 0, 1000))
        + Presses.Count * 200;
}
