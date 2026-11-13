using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Each declared deployable's release target, keyed by deployable: the release nodes whose values over a workflow the repository follows
/// (<see cref="WorkflowValuesKey"/>) name it under <c>deployable</c>, and the earliest act that deploys into it, <c>deploysOn</c>.
/// </summary>
/// <remarks>
/// Daoris reads these two names alone. The target's own ids (a pipeline, a definition, a stage) are the release step's plugin's to resolve
/// and to hold on, listing what it finds; Daoris holds no knowledge of them. An undeclared <c>deploysOn</c> is read as <c>push</c>, the
/// earliest act, so every act that could expose the deployable waits for it.
/// </remarks>
public static class DeployableTargets
{
    /// <summary>The option naming the deployable a node releases.</summary>
    public const string DeployableOption = "deployable";

    /// <summary>The option naming the earliest act that deploys into the node's target.</summary>
    public const string DeploysOnOption = "deploysOn";

    /// <summary>The acts that can deploy into a target, earliest first.</summary>
    public static readonly string[] Acts = ["push", "pull-request", "merge", "start"];

    /// <summary>The act an undeclared <c>deploysOn</c> is read as.</summary>
    public const string Earliest = "push";

    /// <summary>Each of <paramref name="declared"/>'s deployables with the nodes of <paramref name="values"/> that name it.</summary>
    /// <param name="values">The repository's values over its workflows; null where it keeps none, so every target is undeclared.</param>
    public static DeployableTargetsRead Judge(DeclaredDeployables declared, WorkflowValuesKey.Values? values)
    {
        var problems = new List<string>();
        var named = new List<(string Deployable, DeployableNode Node)>();
        foreach (var (workflow, nodes) in values?.ByWorkflow ?? new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>())
        {
            foreach (var (node, options) in nodes)
            {
                if (!options.TryGetValue(DeployableOption, out var json) || Text(json) is not { } deployable) continue;
                if (!declared.Items.Any(each => string.Equals(each.Id, deployable, StringComparison.Ordinal)))
                {
                    problems.Add($"{workflow}/{node} names deployable `{deployable}`, which {DeclarationNames.Manifest} does not declare");
                    continue;
                }

                var act = options.TryGetValue(DeploysOnOption, out var actJson) ? Text(actJson) : null;
                var known = act is not null && Acts.Contains(act, StringComparer.Ordinal);
                if (act is not null && !known)
                {
                    problems.Add($"{workflow}/{node} declares deploysOn `{act}`, which is none of push, pull-request, merge or start, so it is read as push");
                }

                named.Add((deployable, new DeployableNode(workflow, node, known ? act! : Earliest, known)));
            }
        }

        var targets = declared.Items.Select(deployable =>
        {
            var nodes = named.Where(each => each.Deployable == deployable.Id).Select(each => each.Node).ToList();
            var standing = nodes.Count == 0 ? DeployableStanding.Undeclared
                : nodes.GroupBy(each => each.Workflow, StringComparer.Ordinal).Any(same => same.Count() > 1) ? DeployableStanding.Several
                : DeployableStanding.Named;
            return new DeployableTarget(deployable, nodes, standing);
        }).ToList();
        return new(targets, problems);
    }

    /// <summary>An option's JSON value as text, where it is a string with words.</summary>
    private static string? Text(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.String && document.RootElement.GetString()!.Trim() is { Length: > 0 } text ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
