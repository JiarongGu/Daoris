namespace Daoris.Driver;

/// <summary>
/// A candidate of a line's integration run as the store keeps it (INTEGRATE1e part e2; the integration design §5.2, §5.6): the line it is
/// formed on and that line's commit then, the job its processes run in, the run that holds it, and, once its members are replayed, its tip.
/// One stands per line at a time; an ended one is kept as evidence.
/// </summary>
/// <param name="Base">The line's commit the candidate was formed on.</param>
/// <param name="Job">The name of the one job every process the candidate starts runs in (<see cref="IntegrationJobs"/>).</param>
/// <param name="ClaimRun">The process run that holds it, a row of <c>store_runs</c>.</param>
/// <param name="ClaimGeneration">Raised by each take, so a run that lost the claim writes nothing more.</param>
public sealed record IntegrationCandidate(
    string Id, string Repository, string Workspace, string Line, string Base, string Job, string ClaimRun, long ClaimGeneration, DateTimeOffset FormedAt)
{
    /// <summary>The candidate's tip once its members are replayed onto <see cref="Base"/>; null before.</summary>
    public string? Tip { get; init; }

    /// <summary>When its promotion was recorded: by the promotion step's end, or by ancestry at a resume; null before.</summary>
    public DateTimeOffset? PromotedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>One of <see cref="IntegrationCandidateEnds"/>, or a newer build's word, kept as written; null while it stands.</summary>
    public string? EndedWhy { get; init; }

    /// <summary>It stands for its line: no other candidate of the line is formed while it does.</summary>
    public bool Stands => EndedAt is null;
}

/// <summary>A request in a candidate (§5.2): its place in the candidate's order, what the steps have made of it, and its end.</summary>
/// <param name="State">One of <see cref="IntegrationMemberStates"/>, or a newer build's word, kept as written.</param>
public sealed record IntegrationMember(string Candidate, string Request, int Position, string State, DateTimeOffset JoinedAt)
{
    /// <summary>The member's replayed tip on the candidate (step 3); null before, and for a member left out.</summary>
    public string? Replayed { get; init; }

    /// <summary>The earlier member whose replay holds this one's work (step 3, provisional until that member promotes); null otherwise.</summary>
    public string? ContainedBy { get; init; }

