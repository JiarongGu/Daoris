namespace Daoris.Driver;

/// <summary>
/// A move record's phases (FOUNDATION1f-i; the foundation design §3.3): <c>decided</c> → <c>quiescing</c> → <c>old-ended</c> → <c>opening</c> →
/// <c>opened</c> → [<c>copied</c> → <c>verified</c>, the carry's] → <c>delivering</c> → <c>delivered</c>, or <c>fell-back</c>, or <c>held</c>
/// → <c>closed</c>, or <c>closed</c>. The names the course's own reading of a standing move uses are the same names.
/// </summary>
public enum MoveRecordPhase
{
    /// <summary>The course made it and nothing has begun.</summary>
    Decided,

    /// <summary>Waiting for the old part's natural boundary.</summary>
    Quiescing,

    /// <summary>The old part has ended, or there was none to end: a move from an ended record is written here.</summary>
    OldEnded,

    /// <summary>The open asked, under the move's <c>opens</c> key.</summary>
    Opening,

    /// <summary>The open answered: the new record and the words it moved.</summary>
    Opened,

    /// <summary>The carry's copy made.</summary>
    Copied,

    /// <summary>The carry's copy verified.</summary>
    Verified,

    /// <summary>The new part's words going across.</summary>
    Delivering,

    /// <summary>Delivered: the move ended, every word it moved taken.</summary>
    Delivered,

    /// <summary>Fell back: the move ended on the old part, with its code.</summary>
    FellBack,

    /// <summary>Held for the person, with its reason: it stands until it closes <c>resolved</c> by §3.3's one rule.</summary>
    Held,

    /// <summary>Closed, with its reason: superseded, gone or resolved.</summary>
    Closed,
}

/// <summary>A phase's word in <c>moves.phase</c>, and the order the phases go in (§3.3).</summary>
public static class MovePhases
{
    private static readonly (MoveRecordPhase Phase, string Word)[] Words =
    [
        (MoveRecordPhase.Decided, "decided"), (MoveRecordPhase.Quiescing, "quiescing"), (MoveRecordPhase.OldEnded, "old-ended"),
        (MoveRecordPhase.Opening, "opening"), (MoveRecordPhase.Opened, "opened"), (MoveRecordPhase.Copied, "copied"),
        (MoveRecordPhase.Verified, "verified"), (MoveRecordPhase.Delivering, "delivering"), (MoveRecordPhase.Delivered, "delivered"),
        (MoveRecordPhase.FellBack, "fell-back"), (MoveRecordPhase.Held, "held"), (MoveRecordPhase.Closed, "closed"),
    ];

    /// <summary>
    /// The words a move ends at, which free its record for the next move: the partial index <c>moves_standing</c> holds every other row, a word
    /// this build does not know among them, so a phase a newer build adds stands until that build ends it.
    /// </summary>
    public static readonly IReadOnlyList<string> Ending = ["delivered", "fell-back", "closed"];

    /// <summary>A phase's word.</summary>
    public static string Word(MoveRecordPhase phase) => Words.First(each => each.Phase == phase).Word;

    /// <summary>The phase a word names, or null for one this build does not know, which it reads as standing and writes no phase over.</summary>
    public static MoveRecordPhase? Of(string word) => Words.Where(each => each.Word == word).Select(each => (MoveRecordPhase?)each.Phase).FirstOrDefault();

    /// <summary>Whether a phase's word ends its move.</summary>
    public static bool Ends(string word) => Ending.Contains(word, StringComparer.Ordinal);

    /// <summary>
    /// Whether a move at <paramref name="from"/> may go to <paramref name="to"/>, forward only: a hold only closes; an ended move takes no phase;
    /// any phase before the end may fall back, be held or close; and the working phases go in their order, the open before what it answers,
    /// the carry's copy and its check between the open and the delivery, and <c>delivered</c> only from <c>delivering</c>.
    /// </summary>
    public static bool Follows(MoveRecordPhase from, MoveRecordPhase to) => from switch
    {
        MoveRecordPhase.Delivered or MoveRecordPhase.FellBack or MoveRecordPhase.Closed => false,
        MoveRecordPhase.Held => to == MoveRecordPhase.Closed,
        _ => to switch
        {
            MoveRecordPhase.FellBack or MoveRecordPhase.Held or MoveRecordPhase.Closed => true,
            MoveRecordPhase.Quiescing => from == MoveRecordPhase.Decided,
            MoveRecordPhase.OldEnded => from is MoveRecordPhase.Decided or MoveRecordPhase.Quiescing,
            MoveRecordPhase.Opening => from == MoveRecordPhase.OldEnded,
            MoveRecordPhase.Opened => from == MoveRecordPhase.Opening,
            MoveRecordPhase.Copied => from == MoveRecordPhase.Opened,
            MoveRecordPhase.Verified => from == MoveRecordPhase.Copied,
            MoveRecordPhase.Delivering => from is MoveRecordPhase.Opened or MoveRecordPhase.Verified,
            MoveRecordPhase.Delivered => from == MoveRecordPhase.Delivering,
            _ => false,
        },
    };
}

