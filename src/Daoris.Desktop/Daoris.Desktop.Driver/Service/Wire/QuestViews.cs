using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The quest doors, sent as their records and read into the driver's own views. A part named only in half reads as none, as the
/// service passes over what it cannot read: never a failed read of the quest.
/// </summary>
public static class QuestViews
{
    /// <summary>Every quest, closed ones included.</summary>
    public static async Task<IReadOnlyList<QuestView>> EveryQuestAsync(this ServiceClient service, CancellationToken ct = default) =>
        [.. (await service.SendAsync(new ListQuests(IncludeClosed: true), ct).ConfigureAwait(false)).Answered().Select(View)];

    /// <summary>
    /// One quest as it stands, closed ones included: how a session's end is observed, and what a resumed session is told its question
    /// came back with. Null where the service has none.
    /// </summary>
    public static async Task<QuestView?> FindQuestAsync(this ServiceClient service, string id, CancellationToken ct = default) =>
        (await service.EveryQuestAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(quest => string.Equals(quest.Id, id.TrimStart('#'), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every quest as the service wrote it: what a trace reads a quest's answers, its yes and its moments from, which
    /// <see cref="QuestView"/> leaves out because no planner needs them.
    /// </summary>
    /// <exception cref="DriverException">A refused read, in the service's words.</exception>
    public static async Task<string> QuestsJsonAsync(this ServiceClient service, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ListQuests(IncludeClosed: true), ct).ConfigureAwait(false);
        sent.Answered();
        return sent.Reply.Payload;
    }

    /// <summary>
    /// One quest's moves as the service wrote them, in the order its history replays. Null from a host without the door, which
    /// answers the route not found: its own answer is never a 404, since it answers an id with no history with no moves.
    /// </summary>
    /// <exception cref="DriverException">The host answered the door with a failure.</exception>
    public static async Task<string?> QuestMovesJsonAsync(this ServiceClient service, string id, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ReadQuestMoves(id.TrimStart('#')), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!sent.Reply.Ok) throw new DriverException($"the service would not read quest `#{id}`'s moves: {sent.Reply.Payload}");
        return sent.Reply.Payload;
    }

    /// <summary>
    /// Decline a quest with the person's reason, verbatim: an abandon's decline. With <paramref name="whileOpen"/> it applies only while
    /// the quest is open, so a take that reached a remote first stands and the decline is kept on the quest as a conflict. A refusal is
    /// an answer: a decline refused because the quest is taken meanwhile is not an exception.
    /// </summary>
    public static Task<(bool Ok, string Message)> DeclineQuestAsync(
        this ServiceClient service, string id, string reason, bool whileOpen, CancellationToken ct = default) =>
        service.MovedAsync(new RespondQuest(id.TrimStart('#'), "decline", reason, WhileOpen: whileOpen ? true : null), "respond", ct);

    /// <summary>The person's yes to a done's departure from what they required: what it held goes on.</summary>
    public static Task<(bool Ok, string Message)> AcceptDepartureAsync(this ServiceClient service, string id, CancellationToken ct = default) =>
        service.MovedAsync(new AcceptQuest(id.TrimStart('#')), "accept", ct);

    /// <summary>
    /// The person marks a quest done: their words, to the service's own door for it, which says the done is theirs and answers none
    /// of their requirements. Never respond's door, which is an agent's done.
    /// </summary>
    public static Task<(bool Ok, string Message)> PersonDoneAsync(this ServiceClient service, string id, string? note, CancellationToken ct = default) =>
        service.MovedAsync(new MarkQuestDone(id.TrimStart('#'), string.IsNullOrWhiteSpace(note) ? null : note.Trim()), "done", ct, "done door for a person");

    /// <summary>The person's reopen of a sealed submission, named by its machine and sequence, with their words where they wrote any.</summary>
    public static Task<(bool Ok, string Message)> ReopenQuestAsync(
        this ServiceClient service, string id, QuestSubmissionRef submission, string? note, CancellationToken ct = default) =>
        service.MovedAsync(
            new ReopenQuest(id.TrimStart('#'), new QuestSubmissionRefWire(submission.Machine, submission.Sequence), string.IsNullOrWhiteSpace(note) ? null : note.Trim()),
            "reopen", ct);

    /// <summary>The person's word to go on with a step its source's reopen held.</summary>
    public static Task<(bool Ok, string Message)> ContinueQuestAsync(this ServiceClient service, string id, CancellationToken ct = default) =>
        service.MovedAsync(new ContinueQuest(id.TrimStart('#')), "continue", ct);

    /// <summary>
    /// The person's bring-forward of one side of two machines' work on a quest. A refusal or a loss again (409) and a quest not found
    /// (404) come back as the service's sentence, not as a tool error.
    /// </summary>
    public static Task<(bool Ok, string Message)> ForwardConflictAsync(
        this ServiceClient service, string id, string machine, long sequence, CancellationToken ct = default) =>
        service.MovedAsync(new ForwardConflict(id.TrimStart('#'), machine, sequence), "conflicts/forward", ct);

    /// <summary>
    /// A workflow request's quest: the same key sent again answers the quest it made, so a lost answer is sent once more without a
    /// second quest. The service's refusal comes back as an answer; an unanswered host throws.
    /// </summary>
    public static async Task<RequestPublished> PublishAsync(this ServiceClient service, RequestAsk ask, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new PublishQuest(ask.From, ask.To, ask.Title, ask.Body, Request: ask.Key), ct).ConfigureAwait(false);
        if (sent.Answer is { } made) return new(true, null, made.Message ?? "", made.Quest is { } quest ? View(quest) : null);
        return Error(sent) is { } error
            ? new(false, "refused", error, null)
            : new(false, "no-door", $"the service at {service.BaseUrl} has no quest door ({sent.Reply.Status})", null);
    }

