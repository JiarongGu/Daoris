namespace Daoris.Driver;

/// <summary>
/// What the supervisor's course act is kept against (CONTEXTDRIVE1j): the line's repository, the point a <c>refer</c> holds it at (none
/// where none is open), and the counts the keeping read, which a reason's <c>count:</c> source names.
/// </summary>
public sealed record ReferredPoint(string Repository, RuleActed? Open, IReadOnlyList<CountFact>? Counts);

/// <summary>
/// The release side of a referred point, as the look reads it at a course decision's release: why the point no longer takes the
/// supervisor's go-on (null where it does), whether the line's session runs a turn now, and the count's own door.
/// </summary>
public sealed record CourseLine(Func<string, string, string?> Why, Func<string, bool> Running, Func<string, bool> GoOn);

/// <summary>
/// The points a <c>refer</c> rule holds a line at (CONTEXTDRIVE1j; the context counters design §4.6 point 3), read for the holder's list, at
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
    /// Why <paramref name="point"/> no longer takes the supervisor's go-on: <c>answered</c> where it is no longer open (the person or another
    /// door released it, or a later hold replaced it), <c>rule-holds</c> where the line's rule no longer refers it; null where it does.
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
    /// The release side, each record read by <paramref name="recordOf"/> and its run by <paramref name="running"/>. Given the planner's
    /// <paramref name="plan"/> and the quest's own <paramref name="retry"/> (its <i>Try again</i>, true where it was done), a quest's point is
    /// released through that door; without them only answers alone's is.
    /// </summary>
    public CourseLine Line(
        Func<string, PriorSession?> recordOf, Func<string, bool> running, IReadOnlyList<Consideration>? plan = null, Func<string, bool>? retry = null) =>
        new(
            (session, point) => Why(recordOf(session), session, point, plan), running,
            session => marks.Read(session) is { Why: GoOnMarks.AnswersAlone }
                ? GoOn(session)
                : retry is not null && Open(session, plan) is { } open && retry(open.Line));

    // The plan holds the quest the act is on, on the session it acted on: parked by its failed sessions, or asking at that part's end.
    private static bool Holds(IReadOnlyList<Consideration> plan, RuleActed act, string session) =>
        plan.Any(held => string.Equals(held.Quest.Id, act.Line, StringComparison.OrdinalIgnoreCase)
            && (act.Rule.Count == SupervisorRules.Failures
                ? held is { Verdict: StartVerdict.Exhausted } && held.LastSession == session
                : held is { Verdict: StartVerdict.PolicyHeld, Said.Code: SittingFact.PolicyAsk } && held.HeldBy?.Session == session));
}
