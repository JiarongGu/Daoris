namespace Daoris.Driver;

/// <summary>One of the person's words on an ask (DRIFT1a, D133 §1), as the service answers it.</summary>
/// <param name="Kind">
/// <c>asked</c>, <c>answered</c>, <c>added</c>, <c>reopened</c>, or a review's <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, as the
/// service spells it. A kind this build does not know is kept as spelled and said as a word they said, never dropped: what the
/// person said outranks what this build can name.
/// </param>
/// <param name="Text">Their words, verbatim.</param>
/// <param name="Session">The session it was said to; none for the ask's own sentence, and none for a review's verdict.</param>
/// <param name="Quest">
/// The quest that session worked, or the quest a review's verdict was given on; none for the ask's own sentence, an intake's,
/// and a conversation's.
/// </param>
public sealed record AskWordView(string Kind, string Text, DateTimeOffset At, string? Session = null, string? Quest = null)
{
    public const string Asked = "asked";
    public const string Answered = "answered";
    public const string Added = "added";

    /// <summary>Said to a session after it ended, kept once a session took them (MSG1a's <c>reopened</c>, D137 §2.4).</summary>
    public const string Reopened = "reopened";

    /// <summary>Their words with a <c>reviewed</c> on a set-up step (REVIEWENV1b, D154 point 8; design §3.5).</summary>
    public const string Reviewed = "reviewed";

    /// <summary>Their words with a <c>not-yet</c> on a set-up step: what is not right yet (REVIEWENV1b; design §3.4).</summary>
    public const string NotYet = "not-yet";

    /// <summary>Their words with a skip of a review (REVIEWENV1b; design §3.6).</summary>
    public const string Skipped = "skipped";

    /// <summary>
    /// Their message on the ask's channel (SESSONE2a's <c>AskWordKind.Channel</c>, D167 point 7), kept on the ask when they post it. A
    /// part that takes it reports nothing more (SESSONE2b), so it is held here once.
    /// </summary>
    public const string Channel = "channel";
}

/// <summary>One session's request for a go-ahead (KNOWUSE1a), as the service answers it.</summary>
public sealed record GoAheadRequestView(string Session, string? Quest, DateTimeOffset At, string Why);

/// <summary>The person's answer to a go-ahead (KNOWUSE1a): yes or no, their words where they gave any, and when.</summary>
public sealed record GoAheadAnswerView(bool Approved, string? Words, DateTimeOffset At);

/// <summary>
/// A go-ahead held on an ask (KNOWUSE1a, D135 §2), as the service answers it: the person's yes for one act outside a
/// repository, named by its kind, where it lands and what it touches, with each session's request and the person's answer.
/// </summary>
/// <param name="Number">Its number on the ask, from 1: how a session and the person name it.</param>
/// <param name="Kind">write, release, push, sign-in or run, as the service spells it.</param>
/// <param name="On">Where it lands, as the service read it.</param>
/// <param name="Act">What it touches, in the words of the session that first asked it.</param>
public sealed record GoAheadView(int Number, string Kind, string On, string Act, IReadOnlyList<GoAheadRequestView> Asked)
{
    /// <summary>The person's answer, the latest where they answered twice; null while it waits on them.</summary>
    public GoAheadAnswerView? Answer { get; init; }

    /// <summary>The go-ahead whose words this one's shared where they could not tell, when that is why it was asked again.</summary>
    public int? Near { get; init; }

    /// <summary>The act in one line, as the service names it: <c>write on production: "dashboard configuration"</c>.</summary>
    public string Named => $"{Kind} on {On}: \"{Act}\"";
}

/// <summary>
/// An ask the mirrored messages name and this machine does not hold (SESSONE2h): the service's list row, field for field. There is
/// no ask record here, so no title; <paramref name="Origins"/> are the machines its messages came from.
/// </summary>
public sealed record ElsewhereAskAnswer(string Id, string Workspace, IReadOnlyList<string> Origins, DateTimeOffset Latest, int Messages, int Unread);

/// <summary>
/// The kind and workflow a new ask carries from a terminal (WORKFLOW1e): each null where none is chosen, and the person's words.
/// </summary>
public sealed record AskWorkflowComposed(string? Kind, string? Workflow, string? Words);

