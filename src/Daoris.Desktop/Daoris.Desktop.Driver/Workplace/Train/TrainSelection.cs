namespace Daoris.Driver;

/// <summary>
/// What the line's queue offers the next candidate where its requests are parts of an ask: only the ready ones, in the plan's landing
/// order. The queue stays the line's one queue; this reads it and answers a subset in an order.
/// </summary>
/// <remarks>
/// <para><b>Ready</b> is the plan's reading: every part it builds on (<c>after</c>) has landed. A part the plan does not name, and a part whose
/// plan is not read, waits, never guessed.</para>
/// <para><b>Order</b> is topological over the plan's <c>orders</c> edges among the ready parts; a tie, and a cycle the plan's judge already
/// flagged, keep the queue's arrival order. A request that is no part's keeps its place first.</para>
/// </remarks>
public sealed class TrainSelection(TrainParts parts, ITrainPlans plans)
{
    public async Task<IReadOnlyList<IntegrationRequest>> ReadyAsync(IReadOnlyList<IntegrationRequest> queued, CancellationToken ct)
    {
        var tags = parts.Of(queued.Select(request => request.Id));
        if (tags.Count == 0) return queued;

        var read = new Dictionary<string, (TrainPlan? Plan, IReadOnlySet<string> Landed)>(StringComparer.Ordinal);
        foreach (var ask in tags.Values.Select(tag => tag.Ask).Distinct(StringComparer.Ordinal))
        {
            read[ask] = (await plans.OfAsync(ask, ct).ConfigureAwait(false), parts.Landed(ask));
        }

        var ready = new List<(IntegrationRequest Request, TrainTag Tag, TrainPlanPart Part)>();
        foreach (var request in queued)
        {
            if (!tags.TryGetValue(request.Id, out var tag)) continue;
            var (plan, landed) = read[tag.Ask];
            if (plan?.Parts.FirstOrDefault(each => each.Id == tag.Part) is not { } part) continue;
            var named = plan.Parts.Select(each => each.Id).ToHashSet(StringComparer.Ordinal);
            if (part.After.Any(on => named.Contains(on) && !landed.Contains(on))) continue;
            ready.Add((request, tag, part));
        }

        return [.. queued.Where(request => !tags.ContainsKey(request.Id)), .. Ordered(ready)];
    }

    private static IEnumerable<IntegrationRequest> Ordered(List<(IntegrationRequest Request, TrainTag Tag, TrainPlanPart Part)> ready)
    {
        var left = ready.ToList();
        while (left.Count > 0)
        {
            // The first in arrival order that none of the others still left must land before it; a cycle yields the first by arrival.
            var next = left.FirstOrDefault(each => !left.Any(other =>
                !ReferenceEquals(other.Request, each.Request) && other.Tag.Ask == each.Tag.Ask && each.Part.Orders.Contains(other.Part.Id)));
            if (next.Request is null) next = left[0];
            left.Remove(next);
            yield return next.Request;
        }
    }
}
