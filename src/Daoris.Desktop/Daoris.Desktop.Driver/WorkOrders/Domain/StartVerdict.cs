namespace Daoris.Driver;

public enum StartVerdict
{
    /// <summary>Start a session for this quest, now.</summary>
    Start,

    /// <summary>The receiver has not opted into driving on this machine — the person's choice.</summary>
    NotDrivable,

    /// <summary>The person holds this repository: drivable, and nothing new starts there until they resume it.</summary>
    Held,

    /// <summary>
    /// Nothing here could answer it: the receiver is not registered on this machine, or it has not
    /// adopted and either has no root or would be carried by the pipe door, which has no connector to
    /// hand it. The reason says which.
    /// </summary>
    NotAdopted,

    /// <summary>No filesystem root is known — nowhere to spawn. `connect` from the repository fixes it.</summary>
    NoRoot,

    /// <summary>An active session (or an older quest this tick) holds the repository, or the quest itself.</summary>
    RepositoryBusy,

    /// <summary>The concurrency cap is spent.</summary>
    AtCapacity,

    /// <summary>
    /// Enough sessions have failed on this quest that trying again is spending an account rather than
    /// making progress. The person restarts it deliberately.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Planned to start, and held at SPAWN by what only the spawn can see — a dirty tree, an absent
    /// or logged-out harness, a tree that would not grow, a trust flag never given. The reason is the
    /// hold's own sentence, which names the fix. Never produced by the planner: it is what a tick
    /// reports when what happened differs from what the plan decided.
    /// </summary>
    Blocked,

    /// <summary>
    /// Taken, and waiting on a question asked of another repository. It resumes, in the same
    /// tree, once that quest is answered — nothing for the person to do.
    /// </summary>
    Waiting,

    /// <summary>
    /// Held by the person's stop: its last session here ended because the person stopped it, open or taken, and nothing starts it
    /// on this machine until they release that stop with *Try again* (<see cref="DriverConfig.Released"/>). A stop is not a strike,
    /// and another machine may still take an open one.
    /// </summary>
    Stopped,

    /// <summary>
    /// In the work of an ask or a quest the person paused on this machine: nothing of it starts here until *Resume*, which releases
    /// the stops the pause made. It sits before every other reason, a person's stop included, so a release of a stop starts nothing
    /// while it holds. A pause is this machine's: another machine may still take an open one, and a taken one stays taken here.
    /// </summary>
    Paused,

    /// <summary>
    /// Taken, and the take is not this machine's: another machine's, by the quest's log, or one made here after this machine's last
    /// session on it ended, by a chat or by work outside Daoris. The ledger refuses a carry-on over it, so the quest sits with the
    /// ledger's own sentence and nothing is asked. No strike: nothing ran.
    /// </summary>
    TakenElsewhere,

    /// <summary>
    /// A set-up step this machine cannot honestly start: no review rule here names its environment, or a local one has no address
    /// or no window here to show the work in (a headless loop). The reason names the door that would let it start. No strike.
    /// </summary>
    CannotShow,

    /// <summary>
    /// Waits for a second opinion: a set-up step while an agent is still at work on the opinion of the work it shows, or, under
    /// <c>steps</c>, a chain's next step while the opinion on the step before is unsettled. No strike: nothing ran.
    /// </summary>
    WaitsForOpinion,

    /// <summary>
    /// A chain's step whose source the person reopened (<see cref="QuestView.HeldByReopen"/>): withdrawn while open, held while
    /// taken, and nothing starts it, carries it on or goes on with words written to its session until the person chooses to go on
    /// with it on the new work or to abandon it. Daoris never chooses. No strike: nothing ran.
    /// </summary>
    SourceReopened,

    /// <summary>
    /// Held for the person: its last session here ended past its transient bound, <c>ended.transient</c>, read by that code. Nothing
    /// carries it on, starts it afresh or resumes it until a word of the person's said after that end, or a hand-over from that
    /// record. No strike and no slot.
    /// </summary>
    TransientHeld,

    /// <summary>
    /// Held for the person by its role's policy: the policy asks before it moves the line to another choice, or the line made the
    /// policy's bound of moves in 24 hours. <i>Try again</i> is the yes: one approval releases one move.
    /// </summary>
    PolicyHeld,

    /// <summary>Taken, its session finished and nothing carrying it on (<see cref="UnclosedQuests"/>): it waits on the person's done or word.</summary>
    Unclosed,
}
