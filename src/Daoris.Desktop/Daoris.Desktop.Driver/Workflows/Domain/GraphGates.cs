using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a graph's gate nodes say, read from their options: the look, the second opinion and the sessions' options.</summary>
/// <param name="Problem">The first option that does not read, a sentence after the node's id; null where all read.</param>
public sealed record GraphGateReading(
    WorkflowLook Look, ResolvedOpinion? Opinion, string? OpinionCannot, IReadOnlyDictionary<string, string> Claimed, string? Problem);

/// <summary>
/// A graph's gates read straight from its nodes' options, without turning the graph back into
/// steps: each option is read as the node of its kind reads that field, absence as its default, and each chooses among what the
/// repository declares, never beyond it. A bad option holds the run, naming the node.
/// </summary>
public static class GraphGates
{
    /// <summary>Reads <paramref name="graph"/>'s <c>claimed</c>, <c>opinion</c> and <c>look</c> over <paramref name="declared"/>, the rules as they stand.</summary>
    public static GraphGateReading Of(NodeGraph graph, WorkflowProcess declared)
    {
        var claimed = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Claimed);
        var opinionNode = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Opinion);
        var lookNode = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Look);
        var none = new WorkflowLook(null, null);
        var claims = claimed?.Options ?? new Dictionary<string, string>();

        if (claimed is not null && Read(claimed, WorkflowKinds.Work) is { Problem: { } unread }) return new(none, null, null, claims, unread);

        var look = none;
        if (lookNode is not null)
        {
            var read = Read(lookNode, WorkflowKinds.Look)!;
            if (read.Problem is not null) return new(none, null, null, claims, read.Problem);
            look = WorkflowProcess.ReadLook(declared, declared.Repository, lookNode.Id, (string?)read.Values["environment"]) with { Node = lookNode.Id };
        }

        ResolvedOpinion? opinion = null;
        string? cannot = null;
        if (opinionNode is not null)
        {
            var read = Read(opinionNode, WorkflowKinds.Opinion)!;
            if (read.Problem is not null) return new(look, null, null, claims, read.Problem);
            (opinion, cannot) = WorkflowProcess.ReadOpinion(
                declared, declared.Repository, opinionNode.Id, read.Values["reviewers"] as IReadOnlyList<string>, (bool)read.Values["steps"]!,
                (bool)read.Values["required"]!, (bool)read.Values["recheck"]!);
        }

        return new(look, opinion, cannot, claims, null);
    }

    private sealed record Fields(IReadOnlyDictionary<string, object?> Values, string? Problem);

    private static Fields? Read(NodeDeclaration node, string kind)
    {
        var values = new Dictionary<string, object?>();
        foreach (var field in WorkflowKindTable.Of(kind)!.Fields)
        {
            JsonElement? raw = null;
            if (node.Options.TryGetValue(field.Name, out var text))
            {
                try
                {
                    using var document = JsonDocument.Parse(text);
                    raw = document.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return new(values, $"node `{node.Id}`: its `{field.Name}` is not a JSON value.");
                }
            }

            var (value, problem) = WorkflowNamed.ReadField(field, raw);
            if (problem is not null) return new(values, $"node `{node.Id}`: {problem}");
            values[field.Name] = value;
        }

        return new(values, null);
    }
}
