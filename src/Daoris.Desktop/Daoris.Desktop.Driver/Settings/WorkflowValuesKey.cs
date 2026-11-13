using System.Collections.Immutable;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A repository's option values over the workflows it follows, as <c>driver.json</c> keeps them, <c>workflowValues</c>: by repository,
/// by workflow id, by node id, each option's JSON value. They are laid over that node's own options wherever a run of that repository
/// bound to that workflow reads it, so one workflow a workspace shares serves each repository with its own values (a release step's
/// target, say), never a copy of the workflow per repository. Its own key, so naming values never makes a repository name a workflow.
/// </summary>
public static class WorkflowValuesKey
{
    /// <summary>The file's key.</summary>
    public const string Key = "workflowValues";

    /// <summary>A repository's values: by workflow, by node, each option's JSON text.</summary>
    public sealed record Values(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ByWorkflow);

    /// <summary>
    /// The map as <c>driver.json</c> holds it: a blank repository, a node that is no object and an entry left with no values passed over;
    /// a repository written twice in any case read where first readable; none where the key is absent or no object.
    /// </summary>
    internal static IReadOnlyDictionary<string, Values> Map(JsonElement root)
    {
        var map = new Dictionary<string, Values>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var repository in element.EnumerateObject())
        {
            var named = repository.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || repository.Value.ValueKind != JsonValueKind.Object) continue;
            var workflows = new SortedDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
            foreach (var workflow in repository.Value.EnumerateObject())
            {
                if (!WorkflowKey.IsId(workflow.Name) || workflow.Value.ValueKind != JsonValueKind.Object) continue;
                var nodes = new SortedDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                foreach (var node in workflow.Value.EnumerateObject())
                {
                    if (node.Name.Length == 0 || node.Value.ValueKind != JsonValueKind.Object) continue;
                    var options = node.Value.EnumerateObject().Where(option => option.Name.Length > 0)
                        .ToImmutableSortedDictionary(option => option.Name, option => option.Value.GetRawText(), StringComparer.Ordinal);
                    if (options.Count > 0) nodes[node.Name] = options;
                }

                if (nodes.Count > 0) workflows[workflow.Name] = nodes;
            }

            if (workflows.Count > 0) map[named] = new Values(workflows);
        }

        return map;
    }

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, each level in ordinal order.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, Values> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, values) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(repository);
            foreach (var (workflow, nodes) in values.ByWorkflow.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(workflow);
                foreach (var (node, options) in nodes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartObject(node);
                    foreach (var (name, json) in options.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(name);
                        writer.WriteRawValue(json);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>The values <paramref name="repository"/> keeps for node <paramref name="node"/> of <paramref name="workflow"/>, or null where it keeps none.</summary>
    public static IReadOnlyDictionary<string, string>? Of(IReadOnlyDictionary<string, Values> map, string repository, string workflow, string node) =>
        map.TryGetValue(repository.Trim(), out var values) && values.ByWorkflow.TryGetValue(workflow, out var nodes)
            && nodes.TryGetValue(node, out var options) ? options : null;
}
