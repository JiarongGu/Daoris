using static Daoris.Driver.CourseDecision;
using static Daoris.Driver.LineCourse;

namespace Daoris.Driver;

/// <summary>The course of a limit's end and its wake, a carry-on and a transient past its bound, read by <see cref="CourseDecision"/>.</summary>
internal static class LimitCourse
{
    // ——— A limit's end and its wake.

    internal static LineCourse Limit(CourseFacts facts, CourseTrigger trigger)
    {
        var head = facts.Head;
        // The person's stop of the wait, or a pause: nothing goes on by itself (LimitWaits.PersonStopped).
        if (facts.Press?.Kind is CoursePressKind.Stop or CoursePressKind.Pause) return Held(facts, CourseHoldWhy.Stays);
        if (!head.Conversation) return DrivenLimit(facts);

        // An ended chat whose words waited for its own account never met a limit: its wake is its go-on (LimitWaits.cs:203-223).
        if (!head.Limited) return trigger == CourseTrigger.LimitWake ? GoOn(facts, CourseTrigger.WordsKept) : Held(facts, CourseHoldWhy.Stays);

        // The wake's own conversation, tried on its own account, refused on the wire: the judge's reason decides as at a go-on.
        if (head.Judged is { } judged) return Judged(facts, judged);

        var account = facts.Account;
        var answer = account?.Answer;
        // Own first (LimitWaits.cs:173-198): its own account ready, the same conversation goes on, Daoris's limit word last, keyed by the
        // wake's clock (DaorisKeys.LimitReset, LimitWaits.cs:183, :251; ChatGoOn.cs:437).
        if (trigger == CourseTrigger.LimitWake && answer is LineAnswer.Resume)
        {
            var key = facts.Limit?.Woke is { } woke ? DaorisKeys.LimitReset(head.Session ?? "", woke) : null;
            return Resumed(facts, new CourseWords(All(head.Words).Ids, CourseDaorisWord.LimitReset, key));
        }

        // An account the person picked is never moved off; it waits for that account's reset.
        if (account?.Picked == true && answer is LineAnswer.Wait picked)
        {
            return Waited(facts, picked.Earliest, CourseWaitWhy.LimitReset, [picked.Earliest.Account ?? ""]);
        }

        var persons = Ids(head.Words.Where(word => word.Persons));
        // Another account of its agent with room, never its own (LimitMove.cs:107, :298-299): a new part there, by the limit.
        if (answer is LineAnswer.Elsewhere elsewhere && !Same(elsewhere.Selection.Profile, head.Profile))
        {
            return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), CourseFromWhy.Limit, CourseFromBy.Daoris, persons);
        }

        // None of its agent's has room, so its actor's next choice on another agent, never for an agent's pick.
        if (Later(facts) is { } later) return Moved(facts, CourseWhen.Now, later, CourseFromWhy.Limit, CourseFromBy.Daoris, persons);

        // Nothing has room: it waits for the earliest reset of any account of its agent (LimitWaits.cs:236-237).
        var earliest = (answer as LineAnswer.Wait)?.Earliest ?? account?.Earliest;
        return earliest is null
            ? Held(facts, CourseHoldWhy.Stays)
            : Waited(facts, earliest, CourseWaitWhy.LimitReset, (answer as LineAnswer.Wait)?.Accounts ?? [earliest]);
    }

    // The resume steps: step 0, a hand-over made from it decides; step 1, its own account ready (its maker's
    // reading asked), the protocol door and the judge passing, its own conversation resumes with Daoris's word; step 2, another account it
    // may run on: a briefed part there, `limit` on another account, else `carry-on`; step 3, none ready: it waits for the earliest reset.
    internal static LineCourse DrivenLimit(CourseFacts facts)
    {
        var head = facts.Head;
        if (facts.Press is { Kind: CoursePressKind.HandOver } handed) return Handed(facts, handed);

        var answer = facts.Account?.Answer;
        if (answer is LineAnswer.Resume && head.Protocol && head.Judged is null)
        {
            // A released transient record's words go on alone, as the go-on's; only Try again's start with none takes the word.
            if (facts.Failure is { PastBound: true } && head.Words.Count > 0) return Resumed(facts, All(head.Words));
            return Resumed(facts, new CourseWords(All(head.Words).Ids, WordKind(facts), WordKey(facts)));
        }

        if (head.Judged is { } judged) return Judged(facts, judged);
        var persons = Ids(head.Words.Where(word => word.Persons));
        switch (answer)
        {
            case LineAnswer.Elsewhere elsewhere:
            {
                var (why, by) = DrivenWhy(facts, elsewhere.Selection, retried: false);
                return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), why, by, persons);
            }

            case LineAnswer.Resume:
                // A door whose resume of a missing conversation is not known to fail closed goes by the brief: its walk's own account.
                return Moved(facts, CourseWhen.Now, OwnOrWalk(facts), CourseFromWhy.CarryOn, CourseFromBy.Daoris, persons);
            case LineAnswer.Wait wait:
                return Waited(facts, wait.Earliest, CourseWaitWhy.LimitReset, wait.Accounts);
            case LineAnswer.Hold hold:
                return Held(facts, CourseHoldWhy.Account, repair: hold.Repair);
            default:
                return Held(facts, CourseHoldWhy.Stays);
        }
    }

    // Daoris's word at step 1, by what ended the record (DriverContinuations.KeepLimitWordAsync): a released transient end's go-on, an
    // update's restart, else the limit word, keyed by the cool-off's `until` as the record's note says it (DaorisKeys, LimitResume.Until).
    internal static CourseDaorisWord WordKind(CourseFacts facts) =>
        facts.Failure is { PastBound: true } ? CourseDaorisWord.TransientRetry
        : facts.Limit?.Build is not null ? CourseDaorisWord.Restart
        : CourseDaorisWord.LimitReset;

    internal static string WordKey(CourseFacts facts)
    {
        var session = facts.Head.Session ?? "";
        return WordKind(facts) switch
        {
            CourseDaorisWord.TransientRetry => DaorisKeys.TransientRelease(session, facts.Limit?.Ended ?? DateTimeOffset.UnixEpoch),
            CourseDaorisWord.Restart => DaorisKeys.Restart(session, facts.Limit!.Build!),
            _ => DaorisKeys.LimitReset(session, facts.Limit?.Until ?? "unsaid"),
        };
    }

    // ——— The planner's carry-ons, and what follows a transient bound.

    internal static LineCourse CarryOnOf(CourseFacts facts)
    {
        var head = facts.Head;
        // The person's stop holds the quest, its door *Try again*; a pause sits it (the planner's StopHolding, Paused).
        if (facts.Press?.Kind == CoursePressKind.Stop) return Held(facts, CourseHoldWhy.Stays, [CourseDoor.TryAgain]);
        if (facts.Press?.Kind == CoursePressKind.Pause) return Held(facts, CourseHoldWhy.Stays);
        if (facts.Press is { Kind: CoursePressKind.HandOver } handed) return Handed(facts, handed);

        // A limit's or an update's end, or a released transient one, is decided by the limit resume's steps (LimitResume.Line).
        if (head.Limited || facts.Limit?.Build is not null || facts.Failure is { PastBound: true }) return DrivenLimit(facts);

        var persons = Ids(head.Words.Where(word => word.Persons));
        if (facts.CarryOn == CourseCarryOn.Retried)
        {
            return Moved(facts, CourseWhen.Now, OwnOrWalk(facts), CourseFromWhy.Retry, CourseFromBy.Daoris, persons);
        }

        switch (facts.Account?.Answer)
        {
            case LineAnswer.Elsewhere elsewhere:
            {
                var (why, by) = DrivenWhy(facts, elsewhere.Selection, retried: false);
                return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), why, by, persons);
            }

            case LineAnswer.Wait wait:
                return Waited(facts, wait.Earliest, CourseWaitWhy.AccountCooling, wait.Accounts);
            case LineAnswer.Hold hold:
                return Held(facts, CourseHoldWhy.Account, repair: hold.Repair);
            default:
            {
                var (why, by) = DrivenWhy(facts, null, retried: false);
                return Moved(facts, CourseWhen.Now, OwnOrWalk(facts), why, by, persons);
            }
        }
    }

    // Planner.TransientHeld: the retries are the run's; past their bound the line waits for the person's word
    // or *Try again*, which releases that end into the limit resume's step 1 (LimitResume.Line, LimitResume.cs:28-34).
    internal static LineCourse PastBound(CourseFacts facts)
    {
        if (facts.Press?.Kind == CoursePressKind.TryAgain) return DrivenLimit(facts);
        return facts.Head.Words.Any(word => word.Persons && word.AfterEnd)
            ? GoOn(facts, CourseTrigger.WordsKept)
            : Held(facts, CourseHoldWhy.TransientBound, [CourseDoor.TryAgain, CourseDoor.Say]);
    }

    // The new part's `from` for a driven carry-on (Driver.ContinuesFrom, Driver.cs:4776-4788): a limit's on another account or agent, the
    // curve's where the walk's first step passed an account over its curve to another, `retry` where its resume fell back, else `carry-on`,
    // the person's where their press released the stop or brought the work forward.
    internal static (CourseFromWhy Why, CourseFromBy By) DrivenWhy(CourseFacts facts, HarnessSelection? selection, bool retried)
    {
        var head = facts.Head;
        var another = selection is not null && (!Same(selection.Profile, head.Profile) || facts.Account?.Agent is not null);
        if (head.Limited && another) return (CourseFromWhy.Limit, CourseFromBy.Daoris);
        if (facts.Account?.Curve == true && another) return (CourseFromWhy.Curve, CourseFromBy.Daoris);
        if (retried) return (CourseFromWhy.Retry, CourseFromBy.Daoris);
        var pressed = facts.CarryOn is CourseCarryOn.StopReleased or CourseCarryOn.BroughtForward;
        return (CourseFromWhy.CarryOn, pressed ? CourseFromBy.Person : CourseFromBy.Daoris);
    }

    // ——— The curve and the hand-overs.
}