/// <summary>Who executes a move (§3.3): a conversation's by the shell whose chat runner runs it, a driven line's by the loop whose look runs it.</summary>
public static class MoveExecutors
{
    public const string Conversation = "conversation";

    public const string Driven = "driven";

    public static readonly IReadOnlyList<string> All = [Conversation, Driven];
}

/// <summary>How a move goes (the design §3.1's <c>Move</c>): <c>brief</c>, or <c>carry</c> once SWITCHCTX1 lands.</summary>
public static class MoveHows
{
    public const string Brief = "brief";

    public const string Carry = "carry";

    public static readonly IReadOnlyList<string> All = [Brief, Carry];
}

/// <summary>Why a move closed (§3.3): the old part went on before the open; its record left or its destination is gone; a hold ended by the one rule.</summary>
public static class MoveCloseWhy
{
    public const string Superseded = "superseded";

    public const string Gone = "gone";

    public const string Resolved = "resolved";

    public static readonly IReadOnlyList<string> All = [Superseded, Gone, Resolved];
}

/// <summary>The external effects a move marks before it makes them (§3.3, §13.4 step 2): a spawn, a prompt, a copy.</summary>
public static class MoveEffects
{
    public const string Spawn = "spawn";

    public const string Prompt = "prompt";

    public const string Copy = "copy";

    public static readonly IReadOnlyList<string> All = [Spawn, Prompt, Copy];
}

/// <summary>Why a take of a move was refused, nothing written (§13.4, §19.1).</summary>
public static class MoveTakeRefusals
{
    /// <summary>The claim is the taker's own run's: a live run keeps its moves.</summary>
    public const string Own = "own";

    /// <summary>The claim's run lives by the lease's test, or cannot be told dead: a live executor keeps every move it claimed, however long it pauses.</summary>
    public const string Live = "live";

    /// <summary>The move's claim moved since the taker read it: another took it first.</summary>
    public const string Moved = "moved";

    /// <summary>The move has ended: there is nothing to take up.</summary>
    public const string Ended = "ended";

    /// <summary>No such move: its record was deleted, and the move with it.</summary>
    public const string Gone = "gone";
}

/// <summary>What the course decided a move of a record left to be, as the move record writes it at its claim (§3.3, §13.4 step 1).</summary>
/// <param name="Left">The record left: the line's head the move is from.</param>
/// <param name="Executor"><see cref="MoveExecutors"/>' word.</param>
/// <param name="How"><see cref="MoveHows"/>' word.</param>
/// <param name="Why">The move's closed <c>from_why</c>, today's hand-over reason codes.</param>
/// <param name="By">Its <c>from_by</c>, today's hand-over asker codes.</param>
public sealed record MoveDecision(string Left, string Executor, string How, string Why, string By)
{
    /// <summary>A move from an ended record, which has no part to end: written at <c>old-ended</c> (§3.3).</summary>
    public bool FromEnded { get; init; }

    /// <summary>The settled selection's account, by profile name, where it is settled.</summary>
    public string? Account { get; init; }

    /// <summary>The brief's recipe: the why it is composed for.</summary>
    public string? RecipeWhy { get; init; }

    /// <summary>The brief's recipe: how many of the old part's events it is composed up to, or null for all.</summary>
    public int? RecipeBound { get; init; }
}

/// <summary>A move's claim (§13.3, §13.4): the move, the process run that holds it and its generation, which every write checks inside its transaction.</summary>
public sealed record MoveClaim(long Move, string Run, long Generation);

/// <summary>A move's effect mark (§3.3): the effect begun and its moment, and, once known, its outcome and its moment.</summary>
public sealed record MoveEffect(string What, string Begun, string? Outcome, string? Ended);