/// <summary>A repository's declaration as the registry answered it — what the intake decides from (D34).</summary>
/// <param name="Registered">Whether it declared a domain at all — adopted and silent is not the same as owning nothing.</param>
/// <param name="Root">Where it is on this machine, answered only to this machine; null elsewhere.</param>
public sealed record DeclarationView(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, string? Root)
{
    /// <summary>
    /// The lanes it declares (D115 §2.2), as the registry answers them — their words, never their paths.
    /// Absent is none: a host from before lanes answers without them.
    /// </summary>
    public IReadOnlyList<LaneView> Lanes { get; init; } = [];
}

/// <summary>An ask as the local service answered it (D65 §1a) — enough to open an intake and observe it.</summary>
/// <param name="State">`Open`, `Proposed`, `Published` or `Closed`, as the record spells it.</param>
/// <param name="Tier">Which tier answered — `declarations`, `named` or `intake` — never a model (D24).</param>
public sealed record AskView(string Id, string Workspace, string Sentence, string State, string Tier)
{
    public string? Asker { get; init; }

    /// <summary>Why it was closed, when it was.</summary>
    public string? Note { get; init; }

    /// <summary>The intake session that served it, once one opened.</summary>
    public string? Intake { get; init; }

    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Its files, each with where THIS machine keeps it — the service's answer, never derived here.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>The quests it became, in order.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>What the declarations tier proposed, best first — a name the sentence holds, or a word match, offered as one.</summary>
    public IReadOnlyList<string> Proposed { get; init; } = [];

    /// <summary>
    /// Those of <see cref="Proposed"/> the ask's sentence names (ASKNAME1b): a proposal whose evidence is its own name alone,
    /// which the tier ranks above every word match (ASKNAME1). The rest are word matches. Absent is none.
    /// </summary>
    public IReadOnlyList<string> Named { get; init; } = [];

    /// <summary>Whether the service would delete it with its quests (D95). Absent is false.</summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The person's words on it (DRIFT1a, D133 §1), oldest first: its sentence, then each answer and each message added to a
    /// session on its work. Null where the host answered none, a host from before them: never read as having said nothing.
    /// </summary>
    public IReadOnlyList<AskWordView>? Words { get; init; }

    /// <summary>For an ask made before its words were kept: from when they are, since nothing is back-filled (DRIFT1a).</summary>
    public DateTimeOffset? WordsKeptFrom { get; init; }

    /// <summary>
    /// The go-aheads its sessions asked the person for (KNOWUSE1a, D135 §2), oldest first, one per act. Null where the host
    /// answered none, a host from before them.
    /// </summary>
    public IReadOnlyList<GoAheadView>? GoAheads { get; init; }

    // The review's and the workflow's parts of an ask; their element records are declared in Service/Domain.

    /// <summary>The person's review choices on it, oldest first, the latest standing (REVIEWENV1b); empty where they made none.</summary>
    public IReadOnlyList<AskReviewChoiceView> ReviewChoices { get; init; } = [];

    /// <summary>Its intake's review proposals, oldest first (REVIEWENV1b); empty where it proposed none.</summary>
    public IReadOnlyList<AskReviewProposalView> ReviewProposals { get; init; } = [];

    /// <summary>Its intake's kind proposals, oldest first (WORKFLOW1i); empty where it proposed none. Nothing here applies one.</summary>
    public IReadOnlyList<AskWorkflowProposalView> WorkflowProposals { get; init; } = [];

    /// <summary>The person's kind and workflow choices on it, oldest first, the latest standing (WORKFLOW1e); empty where they made none.</summary>
    public IReadOnlyList<AskWorkflowChoiceView> WorkflowChoices { get; init; } = [];
}

public static class Asks
{
    /// <summary>
    /// What a set of asks SAYS to the attention band, as one string — equal when the same asks stand
    /// in the same states with the same intake and quests, whatever the order. The shell forwards a
    /// tick to the page when this changes (INT4d), as it does for <see cref="Considerations.Signature"/>:
    /// an ask made by the other door moves nothing else a tick reports.
    /// </summary>
    public static string Signature(IEnumerable<AskView> asks) =>
        string.Join("\n", asks
            .Select(ask => $"{ask.Id}\t{ask.State}\t{ask.Intake}\t{ask.Quests.Count}")
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// Whether a proposal was made because the ask's sentence names its repository (ASKNAME1b): its evidence is that name
    /// alone, which is what the tier writes for one (ASKNAME1). Without case, as the tier finds the name. The page reads a
    /// proposal by the same rule, so the intake and the person are told the same thing.
    /// </summary>
    public static bool IsNamed(string repository, IReadOnlyList<string> matched) =>
        matched is [var only] && string.Equals(only, repository, StringComparison.OrdinalIgnoreCase);
}
