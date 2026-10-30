namespace Daoris.Driver;

// FOUNDATION1i (the foundation design §10 row 1i, D212): the driven lines' paths decide through CourseDecision.Decide, one path a commit, as
// row 1h's do for conversations. The executors (the driver's start, the planner, the hand-over door) keep the readings they have always made
// and put them in as facts; this reads them into CourseFacts and says the course's answer, which the executor then acts on in its own words.

/// <summary>The facts a driven line's paths hand the course, and the answers it gives them.</summary>
internal static class DrivenCourse
{
    /// <summary>A record's waiting words by their authors, an answered park's answer standing as the person's word it is.</summary>
    internal static IReadOnlyList<CourseWord> WordsOf(PriorSession record)
    {
        var words = record.Waiting;
        var ended = record.Updated;
        List<CourseWord> said =
        [
            .. words.Select(word => new CourseWord(
                word.Id ?? "",
                word.Daoris ? CourseAuthor.Daoris
                : word.Supervisors ? CourseAuthor.Supervisor
                : word.By is not null ? CourseAuthor.Findings
                : word.Helper is not null ? CourseAuthor.Helper
                : CourseAuthor.Person,
                word.Key,
                ended is { } end && word.At > end)),
        ];
        if (said.Count == 0 && record.Answer is not null) said.Add(new CourseWord("", CourseAuthor.Person));
        return said;
    }

    /// <summary>The newest part of a driven line as the record keeps it.</summary>
    internal static CourseHead HeadOf(PriorSession record, string? questStatus) =>
        new(
            record.State, Limited: record.Limit, Role: record.Actor, Choice: record.Choice, Adapter: record.Adapter, Profile: record.Profile,
            Tree: record.Tree, Teammate: record.Teammate, Intake: record.Ask is { Length: > 0 }, StoodDown: record.State == "stood-down",
            Waiting: WordsOf(record), Session: record.Session, QuestStatus: questStatus);

    /// <summary>
    /// A move record's standing as the course reads the line's destination (§3.1): the move that still stands for the record, as
    /// <see cref="MoveResolve.StandingAsync"/> answers after it has closed a held one whose words were taken back (round 8, O7-F11).
    /// </summary>
    internal static async Task<CourseStanding?> StandingAsync(MoveResolve resolve, string session, CancellationToken ct = default)
    {
        if (await resolve.StandingAsync(session, ct).ConfigureAwait(false) is not { Stands: true } move) return null;
        var phase = move.Phase switch
        {
            MoveRecordPhase.Held => CourseMovePhase.Held,
            MoveRecordPhase.Delivering => CourseMovePhase.Delivering,
            _ => CourseMovePhase.Decided,
        };
        return new CourseStanding(
            phase, string.Equals(move.Destination, session, StringComparison.Ordinal), string.Equals(move.Left, session, StringComparison.Ordinal));
    }

    /// <summary>
    /// The go-on of a record the person's words, an answer or Daoris's own wait on (path A): whether it resumes its own conversation, which
    /// the executor then asks for, or goes by a new part or to the person. <paramref name="judged"/> is the resume judge's code
    /// (<see cref="Continuations.Judge"/>, or <see cref="ContinueWhy.Unkept"/> for a conversation nothing kept), null where it passes.
    /// </summary>
    internal static LineCourse GoOn(PriorSession record, string? questStatus, string? judged, bool agentGone, LineAnswer? account = null) =>
        CourseDecision.Decide(
            new CourseFacts(
                HeadOf(record, questStatus) with { Judged = judged, AgentGone = agentGone },
                CourseBoundary.ProcessExited, account is null ? null : new CourseAccount(account)),
            CourseTrigger.WordsKept);

