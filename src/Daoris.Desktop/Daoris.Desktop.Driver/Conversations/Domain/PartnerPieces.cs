namespace Daoris.Driver;

/// <summary>
/// Where one piece stands, one of <see cref="All"/> (COAGENT1b, D199 points 2 and 3; the co-agent design §7.2). A side effect is
/// always made before the state that names it, so a restart reconciles each state with what it observes
/// (<see cref="PartnerReconcile"/>), never doing a side effect twice.
/// </summary>
public static class PartnerStates
{
    /// <summary>The ask is on file and not yet judged.</summary>
    public const string Asked = "asked";

    /// <summary>The lead's ask waits for the person's yes under the rule's <c>ask</c> (COAGENT1d).</summary>
    public const string Waiting = "waiting";

    /// <summary>The piece's slot and its place in the lead's allowance are reserved in the common admission.</summary>
    public const string Approved = "approved";

    /// <summary>The copy's folder is named, and the copy is being made at the ask's base.</summary>
    public const string Preparing = "preparing";

    /// <summary>The partner's record is open, named before its turn.</summary>
    public const string Started = "started";

    /// <summary>The partner's job is empty; the keeper's moment is written before keeping.</summary>
    public const string Ending = "ending";

    /// <summary>The import was refused: the copy is kept for the person's look and <i>Discard…</i>.</summary>
    public const string Held = "held";

    /// <summary>The keeper's commit is in the repository, frozen as <see cref="PieceRefs.Of"/>.</summary>
    public const string Imported = "imported";

    /// <summary>The answer's and the facts' files are written.</summary>
    public const string Answered = "answered";

    /// <summary>The answer was handed to the lead, or its successor, by the delivery receipt.</summary>
    public const string Handed = "handed";

    /// <summary>Daoris read the frozen commit as an ancestor of the lead's branch: recorded once, never unset.</summary>
    public const string TakenIn = "taken-in";

    /// <summary>The lead's work landed without it; its ref stays until the person's <i>Discard…</i>.</summary>
    public const string NotTakenIn = "not-taken-in";

    /// <summary>The person discarded it, its ref and its copy with it.</summary>
    public const string Discarded = "discarded";

    /// <summary>Refused, its code saying why.</summary>
    public const string Refused = "refused";

    /// <summary>Daoris cannot know what happened (a restart with the job unknown, an unreadable state): the copy is kept untouched for the person.</summary>
    public const string Uncertain = "uncertain";

    public static readonly IReadOnlyList<string> All =
    [
        Asked, Waiting, Approved, Preparing, Started, Ending, Held, Imported, Answered, Handed, TakenIn, NotTakenIn, Discarded,
        Refused, Uncertain,
    ];

    /// <summary>
    /// The states whose reservation the common admission counts beside the records (the design §5): before the partner's record
    /// opens, its reservation holds the slot; from <see cref="Started"/> the record's <c>serves</c> holds it, counted once.
    /// </summary>
    public static bool Reserves(string state) => state is Approved or Preparing;

    /// <summary>
    /// The states that count toward a lead's two open pieces, from <see cref="Approved"/> on (the design §2.3 step 2), so two looks
    /// cannot both pass the second; a piece whose end is not known stays open until the person's act.
    /// </summary>
    public static bool Open(string state) => state is Approved or Preparing or Started or Ending or Uncertain;

    /// <summary>
    /// The states whose box still owns its copy (the design §7.2): the strays' cleanup leaves the folder, and only a verified import
    /// or the person's <i>Discard…</i> removes it. An imported or later piece holds it only until its removal is done.
    /// </summary>
    public static bool HoldsCopy(PartnerState state) => state.State switch
    {
        Preparing or Started or Ending or Held or Uncertain => true,
        Imported or Answered or Handed or TakenIn or NotTakenIn => !state.CopyRemoved,
        _ => false,
    };

    /// <summary>The states after which nothing runs by itself.</summary>
    public static bool Final(string state) => state is Handed or TakenIn or NotTakenIn or Discarded or Refused;
}

/// <summary>How a partner's turn ended, kept beside <see cref="PartnerStates.Ending"/> (the design §2.3 steps 6 and 11).</summary>
public static class PartnerEnds
{
    public const string Answered = "answered";
    public const string Stopped = "stopped";
    public const string OutOfTime = "out-of-time";
    public const string Limited = "limited";

    public static readonly IReadOnlyList<string> All = [Answered, Stopped, OutOfTime, Limited];
}

