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

    /// <summary>The run's step that reads a check's result: it settles, sends a red back or holds.</summary>
    public const string Step = "driver/step";

    /// <summary>The opinion's gate, which judges the answers to a pass's findings.</summary>
    public const string OpinionGate = "driver/opinion-gate";

    /// <summary>The reviewer's door the envelope's answerer names.</summary>
    public const string Reviewer = "reviewer";

    /// <summary>The working session the payload's <c>session</c> names.</summary>
    public const string Session = "session";

    /// <summary>The watchdog's stalls and give-ups, and the Stalls view that shows them.</summary>
    public const string Watchdog = "driver/watchdog", Stalls = "driver/stalls";

    /// <summary>Where the service's ledger is heard (its change stream), the node runtime's records, and this process's standing choices.</summary>
    public const string Ledger = "driver/ledger", Nodes = "driver/nodes", Choices = "driver/choices";

    private const string Unmeasured = "no handling time measured yet: the install's `daoris bus stats` at cddcdac313 (2026-11-21) holds one check start, no settle and no pass, its quests paused";

    private const string AtALook =
        Unmeasured + "; its handler acts at a look, and the looks ran about once a minute there (each stage n=1356 that day), so ten minutes is ten looks";

    private static readonly IReadOnlyList<BusField> Settled =
    [
        new("invocation", BusShapes.Id), new("commit", BusShapes.Id), new("verdict", BusShapes.Code), new("code", BusShapes.Code),
        new("failed", BusShapes.Code), new("red", BusShapes.Ids), new("redCount", BusShapes.Count), new("gates", BusShapes.Count),
    ];

    private static readonly BusDeadline StepLook = BusDeadline.Stated(TimeSpan.FromMinutes(10), AtALook);

    /// <summary>A check's attempt began, both its slots held: its executor must settle it.</summary>
    public static readonly BusType CheckStarted = new(
        "check.started", 1, BusClasses.Fact, Checks, Checks,
        BusDeadline.Stated(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15),
            Unmeasured + "; a check runs under its node's bound, two hours where it declares none, past which its executor settles it unknown, and a quarter hour more for that settle"),
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
        BusDeadline.Stated(TimeSpan.FromMinutes(OpinionKey.DefaultMinutes + 10),
            Unmeasured + "; a pass runs under its rule's bound, twenty minutes where the rule names none (5 to 120), and ten more are its answer's landing; a rule's longer bound outlasts it, which the shadow watchdog counts"),
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

    /// <summary>The person's word at a session's gate (<i>Go on anyway</i>, <i>I looked myself</i>, an Accept's answer): the opinion's gate must judge that session's owed opinions.</summary>
    public static readonly BusType OpinionSaid = new(
        "opinion.said", 1, BusClasses.Fact, Opinions, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("session", BusShapes.Id), new("said", BusShapes.Code), new("door", BusShapes.Code)]);

    /// <summary>The service's quests moved, as its change stream named them (a hold lifted, a step declined): the opinion's gate must judge every owed opinion.</summary>
    public static readonly BusType QuestMoved = new(
        "quest.moved", 1, BusClasses.Fact, Ledger, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("families", BusShapes.Ids)]);

    /// <summary>A run's node record began nodes (left blocked or ready): the opinion's gate must judge every owed opinion, which a graph's opinion node holds until begun.</summary>
    public static readonly BusType NodeBegun = new(
        "node.begun", 1, BusClasses.Fact, Nodes, OpinionGate, BusDeadline.Stated(TimeSpan.FromMinutes(10), AtTheGate), 2, BusGiveUps.Held,
        [new("nodes", BusShapes.Ids)]);

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

    /// <summary>Every type, each once.</summary>
    public static readonly IReadOnlyList<BusType> All =
    [
        OpinionOwed, OpinionAsked, OpinionGiven, OpinionDelivered, OpinionAnswered, OpinionSaid, QuestMoved, NodeBegun, ChoicesWritten, CheckStarted, CheckPassed,
        CheckFailed, CheckUnknown, CheckRequeued, CheckEnded, Stalled, GiveUp,
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
