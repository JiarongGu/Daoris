namespace Daoris.Driver;

/// <summary>
/// The quest's <i>Try again</i> as a standing choice judges it (PUSH1f2a, D194 point 4; FOUNDATION1 mod-iii): the work orders' judgement
/// over the planner's verdicts, which the settings' <see cref="StandingChoices"/> held until the settings' own types named none of
/// the planner's. The window's route and <c>daoris-driver driver retry</c> acting alone call this one.
/// </summary>
public static class QuestRetry
{
    /// <summary>
    /// *Try again* on <paramref name="quest"/> (RETRY1, SESSUX1b, D126 §3.4), judged from the planner's <paramref name="verdicts"/>: a
    /// quest parked on its failed sessions is marked at its failures (RETRY1b), a verdict carrying no count at the least count the
    /// planner parks at; one the person's stop holds is released from that session, with no mark, since a stop is not a strike; one
    /// held past its transient bound (AGENTERRORS1a2) is released for that record's end alone, keyed to its <c>updated</c>.
    /// </summary>
    /// <exception cref="ChoiceRefusal">The quest is paused (PAUSE1b), or neither parked nor held: nothing is written.</exception>
    public static RetryDecision Decide(string quest, IReadOnlyList<Consideration> verdicts)
    {
        var verdict = verdicts.FirstOrDefault(c => string.Equals(c.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));
        switch (verdict)
        {
            case { Verdict: StartVerdict.Exhausted } exhausted:
                // Marked where it stands rather than erased, so the records still read true and the next `strikes` failures park it
                // again. At its failures, not the limit (RETRY1b): a quest parked a second time has more failures than the limit.
                return new(quest, RetryDecision.Marked, null,
                    config => config.WithForgiven(quest, exhausted.Failures ?? config.ForgivenAt(quest) + config.Strikes));
            case { Verdict: StartVerdict.Stopped, HeldBy: { } stop }:
                return new(quest, RetryDecision.Released, stop.Session, config => config.WithReleased(quest, stop.Session));
            case { Verdict: StartVerdict.TransientHeld, HeldBy: { Updated: { } ended } exhausted }:
                // AGENTERRORS1a2 (D210, the agent-errors design §5.1): a transient hold released for the end the press read, never the
                // stop's session-keyed release, so the same record's next exhaustion is held again. No mark: an outage is not a strike.
                return new(quest, RetryDecision.Released, exhausted.Session,
                    config => config.WithTransientReleased(quest, exhausted.Session, ended));
            case { Verdict: StartVerdict.Paused, PausedBy: { } pause }:
                // A pause is the reason before a stop and the strikes (PAUSE1b, D132 §6.1): Resume moves it, and nothing is written.
                throw pause.Scope == WorkScope.Ask
                    ? new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused with ask #{pause.Id}; Resume carries it on.",
                        ("id", quest), ("pause", pause.Id))
                    : new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused on its own; Resume carries it on.",
                        ("id", quest), ("pause", pause.Id), ("context", "quest"));
            default:
                throw new ChoiceRefusal(ChoiceRefusal.NotHeld,
                    $"#{quest} is neither parked nor held by your stop on this machine, so there is nothing to try again.", ("id", quest));
        }
    }
}
