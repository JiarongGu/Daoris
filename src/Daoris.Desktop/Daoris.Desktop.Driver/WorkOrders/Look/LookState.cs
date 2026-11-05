namespace Daoris.Driver;

/// <summary>
/// What one look carries from pass to pass, handed through its <see cref="LookRound"/>: the snapshot the plan reads, as each pass
/// before the plan shapes it, and what a pass marks or concludes for the passes after it. A pass reads only what a pass it runs
/// after wrote, so the kit's order is the order these are written in.
/// </summary>
/// <remarks>
/// A member a pass writes is unwritten until that pass runs, and reading it earlier throws, naming the pass that writes it: a pass
/// declared without the <c>after</c> its reads need fails at its first look, never on a value left over or a default.
/// </remarks>
/// <param name="concluded">What ended, this look's report's structural half: shared with the look that reports it when it fails.</param>
internal sealed class LookState(List<SessionEnded> concluded)
{
    private Snapshot? _snapshot;
    private long? _mark;
    private IReadOnlyList<StartReservation>? _reservedBefore;
    private AnswersAloneHold? _alone;
    private Snapshot? _looked;
    private int? _passes;
    private IReadOnlyList<Consideration>? _plan;
    private StartsOutcome? _outcome;
    private IReadOnlyList<Consideration>? _starts;
    private IReadOnlyList<AskView>? _intakes;
    private IReadOnlyList<IJudgedOpinion>? _judgedOpinions;

    /// <summary>
    /// The quests, sessions and records the plan reads: read by the <c>snapshot</c> pass, and set again by each pass that shapes it,
    /// so a pass reads it as the passes before it left it.
    /// </summary>
    public Snapshot Snapshot
    {
        get => _snapshot ?? throw Unwritten(nameof(Snapshot), "snapshot");
        set => _snapshot = value;
    }

    /// <summary>
    /// The roster's number of the last start chosen, taken before the snapshot is read, so the goal's walk keeps every start chosen
    /// while the records were read.
    /// </summary>
    public long Mark
    {
        get => _mark ?? throw Unwritten(nameof(Mark), "snapshot");
        set => _mark = value;
    }

    /// <summary>
    /// The starts still opening when the snapshot was read: the plan holds these as well as the ones opening when it plans, so a start
    /// whose record opened while the snapshot was read is held by one of the two.
    /// </summary>
    public IReadOnlyList<StartReservation> ReservedBefore
    {
        get => _reservedBefore ?? throw Unwritten(nameof(ReservedBefore), "snapshot");
        set => _reservedBefore = value;
    }

    /// <summary>The look's answers-alone bound, its rules read once: the supervisor's release asks it, and the look holds by it.</summary>
    public AnswersAloneHold Alone
    {
        get => _alone ?? throw Unwritten(nameof(Alone), "supervisor");
        set => _alone = value;
    }

    /// <summary>The snapshot as the roster pass left it, which the channel, the reopens, the reviews and the opinions read.</summary>
    public Snapshot Looked
    {
        get => _looked ?? throw Unwritten(nameof(Looked), "roster");
        set => _looked = value;
    }

    /// <summary>The slots the look's passes started, counted under the cap: the opinions' write it, and each pass after them adds.</summary>
    public int Passes
    {
        get => _passes ?? throw Unwritten(nameof(Passes), "opinions");
        set => _passes = value;
    }

    /// <summary>What the second opinions judged this look, for its report.</summary>
    public IReadOnlyList<IJudgedOpinion> JudgedOpinions
    {
        get => _judgedOpinions ?? throw Unwritten(nameof(JudgedOpinions), "opinions");
        set => _judgedOpinions = value;
    }

    /// <summary>Every consideration the plan made, the starts among them: written by the plan pass.</summary>
    public IReadOnlyList<Consideration> Plan
    {
        get => _plan ?? throw Unwritten(nameof(Plan), "plan");
        set => _plan = value;
    }

    /// <summary>What the starts came to, for the look's report: written by the start pass.</summary>
    public StartsOutcome Outcome
    {
        get => _outcome ?? throw Unwritten(nameof(Outcome), "start");
        set => _outcome = value;
    }

    /// <summary>The starts the plan chose, each with its line of work on an ask: written by the plan pass, before the Planned phase.</summary>
    public IReadOnlyList<Consideration> Starts
    {
        get => _starts ?? throw Unwritten(nameof(Starts), "plan");
        set => _starts = value;
    }

    /// <summary>The asks an intake is begun for, in the slots the quests left.</summary>
    public IReadOnlyList<AskView> Intakes
    {
        get => _intakes ?? throw Unwritten(nameof(Intakes), "intakes");
        set => _intakes = value;
    }

    /// <summary>What ended this look: a parked intake it ends, and every session that ended since the last look.</summary>
    public List<SessionEnded> Concluded { get; } = concluded;

    private static InvalidOperationException Unwritten(string member, string pass) =>
        new($"the look's {member} is written by the {pass} pass, which has not run before the pass reading it: that pass runs after {pass}");
}
