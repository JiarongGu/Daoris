namespace Daoris.Driver;

/// <summary>A graph's step that the kind of a department's place cannot run, by the places its kind declares (<see cref="INodeKind.Places"/>).</summary>
public static class PlaceKindUnserved
{
    /// <summary>
    /// Why <paramref name="graph"/> cannot bind a department of kind <paramref name="place"/>, or null: its first step, in the graph's order, whose
    /// kind does not run there. A place whose kind is not known (null) binds anything, as a repository the registry does not answer for.
    /// </summary>
    public static BindingProblem? Of(NodeGraph graph, string? place, string workflow, int version)
    {
        if (PlaceKind.Of(place) is not { } kind) return null;
        foreach (var node in graph.Nodes)
        {
            if (NodeKit.Of(node.Kind)?.Places is { } serves && !serves.Contains(kind, StringComparer.Ordinal))
            {
                return BindingProblem.ForPlaceKind(workflow, version, node.Id, node.Kind, kind, serves);
            }
        }

        return null;
    }
}
