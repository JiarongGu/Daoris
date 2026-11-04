using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The quest doors the person and the driver use: the graph's close, the review's set-ups and verdicts, the workflow choice, a
/// conflict's dismissal and a delete. Each judges through the exchange the connector's tools use. All but the dismissal are a local
/// host's: said or read on the person's own machine, travelling from there as operations, and no connector tool reaches them.
/// </summary>
public sealed class QuestPersonRoutes(HostWiring host) : IRouteModule
{
    private readonly QuestAnswers _answers = new(host);

    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        // Committed here like any verb and carried by the next pass, so the conflict goes on every machine.
        routes.Map<DismissConflict, QuestActionResponse>(DismissAsync);
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<CloseQuest, QuestCloseResponse>(async (request, http, ct) =>
            await CloseAnswerAsync(http, await Service.Exchange.CloseAsync(request.Id, request.Key, request.Token, Named(request.Quests), DateTimeOffset.UtcNow, ct), ct));
        routes.Map<CloseQuestOutcome, QuestCloseResponse>(async (request, http, ct) =>
            await CloseAnswerAsync(http, await Service.Exchange.CloseOutcomeAsync(request.Id, request.Key, request.Token, Named(request.Quests), DateTimeOffset.UtcNow, ct), ct));
        routes.Map<PostQuestSetUp, QuestActionResponse>(SetUpAsync);
        routes.Map<ReviewQuest, QuestActionResponse>(ReviewAsync);
        routes.Map<PublishSetUpStep, QuestActionResponse>(async (request, http, ct) =>
            await ReviewAnswerAsync(http, await Service.Exchange.PublishSetUpStepAsync(request.Id, request.Environment, DateTimeOffset.UtcNow, ct), ct));
        routes.Map<ChooseQuestWorkflow, QuestActionResponse>(WorkflowAsync);
        routes.Map<DeleteQuest, DeletedResponse>(DeleteAsync);
    }

    // A half-named submission arrives blank and is refused by the exchange, which says what a close names.
    private static List<(string, QuestOperationRef)> Named(IReadOnlyList<QuestCloseItemWire?>? quests) =>
        [.. (quests ?? []).Select(q => (q?.Id ?? "", new QuestOperationRef(q?.Machine ?? "", q?.Sequence ?? 0)))];

    private async Task<Answer<QuestCloseResponse>> CloseAnswerAsync(HttpContext http, QuestCloseOutcome outcome, CancellationToken ct)
    {
        if (outcome.Refusal == QuestCloseRefusal.None)
        {
            var answered = new List<QuestResponse>();
            foreach (var quest in outcome.Quests) answered.Add(await _answers.AnswerAsync(http, quest, ct));
            return new QuestCloseResponse(answered, outcome.Message);
        }

        // Each refusal names its check by a code (`key-reused`, `stale`, ...), which the driver keeps beside the sentence.
        var code = QuestCloseCodes.Of(outcome.Refusal);
        return outcome.Refusal switch
        {
            QuestCloseRefusal.Malformed => Refusal.BadRequest(outcome.Message, code),
            QuestCloseRefusal.NotFound => Refusal.NotFound(outcome.Message, code),
            // A check that refused on a quest's state: stale, held, closed, unbound or not this machine's; a key not that close's or
            // withdrawn. Nothing was written.
            _ => Refusal.Conflict(outcome.Message, code),
        };
    }

    private async Task<Answer<QuestActionResponse>> SetUpAsync(PostQuestSetUp request, HttpContext http, CancellationToken ct)
    {
        // Naming a session, the driver's post of what that session said; naming none, the person's own.
        if (host.Gate.Refused(http, PersonDoors.SetUp, form: request.Session is null) is { } refused) return refused;

        return await ReviewAnswerAsync(
            http,
            await Service.Exchange.PostSetUpAsync(
                request.Id, new QuestSetUpPost(request.Commit, request.Kind, request.Session, request.Look, request.Shows, request.Again),
                DateTimeOffset.UtcNow, ct),
            ct);
    }

    private async Task<Answer<QuestActionResponse>> ReviewAsync(ReviewQuest request, HttpContext http, CancellationToken ct)
    {
        // A set-up named by half its reference is refused, never read as none: none once meant the newest, which the person may not have seen.
        if (request.SetUp is { } half && (string.IsNullOrWhiteSpace(half.Machine) || half.Sequence is null))
        {
            return Refusal.BadRequest(
                "`setUp` names a set-up by both its `machine` and its `sequence`, as the quest's `setUps` answer them. Nothing was kept.");
        }

        return await ReviewAnswerAsync(
            http,
            await Service.Exchange.ReviewAsync(
                request.Id, request.Verdict, request.Words,
                request.SetUp is { Machine: { } machine, Sequence: { } sequence } ? new QuestOperationRef(machine, sequence) : null,
                DateTimeOffset.UtcNow, ct),
            ct);
    }

    // A state the review refuses is the lock's own shape, 409, and a shape is a bad request.
    private async Task<Answer<QuestActionResponse>> ReviewAnswerAsync(HttpContext http, QuestRespondOutcome outcome, CancellationToken ct) =>
        outcome.Refusal switch
        {
            QuestRespondRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            QuestRespondRefusal.NotFound => Refusal.NotFound(outcome.Message),
            QuestRespondRefusal.BadSetUp or QuestRespondRefusal.BadReviewVerdict => Refusal.BadRequest(outcome.Message),
            _ => Refusal.Conflict(outcome.Message),
        };

    private async Task<Answer<QuestActionResponse>> WorkflowAsync(ChooseQuestWorkflow request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Exchange.ChooseWorkflowAsync(request.Id, request.Kind, request.Workflow, request.Words, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => await _answers.MovedAsync(http, outcome.Quest!, outcome.Message, ct),
            QuestRespondRefusal.NotFound => Refusal.NotFound(outcome.Message),
            QuestRespondRefusal.BadWorkflow => Refusal.BadRequest(outcome.Message),
            _ => Refusal.Conflict(outcome.Message),
        };
    }

    private async Task<Answer<QuestActionResponse>> DismissAsync(DismissConflict request, HttpContext http, CancellationToken ct)
    {
        var quest = request.Id.TrimStart('#');
        var dismissal = await Service.Quests.DismissAsync(quest, request.Machine, request.Sequence, DateTimeOffset.UtcNow, ct);
        if (dismissal.Quest is null) return Refusal.NotFound($"quest `#{quest}` is not one this service holds.");

        return await _answers.MovedAsync(
            http, dismissal.Quest,
            dismissal.Dismissed switch
            {
                0 => $"quest `#{quest}` carries no such conflict — nothing to dismiss.",
                1 => $"Dismissed one conflict on quest `#{quest}`; every machine drops it on its next sync.",
                var count => $"Dismissed {count} conflicts on quest `#{quest}`; every machine drops them on its next sync.",
            },
            ct);
    }

    private async Task<Answer<DeletedResponse>> DeleteAsync(DeleteQuest request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Exchange.DeleteAsync(request.Id, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestDeleteRefusal.None => new DeletedResponse(outcome.Quest!.Id, outcome.Message),
            QuestDeleteRefusal.NotFound => Refusal.NotFound(outcome.Message),
            // Something stands on it, or a take won the race: a state conflict, the lock's own shape.
            _ => Refusal.Conflict(outcome.Message),
        };
    }
}
