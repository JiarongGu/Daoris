namespace Daoris.Driver;

/// <summary>
/// The words a commit is named by, apart from <see cref="Trace"/> so the records below read them without the trace's reads
/// (STRUCT3g-i); the callers name it here (FLAT1b-iii-b).
/// </summary>
public static class CommitNames
{
    /// <summary>The fewest hexadecimal digits a commit is named by: git's own floor for an abbreviation.</summary>
    public const int Digits = 4;

    /// <summary>Whether a word could name a commit: at least <see cref="Digits"/> hexadecimal digits and no more than a whole id.</summary>
    public static bool IsCommit(string word) =>
        word.Length is >= Digits and <= 64 && word.All(Uri.IsHexDigit);
}

/// <summary>
/// A session record as a trace reads it (TRACE1, D143): every field the service answers this machine about where a change
/// came from, read from <c>/api/sessions?includeClosed=true</c>. <see cref="SessionRecord"/> keeps what the list groups by;
/// this keeps what ran the work, which no other reader needs.
/// </summary>
/// <param name="Id">Its id. A teammate's record came down with the sync keyed <c>origin/id</c> (SYNC4).</param>
/// <param name="Repository">Where it ran: a repository's name, <c>ask #a</c> for an intake, <c>daoris:help</c> for Ask Daoris.</param>
public sealed record TracedSession(string Id, string Repository, string State)
{
    public string? Quest { get; init; }

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    public string? Adapter { get; init; }

    /// <summary>The tool's version the record opened on (D49 §4), or null where it names none.</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>The account it ran as, or null: the tool's own sign-in on this machine's record, unsaid on a teammate's.</summary>
    public string? Profile { get; init; }

    public string? Tree { get; init; }