    /// <summary>
    /// What Daoris read of a done's evidence, posted to the one door that takes it, as records are written. A refusal is an answer: the
    /// done no longer waits on evidence (409), the verdict does not read what it waits on (400), or no such quest (404).
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the caller says so, and posts again at a later read.</exception>
    public static async Task<EvidencePosted> EvidenceAsync(this ServiceClient service, string id, EvidenceVerdict verdict, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(
            new PostQuestEvidence(
                id.TrimStart('#'), verdict.Commit, verdict.How, verdict.Session,
                [.. verdict.Items.Select(item => (QuestEvidenceReadWire?)new QuestEvidenceReadWire(
                    item.Requirement, item.Path, item.Gate, item.Result, item.Object, item.Changed, item.Spelled))],
                Submission: verdict.Submission is { } submission ? new QuestSubmissionRefWire(submission.Machine, submission.Sequence) : null),
            ct).ConfigureAwait(false);
        if (sent.Answer is { } kept) return new(EvidencePosted.Kept, kept.Message ?? "");
        if (Error(sent) is not { } error)
        {
            return new(EvidencePosted.NoDoor, $"the service at {service.BaseUrl} has no evidence door ({sent.Reply.Status}) — is it older than this driver?");
        }

        return sent.Reply.Status == (int)System.Net.HttpStatusCode.Conflict
            ? new(EvidencePosted.NotWaiting, error)
            : new(EvidencePosted.Refused, error);
    }

