namespace Daoris.Driver;

/// <summary>
/// The points a <c>refer</c> rule holds a line at, read for the holder's list, at
/// the keeping of the supervisor's course act and again at its release, which goes through the count's own door.
/// <list type="bullet">
/// <item>Answers alone: the line's latest act a refer, its mark still standing; the door takes the mark away and keeps the supervisor's
/// note, which the count reads as it reads the person's <i>Go on with this answer</i>.</item>
/// <item>The counts only a driven quest has (<see cref="SupervisorRules.QuestCounts"/>): the line's latest act a refer on the session the
/// plan holds the quest on, while the plan still holds it (parked, or asking); the door is the quest's <i>Try again</i> between sessions,
/// which the caller hands as <c>retry</c> and the person's press is the same one of.</item>
/// </list>
/// </summary>
public sealed class ReferredPoints(string home, ISessionEvents events, GoOnMarks marks, SupervisorRuleBook? rules = null)
{
    /// <summary>The points of <paramref name="home"/>, by the rules its store holds now.</summary>
    public static ReferredPoints For(string home, ISessionEvents events, GoOnMarks marks) => new(home, events, marks, SupervisorRuleBook.Read(home));

    /// <summary>
    /// The point a refer holds <paramref name="session"/> at now: its latest answers-alone act, while its mark stands; else, given the
    /// planner's <paramref name="plan"/>, its quest's latest act on a quest count while the plan still holds the quest there; none else.
    /// </summary>
    public RuleActed? Open(string session, IReadOnlyList<Consideration>? plan = null)
    {
        if (marks.Read(session) is { Why: GoOnMarks.AnswersAlone })
        {
            var act = new SupervisorRuleActs(home).Of(session).LastOrDefault(each => each.Rule.Count == SupervisorRules.AnswersAlone);
            return act is { Rule.Do: SupervisorRules.Refer } ? act : null;
        }

        if (plan is null) return null;
        var asked = new SupervisorRuleActs(home).Of(session).LastOrDefault(each => SupervisorRules.QuestCounts.Contains(each.Rule.Count));
        return asked is { Rule.Do: SupervisorRules.Refer } && Holds(plan, asked, session) ? asked : null;
    }

    /// <summary>The point as the keeping reads it, with the counts the holder's print handed.</summary>
    public ReferredPoint PointOf(PriorSession record, IReadOnlyList<CountFact>? counts, IReadOnlyList<Consideration>? plan = null) =>
        new(record.Repository ?? "", Open(record.Session, plan), counts);

    /// <summary>
    /// Why <paramref name="point"/> does not take the supervisor's go-on: <c>answered</c> where it is not open (the person or another
    /// door released it, or a later hold took its place), <c>rule-holds</c> where the line's rule does not refer it; null where it takes it.
    /// </summary>
    public string? Why(PriorSession? record, string session, string point, IReadOnlyList<Consideration>? plan = null)
    {
        if (Open(session, plan) is not { } open || open.Id != point) return SupervisorWhy.Answered;
        var effective = RuledLine.Of(home, record).In(rules ?? SupervisorRuleBook.Read(home));
        var refers = open.Rule.Count == SupervisorRules.AnswersAlone
            ? SupervisorRuleBook.AnswersAloneActing(effective) is { Do: SupervisorRules.Refer }
            : SupervisorRuleBook.Acting(effective, open.Rule.Count, SupervisorRules.Refer) is not null;
        return refers ? null : SupervisorWhy.RuleHolds;
    }

    /// <summary>The count's door as the supervisor goes through it: its note kept first, then the mark taken away. False where none is held.</summary>
    public bool GoOn(string session)
    {
        if (marks.Read(session) is not { Why: GoOnMarks.AnswersAlone } held) return false;
        events.Keep(session, AnswersAloneNotes.Supervised(held.Said), say: null);
        marks.Clear(session);
        return true;
    }

    /// <summary>
    /// The quest's door as the supervisor goes through it: its own <i>Try again</i> (<paramref name="retry"/>, true where it
    /// went through), and where it did, the note on the line that says who released it.
    /// </summary>
    private bool TryAgain(string session, Func<string, bool>? retry, IReadOnlyList<Consideration>? plan)
    {
        if (retry is null || Open(session, plan) is not { } open || !retry(open.Line)) return false;
        events.Keep(session, AnswersAloneNotes.Retried(), say: null);
        return true;
    }

    /// <summary>
    /// The release side, each record read by <paramref name="recordOf"/> and its run by <paramref name="running"/>. Given the planner's
    /// <paramref name="plan"/> and the quest's own <paramref name="retry"/> (its <i>Try again</i>, true where it went through), a quest's point is
    /// released through that door; without them only answers alone's is.
    /// </summary>
    public CourseLine Line(
        Func<string, PriorSession?> recordOf, Func<string, bool> running, IReadOnlyList<Consideration>? plan = null, Func<string, bool>? retry = null) =>
        new(
            (session, point) => Why(recordOf(session), session, point, plan), running,
            session => marks.Read(session) is { Why: GoOnMarks.AnswersAlone } ? GoOn(session) : TryAgain(session, retry, plan));

    // The plan holds the quest the act is on, on the session it acted on: parked by its failed sessions, or asking at that part's end.
    private static bool Holds(IReadOnlyList<Consideration> plan, RuleActed act, string session) =>
        plan.Any(held => string.Equals(held.Quest.Id, act.Line, StringComparison.OrdinalIgnoreCase)
            && (act.Rule.Count == SupervisorRules.Failures
                ? held is { Verdict: StartVerdict.Exhausted } && held.LastSession == session
                : held is { Verdict: StartVerdict.PolicyHeld, Said.Code: SittingFact.PolicyAsk } && held.HeldBy?.Session == session));
}
