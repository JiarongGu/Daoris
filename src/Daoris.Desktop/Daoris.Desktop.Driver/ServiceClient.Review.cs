using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A chain's review choice as a quest carries it (REVIEWENV1b, D154 point 3; the review environment design §1.4): <c>off</c>,
/// <c>on</c> or an environment's name, with the person's words it was set on where it was set on theirs.
/// </summary>
public sealed record QuestReviewChoiceView(string Choice, string? Words = null);

/// <summary>
/// One set-up said on a set-up step (REVIEWENV1b, D154 point 9; design §2.6, §3.5), as the service answers it: the commit
/// Daoris read from the step's tree, what it shows and how to show it again. Oldest first on the quest; the newest is the one
/// a verdict answers.
/// </summary>
/// <param name="Commit">The commit read, by its full id: what the gate asks git about.</param>
public sealed record QuestSetUpView(string Commit)
{
    /// <summary>Where to look; absent for a local set-up read on another machine, whose tab is on the machine that showed it.</summary>
    public string? Look { get; init; }

    /// <summary>What it showed and what to look at.</summary>
    public string? Shows { get; init; }

    /// <summary>How to show it again by hand.</summary>
    public string? Again { get; init; }

    /// <summary>The folder <c>review_serve</c> named, as a path in the step's tree; null where nothing was served.</summary>
    public string? Served { get; init; }

    /// <summary>A review run's command, quoted from the procedure; null where none ran.</summary>
    public string? Run { get; init; }

    /// <summary>The session that said it; null for the person's own set-up.</summary>
    public string? Session { get; init; }

    /// <summary>Whether it was shown on a machine (the local kind), rather than in a deployed environment.</summary>
    public bool Local { get; init; }

    public DateTimeOffset? At { get; init; }

    /// <summary>The machine it was kept on, which with <see cref="Sequence"/> names it to a verdict.</summary>
    public string? Machine { get; init; }

    public long? Sequence { get; init; }
}

/// <summary>
/// The person's verdict on a review (REVIEWENV1b, D154 point 8; design §3.3–§3.5), as the service answers it: theirs alone, and
/// no connector tool or Ask Daoris card gives one. Oldest first on the quest.
/// </summary>
/// <param name="Said">
/// <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, spelled as the ask's words of the same kinds are (<see cref="AskWordView"/>).
/// A verdict this build does not know is kept as spelled.
/// </param>
public sealed record QuestReviewVerdictView(string Said)
{
    /// <summary>The machine of the set-up it answers; null for a skip.</summary>
    public string? SetUpMachine { get; init; }

    /// <summary>The sequence of the set-up it answers; null for a skip.</summary>
    public long? SetUpSequence { get; init; }

    /// <summary>The commit of the set-up it answers, as that set-up holds it; null for a skip.</summary>
    public string? Commit { get; init; }

    /// <summary>The person's words: a <c>not-yet</c>'s always.</summary>
    public string? Words { get; init; }

    public DateTimeOffset? At { get; init; }

    /// <summary>The machine it was given on.</summary>
    public string? Machine { get; init; }
}

/// <summary>The person's review choice on an ask (REVIEWENV1b; design §1.5), with when and their words: the latest stands.</summary>
public sealed record AskReviewChoiceView(string Choice, DateTimeOffset At, string? Words = null);

/// <summary>
/// An intake's review proposal on an ask (REVIEWENV1b; design §1.5–§1.6): a reading with its reason, which only the person's
/// press applies, the intake session that made it and the quest it was published with.
/// </summary>
public sealed record AskReviewProposalView(string Choice, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);

public sealed partial record QuestView
{
    /// <summary>Its chain's review choice (REVIEWENV1b); null where it chose none, and from a host before the field.</summary>
    public QuestReviewChoiceView? Review { get; init; }

    /// <summary>
    /// The environment it shows its chain's work in, which makes it a set-up step (REVIEWENV1b, D154 point 4); null for every
    /// other quest, and from a host before the field.
    /// </summary>
    public string? SetUpIn { get; init; }

    /// <summary>The set-ups said on it, oldest first (REVIEWENV1b); empty where none was, and from a host before them.</summary>
    public IReadOnlyList<QuestSetUpView> SetUps { get; init; } = [];

    /// <summary>The person's verdicts on its review, oldest first (REVIEWENV1b); empty where none was given, and from a host before them.</summary>
    public IReadOnlyList<QuestReviewVerdictView> Verdicts { get; init; } = [];
}

public sealed partial record QuestStepView
{
    /// <summary>The environment the step shows its chain's work in, which makes it a set-up step (REVIEWENV1b); null otherwise.</summary>
    public string? SetUpIn { get; init; }
}

public sealed partial record AskView
{
    /// <summary>The person's review choices on it, oldest first, the latest standing (REVIEWENV1b); empty where they made none.</summary>
    public IReadOnlyList<AskReviewChoiceView> ReviewChoices { get; init; } = [];

    /// <summary>Its intake's review proposals, oldest first (REVIEWENV1b); empty where it proposed none.</summary>
    public IReadOnlyList<AskReviewProposalView> ReviewProposals { get; init; } = [];
}