    /// <summary>Why it is where it is, in words: what was handed back, what holds it; null where nothing was said.</summary>
    public string? Said { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>It stands in its candidate: the request is in no other while it does.</summary>
    public bool Stands => EndedAt is null;
}

/// <summary>
/// One attempt of a candidate's step (§5.6): its start, written before the step's effect, and its end, written after, each in one
/// <c>BEGIN IMMEDIATE</c> transaction under the candidate's claim. An attempt with a start and no end is what a resume judges: by ancestry
/// where the candidate's tip may be on the line, else run again from its start as a new attempt.
/// </summary>
/// <param name="Step">One of <see cref="IntegrationStepNames"/>, or a newer build's word, kept as written.</param>
/// <param name="Attempt">1 for the step's first start, one more for each start again from its beginning.</param>
/// <param name="ClaimRun">The run that began it.</param>
public sealed record IntegrationStep(
    long Id, string Candidate, string Step, int Attempt, string ClaimRun, long ClaimGeneration, DateTimeOffset BeganAt)
{
    /// <summary>The member a member's step is of (a replay, a settle); null for the candidate's own.</summary>
    public string? Request { get; init; }

    /// <summary>The line's commit the step read at its start, which its effect goes from; null where it read none.</summary>
    public string? LineCommit { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>One of <see cref="IntegrationStepOutcomes"/>, or a newer build's word; null while it is open.</summary>
    public string? Outcome { get; init; }

    public string? Said { get; init; }

    /// <summary>Begun and not ended: the candidate's one open step.</summary>
    public bool Open => EndedAt is null;
}

/// <summary>The candidate's claim, which every write of its steps, members and end is checked against inside its own transaction.</summary>
public sealed record IntegrationClaim(string Candidate, string Run, long Generation);

/// <summary>A candidate to form (§5.2 step 2): its line, that line's commit, and the requests it takes, in their order.</summary>
public sealed record IntegrationForming(string Repository, string Workspace, string Line, string Base, IReadOnlyList<string> Requests);

/// <summary>
/// What a form answered: the candidate made, with its claim and its form step begun; or the candidate that already stands for the line, with
/// no claim and nothing written.
/// </summary>
public sealed record IntegrationFormed(IntegrationCandidate Candidate, IntegrationClaim? Claim, IntegrationStep? Step)
{
    public bool Claimed => Claim is not null;
}

/// <summary>
/// What a step's end writes in its one transaction, under the claim: its outcome and words, and what the step made of the candidate, its
/// members and their requests.
/// </summary>
/// <param name="Outcome">One of <see cref="IntegrationStepOutcomes"/>.</param>
public sealed record IntegrationStepEnd(string Outcome)
{
    public string? Said { get; init; }

    /// <summary>The candidate's tip, once its members are replayed; null leaves it as it is.</summary>
    public string? Tip { get; init; }

    /// <summary>The candidate's promotion recorded with this end.</summary>
    public bool Promoted { get; init; }

    /// <summary>What the step made of its members, each a standing member of the candidate.</summary>
    public IReadOnlyList<IntegrationMemberChange> Members { get; init; } = [];

    /// <summary>The requests this end closes, each with its reason (<see cref="IntegrationRequestCloses"/>).</summary>
    public IReadOnlyList<IntegrationRequestClose> Closes { get; init; } = [];

    /// <summary>The candidate ended with this end, with its reason (<see cref="IntegrationCandidateEnds"/>), its standing members with it; null where it goes on.</summary>
    public string? Ends { get; init; }
}

/// <summary>A member's new state, with what it carries; <see cref="Ends"/> ends its membership, freeing its request to be formed again.</summary>
public sealed record IntegrationMemberChange(string Request, string State)
{
    public string? Replayed { get; init; }

    public string? ContainedBy { get; init; }

    public string? Said { get; init; }

    public bool Ends { get; init; }
}

/// <summary>A request closed by a step's end, with its reason.</summary>
public sealed record IntegrationRequestClose(string Request, string Why);

/// <summary>What a take answered: the candidate as it stands, and the new claim; or the reason it was refused (<see cref="IntegrationTakeRefusals"/>).</summary>
public sealed record IntegrationTaken(IntegrationCandidate? Candidate, IntegrationClaim? Claim, string? Refused);

/// <summary>A candidate's steps (§5.2), in their order; the request, step 1, is the door's and is kept as the request's own row.</summary>
public static class IntegrationStepNames
{
    public const string Form = "form";
    public const string Replay = "replay";
    public const string Resolve = "resolve";
    public const string Conflict = "conflict";
    public const string Gates = "gates";
    public const string Judge = "judge";
    public const string Promote = "promote";
    public const string Settle = "settle";
    public const string Tidy = "tidy";
    public const string Close = "close";

    public static IReadOnlyList<string> All { get; } = [Form, Replay, Resolve, Conflict, Gates, Judge, Promote, Settle, Tidy, Close];
}

/// <summary>How a step's attempt ended.</summary>
public static class IntegrationStepOutcomes
{
    /// <summary>Its effect is made and recorded.</summary>
    public const string Done = "done";

    /// <summary>The line holds the candidate's tip: by the promotion's own end, or read by ancestry at a resume.</summary>
    public const string Promoted = "promoted";

    /// <summary>It could not go on, said why, and is tried at each look.</summary>
    public const string Held = "held";

    /// <summary>A member went back to its session, said why.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>The line's integration stopped.</summary>
    public const string Stopped = "stopped";

    /// <summary>A resume found the attempt begun and not ended, and the line not holding the candidate's tip: it runs again from its start.</summary>
    public const string Interrupted = "interrupted";

    /// <summary>
    /// The judge ran the candidate's members one at a time (INTEGRATE1e part e4a): the failing members are handed back, the candidate is formed
    /// again, and the next form takes one member. A word added beside the six, so an older step is read as it was.
    /// </summary>
    public const string Split = "split";

    public static IReadOnlyList<string> All { get; } = [Done, Promoted, Held, HandedBack, Stopped, Interrupted, Split];
}

/// <summary>What the steps make of a member (§5.2, §17.4's states).</summary>
public static class IntegrationMemberStates
{
    public const string Formed = "formed";
    public const string Replayed = "replayed";

    /// <summary>Held by an earlier member's replay, or by the line itself; provisional until that member promotes.</summary>
    public const string Contained = "contained";

    public const string Conflict = "conflict";
    public const string HandedBack = "handed-back";
    public const string Dropped = "dropped";
    public const string Promoted = "promoted";
    public const string Settled = "settled";

    /// <summary>
    /// A conflict of meaning under <c>conflicts: hold</c>, or one whose session could not be told (INTEGRATE1e part e3b, §5.4): set aside, its
    /// request open and out of the queue, for the steward's or the owner's act. A state added beside the eight, so an older row is read as it was.
    /// </summary>
    public const string Held = "held";

    public static IReadOnlyList<string> All { get; } = [Formed, Replayed, Contained, Conflict, HandedBack, Dropped, Promoted, Settled, Held];
}

/// <summary>Why a candidate ended, freeing its line for the next.</summary>
public static class IntegrationCandidateEnds
{
    /// <summary>Promoted, and every member settled.</summary>
    public const string Settled = "settled";

    /// <summary>Formed again: a member left it, or the line moved under it (§5.2 step 8).</summary>
    public const string FormedAgain = "formed-again";

    /// <summary>Dropped by a person's stop or the steward's act.</summary>
    public const string Dropped = "dropped";

    /// <summary>The line's integration stopped (§5.2's last paragraph).</summary>
    public const string Stopped = "stopped";

    /// <summary>
    /// Every member left the candidate before any was left to promote (INTEGRATE1e part e3a): held by the line, or handed back. A code added
    /// beside the four, so a candidate ended before it is read as it was.
    /// </summary>
    public const string Empty = "empty";

    public static IReadOnlyList<string> All { get; } = [Settled, FormedAgain, Dropped, Stopped, Empty];
}

/// <summary>Why a request is no longer open.</summary>
public static class IntegrationRequestCloses
{
    /// <summary>Its member promoted and settled, or was contained and settled with the member that holds it.</summary>
    public const string Settled = "settled";

    /// <summary>Handed back to its session or the steward, with its words.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>Dropped by the person's stop, or by its requester.</summary>
    public const string Dropped = "dropped";

    public static IReadOnlyList<string> All { get; } = [Settled, HandedBack, Dropped];
}

/// <summary>Why a take was refused, nothing written.</summary>
public static class IntegrationTakeRefusals
{
    public const string Gone = "gone";
    public const string Ended = "ended";
    public const string Own = "own";

    /// <summary>The generation moved since it was read: another took it first.</summary>
    public const string Moved = "moved";

    /// <summary>Its run lives, or cannot be told dead.</summary>
    public const string Live = "live";
}

/// <summary>
/// The candidate's one job (§5.6, CISCOPE1e2's job, QUEUEDRAIN1's lesson): every process a candidate starts runs in the job its name names,
/// one per candidate, so a drop, a stop or the run's end closes it and its descendants end with it. Row f's verdict port makes and closes it;
/// the name is the store's, kept with the candidate.
/// </summary>
public static class IntegrationJobs
{
    /// <summary>A Windows job object's name, local to the session as every job Daoris names is.</summary>
    public const string Prefix = "Local\\daoris-integration-";

    /// <summary>The job of the candidate of that id: the candidate's id makes it, and no other candidate's.</summary>
    public static string NameOf(string candidate) => Prefix + candidate;
}

/// <summary>The claim no longer holds the candidate: another run took it, or it ended; nothing was written.</summary>
public sealed class IntegrationClaimLostException(IntegrationClaim claim, string why)
    : InvalidOperationException($"candidate {claim.Candidate} is not held by run {claim.Run} at generation {claim.Generation}: {why}. Nothing was written.")
{
    public IntegrationClaim Claim { get; } = claim;
}

/// <summary>The write does not follow from the candidate as it stands; nothing was written.</summary>
public sealed class IntegrationRefusedException(string candidate, string why)
    : InvalidOperationException($"candidate {candidate}: {why}. Nothing was written.")
{
    public string Candidate { get; } = candidate;
}
