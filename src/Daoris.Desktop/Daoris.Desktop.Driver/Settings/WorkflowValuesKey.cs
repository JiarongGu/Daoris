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

    /// <summary>The key the reasons are kept under, beside the values: by <c>repository/workflow/node</c>, the reason the values were last set.</summary>
    public const string WhyKey = "workflowValuesWhy";

    /// <summary>Where a node's reason is kept: its repository, workflow and node, as one key.</summary>
    public static string WhyOf(string repository, string workflow, string node) => $"{repository.Trim()}/{workflow}/{node}";

    /// <summary>
    /// The one door's judge (RELEASEVALUES1): a node's values set, each named option's JSON value laid over what it kept, or the node's
    /// values taken away with <paramref name="values"/> null, and the reason recorded beside them. The window's route and the
    /// <c>daoris workflow values</c> verb both call it before anything is written, so they refuse alike.
    /// </summary>
    /// <exception cref="DriverException">The refusal, nothing changed: no repository, workflow or node, a value that is no JSON, or no reason.</exception>
    public static DriverConfig Apply(DriverConfig config, string? repository, string? workflow, string? node, IReadOnlyDictionary<string, string>? values, string? why)
    {
        var named = repository?.Trim() ?? "";
        if (named.Length == 0) throw new DriverException("a workflow's values are set for a repository: name one.");
        if (workflow is null || !WorkflowKey.IsId(workflow)) throw new DriverException($"`{workflow}` is not a workflow's id.");
        if (string.IsNullOrWhiteSpace(node)) throw new DriverException("a workflow's values are set on one of its nodes: name its id.");
        if (string.IsNullOrWhiteSpace(why)) throw new DriverException("say why the values are set (`--why`): the reason is recorded with them.");
        foreach (var (name, json) in values ?? new Dictionary<string, string>())
        {
            if (name.Length == 0) throw new DriverException("an option's name is empty.");
            try
            {
                using var _ = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                throw new DriverException($"`{name}`'s value `{json}` is no JSON: a number, `true`, or a word in quotes.");
            }
        }

        var map = new Dictionary<string, Values>(config.WorkflowValues, StringComparer.OrdinalIgnoreCase);
        var workflows = map.TryGetValue(named, out var kept) ? new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(kept.ByWorkflow, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        var nodes = workflows.TryGetValue(workflow, out var held) ? new Dictionary<string, IReadOnlyDictionary<string, string>>(held, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        if (values is null) nodes.Remove(node);
        else nodes[node] = (nodes.TryGetValue(node, out var before) ? before : ImmutableSortedDictionary<string, string>.Empty)
            .ToImmutableSortedDictionary(StringComparer.Ordinal).SetItems(values);
        if (nodes.Count > 0) workflows[workflow] = nodes;
        else workflows.Remove(workflow);
        if (workflows.Count > 0) map[named] = new Values(workflows);
        else map.Remove(named);

        var reasons = new Dictionary<string, string>(config.WorkflowValuesWhy, StringComparer.OrdinalIgnoreCase) { [WhyOf(named, workflow, node)] = why.Trim() };
        return config with { WorkflowValues = map, WorkflowValuesWhy = reasons };
    }

    /// <summary>The reasons as <c>driver.json</c> keeps them: each a string; anything else passed over.</summary>
    internal static IReadOnlyDictionary<string, string> WhyMap(JsonElement root) =>
        root.TryGetProperty(WhyKey, out var element) && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String)
                .GroupBy(each => each.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(each => each.Key, each => each.First().Value.GetString()!, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The reasons written, only when any is kept, in ordinal order.</summary>
    internal static void WriteWhy(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> reasons)
    {
        if (reasons.Count == 0) return;
        writer.WriteStartObject(WhyKey);
        foreach (var (key, why) in reasons.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, why);
        writer.WriteEndObject();
    }
}
