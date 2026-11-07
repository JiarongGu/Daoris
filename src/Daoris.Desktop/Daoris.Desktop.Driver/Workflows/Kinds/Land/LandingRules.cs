using static Daoris.Driver.NodeGraphStarts;

namespace Daoris.Driver;

/// <summary>
/// The rules a graph's save holds about the landing that read every kind beside it, run once with the kinds'
/// own (<see cref="NodeKit.Problems"/>) and only where the graph is being saved by a person: a stored graph that read before is never made
/// unreadable by a rule added after it (<c>compatible-builds.md</c>).
/// </summary>
internal static class LandingRules
{
    /// <summary>What a save refuses of the landing in <paramref name="standing"/>'s graph: a <c>land</c> with no method chosen, and a kind that reads the landing with a way to it that passes none.</summary>
    public static IReadOnlyList<WireProblem> Problems(GraphStanding standing)
    {
        var problems = new List<WireProblem>();
        var (byId, local, starts, landings) = (standing.ById, standing.Local, standing.Starts, standing.Landings);
        foreach (var chosen in landings.Where(land => !land.Options.ContainsKey("method")))
        {
            problems.Add(new(GraphRefusal.LandMethod, null, chosen.Id, $"node `{chosen.Id}`: a `land` is saved with its method chosen: {string.Join(", ", LandNodes.Methods.SkipLast(1))} or {LandNodes.Methods[^1]}."));
        }

        if (landings.Count == 0) return problems;

        // Every kind that observes the landing record is refused where some way to it passes no `land`, since it would wait for ever
        // before a landing. A way is any wire from an entry, a failure's included, and a `land` is passed through whole: its own wires are not followed.
        var landed = landings.Select(landing => landing.Id).ToHashSet(StringComparer.Ordinal);
        var unlanded = Reach(starts, [.. local.Where(wire => !landed.Contains(wire.From!.Node))], forward: true);
        foreach (var node in byId.Values.Where(node => NodeKit.Of(node.Kind)?.ReadsTheLanding == true && unlanded.Contains(node.Id)))
        {
            var article = "aeiou".Contains(node.Kind[0]) ? "an" : "a";
            problems.Add(new(GraphRefusal.BeforeLanding, null, node.Id, $"node `{node.Id}`: {article} `{node.Kind}` observes the landing, and a way to it passes no "
                + $"`{landings[0].Kind}` ({string.Join(", ", landings.Select(landing => $"`{landing.Id}`"))}), so it would wait for ever before one."));
        }

        return problems;
    }
}
