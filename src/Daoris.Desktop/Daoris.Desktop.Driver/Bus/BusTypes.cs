namespace Daoris.Driver;

/// <summary>
/// The one registry of the bus's types: each declared once here with its payload's fields, its emitter, its obliged handler
/// or answerer, its deadline, its retries and its give-up. Checked at emit (<see cref="BusLog.Append"/> refuses a type not here and drops a
/// field or value it does not declare) and at subscribe (<see cref="Subscribe"/>); <c>BusTypesCensusTests</c> reads it.
/// </summary>
/// <remarks>
/// <para><b>Deadlines.</b> A deadline is set from the handling times <c>daoris bus stats</c> reads where they are measured; where none are,
/// it is stated provisional with what it stands on, and the watchdog runs against it in shadow before any give-up acts; a draft names none.</para>
/// <para><b>Give-ups.</b> Every chain ends at the owner's final exit: a request the bus answers itself (<c>refused</c>) or
/// a gate that stays shut (<c>held</c>) leaves its step a wait, which a vacancy bounds and the final exit ends, never a silent give-up.</para>
/// </remarks>
public static class BusTypes
{
    /// <summary>The check facts' emitter.</summary>
    public const string Checks = "driver/checks";

    /// <summary>The opinion facts' emitter, and the deliverer that hands a pass's findings on.</summary>
    public const string Opinions = "driver/opinions";

    /// <summary>The run's step that reads a check's result: it settles, sends a red back or holds. Its consumer is the run's progression (<c>RunStepConsumer</c>).</summary>
    public const string Step = "driver/step";

    /// <summary>The emitter of the facts a step's ending is: a settle taken, a session's turn ended, a landing's try ended.</summary>
    public const string Steps = "driver/steps";

    /// <summary>The opinion's gate, which judges the answers to a pass's findings.</summary>
    public const string OpinionGate = "driver/opinion-gate";

    /// <summary>The reviewer's door the envelope's answerer names.</summary>
    public const string Reviewer = "reviewer";

    /// <summary>The working session the payload's <c>session</c> names.</summary>
    public const string Session = "session";

    /// <summary>The dispatch's retry facts: a consumer's failed work, tried again when due.</summary>
    public const string Flow = "driver/flow";

    /// <summary>The watchdog's stalls and give-ups, and the Stalls view that shows them.</summary>
    public const string Watchdog = "driver/watchdog", Stalls = "driver/stalls";

    /// <summary>Where the service's ledger is heard (its change stream), the node runtime's records, and this process's standing choices.</summary>
    public const string Ledger = "driver/ledger", Nodes = "driver/nodes", Choices = "driver/choices", Sessions = "driver/sessions";

    /// <summary>The commit the install ran when its measures were read; the days they cover ran earlier builds too, so it dates the reading, not the figures.</summary>
    public const string MeasuredAt = "99ce8edc60";

    private const string Unmeasured = "no handling time of it is measured yet: the install's `daoris bus stats` at " + MeasuredAt
        + " (2026-11-21 to 23) holds none for it (a consumer's acknowledgement time is the `handled` measure, which begins there)";

    private const string AtALook =
        Unmeasured + "; the run's step consumer began nodes at an ending fact a mean 68 s and at most 65 minutes after it (n=178, 2026-11-22 and 23), so ten minutes stands until its handling times are in";

    private static readonly IReadOnlyList<BusField> Settled =
    [
        new("invocation", BusShapes.Id), new("commit", BusShapes.Id), new("verdict", BusShapes.Code), new("code", BusShapes.Code),
        new("failed", BusShapes.Code), new("red", BusShapes.Ids), new("redCount", BusShapes.Count), new("gates", BusShapes.Count),
    ];

    private static readonly BusDeadline StepLook = BusDeadline.Stated(TimeSpan.FromMinutes(10), AtALook);

