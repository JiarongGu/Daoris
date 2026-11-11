namespace Daoris.Driver;

/// <summary>
/// Where a graph's effects (<see cref="IWritesWork"/>) stand: where it holds a <c>review</c>, every way to an effect passes one and the last
/// review on every way is the same, the one whose record the effect is fed (<see cref="Fed"/>); and its end is a node no effect lies past, so
/// the work's outcome is read after what carried it out. A landing's own order before it is <see cref="ProtectedOrder"/>'s.
/// </summary>
internal static class EffectOrder
{
    /// <summary>What the order refuses in <paramref name="standing"/>'s graph, ending at <paramref name="end"/>, each said against its effect.</summary>
    public static IEnumerable<WireProblem> Problems(GraphStanding standing, string? end)
    {
        var effects = standing.ById.Values.Where(Writes).ToList();
        if (end is not null && standing.ById.ContainsKey(end))
        {
            var past = NodeGraphStarts.Reach([end], standing.Ordered, forward: true);
            foreach (var effect in effects.Where(effect => effect.Id != end && past.Contains(effect.Id)))
            {
                yield return new WireProblem(GraphRefusal.EffectPastEnd, null, effect.Id,
                    $"node `{effect.Id}`: the graph ends at `{end}`, and `{effect.Id}` (`{effect.Kind}`) writes the work after it: a graph ends where no step writing the work lies past its end.");
            }
        }

        if (!standing.ById.Values.Any(Reviews)) yield break;
        var unreviewed = NodeGraphStarts.StartsWithout(standing.ById, standing.Starts, standing.Local, Reviews);
        foreach (var effect in effects)
        {
            if (effect.Kind != NodeKinds.Land && unreviewed.Contains(effect.Id))
            {
                yield return new WireProblem(GraphRefusal.ReviewOrder, null, effect.Id,
                    $"node `{effect.Id}`: a way reaches it around the review, and a `{effect.Kind}` writes the work the review accepted: every way to it passes a `{NodeKinds.Review}`.");
            }
            else if (Last(standing, effect.Id) is { Count: > 1 } feeding)
            {
                yield return new WireProblem(GraphRefusal.ReviewAmbiguous, null, effect.Id,
                    $"node `{effect.Id}`: {string.Join(" and ", feeding.Select(review => $"`{review}`"))} each feed it by a way of their own, so which accepted record it writes "
                    + "is not one: let one review come last on every way to it.");
            }
        }
    }

    /// <summary>The review whose record <paramref name="effect"/> is fed: the last on every way to it; null where the graph holds none, or no one review is last.</summary>
    public static string? Fed(NodeGraph graph, string effect) => Last(GraphStanding.Of(graph), effect) is [var review] ? review : null;

    /// <summary>Each review that reaches <paramref name="effect"/> by a way passing no other review after it.</summary>
    private static List<string> Last(GraphStanding standing, string effect)
    {
        var last = new List<string>();
        foreach (var review in standing.ById.Values.Where(Reviews))
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>([review.Id]);
            while (queue.TryDequeue(out var node))
            {
                foreach (var wire in standing.Ordered.Where(wire => wire.From!.Node == node))
                {
                    if (reached.Add(wire.To.Node) && !Reviews(standing.ById[wire.To.Node])) queue.Enqueue(wire.To.Node);
                }
            }

            if (reached.Contains(effect)) last.Add(review.Id);
        }

        return last;
    }

    private static bool Writes(NodeDeclaration node) => NodeKit.Of(node.Kind) is IWritesWork;

    private static bool Reviews(NodeDeclaration node) => node.Kind == NodeKinds.Review;
}
