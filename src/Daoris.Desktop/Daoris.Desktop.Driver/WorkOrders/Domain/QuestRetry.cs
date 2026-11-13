namespace Daoris.Driver;

/// <summary>
/// The quest's <i>Try again</i> as a standing choice judges it: the work orders' judgement
/// over the planner's verdicts, which the settings' <see cref="StandingChoices"/> keep their own types free of. The window's route and <c>daoris quest retry</c> acting alone call this one.
/// </summary>
public static class QuestRetry
{
    /// <summary>
    /// *Try again* on <paramref name="quest"/>, judged from the planner's <paramref name="verdicts"/>: a
    /// quest parked on its failed sessions is marked at its failures, a verdict carrying no count at the least count the
    /// planner parks at; one the person's stop holds is released from that session, with no mark, since a stop is not a strike; one
    /// held past its transient bound is released for that record's end alone, keyed to its <c>updated</c>.
    /// </summary>
    /// <exception cref="ChoiceRefusal">The quest is paused, or neither parked nor held: nothing is written.</exception>
    public static RetryDecision Decide(string quest, IReadOnlyList<Consideration> verdicts)
    {
        var verdict = verdicts.FirstOrDefault(c => string.Equals(c.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));
        switch (verdict)
        {
            case { Verdict: StartVerdict.Exhausted } exhausted:
                // Marked where it stands rather than erased, so the records still read true and its failures rule parks it again
                // after as many more. At its failures, not the rule's number: a quest parked a second time has more.
                return new(quest, RetryDecision.Marked, null,
                    config => config.WithForgiven(quest, exhausted.Failures ?? config.ForgivenAt(quest) + (exhausted.ParksAt ?? 0)))
                {
                    ParksAt = exhausted.ParksAt,
                };
            case { Verdict: StartVerdict.Stopped, HeldBy: { } stop }:
                return new(quest, RetryDecision.Released, stop.Session, config => config.WithReleased(quest, stop.Session));
            case { Verdict: StartVerdict.TransientHeld, HeldBy: { Updated: { } ended } exhausted }:
                // A transient hold released for the end the press read, never the
                // stop's session-keyed release, so the same record's next exhaustion is held again. No mark: an outage is not a strike.
                return new(quest, RetryDecision.Released, exhausted.Session,
                    config => config.WithTransientReleased(quest, exhausted.Session, ended));
            case { Verdict: StartVerdict.PolicyHeld, HeldBy: { } held }:
                // The yes to a role policy's ask or bound: one approval, one move, written beside the moves.
                return new(quest, RetryDecision.Approved, held.Session, config => config, held.Actor ?? RoleWork.Build);
            case { Verdict: StartVerdict.Paused, PausedBy: { } pause }:
                // A pause is the reason before a stop and the strikes: Resume moves it, and nothing is written.
                // A quest's pause holds every quest its work published too: that pause is named, so the resume it points to is the one
                // that carries the quest on.
                throw pause.Scope == WorkScope.Ask
                    ? new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused with ask #{pause.Id}; Resume carries it on.",
                        ("id", quest), ("pause", pause.Id))
                    : string.Equals(DriverConfigEdits.PauseId(pause.Id), quest, StringComparison.OrdinalIgnoreCase)
                        ? new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused on its own; Resume carries it on.",
                            ("id", quest), ("pause", pause.Id), ("context", "quest"))
                        : new ChoiceRefusal(ChoiceRefusal.Paused,
                            $"#{quest} is paused with quest #{pause.Id}, whose work published it; resuming #{pause.Id} carries it on.",
                            ("id", quest), ("pause", pause.Id), ("context", "published"));
            case { Quest.Status: "Taken", Resumes: { } ended }:
                // A take its session's end left: the loop carries it on, so nothing is held for a press to release. Said as whose
                // take it is and how that session ended, never as a quest nobody holds.
                throw new ChoiceRefusal(ChoiceRefusal.NotHeld,
                    $"#{quest} is taken by session {ended.Session}, which {ended.State}: {ended.Note ?? "it ended before closing the quest."} "
                    + "Daoris carries it on from that session's tree at its next look; nothing is held, so there is nothing to try again.",
                    ("id", quest), ("session", ended.Session));
            default:
                throw new ChoiceRefusal(ChoiceRefusal.NotHeld,
                    $"#{quest} is neither parked nor held by your stop on this machine, so there is nothing to try again.", ("id", quest));
        }
    }
}