/// <summary>Why a piece is refused, held or uncertain, by code: what the state file's <c>code</c> says and the note's <c>why</c> carries.</summary>
public static class PartnerWhy
{
    /// <summary>The asker is a partner, a helper, a reviewer, an intake, Ask Daoris's conversation or a brainstorm pass.</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The asking session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The piece is blank.</summary>
    public const string Blank = "blank";

    /// <summary>A path the ask names leads outside the repository.</summary>
    public const string PathOutside = "path-outside";

    /// <summary>The lead has no commit to grow from.</summary>
    public const string NoCommit = "no-commit";

    /// <summary>The ask's base commit is not in the repository: no fallback to another base.</summary>
    public const string BaseMissing = "base-missing";

    /// <summary>The lead already has two pieces open across its <c>from</c> chain.</summary>
    public const string TooMany = "too-many";

    /// <summary>The named agent is not one the person's rule lists.</summary>
    public const string NotListed = "not-listed";

    /// <summary>The agent hands no connector to its sessions.</summary>
    public const string NoConnector = "no-connector";

    /// <summary>No measured row holds the effective configuration a start would hand the agent.</summary>
    public const string NotMeasured = "not-measured";

    /// <summary>The partner's copy could not be made.</summary>
    public const string CopyFailed = "copy-failed";

    /// <summary>The partner's process could not be joined to its strict job, so it was never run.</summary>
    public const string NoJob = "no-job";

    /// <summary>The state file could not be read: the piece waits for the person, its copy untouched.</summary>
    public const string StateUnreadable = "state-unreadable";

    /// <summary>A restart found the partner's job unknown or not empty.</summary>
    public const string JobUnknown = "job-unknown";

    /// <summary>The piece's ref stands at a commit other than the keeper's.</summary>
    public const string RefMoved = "ref-moved";

    /// <summary>The import refused what the copy held, leaving nothing to hand (<see cref="PieceImport"/>).</summary>
    public const string ImportRefused = "import-refused";

    /// <summary>The partner left no work in its copy.</summary>
    public const string NoWork = "no-work";
}

/// <summary>
/// One piece's ask as <c>&lt;id&gt;.ask.json</c> keeps it (the design §2.3 step 1, §7.1): written once, never changed, with the exact
/// commit the lead's tree stood at and the lead's uncommitted paths as Daoris read them then.
/// </summary>
/// <param name="Asker">The lead's session.</param>
/// <param name="Paths">The repository-relative paths the piece is about; empty where the ask named none.</param>
/// <param name="Agent">The agent named, one word; null where none was (only the lead's ask, from COAGENT1d).</param>
/// <param name="Base">The exact commit the piece grows from.</param>
/// <param name="Uncommitted">The lead's uncommitted paths at the ask.</param>
/// <param name="AskedBy"><see cref="ByPerson"/> or <see cref="ByAgent"/>.</param>
public sealed record PartnerAsk(
    string Id, string Asker, string Piece, IReadOnlyList<string> Paths, string? Agent, string Base,
    IReadOnlyList<string> Uncommitted, string AskedBy, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByPerson = "person";

    /// <summary>A piece's words at most (the design §2.3 step 1).</summary>
    public const int PieceLimit = 4_000;
}

/// <summary>
/// One piece's state as the driver keeps it, <c>&lt;id&gt;.state.json</c>, the driver alone writing it (the design §7.1): the state,
/// its code, and the identity of each side effect made before it.
/// </summary>
/// <param name="Code">A refusal's, a hold's or an uncertainty's code (<see cref="PartnerWhy"/>); null otherwise.</param>
public sealed record PartnerState(string Id, string State, string? Code, DateTimeOffset At)
{
    /// <summary>The copy's folder, named at <see cref="PartnerStates.Preparing"/> before it is made.</summary>
    public string? Copy { get; init; }

    /// <summary>The partner's record, named at <see cref="PartnerStates.Started"/> before its turn.</summary>
    public string? Partner { get; init; }

    /// <summary>The agent the choice resolved to.</summary>
    public string? Agent { get; init; }

    /// <summary>The account the partner ran on, by its profile name.</summary>
    public string? Account { get; init; }

    /// <summary>The strict job's name, where one held the partner.</summary>
    public string? Job { get; init; }

    /// <summary>How the turn ended (<see cref="PartnerEnds"/>), kept beside <see cref="PartnerStates.Ending"/>.</summary>
    public string? End { get; init; }

    /// <summary>The keeper's moment, written before keeping: it dates the commit, so a retry makes the same one.</summary>
    public DateTimeOffset? Kept { get; init; }

    /// <summary>The imported commit, frozen at <see cref="PieceRefs.Of"/>.</summary>
    public string? Commit { get; init; }

    /// <summary>The copy's removal is done.</summary>
    public bool CopyRemoved { get; init; }

