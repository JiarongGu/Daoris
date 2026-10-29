namespace Daoris.Driver;

// FOUNDATION1e part 2 (the foundation design §3.1, §3.2, §10 row 1e): the one decision of how a line goes on, read by nothing until rows h
// and i adopt it one path per branch. It answers every path A to N as that path answers today (LineCourseTests' pinned table, ported from
// the paths' own suites); where two paths answer the same facts two ways it tells them apart by the trigger or a fact and keeps both, each
// difference a decision for the row that adopts it (D212's FOUNDATION1e part 2 note). It reads the account rule's answer once, as the
// executor handed it, and never walks.

public abstract partial record LineCourse
{
    /// <summary>
    /// How the line goes on, given what pulled the course and the facts the executor read (the foundation design §3.1): what never goes on
    /// first, then the trigger's path, each answer carrying the preconditions it was read on. A part that is a standing move's destination
    /// takes no move of its own until that move ends, and nothing opens while the application winds up.
    /// </summary>
    public static LineCourse Decide(CourseFacts facts, CourseTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var answer = NeverOf(facts, trigger) ?? Reserved(facts, trigger) ?? Course(facts, trigger);
        if (answer is Move && facts.Standing is { Stands: true, Destination: true }) return Held(facts, CourseHoldWhy.MoveHeld);
        return answer is Move or Start && facts.Boundary == CourseBoundary.WindingUp ? Held(facts, CourseHoldWhy.Stays) : answer;
    }

    // ——— What never goes on, and a line a press holds.

    // D137 §2.2's three nevers, a record a later part supersedes (WordsNever's superseded and continued), and the old part of a standing
    // move, whose words are the move's (O-2). A stand-down's lost take the person brought forward is carried on all the same (SYNCMERGE1b).
    private static Never? NeverOf(CourseFacts facts, CourseTrigger trigger)
    {
        var head = facts.Head;
        if (head.Teammate) return new Never(CourseNeverWhy.Teammate);
        if (head.Intake) return new Never(CourseNeverWhy.Intake);
        if (head.StoodDown && !(trigger == CourseTrigger.CarryOn && facts.CarryOn == CourseCarryOn.BroughtForward)) return new Never(CourseNeverWhy.StoodDown);
        if (head.SupersededBy is not null) return new Never(CourseNeverWhy.Superseded);
        return facts.Standing is { Stands: true, OldPart: true } ? new Never(CourseNeverWhy.StandingMove) : null;
    }

    // A press holds the line's reservation and nothing else takes it: a second hand-over waits behind the first (HandOver.Waiting), and
    // nothing automatic is judged while a hand-over waits or is taken (CurveMove.cs:65-68, LimitMove.cs:62).
    private static LineCourse? Reserved(CourseFacts facts, CourseTrigger trigger) =>
        facts.Boundary != CourseBoundary.Reserved
            ? null
            : trigger == CourseTrigger.HandOver
                ? Refused(facts, HandOver.Waiting)
                : Held(facts, CourseHoldWhy.Stays);

    private static LineCourse Course(CourseFacts facts, CourseTrigger trigger) => trigger switch
    {
        CourseTrigger.StartFrom => StartFromOf(facts),
        CourseTrigger.GoOnNew => GoOnNewOf(facts),
        CourseTrigger.BriefedPress => Briefed(facts, CourseFromBy.Person),
        CourseTrigger.HandOver => HandOverOf(facts),
        CourseTrigger.TurnEnd => Curve(facts),
        CourseTrigger.CarryOn => CarryOnOf(facts),
        CourseTrigger.TransientPastBound => PastBound(facts),
        CourseTrigger.ConversationLost => Lost(facts),
        CourseTrigger.LimitEnd or CourseTrigger.LimitWake => Limit(facts, trigger),
        // A workflow's send-back is no decision of its own (path L): an ended conversation's go-on from it is path B's.
        _ => GoOn(facts, trigger),
    };

    // ——— Paths A and B: words kept, an answer, the come-up.