    /// <summary>A check's attempt began, both its slots held: its executor must settle it.</summary>
    public static readonly BusType CheckStarted = new(
        "check.started", 1, BusClasses.Fact, Checks, Checks,
        BusDeadline.Measured(TimeSpan.FromMinutes(105),
            "the checks' start-to-verdict times, read at " + MeasuredAt + ", 2026-11-21 to 23: n=214 (passed 154, failed 53, unknown 7), the means 9 to 27 minutes a day, the most 3,091,955 ms (51.5 minutes); "
            + "twice the most, to a quarter hour. A check runs under its node's bound, two hours where it declares none, past which its executor settles it unknown, which ends the timer; "
            + "one running longer than any measured is stalled once, said on Stalls, and tried again"),
        1, BusGiveUps.FinalExit,
        [new("invocation", BusShapes.Id), new("repository", BusShapes.Id), new("commit", BusShapes.Id), new("gates", BusShapes.Count), new("reach", BusShapes.Code)]);

    /// <summary>A check's attempt passed: the run's step must settle on it.</summary>
    public static readonly BusType CheckPassed = new("check.passed", 1, BusClasses.Fact, Checks, Step, StepLook, 2, BusGiveUps.Held, Settled);

    /// <summary>A check's attempt failed, naming its gate and its red classes: the run's step must send the red back or hold it.</summary>
    public static readonly BusType CheckFailed = new("check.failed", 1, BusClasses.Fact, Checks, Step, StepLook, 2, BusGiveUps.FinalExit, Settled);

    /// <summary>A check's attempt ended unknown (its code says why), never a failure: the run's step must try again or hold it.</summary>
    public static readonly BusType CheckUnknown = new("check.unknown", 1, BusClasses.Fact, Checks, Step, StepLook, 2, BusGiveUps.FinalExit, Settled);

    /// <summary>A check's attempt sent back to the queue by its busy runner: its executor must start it again at a later offer.</summary>
    public static readonly BusType CheckRequeued = new(
        "check.requeued", 1, BusClasses.Fact, Checks, Checks,
        BusDeadline.Stated(TimeSpan.FromMinutes(10) + TimeSpan.FromMinutes(5),
            Unmeasured + "; a busy runner is offered again ten minutes later, and five more are the offer's turn"),
        1, BusGiveUps.FinalExit,
        [new("invocation", BusShapes.Id), new("commit", BusShapes.Id), new("code", BusShapes.Code), new("offers", BusShapes.Count)]);

    /// <summary>A check's attempt ended with no result (the driver closing, its result unkept): its restart must send it again.</summary>
    public static readonly BusType CheckEnded = new(
        "check.ended", 1, BusClasses.Fact, Checks, Checks,
        BusDeadline.Stated(TimeSpan.FromMinutes(30), Unmeasured + "; its restart sends it again, and no restart's resend is measured: a restart after an update or a crash takes minutes"),
        1, BusGiveUps.FinalExit,
        [new("invocation", BusShapes.Id), new("commit", BusShapes.Id), new("code", BusShapes.Code)]);

    /// <summary>A pass asked of a reviewer: its door must give it.</summary>
    public static readonly BusType OpinionAsked = new(
        "opinion.asked", 1, BusClasses.Request, Opinions, Reviewer,
        BusDeadline.Measured(TimeSpan.FromMinutes(30),
            "the passes' times, read at " + MeasuredAt + ", 2026-11-21 to 23: n=81, the means 8 to 9 minutes a day, the most 867,000 ms (14.5 minutes); twice the most, to five minutes. "
            + "A pass runs under its rule's bound, twenty minutes where the rule names none (5 to 120); a rule's longer bound outlasts it, which the watchdog counts"),
        1, BusGiveUps.Refused,
        [new("session", BusShapes.Id), new("occasion", BusShapes.Code), new("pass", BusShapes.Code)]);

