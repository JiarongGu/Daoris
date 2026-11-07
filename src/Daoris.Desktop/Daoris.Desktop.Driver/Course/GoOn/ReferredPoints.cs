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
/// The points the answers-alone rule's <c>refer</c> holds a line at (CONTEXTDRIVE1j; the context counters design §4.6 point 3): the line's
/// latest answers-alone act a refer, its mark still standing. Read for the holder's list, at the keeping of the supervisor's course act
/// and again at its release, which goes through the count's own door: the mark taken away and the supervisor's note kept, which the count
/// reads as it reads the person's <i>Go on with this answer</i>.
/// </summary>
public sealed class ReferredPoints(string home, ISessionEvents events, GoOnMarks marks, SupervisorRuleBook? rules = null)
{
    /// <summary>The points of <paramref name="home"/>, by the rules its store holds now.</summary>
    public static ReferredPoints For(string home, ISessionEvents events, GoOnMarks marks) => new(home, events, marks, SupervisorRuleBook.Read(home));

    /// <summary>The point a refer holds <paramref name="session"/> at now: its latest answers-alone act, while its mark stands; none else.</summary>
    public RuleActed? Open(string session)
    {
        if (marks.Read(session) is not { Why: GoOnMarks.AnswersAlone }) return null;
        var act = new SupervisorRuleActs(home).Of(session).LastOrDefault(each => each.Rule.Count == SupervisorRules.AnswersAlone);
        return act is { Rule.Do: SupervisorRules.Refer } ? act : null;
    }

    /// <summary>The point as the keeping reads it, with the counts the holder's print handed.</summary>
    public ReferredPoint PointOf(PriorSession record, IReadOnlyList<CountFact>? counts) => new(record.Repository ?? "", Open(record.Session), counts);

    /// <summary>
    /// Why <paramref name="point"/> no longer takes the supervisor's go-on: <c>answered</c> where it is no longer open (the person or another
    /// door released it, or a later hold replaced it), <c>rule-holds</c> where the line's rule no longer refers it; null where it does.
    /// </summary>
    public string? Why(PriorSession? record, string session, string point)
    {
        if (Open(session) is not { } open || open.Id != point) return SupervisorWhy.Answered;
        var effective = RuledLine.Of(home, record).In(rules ?? SupervisorRuleBook.Read(home));
        return SupervisorRuleBook.AnswersAloneActing(effective) is { Do: SupervisorRules.Refer } ? null : SupervisorWhy.RuleHolds;
    }

    /// <summary>The count's door as the supervisor goes through it: its note kept first, then the mark taken away. False where none is held.</summary>
    public bool GoOn(string session)
    {
        if (marks.Read(session) is not { Why: GoOnMarks.AnswersAlone } held) return false;
        events.Keep(session, AnswersAloneNotes.Supervised(held.Said), say: null);
        marks.Clear(session);
        return true;
    }

    /// <summary>The release side, each record read by <paramref name="recordOf"/> and its run by <paramref name="running"/>.</summary>
    public CourseLine Line(Func<string, PriorSession?> recordOf, Func<string, bool> running) =>
        new((session, point) => Why(recordOf(session), session, point), running, GoOn);
}
