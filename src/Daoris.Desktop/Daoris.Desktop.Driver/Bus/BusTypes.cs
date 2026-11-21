namespace Daoris.Driver;

/// <summary>
/// The one registry of the bus's types: each declared once here with its payload's fields, its emitter, its obliged handler
/// or answerer, its deadline, its retries and its give-up. Checked at emit (<see cref="BusLog.Append"/> refuses a type not here and drops a
/// field or value it does not declare) and at subscribe (<see cref="Subscribe"/>); <c>BusTypesCensusTests</c> reads it, and the page's
/// list is written from it (<c>tools/contracts/PageBusTypes.cs</c>).
/// </summary>
/// <remarks>
/// <para><b>Deadlines.</b> A deadline is set from the handling times <c>daoris bus stats</c> reads where they are measured; where none are,
/// it is stated provisional with what it stands on, and the watchdog runs against it in shadow before any give-up acts.</para>
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

    /// <summary>A pass given, its findings counted by weight: the deliverer must hand them to the working session.</summary>
    public static readonly BusType OpinionGiven = new(
        "opinion.given", 1, BusClasses.Fact, Opinions, Opinions, StepLook, 2, BusGiveUps.FinalExit,
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
        "opinion.answered", 1, BusClasses.Fact, Opinions, OpinionGate, StepLook, 2, BusGiveUps.Held,
        [new("fixed", BusShapes.Count), new("rejected", BusShapes.Count), new("unresolved", BusShapes.Count), new("notAnswered", BusShapes.Count)]);

    /// <summary>Every type, each once.</summary>
    public static readonly IReadOnlyList<BusType> All =
    [
        OpinionAsked, OpinionGiven, OpinionDelivered, OpinionAnswered, CheckStarted, CheckPassed, CheckFailed, CheckUnknown, CheckRequeued, CheckEnded,
    ];

    private static readonly Dictionary<string, BusType> ByKind = All.ToDictionary(type => type.Kind, StringComparer.Ordinal);

    /// <summary>The type of <paramref name="kind"/> (<c>name/version</c>), or null where none is declared.</summary>
    public static BusType? Of(string kind) => ByKind.GetValueOrDefault(kind);

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

/// <summary>A consumer's named subscription: the declared types it reads; its cursor and acknowledgement come with the first consumer.</summary>
public sealed record BusSubscription(string Consumer, IReadOnlyList<BusType> Types);

/// <summary>A subscription named a type no registry entry declares, so it never registers.</summary>
public sealed class BusTypeUnknownException(string consumer, IReadOnlyList<string> kinds)
    : Exception($"`{consumer}` subscribes to {string.Join(", ", kinds.Select(kind => $"`{kind}`"))}, which the bus's registry does not declare.")
{
    public IReadOnlyList<string> Kinds { get; } = kinds;
}