    private const string AtTheGate =
        Unmeasured + "; the gate is the queue's consumer and judges at the fact (BUS2d), so ten minutes covers a judgment's reads of the service, git and the run's process with room";

    /// <summary>A session's work owed a second opinion, or a person's ask of the gate: the opinion's gate must judge it. Its subject is <c>session/occasion</c>.</summary>
    public static readonly BusType OpinionOwed = new(
        "opinion.owed", 1, BusClasses.Fact, Opinions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("session", BusShapes.Id), new("quest", BusShapes.Id), new("occasion", BusShapes.Code), new("by", BusShapes.Code)]);

    /// <summary>A pass given, its findings counted by weight: the opinion's gate must deliver them, or pass it by the policy where clean.</summary>
    public static readonly BusType OpinionGiven = new(
        "opinion.given", 1, BusClasses.Fact, Opinions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.FinalExit,
        [
            new("pass", BusShapes.Code), new("must", BusShapes.Count), new("should", BusShapes.Count), new("note", BusShapes.Count),
            new("seconds", BusShapes.Count), new("tier", BusShapes.Code),
        ]);

    /// <summary>A pass's findings handed to the working session: it must answer them.</summary>
    public static readonly BusType OpinionDelivered = new(
        "opinion.delivered", 1, BusClasses.Fact, Opinions, Session,
        BusDeadline.Stated(TimeSpan.FromHours(2), Unmeasured + "; the working session answers at its turn's end, and an agent's turn runs minutes to an hour or more"),
        1, BusGiveUps.FinalExit,
        [new("session", BusShapes.Id), new("findings", BusShapes.Count), new("how", BusShapes.Code)]);

    /// <summary>The working session's answers to a pass's findings, counted as its turn's end read them: the opinion's gate must judge them.</summary>
    public static readonly BusType OpinionAnswered = new(
        "opinion.answered", 1, BusClasses.Fact, Opinions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("fixed", BusShapes.Count), new("rejected", BusShapes.Count), new("unresolved", BusShapes.Count), new("notAnswered", BusShapes.Count)]);

    /// <summary>Answers kept to a pass's findings mid-turn, as the service journalled them: an observation, so only the turn's end (<see cref="OpinionAnswered"/>) is judged.</summary>
    public static readonly BusType OpinionKept = new(
        "opinion.kept", 1, BusClasses.Fact, Ledger, Stalls, BusDeadline.Stated(TimeSpan.FromDays(1), Observed), 0, BusGiveUps.Dropped,
        [new("session", BusShapes.Id), new("fixed", BusShapes.Count), new("rejected", BusShapes.Count), new("unresolved", BusShapes.Count)]);

    /// <summary>The person's word at a session's gate (<i>Go on anyway</i>, <i>I looked myself</i>, an Accept's answer): the opinion's gate must judge that session's owed opinions.</summary>
    public static readonly BusType OpinionSaid = new(
        "opinion.said", 1, BusClasses.Fact, Opinions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("session", BusShapes.Id), new("said", BusShapes.Code), new("door", BusShapes.Code)]);

    /// <summary>The service's quests moved (a hold lifted, a step declined): the opinion's gate must judge the owed opinions its named quests and repositories reach, every one where none is named.</summary>
    public static readonly BusType QuestMoved = new(
        "quest.moved", 1, BusClasses.Fact, Ledger, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("families", BusShapes.Ids), new("quests", BusShapes.Ids), new("repositories", BusShapes.Ids)]);

    /// <summary>A run's node record began nodes (left blocked or ready): the opinion's gate must judge the owed opinions of that run (<see cref="NodeBegunReach"/>), which a graph's opinion node holds until begun. Its subject is the unit.</summary>
    public static readonly BusType NodeBegun = new(
        "node.begun", 1, BusClasses.Fact, Nodes, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("nodes", BusShapes.Ids)]);

    /// <summary>A turn of a session whose work is owed a second opinion ended: the opinion's gate must judge that session's owed opinions, a delivery that waited on the turn among them. Its subject is the session.</summary>
    public static readonly BusType SessionTurnEnded = new(
        "session.turn-ended", 1, BusClasses.Fact, Sessions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("session", BusShapes.Id), new("owed", BusShapes.Ids)]);

    private const string AtTheRun =
        Unmeasured + "; the run's step consumer asks one pass for the run at the fact (BUS2e1), which judges that run first with no read of the units, so ten minutes covers a pass held behind another or a drain with room";

    private static readonly BusDeadline StepFact = BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheRun);

    /// <summary>A working session's step settle taken from its box: the run's step must go on from it. Its subject is the run's unit.</summary>
    public static readonly BusType StepSettled = new(
        "step.settled", 1, BusClasses.Fact, Steps, Step, StepFact, 2, BusGiveUps.Held,
        [new("session", BusShapes.Id), new("run", BusShapes.Id), new("node", BusShapes.Id)]);

    /// <summary>The session that performs a plugin step ended its turn, its answer kept or not: the run's step must go on from it. Its subject is the run's unit.</summary>
    public static readonly BusType StepTurnEnded = new(
        "step.turn-ended", 1, BusClasses.Fact, Steps, Step, StepFact, 2, BusGiveUps.FinalExit,
        [new("invocation", BusShapes.Id), new("node", BusShapes.Id)]);

    /// <summary>The risk policy passed a second opinion at its gate: the run's opinion step must go on to the step it holds. Its subject is the run's unit.</summary>
    public static readonly BusType PolicyPassed = new(
        "policy.passed", 1, BusClasses.Fact, Opinions, Step, StepFact, 2, BusGiveUps.Held,
        [new("step", BusShapes.Id), new("tip", BusShapes.Id)]);

    /// <summary>A landing's try ended (landed or refused, its code says): the run's landing step must go on from it. Its subject is the landed unit.</summary>
    public static readonly BusType LandOutcome = new(
        "land.outcome", 1, BusClasses.Fact, Steps, Step, StepFact, 2, BusGiveUps.FinalExit,
        [new("landed", BusShapes.Flag), new("code", BusShapes.Code), new("tip", BusShapes.Id)]);

    /// <summary>A start bound its run to a graph's version (BUS2e2): the run's first step must begin from it, not from the next whole read. Its subject is the run's unit.</summary>
    public static readonly BusType RunBound = new(
        "run.bound", 1, BusClasses.Fact, Steps, Step, StepFact, 2, BusGiveUps.Held,
        [new("run", BusShapes.Id), new("workflow", BusShapes.Id), new("version", BusShapes.Count)]);

    /// <summary>This process wrote the standing choices (a rule's reviewers, a workflow, the policy): the opinion's gate must judge every owed opinion.</summary>
    public static readonly BusType ChoicesWritten = new(
        "choices.written", 1, BusClasses.Fact, Choices, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held, []);

    private const string Observed = "an observation the Stalls view shows when it is read: no one must act on it, so it arms no timer";

    /// <summary>An obliged entry past its due time, its retry counted, the look's reading of its subject beside it (<see cref="BusLookStates"/>).</summary>
    public static readonly BusType Stalled = new(
        "bus.stalled", 1, BusClasses.Fact, Watchdog, Stalls, BusDeadline.Stated(TimeSpan.FromDays(1), Observed), 0, BusGiveUps.Dropped,
        [
            new("entry", BusShapes.Id), new("kind", BusShapes.Code), new("obliged", BusShapes.Code), new("since", BusShapes.Time),
            new("due", BusShapes.Time), new("retry", BusShapes.Count), new("retries", BusShapes.Count), new("next", BusShapes.Time),
            new("look", BusShapes.Code), new("says", BusShapes.Code),
        ]);

    /// <summary>The give-up past an entry's last retry and what it hands to whom (<see cref="BusChains"/>); in shadow, <c>applied</c> false.</summary>
    public static readonly BusType GiveUp = new(
        "bus.give-up", 1, BusClasses.Fact, Watchdog, Stalls, BusDeadline.Stated(TimeSpan.FromDays(1), Observed), 0, BusGiveUps.Dropped,
        [
            new("entry", BusShapes.Id), new("kind", BusShapes.Code), new("consequence", BusShapes.Code), new("applied", BusShapes.Flag),
            new("hands", BusShapes.Code), new("to", BusShapes.Ids), new("since", BusShapes.Time), new("tries", BusShapes.Count),
            new("consumer", BusShapes.Code),
        ]);

    /// <summary>A consumer's failed work as a fact the log keeps, due after the backoff; it arms no timer, the failed entry's own stays.</summary>
    public static readonly BusType FlowRetry = new(
        "flow.retry", 1, BusClasses.Fact, Flow, Flow, BusDeadline.Stated(TimeSpan.FromDays(1), Observed), 5, BusGiveUps.Dropped,
        [
            new("consumer", BusShapes.Code), new("kind", BusShapes.Code), new("scope", BusShapes.Code), new("seq", BusShapes.Count), new("keys", BusShapes.Ids),
            new("attempt", BusShapes.Count), new("due", BusShapes.Time), new("since", BusShapes.Time), new("settles", BusShapes.Flag),
            new("subject", BusShapes.Id),
        ]);

    /// <summary>Every type, each once.</summary>
    public static readonly IReadOnlyList<BusType> All =
    [
        OpinionOwed, OpinionAsked, OpinionGiven, OpinionDelivered, OpinionAnswered, OpinionKept, OpinionSaid, QuestMoved, NodeBegun, ChoicesWritten, SessionTurnEnded, StepSettled, StepTurnEnded, PolicyPassed, LandOutcome, RunBound, CheckStarted, CheckPassed,
        CheckFailed, CheckUnknown, CheckRequeued, CheckEnded, Stalled, GiveUp, FlowRetry,
    ];

    /// <summary>What ends each obliged type's timer (<see cref="BusEnd"/>); until its handler subscribes, such a timer runs out in shadow. <c>BusTypesCensusTests</c> holds that every obliged type has its ends.</summary>
    private static readonly Dictionary<string, IReadOnlyList<BusEnd>> Ending = new(StringComparer.Ordinal)
    {
        [CheckStarted.Kind] =
        [
            BusEnd.OnEffect(CheckPassed), BusEnd.OnEffect(CheckFailed), BusEnd.OnEffect(CheckUnknown), BusEnd.OnEffect(CheckRequeued),
            BusEnd.OnEffect(CheckEnded),
        ],

        // The step settling on a check is on no fact: only its subscription acknowledges it.
        [CheckPassed.Kind] = [BusEnd.Acknowledged],
        [CheckFailed.Kind] = [BusEnd.OnSubject(CheckStarted), BusEnd.Acknowledged],
        [CheckUnknown.Kind] = [BusEnd.OnSubject(CheckStarted), BusEnd.Acknowledged],
        [CheckRequeued.Kind] = [BusEnd.OnEffect(CheckStarted)],
        [CheckEnded.Kind] = [BusEnd.OnEffect(CheckStarted)],
        [OpinionOwed.Kind] = [BusEnd.Acknowledged],
        [OpinionAsked.Kind] = [BusEnd.OnSubject(OpinionGiven)],
        [OpinionGiven.Kind] = [BusEnd.OnSubject(OpinionDelivered), BusEnd.OnSubject(OpinionAnswered), BusEnd.Acknowledged],
        [OpinionDelivered.Kind] = [BusEnd.OnSubject(OpinionAnswered)],
        [OpinionAnswered.Kind] = [BusEnd.Acknowledged],
        [OpinionSaid.Kind] = [BusEnd.Acknowledged],
        [QuestMoved.Kind] = [BusEnd.Acknowledged],
        [NodeBegun.Kind] = [BusEnd.Acknowledged],
        [ChoicesWritten.Kind] = [BusEnd.Acknowledged],
        [SessionTurnEnded.Kind] = [BusEnd.Acknowledged],

        // The run's step goes on at the fact: the consumer's acknowledgement, written once its pass judged the run, is what ends each.
        [StepSettled.Kind] = [BusEnd.Acknowledged],
        [StepTurnEnded.Kind] = [BusEnd.Acknowledged],
        [PolicyPassed.Kind] = [BusEnd.Acknowledged],
        [LandOutcome.Kind] = [BusEnd.Acknowledged],
        [RunBound.Kind] = [BusEnd.Acknowledged],
    };

    private static readonly Dictionary<string, BusType> ByKind = All.ToDictionary(type => type.Kind, StringComparer.Ordinal);

    private static readonly Dictionary<string, IReadOnlyList<(BusType Armed, string By)>> Ended = Ending
        .SelectMany(pair => pair.Value.Where(end => end.Type is not null).Select(end => (End: end.Type!.Kind, Armed: ByKind[pair.Key], end.By)))
        .GroupBy(row => row.End, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => (IReadOnlyList<(BusType, string)>)[.. group.Select(row => (row.Armed, row.By))], StringComparer.Ordinal);

    /// <summary>The type of <paramref name="kind"/> (<c>name/version</c>), or null where none is declared.</summary>
    public static BusType? Of(string kind) => ByKind.GetValueOrDefault(kind);

    /// <summary>Whether its entries oblige someone, so each arms a timer: every give-up but <c>dropped</c>.</summary>
    public static bool Obliges(BusType type) => type.GiveUp != BusGiveUps.Dropped;

    public static IReadOnlyList<BusEnd> Ends(BusType type) => Ending.GetValueOrDefault(type.Kind) ?? [];

    /// <summary>The obliged types a fact of <paramref name="kind"/> ends, each with how it matches them.</summary>
    public static IReadOnlyList<(BusType Armed, string By)> EndedBy(string kind) => Ended.GetValueOrDefault(kind) ?? [];

    /// <summary>Whether <paramref name="type"/> is the one declared here, not a look-alike made elsewhere.</summary>
    public static bool Declares(BusType type) => Of(type.Kind) is { } declared && ReferenceEquals(declared, type);

    /// <summary>A consumer's subscription to <paramref name="kinds"/>, each a declared type, checked as it registers.</summary>
    /// <exception cref="BusTypeUnknownException">A kind no type here declares.</exception>
    public static BusSubscription Subscribe(string consumer, IReadOnlyList<string> kinds)
    {
        if (string.IsNullOrEmpty(consumer) || !BusShapes.Short(consumer)) throw new ArgumentException($"`{consumer}` is no consumer's name.", nameof(consumer));
        var unknown = kinds.Where(kind => Of(kind) is null).ToList();
        if (unknown.Count > 0) throw new BusTypeUnknownException(consumer, unknown);
        return new BusSubscription(consumer, [.. kinds.Distinct(StringComparer.Ordinal).Select(kind => Of(kind)!)]);
    }
}

/// <summary>A consumer's named subscription: the declared types it reads; its cursor and acknowledgements are the queue's (<see cref="BusQueue"/>).</summary>
public sealed record BusSubscription(string Consumer, IReadOnlyList<BusType> Types);

/// <summary>A subscription named a type no registry entry declares, so it never registers.</summary>
public sealed class BusTypeUnknownException(string consumer, IReadOnlyList<string> kinds)
    : Exception($"`{consumer}` subscribes to {string.Join(", ", kinds.Select(kind => $"`{kind}`"))}, which the bus's registry does not declare.")
{
    public IReadOnlyList<string> Kinds { get; } = kinds;
}