    /// <summary>The lead's commit that took the piece in, recorded when Daoris first read it as an ancestor, and never unset.</summary>
    public string? TakenBy { get; init; }
}

/// <summary>A partner's answer as <c>&lt;id&gt;.answer.json</c> keeps it: its summary and what it could not do, its claims (the design §2.3 step 8).</summary>
public sealed record PartnerAnswer(string Id, string Summary, string? Left, DateTimeOffset At)
{
    public const int SummaryLimit = 20_000;

    /// <summary>The partner's record that wrote it through <c>partner_answer</c>; null for the driver's own copy of its last words.</summary>
    public string? Partner { get; init; }

    /// <summary>
    /// Its last words, kept by the driver where the partner gave no answer through its tool and its turn completed: written only once its
    /// job is empty, so no tool of the partner's can still write the file.
    /// </summary>
    public bool LastWords { get; init; }
}

/// <summary>
/// What Daoris read itself, never from the partner's words, as <c>&lt;id&gt;.facts.json</c> keeps it (the design §2.3 step 8): the
/// commit, the files it changes, those beyond the piece's paths, and what the import refused or left out and why.
/// </summary>
public sealed record PartnerFacts(
    string Id, string Commit, IReadOnlyList<string> Files, IReadOnlyList<string> Beyond, IReadOnlyList<PieceLeftOut> LeftOut,
    DateTimeOffset At);

/// <summary>
/// The piece's one delivery receipt, <c>&lt;id&gt;.delivery.json</c> (the design §7.6): the destination claimed before anything is
/// handed, moved along <c>from</c> explicitly, the stable key, and the outcome.
/// </summary>
/// <param name="To">The record the answer is claimed for: the lead, or the newest live record continuing it by <c>from</c>.</param>
/// <param name="Key">The producer key the word carries, stable for the piece.</param>
/// <param name="Outcome">One of <see cref="PartnerDelivery"/>'s outcomes.</param>
public sealed record PartnerReceipt(string Id, string To, string Key, string Outcome, DateTimeOffset At);

/// <summary>A delivery receipt's outcomes, and how a hand reads one (the design §7.6).</summary>
public static class PartnerDelivery
{
    public const string Waiting = "waiting";
    public const string Attempted = "attempted";
    public const string Uncertain = "uncertain";
    public const string Taken = "taken";

    public static readonly IReadOnlyList<string> All = [Waiting, Attempted, Uncertain, Taken];

    /// <summary>The piece's stable key, the producer key its word carries through the session's one dispatcher.</summary>
    public static string KeyOf(string id) => $"partner:{id}";

    /// <summary>
    /// The receipt a hand claims before anything is handed: a fresh one waiting for the lead's chain's newest live record; an
    /// existing one moved explicitly to <paramref name="to"/> where the claimed record left and the answer was never taken; an
    /// existing one waiting, attempted, uncertain or taken left as it is (a key already taken answers <i>handed</i>, an uncertain
    /// one waits for the person and is never handed again by itself).
    /// </summary>
    public static PartnerReceipt Claim(PartnerReceipt? held, string id, string to, DateTimeOffset at) => held switch
    {
        null => new PartnerReceipt(id, to, KeyOf(id), Waiting, at),
        { Outcome: Waiting } when held.To != to => held with { To = to, At = at },
        _ => held,
    };

    /// <summary>Whether a hand may go now on this receipt: only a waiting one, never one attempted, uncertain or taken.</summary>
    public static bool MayHand(PartnerReceipt receipt) => receipt.Outcome == Waiting;

    /// <summary>An attempt a restart settles as uncertain (<c>UncertainWords</c>) marks the receipt so; a taken one stays taken.</summary>
    public static PartnerReceipt Settle(PartnerReceipt receipt, bool taken, DateTimeOffset at) => receipt.Outcome switch
    {
        Taken => receipt,
        Attempted => receipt with { Outcome = taken ? Taken : Uncertain, At = at },
        _ => receipt,
    };
}

/// <summary>What a restart observes of a piece's side effects, by their identities (the design §7.2).</summary>
/// <param name="StateReadable">False where the state file exists and cannot be read.</param>
/// <param name="CopyExists">The copy's named folder exists.</param>
/// <param name="RecordForPiece">A record already open carrying this piece, or null.</param>
/// <param name="Job">What is known of the partner's job.</param>
/// <param name="RefCommit">The commit <see cref="PieceRefs.Of"/> stands at, or null where it does not exist.</param>
public sealed record PartnerObserved(
    bool StateReadable, bool CopyExists, string? RecordForPiece, PartnerJob Job, string? RefCommit);