    /// <summary>Quests as the service wrote them, read as its list is: what a reader of a stand-in's or a kept answer reads.</summary>
    public static IReadOnlyList<QuestView> Read(string json) =>
        [.. (JsonSerializer.Deserialize(json, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<QuestResponse[]>)ContractJson.Options.GetTypeInfo(typeof(QuestResponse[]))) ?? []).Select(View)];

    /// <summary>One quest as the driver plans from it.</summary>
    public static QuestView View(QuestResponse quest) => new(
        quest.Id ?? "", quest.From ?? "", quest.To ?? "", quest.Title ?? "", quest.Body ?? "", quest.Status ?? "")
    {
        Links = [.. (quest.Links ?? []).OfType<string>()],
        // The service answers a path only to this machine, and only where the bytes are here.
        Attachments = [.. (quest.Attachments ?? []).OfType<QuestAttachmentResponse>().Select(file => new QuestFileView(file.Name ?? "", file.Sha256 ?? "", file.Bytes, file.Path))],
        Then = [.. (quest.Then ?? []).OfType<QuestStepResponse>().Select(step => new QuestStepView(step.To ?? "", step.Title ?? "", step.Body ?? "") { SetUpIn = step.SetUpIn })],
        Parent = quest.Parent,
        Holder = Named(quest.Holder),
        Here = Named(quest.Here),
        Short = Named(quest.Short),
        Weight = Named(quest.Weight),
        Paths = [.. (quest.Paths ?? []).OfType<string>()],
        Request = Named(quest.Request),
        Awaits = quest.Awaits,
        PublishedBy = Named(quest.PublishedBy),
        Note = quest.Note,
        NoteParts = quest.NoteParts is { ValueKind: JsonValueKind.Array } parts ? [.. parts.EnumerateArray().Select(NotePartJson.ReadPart).OfType<NotePart>()] : null,
        Workspace = RemoteTarget.Workspace(quest.Workspace),
        Deletable = quest.Deletable,
        Lanes = [.. (quest.Lanes ?? []).OfType<string>()],
        // In the service's order, which a done answers by number, so each keeps its place.
        Requirements = [.. (quest.Requirements ?? []).Select(r => new QuestRequirementView(r?.Quote ?? "", r?.Check ?? "") { Evidence = EvidenceOf(r) })],
        Held = quest.Held,
        Hold = Named(quest.Hold),
        AwaitsEvidence = quest.AwaitsEvidence,
        Evidence = VerdictOf(quest.Evidence),
        Answers = [.. (quest.Answers ?? []).OfType<QuestAnswerResponse>().Select(a => new QuestAnswerView(a.Requirement, a.Met, a.Departed, a.Quote))],
        Updated = Moment(quest.Updated),
        Review = quest.Review is { Choice: { Length: > 0 } choice } review ? new QuestReviewChoiceView(choice, review.Words) : null,
        SetUpIn = quest.SetUpIn,
        // A set-up without a full commit is half of one.
        SetUps =
        [
            .. (quest.SetUps ?? []).OfType<QuestSetUpWire>().Where(setUp => EvidenceCodes.IsObjectId(setUp.Commit)).Select(setUp => new QuestSetUpView(setUp.Commit)
            {
                Look = setUp.Look, Shows = setUp.Shows, Again = setUp.Again, Served = setUp.Served, Run = setUp.Run, Session = setUp.Session,
                Local = setUp.Local, At = setUp.At, Machine = setUp.Machine, Sequence = setUp.Sequence,
            }),
        ],
        // A verdict that says nothing is none.
        Verdicts =
        [
            .. (quest.Verdicts ?? []).OfType<QuestReviewVerdictWire>().Where(verdict => verdict.Said is { Length: > 0 }).Select(verdict => new QuestReviewVerdictView(verdict.Said)
            {
                SetUpMachine = verdict.SetUp?.Machine, SetUpSequence = verdict.SetUp?.Sequence, Commit = verdict.Commit, Words = verdict.Words,
                At = verdict.At, Machine = verdict.Machine,
            }),
        ],
        Closure = Named(quest.Closure),
        // Its commit only where it is a commit's full id, lowered as a verdict's is.
        Submission = quest.Submission is { Machine: { Length: > 0 } machine, Sequence: > 0 } submitted
            ? new QuestSubmissionView(
                machine, submitted.Sequence, Moment(submitted.At), submitted.Note,
                submitted.Commit is { } commit && EvidenceCodes.IsObjectId(commit) ? commit.ToLowerInvariant() : null, Named(submitted.Session))
            : null,
        GrewFrom = RefOf(quest.GrewFrom),
        SourceReopened = RefOf(quest.SourceReopened),
        Reopened = quest.Reopened is { Machine: { Length: > 0 } reopener, Sequence: > 0 } reopen && RefOf(reopen.Of) is { } of
            ? new QuestReopenView(reopener, reopen.Sequence, of, reopen.Note, Moment(reopen.At), Named(reopen.Session))
            : null,
        // A choice without its moment is half of one.
        WorkflowChoices =
        [
            .. (quest.WorkflowChoices ?? []).OfType<AskWorkflowChoiceResponse>().Where(chosen => chosen.At != default)
                .Select(chosen => new AskWorkflowChoiceView(chosen.Kind, chosen.Workflow, chosen.At, chosen.Words)),
        ],
    };

    /// <summary>A requirement's evidence, each item exactly one of a path or a gate; an item naming both or neither is half of a fact, none.</summary>
    internal static IReadOnlyList<QuestEvidenceItem> EvidenceOf(QuestRequirementResponse? requirement)
    {
        var read = (requirement?.Evidence ?? []).OfType<QuestEvidenceResponse>()
            .Select(item => new QuestEvidenceItem(item.Path, item.Gate))
            .Where(item => (item.Path is null) != (item.Gate is null))
            .ToList();
        return read.Count == 0 ? [] : read;
    }

    /// <summary>The last read of a done's evidence, or null: none, or one whose commit, how or any item does not read.</summary>
    internal static EvidenceVerdict? VerdictOf(QuestEvidenceVerdictResponse? verdict)
    {
        if (verdict is not { Commit: { } commit, How: { } how, Items: { } items } || !EvidenceCodes.IsObjectId(commit) || !EvidenceCodes.How.Contains(how))
        {
            return null;
        }

        var read = new List<EvidenceRead>();
        foreach (var item in items)
        {
            if (item is not { Requirement: { } requirement, Result: { } result } || (item.Path is null) == (item.Gate is null)) return null;
            read.Add(new EvidenceRead(requirement, item.Path, item.Gate, result) { Object = item.Object, Changed = item.Changed, Spelled = item.Spelled });
        }

        return new EvidenceVerdict(commit.ToLowerInvariant(), how, read)
        {
            Session = verdict.Session,
            At = verdict.At,
            Machine = verdict.Machine,
            Submission = RefOf(verdict.Submission),
        };
    }

    // A person's or the driver's move on one quest: its sentence, a refusal's included, and a host older than the door said to be.
    internal static async Task<(bool Ok, string Message)> MovedAsync(
        this ServiceClient service, IRoute<QuestActionResponse> route, string door, CancellationToken ct, string? named = null)
    {
        var sent = await service.SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is { } moved) return (true, moved.Message ?? "");
        return Error(sent) is { } error
            ? (false, error)
            : (false, $"the service at {service.BaseUrl} has no {named ?? $"{door} door"} ({sent.Reply.Status}) — is it older than this driver?");
    }

    // A refusal's sentence, else its text; null where the host answered no JSON at all, an older host's bare 404 or 405.
    internal static string? Error<T>(Sent<T> sent) where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(sent.Reply.Payload);
            return sent.Reply.Ok
                ? sent.Refusal
                : document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error)
                  && error.ValueKind == JsonValueKind.String
                    ? error.GetString()!
                    : sent.Reply.Payload;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Named(string? text) => text is { Length: > 0 } ? text : null;

    private static DateTimeOffset? Moment(DateTimeOffset at) => at == default ? null : at;

    private static DateTimeOffset? Moment(DateTimeOffset? at) => at == default(DateTimeOffset) ? null : at;

    private static QuestSubmissionRef? RefOf(QuestSubmissionRefResponse? named) =>
        named is { Machine: { Length: > 0 } machine, Sequence: > 0 and var sequence } ? new QuestSubmissionRef(machine, sequence) : null;
}