    /// <summary>The commit its tree stood at when the spawn began (SURF6).</summary>
    public string? BaseCommit { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null.</summary>
    public string? Ask { get; init; }

    public string? Note { get; init; }

    /// <summary>The commits the driver read off its tree at its end (D46 §4), as written: <c>commits landed:</c> and a line each.</summary>
    public string? Evidence { get; init; }

    /// <summary>The person's answer to its park (STANDDOWN2), or null.</summary>
    public string? Answer { get; init; }

    /// <summary>When it opened, or null where the record's moment does not read.</summary>
    public DateTimeOffset? Created { get; init; }

    public DateTimeOffset? Updated { get; init; }

    /// <summary>Whether it took its quest through its own connector (STANDDOWN2).</summary>
    public bool Took { get; init; }

    /// <summary>A stop that was not the person's (D104).</summary>
    public bool Interrupted { get; init; }

    /// <summary>A failure an account's limit made (TOOL4c).</summary>
    public bool Limit { get; init; }

    /// <summary>The second opinion a reviewer's record reads another session's work for (XAGENT1c), or null.</summary>
    public string? Opinion { get; init; }

    /// <summary>The session a helper's or a partner's record serves (MIXAGENT2a-i, COAGENT1b), or null.</summary>
    public string? Serves { get; init; }

    /// <summary>The piece a partner's record writes (COAGENT1b), or null.</summary>
    public string? Piece { get; init; }

    /// <summary>The session this one continues (MIXAGENT1a-iii), or null.</summary>
    public string? From { get; init; }

    /// <summary>The line of work it belongs to, <c>&lt;origin&gt;/&lt;repository&gt;</c> (SESSONE1a), or null from a host before it.</summary>
    public string? Entry { get; init; }

    /// <summary>The quest a chat took through its own connector (CHATTAKE1), its own <see cref="Quest"/> staying null; null otherwise.</summary>
    public string? TookQuest { get; init; }

    /// <summary>The origin its <see cref="Entry"/> names, what comes before its first slash, or null where it names none.</summary>
    public string? Origin => Entry is { Length: > 0 } entry && entry.IndexOf('/') is > 0 and var slash ? entry[..slash] : null;

    /// <summary>A record that came down from the team (SYNC4): it ran on another machine, which keeps what is machine-local.</summary>
    public bool Teammate => Id.Contains('/');

    /// <summary>
    /// The commits its evidence names, each its abbreviation and its whole line, in the order written: the lines after the
    /// heading <see cref="WorkingTree.CommitsSinceAsync"/> writes, each beginning with git's abbreviation. None where it says
    /// none landed, could not be read, or there is no evidence.
    /// </summary>
    public IReadOnlyList<(string Sha, string Line)> Commits =>
        Evidence is not { } evidence
            ? []
            : [.. evidence.ReplaceLineEndings("\n").Split('\n').Skip(1)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(line => (Sha: line.Split(' ', 2)[0].ToLowerInvariant(), Line: line))
                .Where(commit => CommitNames.IsCommit(commit.Sha))];
}

/// <summary>How a quest's done answered one requirement (DRIFT1d, D133 §4), as the service answers it.</summary>
public sealed record TracedAnswer(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>
/// A quest as a trace reads it (TRACE1): the state its operations replay to, as <c>/api/quests?includeClosed=true</c>
/// answers it, with what <see cref="QuestView"/> leaves out because no planner needs it: its answers, its yes, its moments.
/// </summary>
public sealed record TracedQuest(string Id, string From, string To, string Title, string Status)
{
    public IReadOnlyList<string> Lanes { get; init; } = [];

    public string? Note { get; init; }

    /// <summary>Its note's lines by code (QUESTCLOSE1b5), or null for a quest from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    public DateTimeOffset? Filed { get; init; }

    public DateTimeOffset? Updated { get; init; }

    /// <summary>The quest whose close published this one (D65 §4), or null.</summary>
    public string? Parent { get; init; }

    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    public IReadOnlyList<TracedAnswer> Answers { get; init; } = [];

    public bool Held { get; init; }

    /// <summary>
    /// Why a held done waits (EVID1a, D144 §6): <c>departed</c>, <c>evidence-unread</c> or <c>evidence-missing</c>; null where
    /// nothing holds it, and from a host before evidence, whose one hold was a departure.
    /// </summary>
    public string? Hold { get; init; }

    /// <summary>What Daoris last read of its evidence (EVID1a, EVID1b), or null while nothing was.</summary>
    public EvidenceVerdict? Evidence { get; init; }

    /// <summary>When the person said yes to a departure (DRIFT1d), or null.</summary>
    public DateTimeOffset? Accepted { get; init; }

    /// <summary>The session whose connector published it (SESS1), or null.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>The quest its taker waits on (D79), or null.</summary>
    public string? Awaits { get; init; }

    /// <summary>Whom it asks as a person reads it (the service's <c>QuestAddress.Spell</c>).</summary>
    public string Address => Lanes.Count == 0 ? To : $"{To}:{string.Join('+', Lanes)}";
}

/// <summary>
/// What a trace read from each store (TRACE1, D143), each with why it could not be where it could not: the service's
/// session records and quests, and this machine's landings. An ask is read when the chain names one, and each session's
/// events and rules file when the chain reaches it.
/// </summary>
internal sealed record TraceFacts(
    IReadOnlyList<TracedSession> Sessions, string? SessionsUnread,
    IReadOnlyList<TracedQuest> Quests, string? QuestsUnread,
    IReadOnlyList<LandedBranch> Landings, string? LandingsUnread)
{
    /// <summary>This machine's due list (LAND2b, design §8): each session due to land automatically, and each try's code.</summary>
    public IReadOnlyList<AutoLanding> AutoLandings { get; init; } = [];

    /// <summary>
    /// Each quest's moves the chain reached, by its id (FLOWCHART1a2): read once the id is resolved, for the quests the chain reads
    /// through and no other. A quest with none here was not reached.
    /// </summary>
    public IReadOnlyDictionary<string, TraceMovesRead> Moves { get; init; } = new Dictionary<string, TraceMovesRead>();
}

/// <summary>
/// A quest's moves as the service's door answered them (FLOWCHART1a2): the moves; or none, from a host from before the door
/// (<paramref name="NoDoor"/>); or why the door did not answer.
/// </summary>
internal sealed record TraceMovesRead(IReadOnlyList<TraceMove>? Moves, bool NoDoor = false, string? Unread = null);

/// <summary>What an id was found to name, before the chain is read: the sessions and quests it starts from, and how each was found.</summary>
/// <param name="Kind">The kind it names, one of <see cref="TraceEntry"/>.</param>
/// <param name="Found">How it was found, for a commit: which evidence and which landing named it.</param>
/// <param name="Sessions">The sessions the chain is read through in full.</param>
/// <param name="Quests">The quests the chain is read through, by id, in order.</param>
/// <param name="Unrecorded">Sessions a landing names that no record on the service does.</param>
internal sealed record TraceFound(
    string Kind, string Id, IReadOnlyList<TraceFoundBy> Found, IReadOnlyList<TracedSession> Sessions, IReadOnlyList<string> Quests,
    IReadOnlyList<string> Unrecorded);