/// <summary>
/// The review on the record, read (REVIEWENV1b2): each field optional, so a quest or an ask no review touches, and one from a
/// host before them, reads as it did. A set-up, a verdict, a choice or a proposal that is not whole is passed over, as the
/// service passes over what it cannot read: never a failed read of the quest or the ask.
/// </summary>
public sealed partial class ServiceClient
{
    /// <summary>A quest's chain choice (<c>review</c>), or null where it answers none, or one that names no choice.</summary>
    private static QuestReviewChoiceView? ReadReviewChoice(JsonElement quest) =>
        quest.TryGetProperty("review", out var review) && Text(review, "choice") is { Length: > 0 } choice
            ? new QuestReviewChoiceView(choice, Text(review, "words"))
            : null;

    /// <summary>A set-up step's set-ups (<c>setUps</c>), each with a full commit: one without is half of one.</summary>
    private static IReadOnlyList<QuestSetUpView> ReadSetUps(JsonElement quest) =>
    [
        .. Items(quest, "setUps")
            .Where(setUp => EvidenceCodes.IsObjectId(Text(setUp, "commit")))
            .Select(setUp => new QuestSetUpView(Text(setUp, "commit")!)
            {
                Look = Text(setUp, "look"),
                Shows = Text(setUp, "shows"),
                Again = Text(setUp, "again"),
                Served = Text(setUp, "served"),
                Run = Text(setUp, "run"),
                Session = Text(setUp, "session"),
                Local = Flag(setUp, "local"),
                At = Moment(setUp, "at"),
                Machine = Text(setUp, "machine"),
                Sequence = Whole(setUp, "sequence"),
            }),
    ];

    /// <summary>The person's verdicts (<c>verdicts</c>), each saying one: a verdict that says nothing is none.</summary>
    private static IReadOnlyList<QuestReviewVerdictView> ReadVerdicts(JsonElement quest) =>
    [
        .. Items(quest, "verdicts")
            .Where(verdict => Text(verdict, "said") is { Length: > 0 })
            .Select(verdict =>
            {
                var setUp = verdict.TryGetProperty("setUp", out var named) && named.ValueKind == JsonValueKind.Object ? named : default;
                return new QuestReviewVerdictView(Text(verdict, "said")!)
                {
                    SetUpMachine = Text(setUp, "machine"),
                    SetUpSequence = Whole(setUp, "sequence"),
                    Commit = Text(verdict, "commit"),
                    Words = Text(verdict, "words"),
                    At = Moment(verdict, "at"),
                    Machine = Text(verdict, "machine"),
                };
            }),
    ];

    /// <summary>An ask's review choices (<c>reviewChoices</c>), each a choice with when it was made.</summary>
    private static IReadOnlyList<AskReviewChoiceView> ReadReviewChoices(JsonElement ask) =>
    [
        .. Items(ask, "reviewChoices")
            .Where(chosen => Text(chosen, "choice") is { Length: > 0 } && Moment(chosen, "at") is not null)
            .Select(chosen => new AskReviewChoiceView(Text(chosen, "choice")!, Moment(chosen, "at")!.Value, Text(chosen, "words"))),
    ];

    /// <summary>An ask's intake's review proposals (<c>reviewProposals</c>), each a choice with its reason and when.</summary>
    private static IReadOnlyList<AskReviewProposalView> ReadReviewProposals(JsonElement ask) =>
    [
        .. Items(ask, "reviewProposals")
            .Where(proposed => Text(proposed, "choice") is { Length: > 0 } && Text(proposed, "reason") is { Length: > 0 }
                               && Moment(proposed, "at") is not null)
            .Select(proposed => new AskReviewProposalView(
                Text(proposed, "choice")!, Text(proposed, "reason")!, Moment(proposed, "at")!.Value,
                Text(proposed, "session"), Text(proposed, "quest"))),
    ];

    /// <summary>
    /// Post a set-up step's session's said set-ups with the commit Daoris read from its tree (REVIEWENV1c, design §2.6): the local
    /// host's <c>POST /api/quests/{id}/set-up</c>, <c>{ session, commit, kind }</c>, which keeps each said set-up once. The commit
    /// is read, never the session's word. A refusal is an answer, in the service's words, and a host older than the door says so.
    /// </summary>
    /// <param name="kind">The environment's kind, as the rule declares it: <c>local</c> or <c>deployed</c>.</param>
    public async Task<(bool Ok, string Message)> PostSetUpAsync(
        string quest, string session, string commit, string kind, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteString("commit", commit);
            writer.WriteString("kind", kind);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/set-up", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no set-up door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The person's verdict on a review (REVIEWENV1c, design §3.3): the local host's <c>POST /api/quests/{id}/review</c>, with the
    /// set-up they were shown, named by its <c>machine</c> and <c>sequence</c> whole (REVIEWENV1b3); a skip names none. The
    /// service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    /// <param name="verdict"><c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>.</param>
    public async Task<(bool Ok, string Message)> ReviewAsync(
        string quest, string verdict, string? words, string? machine, long? sequence, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("verdict", verdict);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
            if (machine is not null && sequence is { } at)
            {
                writer.WriteStartObject("setUp");
                writer.WriteString("machine", machine);
                writer.WriteNumber("sequence", at);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/review", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no review door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>The objects of an array field; none where the field is absent or no array, and an item that is no object is passed over.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    /// <summary>A whole number a field holds, or null where it holds none.</summary>
    private static long? Whole(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;
}
