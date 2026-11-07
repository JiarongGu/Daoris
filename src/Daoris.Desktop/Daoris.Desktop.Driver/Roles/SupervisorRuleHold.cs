namespace Daoris.Driver;

/// <summary>
/// Whether a person's rule holds a line on a count other than answers alone (the context counters design §4.6 point 2), read from its counts
/// as <see cref="LineCountsRead"/> gives them, each with what its effective rule does there: a supervisor's answer is refused <c>rule-holds</c>
/// at its keeping and moved stale with it at its release, since going on would pass a hold the person set. Answers alone keeps its own code,
/// <c>supervisor-bound</c>.
/// </summary>
public static class SupervisorRuleHold
{
    /// <summary>Whether a known count other than answers alone stands at or past a <c>hold</c> rule's number at a place.</summary>
    public static bool Holds(IReadOnlyList<CountFact>? counts) =>
        counts?.Any(count => count.Count != SupervisorRules.AnswersAlone && count.N is { } n
            && count.Watch is { Do: SupervisorRules.Hold or SupervisorRules.Refer } watch && n >= watch.At) == true;

    /// <summary>
    /// The code that keeps a supervisor's answer from going on now, read again at its release: <c>supervisor-bound</c> where the line is at its
    /// answers-alone rule, <c>rule-holds</c> where another rule holds it, else none.
    /// </summary>
    public static string? Why(bool atBound, IReadOnlyList<CountFact>? counts) =>
        atBound ? SupervisorWhy.SupervisorBound : Holds(counts) ? SupervisorWhy.RuleHolds : null;
}