    private static LineCourse GoOn(CourseFacts facts, CourseTrigger trigger)
    {
        var head = facts.Head;
        var words = head.Words;

        // A part that runs takes its words through its own dispatcher, its own conversation: a turn's next prompt, or a step's (path L).
        if (facts.Boundary is CourseBoundary.TurnRunning or CourseBoundary.BetweenTurns)
        {
            return AloneHeld(facts, trigger) ?? (words.Count == 0 ? Held(facts, CourseHoldWhy.Stays) : Resumed(facts, All(words)));
        }

        if (words.Count == 0) return Held(facts, CourseHoldWhy.Stays);

        // AGENTERRORS1a: past its transient bound a record goes on only with a word of the person's said after it ended (Planner.cs:894-895).
        if (facts.Failure is { PastBound: true } && facts.Press?.Kind != CoursePressKind.TryAgain && !words.Any(word => word.Persons && word.AfterEnd))
        {
            return Held(facts, CourseHoldWhy.TransientBound, [CourseDoor.TryAgain, CourseDoor.Say]);
        }

        if (AloneHeld(facts, trigger) is { } alone) return alone;

        // MODELROLE1d2 (D205 §3.5(b)): a conversation its agent refused at the open began no conversation: a fresh start of its actor.
        if (head.Conversation && head.NotOffered) return Fresh(facts);

        if (head.AgentGone)
        {
            // A conversation's agent no longer here cannot be resumed (ChatGoOn.cs:583-585); a driven record's waits with the hand-over's offer.
            return head.Conversation
                ? CannotGoOn(facts, ContinueWhy.Refused)
                : Held(facts, CourseHoldWhy.CannotGoOn, [CourseDoor.HandOver], code: GoOnBrief.AgentGone);
        }

        if (head.Judged is { } judged) return Judged(facts, judged);
        return Account(facts);
    }

    // The resume judge's reason (Continuations.Judge, read by its code alone): a conversation goes on by itself only where its own
    // conversation is proven lost (D189 point 3), else waits for the person; a driven record's fall-back carries on in a new part, `retry`,
    // but where another client holds its conversation (D189's amendment), and a closed quest's session only as a conversation in its tree.
    private static LineCourse Judged(CourseFacts facts, string code)
    {
        var head = facts.Head;
        var reason = new ContinueReason(code, "");
        if (head.Conversation) return reason.Lost ? Lost(facts) : CannotGoOn(facts, code);
        if (reason.Holds) return CannotGoOn(facts, code);
        if (!head.Carried)
        {
            return reason.Lost && facts.Rules?.Runner == true && head.Words.Any(word => word.Persons)
                ? Briefed(facts, CourseFromBy.Daoris)
                : CannotGoOn(facts, code);
        }

        return FallBack(facts, facts.Account?.Answer is LineAnswer.Elsewhere { } walked ? walked.Selection : null, switched: false);
    }

    // The account rule's answer, read once (§3.2): its own account resumes; the switch moves the words off a cooling own account to a new
    // part now (LIMITSWITCH1-3); an own account that cannot carry them walks for a driven record (D137 point 7) and is a conversation's
    // *cannot go on* (§8's second question); the words wait for the earliest reset; or nothing carries them and nothing resets.
    private static LineCourse Account(CourseFacts facts)
    {
        var head = facts.Head;
        switch (facts.Account?.Answer)
        {
            case LineAnswer.Elsewhere { Switched: true } switched:
                if (head.Conversation || head.Carried || facts.Rules?.Runner == true) return FallBack(facts, switched.Selection, switched: true);
                return CannotGoOn(facts, ContinueWhy.Account);
            case LineAnswer.Elsewhere elsewhere:
                return head.Conversation ? CannotGoOn(facts, ContinueWhy.Account) : FallBack(facts, elsewhere.Selection, switched: false);
            case LineAnswer.Wait wait:
                return Waited(facts, wait.Earliest, CourseWaitWhy.AccountCooling, wait.Accounts, WaitDoors(facts, wait.Why));
            case LineAnswer.Hold hold:
                return head.Conversation
                    ? CannotGoOn(facts, ContinueWhy.Account, hold.Repair)
                    : Held(facts, CourseHoldWhy.Account, repair: hold.Repair);
            default:
                return Resumed(facts, All(head.Words));
        }
    }

