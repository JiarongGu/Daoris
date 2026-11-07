using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>The quest a chain reaches, as its operations replay on the service.</summary>
public sealed record TraceQuestLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Quests"/>.</summary>
    public string Source => TraceStores.Quests;

    /// <summary>Why it could not be read, <see cref="TraceLinkGaps.Unread"/> or <see cref="TraceLinkGaps.NotFound"/>; null where it is.</summary>
    public string? Missing { get; init; }

    public TraceNamedBy? NamedBy { get; init; }

    /// <summary>Whom it asks as a person reads it: its repository, and its lanes after a colon.</summary>
    public string? Address { get; init; }

    public string? Title { get; init; }

    /// <summary>Open, Taken, Done or Declined, as the record spells it.</summary>
    public string? Status { get; init; }

    public DateTimeOffset? Filed { get; init; }

    public DateTimeOffset? Moved { get; init; }

    /// <summary>The ask it is asked by, or null where no ask asked it.</summary>
    public string? Ask { get; init; }

    /// <summary>Its sender, as the record names it: who asked it where no ask did.</summary>
    public string? From { get; init; }

    /// <summary>The quest whose close published it.</summary>
    public string? Parent { get; init; }

    /// <summary>The session whose connector published it.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>The quest its taker waits on.</summary>
    public string? Awaits { get; init; }

    /// <summary>Held: its done waits for the person's yes, or for its evidence (<see cref="Hold"/> says which).</summary>
    public bool Held { get; init; }

    /// <summary>
    /// Why it is held: <c>departed</c>, <c>evidence-unread</c>, <c>evidence-missing</c> or, a set-up step's,
    /// <c>unreviewed</c>; null where nothing holds it or a host said none.
    /// </summary>
    public string? Hold { get; init; }

    /// <summary>What Daoris last read of its evidence, or null while nothing is.</summary>
    public TraceEvidenceRead? Evidence { get; init; }

    /// <summary>When the person said yes to its departure.</summary>
    public DateTimeOffset? Accepted { get; init; }

    /// <summary>What its close said, as written.</summary>
    public string? Note { get; init; }

    /// <summary>Its close's lines by code, which the page words in its own language; null for a quest from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    public IReadOnlyList<TraceRequirement> Requirements { get; init; } = [];

    /// <summary>The steps its close will publish, as written.</summary>
    public IReadOnlyList<TraceStep> Then { get; init; } = [];

    /// <summary>The session records that name it, oldest first; null where the session records could not be read.</summary>
    public IReadOnlyList<TraceRecordRef>? Records { get; init; }

    /// <summary>
    /// Its moves, in the order its history replays, from the service's
    /// <c>GET /api/quests/{id}/moves</c>: what the quest's page draws on its flowchart. Null where they were not read, and
    /// <see cref="MovesMissing"/> says why.
    /// </summary>
    public IReadOnlyList<TraceMove>? Moves { get; init; }

    /// <summary>
    /// Why its moves are not here: <see cref="TraceMovesGaps.NoDoor"/>, a host from before the door, so its path is not kept there; or
    /// <see cref="TraceMovesGaps.Unread"/>, the door did not answer. Null where they were read, or the quest is not.
    /// </summary>
    public string? MovesMissing { get; init; }

    /// <summary>What the moves' door said where it did not answer: the terminal's alone.</summary>
    [JsonIgnore]
    public string? MovesProblem { get; init; }
}

/// <summary>
/// One move of a quest, as the service answers it by allow-list: its kind as the wire spells it, when and on which
/// machine it is made, its sequence there, and whether the replay applied it. Every other field is absent where the move keeps
/// none, and a reader says it is not recorded.
/// </summary>
public sealed record TraceMove(string Kind, DateTimeOffset At, string Machine, long Sequence, bool Applied)
{
    /// <summary>Where the remote placed it in its order; null while it is pending, and on a machine with no remote.</summary>
    public long? Number { get; init; }

    /// <summary>The status it reached the quest at, as the record spells it; null before the publish.</summary>
    public string? From { get; init; }

    /// <summary>A conflict's: the status the move that lost attempted.</summary>
    public string? Attempted { get; init; }

    /// <summary>A dismissal's: the conflict it settles, by the machine and sequence the conflict keeps.</summary>
    public TraceMoveRef? Dismisses { get; init; }

    /// <summary>A wait's: the quest it waits on.</summary>
    public string? WaitsOn { get; init; }

    /// <summary>A submission's: the session that said its <c>ready</c>.</summary>
    public string? Session { get; init; }

    /// <summary>An evidence read's code: <c>found</c>, <c>missing</c> or <c>commit</c>.</summary>
    public string? Evidence { get; init; }

    /// <summary>A set-up's code: <c>local</c> or <c>deployed</c>.</summary>
    public string? SetUp { get; init; }

    /// <summary>A verdict's: <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>.</summary>
    public string? Verdict { get; init; }

    /// <summary>The hold it set (its code) or let go (<c>none</c>); null where it left the hold as it is.</summary>
    public string? Hold { get; init; }
}

/// <summary>An operation by the machine and sequence that name it everywhere: what a dismissal names its conflict by.</summary>
public sealed record TraceMoveRef(string Machine, long Sequence);

/// <summary>One requirement: the person's words, its check, and how the done answered it (one of <see cref="TraceAnswers"/>).</summary>
public sealed record TraceRequirement(int Number, string Quote, string Check, string Answer)
{
    /// <summary>How it is met, in the done's words.</summary>
    public string? Met { get; init; }

    /// <summary>Why the done departed, in its words.</summary>
    public string? Departed { get; init; }

    /// <summary>The person's words a departure relied on.</summary>
    public string? On { get; init; }

    /// <summary>What Daoris reads itself to tell its check is met, each with what is last read of it.</summary>
    public IReadOnlyList<TraceEvidenceItem> Evidence { get; init; } = [];
}

/// <summary>
/// One evidence item a requirement names, with what is last read of it: its kind and what it
/// names, and its code with the object, whether the work changed it and a <c>case</c> read's spelling; the code null while unread.
/// </summary>
public sealed record TraceEvidenceItem(string Kind, string Named)
{
    public string? Result { get; init; }

    public string? Object { get; init; }

    public bool? Changed { get; init; }

    public string? Spelled { get; init; }
}

/// <summary>
/// What Daoris last read of a done's evidence, as the quest keeps it: the commit and how it is chosen, the
/// session whose end is read (null for the terminal's), the machine that read it and when, and how many items were found.
/// </summary>
public sealed record TraceEvidenceRead(string Commit, string How, int Items, int Found)
{
    public string? Session { get; init; }

    public string? Machine { get; init; }

    public DateTimeOffset? At { get; init; }
}

public sealed record TraceStep(string To, string Title);

public sealed record TraceRecordRef(string Id, string State);
