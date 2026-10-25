using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The cycles' edits as <c>driver.json</c> takes them (FABRIC-GRAPH1, §C2, §D7; FOUNDATION1 mod-iii): a landing rule's cycle and a
/// quest's or an ask's cycle of work, each judged by the graph's own rules before it is kept. They are extension methods of the
/// settings' <see cref="DriverConfig"/>, so each call reads as it did, and the graph's rules stay with the workflows.
/// </summary>
public static class CycleChoices
{
    /// <summary>
    /// Set the cycle on a repository's own landing rule (FABRIC-GRAPH1, §C2), or clear it with null to take the default, today's. The
    /// cycle rides its rule (D164's amendment), so a repository with no rule of its own is refused, and so is a cycle whose graph does
    /// not hold under its rule, naming the first problem.
    /// </summary>
    public static DriverConfig WithLandingCycle(this DriverConfig config, string repository, string? cycle) =>
        config with { Landings = SetCycle(config.Landings, repository, cycle, "repository") };

    /// <summary>Set the cycle on a workspace's landing rule, or clear it with null.</summary>
    public static DriverConfig WithWorkspaceLandingCycle(this DriverConfig config, string workspace, string? cycle) =>
        config with { WorkspaceLandings = SetCycle(config.WorkspaceLandings, workspace, cycle, "workspace") };

    private static IReadOnlyDictionary<string, LandingRule> SetCycle(IReadOnlyDictionary<string, LandingRule> map, string key, string? cycle, string what)
    {
        if (!map.TryGetValue(key.Trim(), out var rule))
        {
            throw new DriverException($"`{key.Trim()}` has no landing rule of its own, and a cycle rides its {what}'s rule (D164): set the rule first, then its cycle.");
        }

        var next = new Dictionary<string, LandingRule>(map, StringComparer.OrdinalIgnoreCase);
        if (cycle is null)
        {
            next[key.Trim()] = rule with { Cycle = null };
            return next;
        }

        var kept = rule with { Cycle = CompactOrRefuse(cycle) };
        if (NodeGraphHomes.OfLanding(kept, what).Problems is [var first, ..]) throw new DriverException(first.Words);
        next[key.Trim()] = kept;
        return next;
    }

    private static string CompactOrRefuse(string json)
    {
        try
        {
            return NodeGraphHomes.Compact(json);
        }
        catch (JsonException error)
        {
            throw new DriverException($"the cycle is not JSON: {error.Message}");
        }
    }

    /// <summary>
    /// Set a repository's cycle for a kind of work, or clear it with null to take its workspace's. A kind with no work cycle is refused,
    /// a branch's included (its cycle is its landing rule's, §D7), and so is a graph that does not hold, naming the first problem.
    /// </summary>
    public static DriverConfig WithWorkCycle(this DriverConfig config, string repository, string kind, string? graph) =>
        config with { WorkCycles = SetWorkCycle(config.WorkCycles, repository, kind, graph) };

    /// <summary>Set a workspace's cycle for a kind of work, or clear it with null.</summary>
    public static DriverConfig WithWorkspaceWorkCycle(this DriverConfig config, string workspace, string kind, string? graph) =>
        config with { WorkspaceWorkCycles = SetWorkCycle(config.WorkspaceWorkCycles, workspace, kind, graph) };

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SetWorkCycle(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map, string key, string kind, string? graph)
    {
        if (!NodeGraphHomes.WorkCycleKinds.Contains(kind))
        {
            throw new DriverException(kind == WorkUnitKind.Branch
                ? "a branch's cycle is its landing rule's `cycle`, so that a repository's landing and its end move together (D164); no kind has two homes."
                : $"`{kind}` is no kind of work with a cycle of its own here — {string.Join(" or ", NodeGraphHomes.WorkCycleKinds.Select(each => $"`{each}`"))}.");
        }

        var kinds = new Dictionary<string, string>(map.GetValueOrDefault(key.Trim()) ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        if (graph is null) kinds.Remove(kind);
        else
        {
            var kept = CompactOrRefuse(graph);
            if (NodeGraphs.Read(kept).Problems is [var first, ..]) throw new DriverException(first.Words);
            kinds[kind] = kept;
        }

        var next = new Dictionary<string, IReadOnlyDictionary<string, string>>(map, StringComparer.OrdinalIgnoreCase);
        var spelled = next.Keys.FirstOrDefault(name => string.Equals(name, key.Trim(), StringComparison.OrdinalIgnoreCase)) ?? key.Trim();
        next.Remove(spelled);
        if (kinds.Count > 0) next[spelled] = kinds;
        return next;
    }
}