    // A driven record's words carried to a new part (Driver.cs:2493-2536, :4776-4788), or a conversation's switched off its cooling own
    // account (the ended limit move's body with `switching`): the person's words, and Daoris's that state a fact where the switch made it.
    private static LineCourse FallBack(CourseFacts facts, HarnessSelection? selection, bool switched)
    {
        var head = facts.Head;
        var words = head.Words;
        // LIMITSWITCH2: a closed quest's session goes on as a conversation in its tree through the runner's ended move, the reason `account`.
        // Words wait and none of them goes (another agent's findings, a helper's answer, Daoris's own): they go to the person (Driver.cs:2524).
        // A limited line with no words left goes on as the carry-on planned for it (LIMITRESUME1b).
        var carried = switched ? words.Where(word => word.NewPartCarries) : words.Where(word => word.Persons);
        if (words.Count > 0 && !carried.Any()) return CannotGoOn(facts, switched ? ContinueWhy.Account : head.Judged ?? ContinueWhy.Account);

        var where = selection is not null ? Settled(facts, selection) : OwnOrWalk(facts);
        var (why, by) = head.Conversation || switched ? (CourseFromWhy.Retry, CourseFromBy.Daoris) : DrivenWhy(facts, selection, retried: true);
        return Moved(facts, CourseWhen.Now, where, why, by, Ids(carried));
    }

    // ——— Paths A and N's bound: the answers-alone hold, and a conversation refused at the open.

    // COWORK1e (D188 point 4): answers alone that would take the line on a third time in a row with nobody between wait for the person; a
    // word of theirs or their *Go on with this answer* lets them go, and their *Not now* keeps them for a word of theirs. The supervisor's
    // route refuses its word at the keeping while the line stands at the bound (SUPERVISOR1b), so nothing is kept to be held.
    private static LineCourse? AloneHeld(CourseFacts facts, CourseTrigger trigger)
    {
        var alone = facts.Alone;
        if (alone is null || !facts.Head.OnlyAnswers) return null;
        if (alone.Kept) return Held(facts, CourseHoldWhy.AnswersAlone, [CourseDoor.Say], code: GoOnMarks.NotNow);
        if (!alone.AtBound) return null;
        if (trigger == CourseTrigger.Answer && facts.Head.Words.Any(word => word.Author == CourseAuthor.Supervisor))
        {
            return Held(facts, CourseHoldWhy.AnswersAlone, code: SupervisorWhy.SupervisorBound);
        }

        return Held(facts, CourseHoldWhy.AnswersAlone, [CourseDoor.LetAnswerGoOn, CourseDoor.NotNow, CourseDoor.Say], code: GoOnMarks.AnswersAlone);
    }

    // MODELROLE1d2 (BriefedMove.NotOfferedAsync): a fresh start of its actor, its words the person's, a pick trying its own agent's entries
    // on its own account; with none of the person's words, or its tree gone, nothing opens.
    private static LineCourse Fresh(CourseFacts facts)
    {
        var head = facts.Head;
        var persons = head.Words.Where(word => word.Persons).ToList();
        if (persons.Count == 0) return Refused(facts, StartFrom.NoWords);
        if (!head.AskDaoris && head.Tree is null) return Refused(facts, HandOver.TreeGone);
        var where = head.Choice == "pick"
            ? new CourseWhere(CourseAccountSource.Pick, head.Role, head.Choice, head.Profile)
            : new CourseWhere(CourseAccountSource.Settled, head.Role, head.Choice);
        return Moved(facts, CourseWhen.Now, where, CourseFromWhy.NotOffered, CourseFromBy.Daoris, Ids(persons));
    }

    // ——— Paths B and J: a conversation proven lost, and the briefed move.

    // SESSCTX1c part B (ChatGoOn.LostAsync): a conversation its limit held, or a restart's, goes on by the limit's ended move, its own
    // account allowed since its conversation is lost, a pick waiting for its account (PICKLOST1); else, with the person's words, by the
    // briefed move; nothing opening, it cannot go on. A driven record's lost conversation is its fall-back, or a closed quest's conversation.
    private static LineCourse Lost(CourseFacts facts)
    {
        var head = facts.Head;
        if (!head.Conversation) return Judged(facts, head.Judged ?? ContinueWhy.Gone);

        var account = facts.Account;
        var restarted = head.Words.Any(word => word.Author == CourseAuthor.Daoris && DaorisKeys.IsRestart(word.Key));
        if (head.Limited || restarted)
        {
            var why = head.Limited ? CourseFromWhy.Limit : CourseFromWhy.Resume;
            var persons = Ids(head.Words.Where(word => word.Persons));
            var picked = account?.Picked == true;
            switch (account?.Answer)
            {
                case LineAnswer.Wait wait when picked:
                    return Waited(facts, wait.Earliest, CourseWaitWhy.LimitReset, [wait.Earliest.Account ?? ""]);
                case LineAnswer.Resume:
                    return Moved(facts, CourseWhen.Now, OwnOrPick(facts), why, CourseFromBy.Daoris, persons);
                case LineAnswer.Elsewhere elsewhere when !picked:
                    return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), why, CourseFromBy.Daoris, persons);
            }

