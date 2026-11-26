namespace Daoris.Driver;

/// <summary>
/// The order a line's queued landings form in: the class of the run each belongs to (<see cref="PriorityBook.OfRun"/>, aged by
/// <see cref="QueueOrder.Age"/> from its arrival), then the queue's own order as <see cref="IntegrationStore.Queued"/> gives it (an <c>order</c>
/// moved first or last, else arrival), which the one comparator leaves equal. A landing whose run is held stands aside and returns, at the
/// place its arrival gives it, once the hold is lifted. A request of no quest (a records branch) is normal and is never held.
/// </summary>
internal static class LandingOrder
{
    /// <param name="queued">The line's queue in its own order.</param>
    /// <param name="askOf">The ask a request's run came from, where it has one.</param>
    public static IReadOnlyList<IntegrationRequest> Of(IReadOnlyList<IntegrationRequest> queued, PriorityLook look, Func<IntegrationRequest, string?> askOf)
    {
        if (queued.Count == 0) return queued;
        var placed = new List<(IntegrationRequest Request, QueuePlace Place)>();
        foreach (var request in queued)
        {
            var ask = request.Quest is { Length: > 0 } ? askOf(request) : null;
            if (request.Quest is { Length: > 0 } quest && look.IsHeld(quest, ask)) continue;
            var @class = request.Quest is { Length: > 0 } run ? PriorityBook.OfRun(look.Events, run, ask, look.Now).Class : PriorityClasses.Normal;
            var aged = QueueOrder.Age(@class, request.At, look.Now, look.Rules);
            placed.Add((request, new QueuePlace(aged.Class, QueueStage.Landing, request.At)));
        }

        return [.. QueueOrder.Stable(placed, each => each.Place).Select(each => each.Request)];
    }
}
