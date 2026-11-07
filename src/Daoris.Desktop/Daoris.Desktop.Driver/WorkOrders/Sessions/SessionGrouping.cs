namespace Daoris.Driver;

/// <summary>Where one session is listed, and what its row's second line says.</summary>
/// <param name="Group">One of <see cref="SessionGroup"/>.</param>
/// <param name="Shown">The record's state, or one of <see cref="ShownState"/>.</param>
public sealed record SessionGrouping(string Session, string Group, string Shown) : IWorkplaceSessionRow
{
    /// <summary>Whether this machine's archive mark stands. A session that needs the person is shown in its group whatever the mark says.</summary>
    public bool Archived { get; init; }

    /// <summary>A teammate's record: read here, and acted on from its own machine.</summary>
    public bool Teammate { get; init; }

    /// <summary>
    /// For a parked session: how many sessions failed since the quest's last *Try again*, as the planner counted to park it. For a
    /// live one of this machine's: how many failed before it, the same count, null where none did.
    /// </summary>
    public int? Strikes { get; init; }

    /// <summary>For a session awaiting reply: the quest its quest waits on.</summary>
    public string? Awaits { get; init; }

    /// <summary>And the repository that quest is addressed to, where the service still lists it.</summary>
    public string? AwaitsOf { get; init; }

    /// <summary>For a session to review: what its own tree holds.</summary>
    public TreeWork? Work { get; init; }

    /// <summary>
    /// What its own tree offers to land, in whichever group it rests bar Working: an ended session of this machine's, the newest on
    /// its tree, nothing live there, with commits no branch of the person's holds. Null otherwise. Its page offers Accept beside it,
    /// and its review does, whatever its ending.
    /// </summary>
    public LandOffer? Lands { get; init; }

    /// <summary>
    /// What its own tree offers where its commits are held by content, in the groups <see cref="Lands"/> is offered in: no landing,
    /// where its work is, and the review's Discard, unforced. Null otherwise; never beside <see cref="Lands"/>.
    /// </summary>
    public DiscardOffer? Discards { get; init; }

    /// <summary>
    /// Whether this session holds its quest here for the person: the person stopped it, or it ended past its transient bound; it is
    /// its quest's last session here, and nothing starts that quest on this machine until they choose *Try again*.
    /// </summary>
    public bool HoldsQuest { get; init; }

    /// <summary>
    /// Whose pause holds this session's quest, where it is its quest's last session here: its line says *paused with ask `#a`;
    /// Resume carries it on*, and *Resume* stands where *Try again* would. Null otherwise.
    /// </summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For an ended record the person's words wait on that resumes later: what holds them, which its line says. Null otherwise, and
    /// for one that goes on (<see cref="ShownState.GoingOn"/>).
    /// </summary>
    public WordsHold? Holds { get; init; }

    /// <summary>
    /// This row held by <paramref name="holds"/>: where its words wait for an account's reset it is <see cref="ShownState.LimitWaits"/>
    /// in place of the state it ended in, whatever it is; a record a limit ended keeps its own <see cref="ShownState.Limited"/>,
    /// which says it already.
    /// </summary>
    public SessionGrouping HeldBy(WordsHold holds) => this with
    {
        Holds = holds,
        Shown = holds is { Why: WordsHold.Cooling, Until: not null } && Shown != ShownState.Limited ? ShownState.LimitWaits : Shown,
    };

    /// <summary>Whether *Delete…* would be taken: the ledger would delete its record, and this machine holds neither its tree nor a landing of it. The page offers the act only here.</summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// For a working session of this machine's: the call its agent is inside, as this process heard it (<see cref="RunningCalls"/>),
    /// which its row and head say as <i>running `npm test` for 8 min</i>. Null otherwise, and where nothing heard it: the terminal's
    /// listing, which runs no session, never has one.
    /// </summary>
    public RunningCall? Calling { get; init; }
}
