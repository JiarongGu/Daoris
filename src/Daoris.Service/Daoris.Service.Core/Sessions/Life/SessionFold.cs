namespace Daoris.Knowledge;

/// <summary>The four states a session folds to: the list's groups are filters over them, and archived is a shelf.</summary>
public enum FoldedState
{
    /// <summary>A live lease: a process of it runs, owned by a driver that runs.</summary>
    Working,

    /// <summary>A named holder (<see cref="HolderKind"/>).</summary>
    Waiting,

    /// <summary>An open question for a person or the steward, or a breach of the invariant, said with its facts.</summary>
    NeedsYou,

    /// <summary>Ended, with its outcome.</summary>
    Ended,
}

/// <summary>
/// Why a session not ended has not exactly one of a live lease, a named holder and an open question: a closed set of codes, which the
/// window's catalogue words. A breach is <see cref="FoldedState.NeedsYou"/>, never working.
/// </summary>
public enum BreachCode
{
    /// <summary>Its turn began and no process of it is alive.</summary>
    TurnWithoutLease,

    /// <summary>Its turn began and its only process outlived the driver that owned it: a void lease, never adopted.</summary>
    TurnOnVoidLease,

    /// <summary>A question stands open while a process of it is alive.</summary>
    QuestionWithLease,

    /// <summary>It waits and names nothing that holds it.</summary>
    WaitWithoutHolder,

    /// <summary>It waits on a holder while a process of it is alive.</summary>
    WaitWithLease,
}

/// <summary>What a session's processes amount to: none, only void ones (their owner gone), or a live one.</summary>
public enum Leasing
{
    None,
    Void,
    Live,
}

/// <summary>
/// A process that works on a session, as its marker proves it alive: the generation it started in, and whether the driver that owns
/// it still runs. It is live only while its owner runs, so a process that outlived its driver's restart is void and never adopted, and one
/// a running terminal's driver owns is never voided by another driver's start.
/// </summary>
public sealed record SessionLease(string Session, long Generation, bool OwnerRuns);

/// <summary>What the fold says of one session: its state, the outcome of an end, its holder, since when, and the invariant's breach.</summary>
/// <param name="State">One of the four.</param>
/// <param name="Outcome">How it ended, for <see cref="FoldedState.Ended"/>.</param>
/// <param name="Holder">What holds it, where it waits or a question stands; null for working, an end, or a breach that names none.</param>
/// <param name="Since">When it came to this state: the fact that put it there.</param>
/// <param name="Breach">The invariant's breach; null where it holds.</param>
public sealed record SessionFolded(FoldedState State, SessionOutcome? Outcome, SessionHolder? Holder, DateTimeOffset Since, BreachCode? Breach);

/// <summary>
/// The fold: one pure function of a session's last fact, the clock and the live leases, giving one of four states, its holder and since
/// when, and auditing the invariant (a session not ended has exactly one of a live lease, a named holder, an open question; a breach is
/// <see cref="FoldedState.NeedsYou"/> with its facts). Its reading of a fact (<see cref="Reading"/>) is the one classification the time
/// buckets read too (<see cref="SessionBuckets"/>). Reads no store and keeps nothing.
/// </summary>
public static class SessionFold
{
    /// <summary>
    /// The fold of a session whose journal ends in <paramref name="last"/>, at <paramref name="now"/>, against the leases proved alive
    /// for it; null for no fact, which witnessed nothing.
    /// </summary>
    /// <remarks>
    /// The clock bounds nothing yet (stillness is part c): it only keeps <see cref="SessionFolded.Since"/> from reading after it, as a
    /// fact written on a clock ahead of the reader's would.
    /// </remarks>
    public static SessionFolded? Of(SessionFact? last, DateTimeOffset now, IReadOnlyList<SessionLease> leases)
    {
        if (last is null) return null;
        var (state, holder, breach) = Reading(last, LeasingOf(leases));
        return new(state, state == FoldedState.Ended ? last.Outcome : null, holder, last.At > now ? now : last.At, breach);
    }

    /// <summary>The fold of a whole journal: of its last fact.</summary>
    public static SessionFolded? Of(IReadOnlyList<SessionFact> journal, DateTimeOffset now, IReadOnlyList<SessionLease> leases) =>
        Of(journal.Count == 0 ? null : journal[^1], now, leases);

    /// <summary>A live lease where any owner runs, void ones alone where none does, else none.</summary>
    public static Leasing LeasingOf(IReadOnlyList<SessionLease> leases) =>
        leases.Any(lease => lease.OwnerRuns) ? Leasing.Live : leases.Count > 0 ? Leasing.Void : Leasing.None;

    /// <summary>
    /// What a session's last fact says against its leasing: its state, what holds it, and a breach. A start's holder is released once
    /// its process runs, the lease then holding it until its turn's fact comes; an end is ended whatever runs.
    /// </summary>
    public static (FoldedState State, SessionHolder? Holder, BreachCode? Breach) Reading(SessionFact last, Leasing leasing) => last.Kind switch
    {
        SessionFactKind.Ended => (FoldedState.Ended, null, null),
        SessionFactKind.TurnBegan => leasing switch
        {
            Leasing.Live => (FoldedState.Working, null, null),
            Leasing.Void => (FoldedState.NeedsYou, null, BreachCode.TurnOnVoidLease),
            _ => (FoldedState.NeedsYou, null, BreachCode.TurnWithoutLease),
        },
        SessionFactKind.QuestionOpened => leasing == Leasing.Live
            ? (FoldedState.NeedsYou, Named(last.Holder, SessionHolder.Person), BreachCode.QuestionWithLease)
            : (FoldedState.NeedsYou, Named(last.Holder, SessionHolder.Person), null),
        SessionFactKind.Opened or SessionFactKind.Starting => leasing == Leasing.Live
            ? (FoldedState.Working, null, null)
            : (FoldedState.Waiting, Named(last.Holder, SessionHolder.MachineRoom), null),
        _ => last.Holder.Kind == HolderKind.None
            ? (FoldedState.NeedsYou, null, BreachCode.WaitWithoutHolder)
            : leasing == Leasing.Live
                ? (FoldedState.NeedsYou, last.Holder, BreachCode.WaitWithLease)
                : (FoldedState.Waiting, last.Holder, null),
    };

    /// <summary>The state a stored status reads as, with its outcome: what the fold is compared with while it runs in shadow.</summary>
    public static (FoldedState State, SessionOutcome? Outcome) OfStored(SessionState stored) => stored switch
    {
        SessionState.Working => (FoldedState.Working, null),
        SessionState.AwaitingPerson => (FoldedState.NeedsYou, null),
        SessionState.Queued or SessionState.Starting or SessionState.AwaitingStep => (FoldedState.Waiting, null),
        _ => (FoldedState.Ended, SessionFact.OutcomeOf(stored)),
    };

    /// <summary>
    /// Whether the fold says other than the stored status: another state, or another outcome of an end. A stored start whose process
    /// already runs folds working, which is the start under way, not a difference.
    /// </summary>
    public static bool Differs(SessionFolded folded, SessionState stored) =>
        !(stored == SessionState.Starting && folded is { State: FoldedState.Working, Breach: null })
        && OfStored(stored) is var (state, outcome) && (folded.State != state || folded.Outcome != outcome);

    private static SessionHolder Named(SessionHolder holder, SessionHolder otherwise) => holder.Kind == HolderKind.None ? otherwise : holder;
}