/// <summary>One word in transfer (§13.3's <c>move_words</c>): its id, producer key, transfer id, order and disposition, never its text.</summary>
/// <param name="Word">The word's id on the record left.</param>
/// <param name="Producer">Its producer's key, or null for a keyless word.</param>
/// <param name="Transfer">The transfer id the open moved it under.</param>
/// <param name="Position">Its order among the move's words.</param>
/// <param name="Disposition">Its disposition once one is known (<c>sent</c>, refused at a limit, uncertain or <i>left</i>, rows f-ii on); null until then.</param>
public sealed record MoveRecordWord(string Word, string? Producer, string Transfer, int Position, string? Disposition = null);

/// <summary>One row of <c>moves</c>, as the store holds it (§3.3, §13.3): never a word's text, a path or a credential.</summary>
/// <param name="Id">The move's id, never handed out again.</param>
/// <param name="Left">The record left.</param>
/// <param name="Executor"><see cref="MoveExecutors"/>' word.</param>
/// <param name="PhaseWord">The phase as the row holds it, a newer build's word among them.</param>
/// <param name="ClaimRun">The run that holds its claim, a row of <c>store_runs</c>.</param>
/// <param name="ClaimGeneration">The claim's generation: 1 at the decision, raised by each take.</param>
/// <param name="How"><see cref="MoveHows"/>' word.</param>
/// <param name="Why">Its <c>from_why</c>.</param>
/// <param name="By">Its <c>from_by</c>.</param>
public sealed record MoveRecord(long Id, string Left, string Executor, string PhaseWord, string ClaimRun, long ClaimGeneration, string How, string Why, string By)
{
    /// <summary>The phase, or null for a word this build does not know.</summary>
    public MoveRecordPhase? Phase => MovePhases.Of(PhaseWord);

    /// <summary>Whether the move still stands for its record left: until it is delivered, falls back or closes.</summary>
    public bool Stands => !MovePhases.Ends(PhaseWord);

    /// <summary>Each phase the move has reached, with its moment.</summary>
    public IReadOnlyDictionary<MoveRecordPhase, string> Moments { get; init; } = new Dictionary<MoveRecordPhase, string>();

    /// <summary>The <c>opens</c> key its open is asked under, from <c>opening</c>.</summary>
    public string? Opens { get; init; }

    /// <summary>The record its open made, from <c>opened</c>.</summary>
    public string? Destination { get; init; }

    /// <summary>The settled selection's account, by profile name.</summary>
    public string? Account { get; init; }

    /// <summary>The brief's recipe: its why.</summary>
    public string? RecipeWhy { get; init; }

    /// <summary>The brief's recipe: the event bound it is composed up to.</summary>
    public int? RecipeBound { get; init; }

    /// <summary>Its fall-back's code.</summary>
    public string? FellBackCode { get; init; }

    /// <summary>Its hold's reason, kept once the hold closes.</summary>
    public string? HeldWhy { get; init; }

    /// <summary>Its close's reason, <see cref="MoveCloseWhy"/>' word.</summary>
    public string? ClosedWhy { get; init; }

    /// <summary>Its last effect mark, or null where it marked none.</summary>
    public MoveEffect? Effect { get; init; }
}

/// <summary>What a decision came to: the move claimed by the deciding run, or the record's standing move, which the decider reads as the course's <c>Never</c>.</summary>
/// <param name="Move">The new move, or the one already standing.</param>
/// <param name="Claim">The decider's claim on the new move; null where another move stands.</param>
public sealed record MoveDecided(MoveRecord Move, MoveClaim? Claim)
{
    /// <summary>Whether this decision made the move.</summary>
    public bool Claimed => Claim is not null;
}

/// <summary>What a take came to: the move under the taker's claim, or why it was refused (<see cref="MoveTakeRefusals"/>), nothing written.</summary>
public sealed record MoveTaken(MoveRecord? Move, MoveClaim? Claim, string? Refused);

/// <summary>A write under a claim the move no longer holds: taken by another run, or the move gone with its record. Nothing was written.</summary>
public sealed class MoveClaimLostException(MoveClaim claim, string why)
    : DriverException($"move {claim.Move} is no longer claimed by run {claim.Run} at generation {claim.Generation}: {why}; nothing was written.")
{
    /// <summary>The claim the write was made under.</summary>
    public MoveClaim Claim { get; } = claim;
}

/// <summary>A write the move's phase or effect does not take: a phase out of its order, an ended move, a second effect over one never recorded.</summary>
public sealed class MoveRefusedException(long move, string why) : DriverException($"move {move}: {why}; nothing was written.")
{
    /// <summary>The move the write was refused on.</summary>
    public long Move { get; } = move;
}
