using System.Text.Json;

namespace Daoris.Driver;

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

    /// <summary>
    /// The person's <i>Set it up in <c>environment</c></i> (REVIEWENV1j, design §3.6): the local host's
    /// <c>POST /api/quests/{id}/set-up-step</c>, <c>{ environment }</c>, the door the gate's press calls, which publishes a set-up step
    /// following a done quest. The service's sentence comes back verbatim, a refusal included, with the step it published; a host
    /// older than the door says so.
    /// </summary>
    public async Task<(bool Ok, string Message, string? Step)> PublishSetUpStepAsync(string quest, string environment, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("environment", environment);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/set-up-step", body, ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no set-up step door ({status}) — is it older than this driver?", null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null);
        var step = answer.TryGetProperty("quest", out var published) && published.ValueKind == JsonValueKind.Object ? Text(published, "id") : null;
        return (true, Text(answer, "message") ?? "", step);
    }

    /// <summary>
    /// The person's review choice on an ask, or its intake's proposal applied (REVIEWENV1j; design §1.5): the local host's
    /// <c>POST /api/asks/{id}/review</c>, <c>{ choice, words? }</c>, the door the ask's page presses. The latest stands. The service's
    /// sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public Task<AskAnswer> ChooseAskReviewAsync(string ask, string choice, string? words, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/review", writer =>
        {
            writer.WriteString("choice", choice);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
        }, ct);

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
