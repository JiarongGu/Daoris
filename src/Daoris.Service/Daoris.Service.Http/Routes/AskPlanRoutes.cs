using Daoris.Contracts.Asks;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// An ask's plan as the person reads it and acts on its delivery, a local host's alone like the ask's other doors: the read, which takes the
/// blobs the reader holds so a moved piece of evidence is named stale, and the person's answer to a question or waiver of a release order.
/// Writing the plan itself is the connector's <c>team_plan</c>.
/// </summary>
public sealed class AskPlanRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ReadAskPlan, AskPlanResponse>(async (request, _, ct) => await ReadAsync(request.Id, request.Seen, ct));
        routes.Map<SettleAskPlan, AskPlanResponse>(SettleAsync);
    }

    private async Task<AskPlanResponse> ReadAsync(string ask, IReadOnlyDictionary<string, string>? seen, CancellationToken ct)
    {
        var (plans, rows) = await Service.Teams.ReadAsync(ask, ct);
        var (members, decisions) = await Service.Teams.TeamAsync(ask, ct);
        return TeamAnswers.Plan(ask, plans, rows, members, decisions, seen);
    }

    private async Task<Answer<AskPlanResponse>> SettleAsync(SettleAskPlan request, HttpContext http, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        TeamOutcome outcome;
        if (!string.IsNullOrWhiteSpace(request.Deployable) || !string.IsNullOrWhiteSpace(request.On))
        {
            if (Ref(request.Deployable) is not { } deployable || Ref(request.On) is not { } on)
            {
                return Refusal.BadRequest("A waiver names both orders' deployables as `repository/id`: `deployable` and `on`.");
            }

            outcome = await Service.Teams.Settling.WaiveAsync(request.Id, deployable, on, request.Reason ?? "", now, ct);
        }
        else
        {
            outcome = await Service.Teams.Settling.AnswerAsync(request.Id, request.Key ?? "", request.Words ?? "", now, ct);
        }

        return outcome.Refusal switch
        {
            TeamRefusal.None => await ReadAsync(request.Id, null, ct),
            TeamRefusal.NotFound => Refusal.NotFound(outcome.Message),
            TeamRefusal.Closed or TeamRefusal.Moved => Refusal.Conflict(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    private static DeliveryRef? Ref(string? spelt)
    {
        var slash = spelt?.LastIndexOf('/') ?? -1;
        return slash > 0 && slash < spelt!.Length - 1 ? new(spelt[..slash], spelt[(slash + 1)..]) : null;
    }
}