/// <summary>What is known of a partner's strict job.</summary>
public enum PartnerJob
{
    /// <summary>Never started, or ended and observed empty.</summary>
    Empty,

    /// <summary>A process of the partner's, or a child of it, still runs in it.</summary>
    Running,

    /// <summary>Its state cannot be known (a restart lost its handle).</summary>
    Unknown,
}

/// <summary>What the look does next for one piece.</summary>
public enum PartnerAct
{
    /// <summary>Nothing by itself: a final state, or one waiting for the person.</summary>
    None,

    /// <summary>Judge the ask (the rule, the asker, the choice) and reserve.</summary>
    Judge,

    /// <summary>Make the copy at the ask's base, open the record and start the partner.</summary>
    Prepare,

    /// <summary>Remove a partial copy, then make it again (Daoris's work, no agent's).</summary>
    RemoveCopyThenPrepare,

    /// <summary>Wait for the running partner's end.</summary>
    Watch,

    /// <summary>Keep and import: the commit is deterministic from the base, the files and the keeper's moment.</summary>
    Keep,

    /// <summary>Read the facts and the answer, then write <see cref="PartnerStates.Answered"/>.</summary>
    Answer,

    /// <summary>Hand the answer by the delivery receipt.</summary>
    Hand,
}

/// <summary>One step of the reconciliation: what to do, and the state to write first where the observation already moved it.</summary>
/// <param name="Write">The state to write before the act, or null where the state on file stands.</param>
public sealed record PartnerStep(PartnerAct Act, PartnerState? Write);

/// <summary>
/// A restart's reconciliation of one piece (the design §7.2, Codex's read 25): it never trusts the last state alone, it reconciles
/// what the state says with what it observes, by each side effect's identity, and nothing at any state runs an agent again unasked.
/// Pure: the look observes and writes.
/// </summary>
public static class PartnerReconcile
{
    public static PartnerStep Next(string id, PartnerState? state, PartnerObserved seen, DateTimeOffset now)
    {
        if (!seen.StateReadable) return Uncertain(id, state, PartnerWhy.StateUnreadable, now);
        if (state is null)
        {
            // An absent state with a side effect already made is a state write that failed: nothing is trusted, the person decides.
            return seen.CopyExists || seen.RecordForPiece is not null || seen.RefCommit is not null
                ? Uncertain(id, null, PartnerWhy.StateUnreadable, now)
                : new(PartnerAct.Judge, null);
        }

        switch (state.State)
        {
            case PartnerStates.Asked or PartnerStates.Waiting:
                return new(PartnerAct.Judge, null);
            case PartnerStates.Approved:
                return new(PartnerAct.Prepare, null);
            case PartnerStates.Preparing:
                // The record opened before the crash is taken as this one; the turn was never begun, since `started` names it first.
                if (seen.RecordForPiece is { } record) return new(PartnerAct.Watch, state with { State = PartnerStates.Started, Partner = record, At = now });
                return seen.CopyExists ? new(PartnerAct.RemoveCopyThenPrepare, null) : new(PartnerAct.Prepare, null);
            case PartnerStates.Started:
                return seen.Job switch
                {
                    PartnerJob.Empty => new(PartnerAct.Keep, state with { State = PartnerStates.Ending, Kept = now, At = now }),
                    PartnerJob.Running => new(PartnerAct.Watch, null),
                    _ => Uncertain(id, state, PartnerWhy.JobUnknown, now),
                };
            case PartnerStates.Ending:
                // The keeper's moment is on file, so keeping again makes the same commit; a ref already standing is judged by Keep.
                return new(PartnerAct.Keep, null);
            case PartnerStates.Imported:
                return new(PartnerAct.Answer, null);
            case PartnerStates.Answered:
                return new(PartnerAct.Hand, null);
            default:
                return new(PartnerAct.None, null);
        }
    }

    private static PartnerStep Uncertain(string id, PartnerState? state, string code, DateTimeOffset now) =>
        new(PartnerAct.None, (state ?? new PartnerState(id, PartnerStates.Uncertain, code, now)) with
        {
            State = PartnerStates.Uncertain, Code = code, At = now,
        });

    /// <summary>
    /// How the create-only import reads the piece's ref (the design §7.2's import row): absent, it is created; at the keeper's
    /// commit, the import already happened and is <see cref="PartnerStates.Imported"/>; at another commit, <see cref="PartnerStates.Uncertain"/>.
    /// </summary>
    public static string? AfterRef(string? refCommit, string kept) =>
        refCommit is null ? null
        : string.Equals(refCommit, kept, StringComparison.OrdinalIgnoreCase) ? PartnerStates.Imported
        : PartnerStates.Uncertain;
}
