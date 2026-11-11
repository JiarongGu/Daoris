using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A saved version naming a renamed plugin's step by its old id (<c>&lt;old&gt;/&lt;step&gt;</c>) read as the step of the id the
/// plugin has now, from the installed manifests' old ids (<see cref="PluginFormerly.Of"/>). The file stays as written, since a
/// version is never edited, and the version keeps the digest of its saved graph, so a run pinned to it reads it at the identity its
/// binding holds and its records hold. A version the person saves next names the step as the editor shows it.
/// </summary>
/// <remarks>The condition that ends a former kind: no installed plugin declares the id it names, after which it binds as any plugin step
/// no plugin performs does.</remarks>
public static class PluginFormerKinds
{
    /// <summary>A workflow read with each version's former plugin kinds as the plugins' kinds now; the read itself where none is.</summary>
    public static NamedWorkflowRead Read(NamedWorkflowRead read, Func<IReadOnlyDictionary<string, string>> formerly)
    {
        if (!read.Versions.Any(version => version.Graph?.Nodes.Any(node => NodeKit.IsPlugin(node.Kind)) == true)) return read;
        var now = formerly();
        if (now.Count == 0) return read;
        return read with
        {
            Versions = [.. read.Versions.Select(version => version.Graph is { } graph && Renamed(graph, now) is { } renamed ? version with { Graph = renamed } : version)],
        };
    }

    /// <summary>A step's kind under the id now held by the plugin whose old id is <c>&lt;old&gt;</c>; any other kind as it is.</summary>
    public static string KindOf(string kind, IReadOnlyDictionary<string, string> formerly)
    {
        if (!NodeKit.IsPlugin(kind)) return kind;
        var slash = kind.IndexOf('/');
        return formerly.TryGetValue(kind[..slash], out var now) ? PluginSteps.KindOf(now, kind[(slash + 1)..]) : kind;
    }

    /// <summary>
    /// A saved graph's own text, as the editor and <c>workflow show</c> are handed it, a copy with each node's kind as
    /// <paramref name="read"/> holds it, which <see cref="Read"/> made the kind now: the file untouched, the new id's step seen with no save.
    /// </summary>
    public static JsonNode Drawn(JsonNode raw, NodeGraph read)
    {
        var drawn = raw.DeepClone();
        if (drawn["nodes"] is not JsonArray nodes) return drawn;
        var kinds = read.Nodes.GroupBy(node => node.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Kind, StringComparer.Ordinal);
        foreach (var node in nodes.OfType<JsonObject>())
        {
            if (node["id"] is JsonValue id && id.TryGetValue<string>(out var named) && kinds.TryGetValue(named, out var now)
                && node["kind"] is JsonValue value && value.TryGetValue<string>(out var kind) && NodeKit.IsPlugin(kind) && now != kind)
            {
                node["kind"] = now;
            }
        }

        return drawn;
    }

    private static NodeGraph? Renamed(NodeGraph graph, IReadOnlyDictionary<string, string> formerly) =>
        graph.Nodes.Any(node => KindOf(node.Kind, formerly) != node.Kind)
            ? graph with { Nodes = [.. graph.Nodes.Select(node => node with { Kind = KindOf(node.Kind, formerly) })] }
            : null;
}
