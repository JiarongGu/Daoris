namespace Daoris.Knowledge;

/// <summary>The four states a session folds to: the list's groups are filters over them, and archived is a shelf.</summary>
public enum FoldedState
{
    /// <summary>A live lease: its process runs in this start of the application.</summary>
    Working,

    /// <summary>A named holder: the machine room's start, its run's step, its own check.</summary>
    Waiting,

    /// <summary>An open question for a person, or a breach of the invariant, said with its facts.</summary>
    NeedsYou,

    /// <summary>Ended, with its outcome.</summary>
    Ended,
}

/// <summary>
/// A process that works on a session, as its marker proves it alive, named by the application's generation it started in
/// (<see cref="AppGenerations.Of"/>): a lease of an earlier generation survived a restart and is void, never adopted.
/// </summary>
/// <param name="Session">The session its process works on.</param>
/// <param name="Generation">The generation in which its process started; 0 before any generation.</param>
public sealed record SessionLease(string Session, long Generation);

/// <summary>What the fold says of one session: its state, the outcome of an end, its holder, since when, and the invariant's breach.</summary>
/// <param name="State">One of the four.</param>
/// <param name="Outcome">How it ended, for <see cref="FoldedState.Ended"/>.</param>
/// <param name="Holder">What holds it: <c>lease</c>, <c>person</c>, or the wait's named holder; null for an end or a breach that names none.</param>
/// <param name="Since">When it came to this state: the fact that put it there.</param>
/// <param name="Breach">
/// Why a session not ended has not exactly one of a live lease, a named holder and an open question, in words of the facts; null where
/// the invariant holds. A breach is <see cref="FoldedState.NeedsYou"/>, never working.
/// </param>
public sealed record SessionFolded(FoldedState State, SessionOutcome? Outcome, string? Holder, DateTimeOffset Since, string? Breach);

/// <summary>
/// The fold: one pure function of a session's journal, the clock and the live leases, giving one of four states, its holder
/// and since when, and auditing the invariant (a session not ended has exactly one of a live lease, a named holder, an open question; a
/// breach is <see cref="FoldedState.NeedsYou"/> with its facts). Reads no store and keeps nothing.
/// </summary>
public static class SessionFold
{
    /// <summary>The holder a live lease names.</summary>
    public const string LeaseHolder = "lease";

    /// <summary>The holder an open question names.</summary>
    public const string PersonHolder = "person";

    /// <summary>
    /// The fold of <paramref name="journal"/> (one session's facts, in journal order) at <paramref name="now"/>, against the leases
    /// proved alive for it, in the application's <paramref name="generation"/>; null for an empty journal, which witnessed nothing.
    /// </summary>
    /// <remarks>
    /// The clock bounds nothing yet (stillness is part c): it only keeps <see cref="SessionFolded.Since"/> from reading after it, as a
    /// fact written on a clock ahead of the reader's would.
    /// </remarks>
    public static SessionFolded? Of(
        IReadOnlyList<SessionFact> journal, DateTimeOffset now, IReadOnlyList<SessionLease> leases, long generation)
    {
        if (journal.Count == 0) return null;
        var last = journal[^1];
        var since = last.At > now ? now : last.At;
        if (last.Kind == SessionFactKind.Ended) return new(FoldedState.Ended, last.Outcome, null, since, null);

        var live = leases.Any(lease => lease.Generation == generation);
        var voided = leases.Where(lease => lease.Generation != generation).Select(lease => lease.Generation).Distinct().ToList();
        return last.Kind switch
        {
            SessionFactKind.TurnBegan => live
                ? new(FoldedState.Working, null, LeaseHolder, since, null)
                : Breach(since, voided.Count > 0
                    ? $"its turn began and its only lease is of generation {string.Join(", ", voided)}, before this start's {generation}"
                    : "its turn began and no process of it is alive"),
            SessionFactKind.QuestionOpened => live
                ? Breach(since, "a question stands open while a process of it is alive", PersonHolder)
                : new(FoldedState.NeedsYou, null, PersonHolder, since, null),
            // A start's holder is released once its process runs: the lease is then what holds it, until its turn's fact comes.
            SessionFactKind.Opened or SessionFactKind.Starting => live
                ? new(FoldedState.Working, null, LeaseHolder, since, null)
                : new(FoldedState.Waiting, null, last.Holder ?? SessionFact.StartHolder, since, null),
            _ => last.Holder is null
                ? Breach(since, "it awaits a step and names nothing that holds it")
                : live
                    ? Breach(since, $"it awaits {last.Holder} while a process of it is alive", last.Holder)
                    : new(FoldedState.Waiting, null, last.Holder, since, null),
        };
    }

    /// <summary>The state a stored status reads as, with its outcome: what the fold is compared with while it runs in shadow.</summary>
    public static (FoldedState State, SessionOutcome? Outcome) OfStored(SessionState stored) => stored switch
    {
        SessionState.Working => (FoldedState.Working, null),
        SessionState.AwaitingPerson => (FoldedState.NeedsYou, null),
        SessionState.Queued or SessionState.Starting or SessionState.AwaitingStep => (FoldedState.Waiting, null),
        _ => (FoldedState.Ended, SessionFact.OutcomeOf(stored)),
    };

    /// <summary>Whether the fold says other than the stored status: another state, or another outcome of an end.</summary>
    public static bool Differs(SessionFolded folded, SessionState stored) =>
        OfStored(stored) is var (state, outcome) && (folded.State != state || folded.Outcome != outcome);

    /// <summary>The state's spelling on the wire: <c>working</c>, <c>waiting</c>, <c>needs-you</c>, <c>ended</c>.</summary>
    public static string Spell(FoldedState state) => state switch
    {
        FoldedState.Working => "working",
        FoldedState.Waiting => "waiting",
        FoldedState.NeedsYou => "needs-you",
        _ => "ended",
    };

    /// <summary>An outcome's spelling on the wire: <c>done</c>, <c>declined</c>, <c>stood-down</c>, <c>failed</c>, <c>stopped</c>.</summary>
    public static string Spell(SessionOutcome outcome) => outcome switch
    {
        SessionOutcome.Done => "done",
        SessionOutcome.Declined => "declined",
        SessionOutcome.StoodDown => "stood-down",
        SessionOutcome.Failed => "failed",
        _ => "stopped",
    };

    private static SessionFolded Breach(DateTimeOffset since, string why, string? holder = null) =>
        new(FoldedState.NeedsYou, null, holder, since, why);
}