    /// <summary>
    /// The link a driven carry-on's new part names (path K, MIXAGENT1a-iii, SESSCTX1b): the course's move over the readings the start made. A
    /// quest handed over from the record is the hand-over's press; a fall-back is the go-on's refused resume, which names `retry` unless the
    /// limit's or the curve's move says more; every other carry-on is the planner's, the person's where their press released or brought it
    /// forward. The new part carries the person's words, so the one the course reads is a stand-in for them.
    /// </summary>
    internal static ContinuedFrom From(
        HandedQuest? handed, PriorSession carried, string? profile, string? walked, bool retried, bool pressed, string? agent,
        Action<LineCourse>? decided)
    {
        var handing = handed is not null && string.Equals(carried.Session, handed.From, StringComparison.Ordinal);
        // The agent counts as another's only where the record names one it did not run on (AccountIdentity.OnAnotherAccount).
        var otherAgent = agent is { Length: > 0 } && carried.Adapter is { Length: > 0 } ran && !string.Equals(ran, agent, StringComparison.OrdinalIgnoreCase)
            ? agent
            : null;
        var account = new CourseAccount(
            new LineAnswer.Elsewhere(new HarnessSelection(Refusal: null, Profile: profile), NextHold.Cooling), Agent: otherAgent,
            Curve: walked is OpeningWhy.OverCurve or OpeningWhy.MostRoom);
        CourseWord[] words = [new("", CourseAuthor.Person)];
        var head = HeadOf(carried, "Taken") with { Waiting = words };
        var kind = carried.State == "stood-down" ? CourseCarryOn.BroughtForward : pressed ? CourseCarryOn.StopReleased : CourseCarryOn.CutOff;
        var course = handing
            ? CourseDecision.Decide(
                new CourseFacts(
                    head, CourseBoundary.ProcessExited, account,
                    Press: new CoursePress(CoursePressKind.HandOver, handed!.Agent, handed.Account, Why: CourseFrom.WhyOf(handed.Why), By: CourseFrom.ByOf(handed.AskedBy)),
                    CarryOn: kind),
                CourseTrigger.CarryOn)
            : retried
                ? CourseDecision.Decide(
                    new CourseFacts(head with { Judged = ContinueWhy.Account }, CourseBoundary.ProcessExited, account), CourseTrigger.WordsKept)
                : CourseDecision.Decide(new CourseFacts(head, CourseBoundary.ProcessExited, account, CarryOn: kind), CourseTrigger.CarryOn);
        decided?.Invoke(course);
        return course is LineCourse.Move move
            ? new ContinuedFrom(carried.Session, CourseFrom.Code(move.Why), CourseFrom.Code(move.By))
            : new ContinuedFrom(carried.Session, HandOverWhy.CarryOn, pressed ? HandOverBy.Person : HandOverBy.Daoris);
    }

    /// <summary>
    /// A second opinion's findings sent back to the working session (path L, D155 point 7): a solicited answer, which no teammate's record,
    /// intake or stood-down take is ever handed, and which a record that takes them goes on with.
    /// </summary>
    internal static LineCourse SendBack(PriorSession record, Action<LineCourse>? decided)
    {
        var course = CourseDecision.Decide(
            new CourseFacts(
                HeadOf(record, null) with { Waiting = [new CourseWord("", CourseAuthor.Findings)], Conversation = record.Kind == "chat" },
                record.Running ? CourseBoundary.TurnRunning : CourseBoundary.ProcessExited),
            CourseTrigger.Answer);
        decided?.Invoke(course);
        return course;
    }

    /// <summary>
    /// What follows a record's end past its transient bound (path N, AGENTERRORS1a): it waits for a word of the person's said after the end or
    /// for <i>Try again</i>, whose release is the press, which goes on by the limit resume's step 1.
    /// </summary>
    internal static LineCourse PastBound(PriorSession record, string questStatus, bool released) =>
        CourseDecision.Decide(
            new CourseFacts(
                HeadOf(record, questStatus), CourseBoundary.ProcessExited, Failure: new CourseFailure(PastBound: true),
                Press: released ? new CoursePress(CoursePressKind.TryAgain) : null, Limit: new CourseLimit(Ended: record.Updated)),
            CourseTrigger.TransientPastBound);

    /// <summary>
    /// The limit resume's step 1 (path F, LIMITRESUME1b): a record an account's limit, an update or a released transient end cut off, on a
    /// door known to fail closed and a judge that passed. Its own account ready is the resume; another the move to it, held while a move
    /// still stands to this record as its destination.
    /// </summary>
    internal static LineCourse LimitOwn(PriorSession limited, LineAnswer? account, CourseStanding? standing) =>
        CourseDecision.Decide(
            new CourseFacts(
                HeadOf(limited, "Taken") with { Protocol = true },
                CourseBoundary.ProcessExited, account is null ? null : new CourseAccount(account),
                Failure: LimitResume.EndedTransient(limited) ? new CourseFailure(PastBound: true) : null,
                Standing: standing,
                Limit: new CourseLimit(Until: LimitResume.Until(limited), Build: LimitResume.UpdateBuild(limited), Ended: limited.Updated)),
            CourseTrigger.LimitEnd);
}
