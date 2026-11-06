using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A line's counts and the acts the person's rules made on it, as <c>SESSION_SUPERVISOR</c> answers them for the page's <i>Details</i>
/// and the head's chip: codes and numbers alone, each count with what Daoris does on it, never a sentence (the web words each code).
/// </summary>
internal static class SessionCountsWire
{
    /// <summary>Each count of the line: its code, what it stands at, whether that is exact, and the rule it acts by.</summary>
    public static object[] Counts(LineFacts facts) => [.. facts.Counts.Select(count => (object)new
    {
        count.Count,
        count.N,
        count.Known,
        count.Why,
        count.From,
        Watch = count.Watch is { } watch ? new { watch.At, watch.Do, watch.Place } : null,
    })];

    /// <summary>Each act a rule made on the line's parts, oldest first: the count it read, its rule's bound, act and place, and when.</summary>
    public static object[] Acts(LineFacts facts) => [.. facts.Acts.Select(act => (object)new
    {
        act.Rule.Count,
        act.N,
        act.Known,
        Rule = new { act.Rule.At, act.Rule.Do, act.Rule.Place },
        act.Point,
        When = Moments.Write(act.At),
    })];
}
