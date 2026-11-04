using Daoris.Contracts.History;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// Clearing finished history from this machine, listed first and then pressed: the desk judges the records' half for both doors. A local
/// host's alone, as a person clears their own machine; a remote's history is the team's, so a shared host maps neither.
/// </summary>
public sealed class HistoryRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<PlanHistory, HistoryPlanResponse>(PlanAsync);
        routes.Map<ClearHistory, HistoryClearResponse>(ClearAsync);
    }

    // Every unit is answered, a refused one with its word. Deletes nothing.
    private async Task<Answer<HistoryPlanResponse>> PlanAsync(PlanHistory request, HttpContext http, CancellationToken ct)
    {
        var named = new[] { request.Workspace, request.Quest, request.Ask }.Count(scope => !string.IsNullOrWhiteSpace(scope));
        if (named != 1 || (request.Failed == true && string.IsNullOrWhiteSpace(request.Quest)))
        {
            return Refusal.BadRequest("name one scope: `workspace`, `quest` or `ask`, and `failed=true` only beside a quest");
        }

        IReadOnlyList<HistoryUnit> units = !string.IsNullOrWhiteSpace(request.Workspace)
            ? await Service.History.PlanWorkspaceAsync(request.Workspace, ct)
            : [await Service.History.PlanAsync(
                !string.IsNullOrWhiteSpace(request.Quest)
                    ? new HistoryUnitRef(request.Failed == true ? HistoryUnitKind.Failed : HistoryUnitKind.Quest, request.Quest)
                    : new HistoryUnitRef(HistoryUnitKind.Ask, request.Ask!),
                ct)];
        return new HistoryPlanResponse([.. units.Select(ToUnit)]);
    }

    // The second press: each unit judged again where it is cleared, and cleared or kept with its word.
    private async Task<Answer<HistoryClearResponse>> ClearAsync(ClearHistory request, HttpContext http, CancellationToken ct)
    {
        var units = new List<HistoryUnitRef>();
        foreach (var unit in request.Units ?? [])
        {
            if (HistoryUnitRef.Parse(unit?.Kind) is not { } kind || string.IsNullOrWhiteSpace(unit!.Id))
            {
                return Refusal.BadRequest("each unit names its `kind` (`quest`, `ask` or `failed`) and its `id`, as the listing gave them");
            }

            units.Add(new HistoryUnitRef(kind, unit.Id.Trim()));
        }

        if (units.Count == 0) return Refusal.BadRequest("`units` names what to clear, as the listing gave them; it named none");

        var outcomes = await Service.History.ClearAsync(units, DateTimeOffset.UtcNow, ct);
        return new HistoryClearResponse(
            [.. outcomes.Select(outcome => new HistoryClearedResponse(
                ToUnit(outcome.Unit), outcome.Cleared, outcome.Message,
                outcome.Failed.Any ? new HistoryFailedResponse(outcome.Failed.Quests, outcome.Failed.Asks) : null))]);
    }

    // Ids only, never a path or a title, and a refusal as its word beside the desk's sentence.
    private static HistoryUnitResponse ToUnit(HistoryUnit unit) => new(
        HistoryUnitRef.Spell(unit.Kind), unit.Id, unit.Workspace, unit.Clearable, unit.Quests, unit.Forgotten, unit.Asks,
        unit.Sessions, unit.Teammates, unit.Refusal is { } refusal ? ToRefusal(refusal) : null,
        [.. unit.Kept.Select(ToRefusal)]);

    private static HistoryRefusalResponse ToRefusal(HistoryKept kept) => new(
        HistoryKept.Spell(kept.Refusal), kept.Message, kept.Quest, kept.Ask, kept.Session, kept.Origin, kept.Workspace,
        kept.Waits is { } waits ? HistoryKept.Spell(waits) : null,
        kept.Stands is { } stands ? HistoryKept.Spell(stands) : null,
        kept.By is { } by ? HistoryKept.Spell(by) : null);
}
