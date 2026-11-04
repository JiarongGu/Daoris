using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The quest doors a local host serves the person and the driver alone, sent as their records: the graph's close, the review's
/// set-ups and verdicts, the workflow choice, a conflict's dismissal and a quest's delete. A refusal is an answer in the service's
/// words, and a host older than a door says so.
/// </summary>
public static class QuestPersonViews
{
    /// <summary>
    /// Dismiss every conflict a quest carries, the terminal's form of the drawer's button. The service's sentence comes back verbatim,
    /// including "nothing to dismiss".
    /// </summary>
    /// <exception cref="DriverException">The service refused, in its own words: no such quest.</exception>
    public static async Task<string> DismissConflictsAsync(this ServiceClient service, string quest, CancellationToken ct = default) =>
        (await service.SendAsync(new DismissConflict(quest.TrimStart('#'), null, null), ct).ConfigureAwait(false)).Answered().Message;

    /// <summary>A person deletes a quest made by mistake: refused, in the service's words, for one anything stands on.</summary>
    public static async Task<(bool Ok, string Message)> DeleteQuestAsync(this ServiceClient service, string id, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new DeleteQuest(id.TrimStart('#')), ct).ConfigureAwait(false);
        if (sent.Answer is { } deleted) return (true, deleted.Message);
        return QuestViews.Error(sent) is { } error
            ? (false, error)
            : (false, $"the service at {service.BaseUrl} has no delete door ({sent.Reply.Status}) — is it older than this driver?");
    }

    /// <summary>
    /// The graph's close: every quest named, with the submission it judged, under its key and token. A refusal is an answer with its
    /// check's code, the same close again answers its first outcome, and a host older than the door is said to be.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the caller keeps the close unknown, and asks what became of it.</exception>
    public static Task<QuestCloseAnswer> CloseQuestsAsync(
        this ServiceClient service, string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct = default) =>
        CloseAsync(service, new CloseQuest(quest.TrimStart('#'), key, token, Items(quests)), ct);

    /// <summary>
    /// What became of a close sent with <paramref name="key"/>: the close as it was sent, answered from the door's own record, or refused
    /// <c>withdrawn</c> where it never took the key. A host before the question has no such door, which proves nothing, so the close stays unknown.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer, or answered as no door of its own: the close stays unknown.</exception>
    public static Task<QuestCloseAnswer> CloseOutcomeAsync(
        this ServiceClient service, string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct = default) =>
        CloseAsync(service, new CloseQuestOutcome(quest.TrimStart('#'), key, token, Items(quests)), ct);

    /// <summary>
    /// Post a set-up step's session's said set-ups with the commit Daoris read from its tree. The commit is read, never the session's word.
    /// </summary>
    /// <param name="kind">The environment's kind, as the rule declares it: <c>local</c> or <c>deployed</c>.</param>
    public static Task<(bool Ok, string Message)> PostSetUpAsync(
        this ServiceClient service, string quest, string session, string commit, string kind, CancellationToken ct = default) =>
        service.MovedAsync(new PostQuestSetUp(quest.TrimStart('#'), commit, kind, session), "set-up", ct);

    /// <summary>
    /// The person's verdict on a review, with the set-up they were shown, named by its machine and sequence whole; a skip names none.
    /// </summary>
    /// <param name="verdict"><c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>.</param>
    public static Task<(bool Ok, string Message)> ReviewAsync(
        this ServiceClient service, string quest, string verdict, string? words, string? machine, long? sequence, CancellationToken ct = default) =>
        service.MovedAsync(
            new ReviewQuest(
                quest.TrimStart('#'), verdict, string.IsNullOrWhiteSpace(words) ? null : words.Trim(),
                machine is not null && sequence is { } at ? new QuestSetUpRefWire(machine, at) : null),
            "review", ct);

    /// <summary>
    /// The person's <i>Set it up in <c>environment</c></i>, which publishes a set-up step following a done quest. The service's sentence
    /// comes back verbatim, a refusal included, with the step it published.
    /// </summary>
    public static async Task<(bool Ok, string Message, string? Step)> PublishSetUpStepAsync(
        this ServiceClient service, string quest, string environment, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new PublishSetUpStep(quest.TrimStart('#'), environment), ct).ConfigureAwait(false);
        if (sent.Answer is { } published) return (true, published.Message ?? "", published.Quest?.Id);
        return QuestViews.Error(sent) is { } error
            ? (false, error, null)
            : (false, $"the service at {service.BaseUrl} has no set-up step door ({sent.Reply.Status}) — is it older than this driver?", null);
    }

    /// <summary>
    /// The person's kind and workflow on a quest no ask published, before it starts; neither clears the choice. The service's sentence
    /// comes back verbatim, a refusal included.
    /// </summary>
    public static Task<(bool Ok, string Message)> ChooseQuestWorkflowAsync(
        this ServiceClient service, string quest, string? kind, string? workflow, string? words, CancellationToken ct = default) =>
        service.MovedAsync(
            new ChooseQuestWorkflow(quest.TrimStart('#'), kind, workflow, string.IsNullOrWhiteSpace(words) ? null : words.Trim()),
            "workflow", ct, "quest workflow door");

    private static IReadOnlyList<QuestCloseItemWire?> Items(IReadOnlyList<QuestCloseItem> quests) =>
        [.. quests.Select(each => (QuestCloseItemWire?)new QuestCloseItemWire(each.Id, each.Machine, each.Sequence))];

    // Only the door's own answer settles a close, its close or its coded refusal. Anything else (a proxy's page, the host's 500, a bare
    // 404, a refusal naming no check of the door's) proves nothing about whether its transaction committed, so the close is unknown and
    // the same close is sent again, whose key answers its first outcome.
    private static async Task<QuestCloseAnswer> CloseAsync(ServiceClient service, IRoute<QuestCloseResponse> route, CancellationToken ct)
    {
        var sent = await service.SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is { Quests: not null } closed)
        {
            return new(QuestCloseOutcomes.Closed, null, closed.Message ?? "", [.. closed.Quests.Select(QuestViews.View)]);
        }

        if (sent.Reply.Status is 400 or 404 or 409 && Coded(sent.Reply.Payload) is var (code, error))
        {
            return new(QuestCloseOutcomes.Refused, code, error ?? sent.Reply.Payload, []);
        }

        throw new HttpRequestException(
            $"the close door at {service.BaseUrl} answered {sent.Reply.Status}, which is not its own answer, so whether it closed is unknown: "
            + TextBounds.Clip(sent.Reply.Payload, 200));
    }

    private static (string Code, string? Error)? Coded(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String
                || !QuestCloseOutcomes.Codes.Contains(code.GetString()!))
            {
                return null;
            }

            return (code.GetString()!, root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