            if (head.Limited && Later(facts) is { } later) return Moved(facts, CourseWhen.Now, later, why, CourseFromBy.Daoris, persons);
        }

        return Briefed(facts, CourseFromBy.Daoris);
    }

    // D189 (BriefedMove.MovingAsync): a new part of the line, why `resume`, Daoris's brief and the person's words alone, on the walk with
    // its own account first where ready (FOUNDATION1a-iii, §3.2 step 6), an account the person picked the one account. The person's press
    // is refused where it does not apply; Daoris's on a lost conversation that nothing opens for cannot go on.
    private static LineCourse Briefed(CourseFacts facts, CourseFromBy by)
    {
        var head = facts.Head;
        var person = by == CourseFromBy.Person;
        if (person)
        {
            if (facts.Boundary is CourseBoundary.TurnRunning or CourseBoundary.BetweenTurns) return Refused(facts, StartFrom.Running);
            if (head.Words.Count == 0) return Refused(facts, StartFrom.NoWords);
            if (head.Carried) return Refused(facts, StartFrom.Carried);
        }

        var persons = head.Words.Where(word => word.Persons).ToList();
        LineCourse Nothing(string code) => person ? Refused(facts, code) : CannotGoOn(facts, head.Judged ?? ContinueWhy.Gone);
        if (persons.Count == 0) return Nothing(StartFrom.NoWords);
        if (!head.AskDaoris && head.Tree is null) return Nothing(HandOver.TreeGone);
        if (head.AgentGone) return Nothing(GoOnBrief.AgentGone);

        var picked = facts.Account?.Picked == true;
        switch (facts.Account?.Answer)
        {
            case LineAnswer.Resume or null:
                return Moved(facts, CourseWhen.Now, OwnOrPick(facts), CourseFromWhy.Resume, by, Ids(persons));
            case LineAnswer.Elsewhere elsewhere when !picked:
                return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), CourseFromWhy.Resume, by, Ids(persons));
            case LineAnswer.Wait wait when person:
                return Waited(facts, wait.Earliest, CourseWaitWhy.AccountCooling, wait.Accounts, WaitDoors(facts, wait.Why));
            case LineAnswer.Hold hold when person:
                return Held(facts, CourseHoldWhy.Account, repair: hold.Repair);
            default:
                return CannotGoOn(facts, head.Judged ?? ContinueWhy.Gone);
        }
    }

    // ——— Paths D, E and F: a limit's end and its wake.

    private static LineCourse Limit(CourseFacts facts, CourseTrigger trigger)
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

        // FOUNDATION1a-ii (D205 §3.5(d)): an account the person picked is never moved off; it waits for that account's reset.
        if (account?.Picked == true && answer is LineAnswer.Wait picked)
        {
            return Waited(facts, picked.Earliest, CourseWaitWhy.LimitReset, [picked.Earliest.Account ?? ""]);
        }

        var persons = Ids(head.Words.Where(word => word.Persons));
        // LIMITGOON1: another account of its agent with room, never its own (LimitMove.cs:107, :298-299): a new part there, by the limit.
        if (answer is LineAnswer.Elsewhere elsewhere && !Same(elsewhere.Selection.Profile, head.Profile))
        {
            return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), CourseFromWhy.Limit, CourseFromBy.Daoris, persons);
        }

        // LIMITGOON2 (D205 §3.5(e)): none of its agent's has room, so its actor's next choice on another agent, never for an agent's pick.
        if (Later(facts) is { } later) return Moved(facts, CourseWhen.Now, later, CourseFromWhy.Limit, CourseFromBy.Daoris, persons);

        // Nothing has room: it waits for the earliest reset of any account of its agent (LimitWaits.cs:236-237).
        var earliest = (answer as LineAnswer.Wait)?.Earliest ?? account?.Earliest;
        return earliest is null
            ? Held(facts, CourseHoldWhy.Stays)
            : Waited(facts, earliest, CourseWaitWhy.LimitReset, (answer as LineAnswer.Wait)?.Accounts ?? [earliest]);
    }

    // LIMITRESUME1b (the limit-resume design §3.3): step 0, a hand-over made from it decides; step 1, its own account ready (its maker's
    // reading asked), the protocol door and the judge passing, its own conversation resumes with Daoris's word; step 2, another account it
    // may run on: a briefed part there, `limit` on another account, else `carry-on`; step 3, none ready: it waits for the earliest reset.
    private static LineCourse DrivenLimit(CourseFacts facts)
    {
        var head = facts.Head;
        if (facts.Press is { Kind: CoursePressKind.HandOver } handed) return Handed(facts, handed);

        var answer = facts.Account?.Answer;
        if (answer is LineAnswer.Resume && head.Protocol && head.Judged is null)
        {
            // AGENTERRORS1a2: a released transient record's words go on alone, as the go-on's; only Try again's start with none takes the word.
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
    // update's restart, else the limit word, keyed by the cool-off's `until` as the record's note said it (DaorisKeys, LimitResume.Until).
    private static CourseDaorisWord WordKind(CourseFacts facts) =>
        facts.Failure is { PastBound: true } ? CourseDaorisWord.TransientRetry
        : facts.Limit?.Build is not null ? CourseDaorisWord.Restart
        : CourseDaorisWord.LimitReset;

    private static string WordKey(CourseFacts facts)
    {
        var session = facts.Head.Session ?? "";
        return WordKind(facts) switch
        {
            CourseDaorisWord.TransientRetry => DaorisKeys.TransientRelease(session, facts.Limit?.Ended ?? DateTimeOffset.UnixEpoch),
            CourseDaorisWord.Restart => DaorisKeys.Restart(session, facts.Limit!.Build!),
            _ => DaorisKeys.LimitReset(session, facts.Limit?.Until ?? "unsaid"),
        };
    }

    // ——— Paths K and N: the planner's carry-ons, and what follows a transient bound.

    private static LineCourse CarryOnOf(CourseFacts facts)
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

    // AGENTERRORS1a (D210 point 3; Planner.TransientHeld): the retries are the run's; past their bound the line waits for the person's word
    // or *Try again*, which releases that end into the limit resume's step 1 (LimitResume.cs:40-42).
    private static LineCourse PastBound(CourseFacts facts)
    {
        if (facts.Press?.Kind == CoursePressKind.TryAgain) return DrivenLimit(facts);
        return facts.Head.Words.Any(word => word.Persons && word.AfterEnd)
            ? GoOn(facts, CourseTrigger.WordsKept)
            : Held(facts, CourseHoldWhy.TransientBound, [CourseDoor.TryAgain, CourseDoor.Say]);
    }

    // The new part's `from` for a driven carry-on (Driver.ContinuesFrom, Driver.cs:4776-4788): a limit's on another account or agent, the
    // curve's where the walk's first step passed an account over its curve to another, `retry` where its resume fell back, else `carry-on`,
    // the person's where their press released the stop or brought the work forward.
    private static (CourseFromWhy Why, CourseFromBy By) DrivenWhy(CourseFacts facts, HarnessSelection? selection, bool retried)
    {
        var head = facts.Head;
        var another = selection is not null && (!Same(selection.Profile, head.Profile) || facts.Account?.Agent is not null);
        if (head.Limited && another) return (CourseFromWhy.Limit, CourseFromBy.Daoris);
        if (facts.Account?.Curve == true && another) return (CourseFromWhy.Curve, CourseFromBy.Daoris);
        if (retried) return (CourseFromWhy.Retry, CourseFromBy.Daoris);
        var pressed = facts.CarryOn is CourseCarryOn.StopReleased or CourseCarryOn.BroughtForward;
        return (CourseFromWhy.CarryOn, pressed ? CourseFromBy.Person : CourseFromBy.Daoris);
    }

    // ——— Paths G, H and I: the curve and the hand-overs.

    // SWITCH1c-ii (CurveMove.MoveAsync): judged once at a turn's end with nothing running, a conversation's alone, never Ask Daoris's nor one
    // on the tool's own home; the scope's switch admits Daoris's move, and the decision names the account it moves to as a pick.
    private static LineCourse Curve(CourseFacts facts)
    {
        var head = facts.Head;
        if (!head.Conversation || head.AskDaoris || head.Profile is null) return Held(facts, CourseHoldWhy.Stays);
        if (facts.Boundary == CourseBoundary.TurnRunning) return Held(facts, CourseHoldWhy.Stays);
        if (facts.Rules?.SwitchAdmits != true) return Held(facts, CourseHoldWhy.SwitchOff);
        if (facts.Rules.CurveAccount is not { } account) return Held(facts, CourseHoldWhy.Stays);
        return Moved(facts, CourseWhen.TurnEnd, new CourseWhere(CourseAccountSource.Pick, head.Role, head.Choice, account), CourseFromWhy.Curve, CourseFromBy.Daoris, Ids([]));
    }

    // MIXAGENT1a, D163: a hand-over pressed or asked. An agent's ask goes through the hand-over rule's consent first (DriverHandOvers.cs:
    // 208-214): `on` admits it, `ask` puts it to the person, `off` refuses it. A conversation's is taken at its turn's end, the brief only,
    // none of the person's words (HandOver.cs:271); a quest's session's is stopped at once for the person's press as their stop (D163 point
    // 1, D188 point 1) and taken at the turn's end for an agent's ask (DriverHandOvers.cs:227-235).
    private static LineCourse HandOverOf(CourseFacts facts)
    {
        var head = facts.Head;
        if (facts.Press is not { Kind: CoursePressKind.HandOver } press) return Held(facts, CourseHoldWhy.Stays);
        if (head.AskDaoris) return Refused(facts, WordsNever.Help);
        var by = press.By ?? CourseFromBy.Person;
        var agent = press.Agent ?? head.Adapter;
        if (Same(agent, head.Adapter) && Same(press.Account, head.Profile)) return Refused(facts, HandOver.SameAgent);

        if (!head.Conversation)
        {
            // HANDOVERSAID1: what the take would refuse is refused at the ask, before any consent: a quest no longer open or taken.
            if (!head.Carried) return Refused(facts, HandOver.QuestClosed);
            if (head.State == "awaiting-person" && by == CourseFromBy.Person && !head.AgentGone) return Refused(facts, HandOver.Parked);
        }
        else if (head.Tree is null)
        {
            return Refused(facts, HandOver.TreeGone);
        }

        if (by == CourseFromBy.Agent && facts.Rules?.HandOverAdmits != true)
        {
            return facts.Rules?.HandOverAsks == true
                ? Held(facts, CourseHoldWhy.ConsentRefused, [CourseDoor.LetAskGo, CourseDoor.NotNow])
                : Held(facts, CourseHoldWhy.ConsentRefused, code: HandOverAskWhy.Off);
        }

        var why = press.Why ?? (by == CourseFromBy.Agent ? CourseFromWhy.Agent : CourseFromWhy.Press);
        if (head.Conversation)
        {
            // A named account opens as a pick; only the person's press keeps it as the conversation's pick (ChatHandOver.cs:156-160).
            var where = press.Account is { } named
                ? new CourseWhere(CourseAccountSource.Pick, head.Role, head.Choice, named, agent, Kept: by == CourseFromBy.Person)
                : new CourseWhere(CourseAccountSource.Settled, head.Role, head.Choice, Agent: agent);
            return Moved(facts, CourseWhen.TurnEnd, where, why, by, Ids([]));
        }

        var when = by == CourseFromBy.Agent && facts.Boundary == CourseBoundary.TurnRunning ? CourseWhen.TurnEnd : CourseWhen.Now;
        return Moved(facts, when, HandedWhere(facts, press), why, by, Ids(head.Words.Where(word => word.Persons)));
    }

    // A quest handed over from this record (step 0, the handed store): its start goes on with the agent and asker the hand-over named.
    private static LineCourse Handed(CourseFacts facts, CoursePress press) =>
        Moved(
            facts, CourseWhen.Now, HandedWhere(facts, press), press.Why ?? CourseFromWhy.Press, press.By ?? CourseFromBy.Person,
            Ids(facts.Head.Words.Where(word => word.Persons)));

    // A handed quest's named account first, then the walk within the named agent (SelectHandedAsync, HarnessRoster.cs:923-934).
    private static CourseWhere HandedWhere(CourseFacts facts, CoursePress press)
    {
        var head = facts.Head;
        var agent = press.Agent ?? head.Adapter;
        if (press.Account is { } named) return new CourseWhere(CourseAccountSource.Named, head.Role, head.Choice, named, agent);
        var walked = (facts.Account?.Answer as LineAnswer.Elsewhere)?.Selection.Profile;
        return new CourseWhere(CourseAccountSource.Settled, head.Role, head.Choice, walked, agent);
    }

    // ——— Path C: go on in a new session; start a conversation with these words.

    // GoOnNew.AskAsync: refused by its code where it does not apply; else the next look's go-on takes the walk's pick for the person's words.
    private static LineCourse GoOnNewOf(CourseFacts facts)
    {
        var head = facts.Head;
        if (head.AskDaoris) return Refused(facts, WordsNever.Help);
        if (facts.Boundary is CourseBoundary.TurnRunning or CourseBoundary.BetweenTurns && head.State != "awaiting-person")
        {
            return Refused(facts, GoOnNew.Running);
        }

        if (head.Words.Count == 0) return Refused(facts, GoOnNew.NoWords);
        if (head.Conversation || head.QuestStatus is null) return Refused(facts, GoOnNew.Conversation, [CourseDoor.StartFrom]);
        if (!head.Carried) return Refused(facts, GoOnNew.Closed, [CourseDoor.StartFrom]);

        switch (facts.Account?.Answer)
        {
            case LineAnswer.Elsewhere elsewhere:
                return Moved(facts, CourseWhen.Now, Settled(facts, elsewhere.Selection), CourseFromWhy.Retry, CourseFromBy.Daoris, Ids(head.Words.Where(word => word.Persons)));
            case LineAnswer.Wait wait:
                return Waited(facts, wait.Earliest, CourseWaitWhy.AccountCooling, wait.Accounts);
            case LineAnswer.Hold hold:
                return Held(facts, CourseHoldWhy.Account, repair: hold.Repair);
            default:
                // Its account is not cooling: the same session goes on with the words at the driver's next look.
                return Refused(facts, GoOnNew.NotCooling);
        }
    }

    // StartFrom.RunAsync: a fresh conversation with no `from`, the person's words as its first, keyed by the press's own intent key so a
    // second press opens a second line and none supersedes the source (X-3.2).
    private static LineCourse StartFromOf(CourseFacts facts)
    {
        var head = facts.Head;
        if (head.AskDaoris) return Refused(facts, WordsNever.Help);
        if (facts.Boundary is CourseBoundary.TurnRunning or CourseBoundary.BetweenTurns) return Refused(facts, StartFrom.Running);
        if (head.Words.Count == 0) return Refused(facts, StartFrom.NoWords);
        if (head.Carried) return Refused(facts, StartFrom.Carried);
        var persons = head.Words.Where(word => word.Persons).ToList();
        if (persons.Count == 0) return Refused(facts, StartFrom.NoWords);
        return new Start(facts.Press?.Intent ?? head.Session ?? "", Ids(persons), ReadOn(facts));
    }

    // ——— The answers' parts.

    private static Resume Resumed(CourseFacts facts, CourseWords words) => new(words, ReadOn(facts));

    private static Move Moved(CourseFacts facts, CourseWhen when, CourseWhere where, CourseFromWhy why, CourseFromBy by, CourseWords words) =>
        new(when, where, CourseHow.Brief, why, by, words, ReadOn(facts));

    private static Wait Waited(
        CourseFacts facts, CoolingEntry earliest, CourseWaitWhy why, IEnumerable<CoolingEntry> accounts, IReadOnlyList<CourseDoor>? doors = null) =>
        Waited(facts, earliest, why, [.. accounts.Select(cooling => cooling.Account ?? "")], doors);

    private static Wait Waited(
        CourseFacts facts, CoolingEntry earliest, CourseWaitWhy why, IReadOnlyList<string> accounts, IReadOnlyList<CourseDoor>? doors = null) =>
        new(earliest.Until, why, accounts, ReadOn(facts, earliest), doors);

    private static Hold Held(
        CourseFacts facts, CourseHoldWhy why, IReadOnlyList<CourseDoor>? doors = null, LineRepair repair = LineRepair.None, string? code = null) =>
        new(why, doors ?? [], repair, ReadOn(facts), code);

    private static Hold Refused(CourseFacts facts, string code, IReadOnlyList<CourseDoor>? doors = null) =>
        Held(facts, CourseHoldWhy.Refused, doors, code: code);

    // A conversation's words that nothing carries on by itself (ChatGoOn.CannotGoOn), or a driven record's held for the person: its doors
    // the presses that apply, *Start a conversation with these words* and *Go on with Daoris's brief*, each only with the person's words.
    private static Hold CannotGoOn(CourseFacts facts, string code, LineRepair repair = LineRepair.None)
    {
        var head = facts.Head;
        var persons = head.Words.Any(word => word.Persons);
        List<CourseDoor> doors = [];
        if (persons && head.Conversation && code != ContinueWhy.Account && code != ContinueWhy.Tree && !head.AgentGone) doors.Add(CourseDoor.GoOnBrief);
        if (persons && (head.Conversation || !head.Carried)) doors.Add(CourseDoor.StartFrom);
        return Held(facts, CourseHoldWhy.CannotGoOn, doors, repair, code);
    }

    // The door out of an account's wait (ResumeWords.NotYetOn's): a conversation's and a closed quest's, a conversation with these words; an
    // open or taken quest's session, a new session, only while its own account cools.
    private static List<CourseDoor> WaitDoors(CourseFacts facts, NextHold why)
    {
        var head = facts.Head;
        if (!head.Carried) return head.Words.Any(word => word.Persons) ? [CourseDoor.StartFrom] : [];
        return why == NextHold.Cooling ? [CourseDoor.GoOnNew] : [];
    }

    // What the answer was read on (§3.1, X): named apart from the answers' own `Needs`, which a member of the base would hide.
    private static CoursePreconditions ReadOn(CourseFacts facts, CoolingEntry? cooling = null) =>
        new(facts.Head.Revision, [.. facts.Head.Words.Select(word => word.Id)], facts.Head.QuestStatus, cooling, facts.Standing?.Phase);

    private static CourseWords All(IEnumerable<CourseWord> words) => new([.. words.Select(word => word.Id)]);

    private static CourseWords Ids(IEnumerable<CourseWord> words) => All(words);

    // The walk's settled selection, on the agent the walk took where it took another (WALKAGENT1).
    private static CourseWhere Settled(CourseFacts facts, HarnessSelection selection) =>
        new(CourseAccountSource.Settled, facts.Head.Role, facts.Head.Choice, selection.Profile, facts.Account?.Agent);

    // The line's own account: kept for a resume's new part where it is a Daoris account, the walk's where it was the tool's own home
    // (OWNSPACE1a, HarnessRoster.CarriedOnAsync).
    private static CourseWhere OwnOrWalk(CourseFacts facts) =>
        facts.Head.Profile is { } own
            ? new CourseWhere(CourseAccountSource.Own, facts.Head.Role, facts.Head.Choice, own)
            : new CourseWhere(CourseAccountSource.Settled, facts.Head.Role, facts.Head.Choice);

    // A fresh part the person pressed for, or one whose own conversation is lost, takes its own account where ready; one the person picked
    // takes it as the one account and keeps the pick (PICKCARRY1).
    private static CourseWhere OwnOrPick(CourseFacts facts) =>
        facts.Account?.Picked == true
            ? new CourseWhere(CourseAccountSource.Pick, facts.Head.Role, facts.Head.Choice, facts.Head.Profile, Kept: true)
            : OwnOrWalk(facts);

    // The role's later choice on another agent (LIMITGOON2), never for an agent's pick (LimitMove.cs:574).
    private static CourseWhere? Later(CourseFacts facts) =>
        facts.Head.Choice != "pick" && facts.Account?.Later is { } later
            ? new CourseWhere(CourseAccountSource.Settled, facts.Head.Role, later.Choice, later.Account, later.Agent)
            : null;

    private static bool Same(string? one, string? other) => string.Equals(one ?? "", other ?? "", StringComparison.OrdinalIgnoreCase);
}
