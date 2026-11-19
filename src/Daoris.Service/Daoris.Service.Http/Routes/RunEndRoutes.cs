using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The driver's doors on a workflow run's quests, a local host's alone: a run is this machine's, and so are its sessions. The bind makes
/// the agent's done a submission; the run's end writes the quests' Done and ends its waiting sessions in one write.
/// </summary>
public sealed class RunEndRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<BindQuestToRun, RunBindResponse>(BindAsync);
        routes.Map<EndRun, RunEndResponse>(EndAsync);
    }

    private async Task<Answer<RunBindResponse>> BindAsync(BindQuestToRun request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Run)) return Refusal.BadRequest("A bind names the run that closes the quest.");
        var move = await Service.Quests.BindAsync(request.Id, request.Run.Trim(), DateTimeOffset.UtcNow, ct);
        if (move.Quest is null) return Refusal.NotFound($"No quest `#{request.Id.TrimStart('#')}`.");
        return move.Moved
            ? new RunBindResponse(true, $"Quest `#{move.Quest.Id}` is bound to run `{request.Run.Trim()}`: its agent's done is a submission.")
            : new RunBindResponse(false, move.Quest.Closure is { } bound
                ? $"Quest `#{move.Quest.Id}` is bound already, to `{bound}`."
                : $"Quest `#{move.Quest.Id}` is {move.Quest.Status}{(move.ClaimLost ? " on another machine's take" : "")}, so it was not bound.");
    }

    private async Task<Answer<RunEndResponse>> EndAsync(EndRun request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.State)) return Refusal.BadRequest("A run's end names the state its sessions end in.");
        var named = (request.Quests ?? []).Select(each => (each.Id.TrimStart('#'), new QuestOperationRef(each.Machine, each.Sequence))).ToList();
        var passed = (request.Quests ?? []).Where(each => each.Passed).Select(each => each.Id.TrimStart('#')).ToList();
        var outcome = await Service.Ledger.EndRunAsync(
            request.Run, named, request.Sessions ?? [], request.State.Trim(), request.Note,
            request.Note is null ? null : NoteParts.Normalize(request.NoteParts), DateTimeOffset.UtcNow, ct, passed);
        return new RunEndResponse(
            outcome.Refusal == QuestCloseRefusal.None ? null : outcome.Refusal.ToString(), outcome.Message,
            [.. outcome.Closed.Select(quest => quest.Id)], [.. outcome.Ended.Select(session => session.Id)]);
    }
}
