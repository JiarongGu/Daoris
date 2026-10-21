using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>workflow</c> kind's writer (WORKFLOW1h-a, D157, design §8.1): a whole version of a workflow, new or an edit on a
/// base version, for a repository or a workspace, and optionally its choice there. Its <c>steps</c> are written as nested
/// JSON, the first kind to carry any; since FABRIC-PIPELINE1i a verification pipeline's <c>graph</c> in their place, and a <c>route</c>
/// the choice is made for, which with neither chooses a saved workflow alone. Only the shape is judged here; the editor's validation, the base's staleness and what
/// the steps need are the driver's to judge. Changes, a kind and an import are WORKFLOW1h2's, so they are refused by name.
/// </summary>
/// <remarks><c>partial</c> for its generated <see cref="WorkflowIdShape"/> alone (PART7), and declared in this one file.</remarks>
internal sealed partial class HelpWorkflowProposalWriter(HelpProposalFile file)
{
    /// <summary>A workflow's id as the workflow store takes it: lower-case letters, digits and dashes, at most 40.</summary>
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowIdShape();

    /// <summary>
    /// Write one proposal of a workflow's whole version. <paramref name="steps"/> is the design's JSON list of step objects;
    /// <paramref name="baseVersion"/> the version an edit is made on, absent for a new workflow; <paramref name="choose"/> asks
    /// that the scope also use this workflow. <paramref name="name"/> (WORKFLOW1h-name) is the person's words for a new workflow,
    /// trimmed and written only when given; the workflow store keeps a name to 60 characters (the driver's
    /// <c>WorkflowNamed.NameProblem</c>), and an edit keeps its workflow's name, so a name beside a base is refused.
    /// </summary>
    public (string? Id, string Message) Propose(
        string? scope, string? workflow, int? baseVersion, string? steps, bool choose,
        string? changes, string? kind, string? import, string why, string? session, DateTimeOffset at, string? name = null,
        string? graph = null, string? route = null)
    {
        var scopeName = Blank(scope);
        var workflowId = Blank(workflow);
        var workflowName = Blank(name);
        var routeName = Blank(route);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var notYet = new (string Name, string? Value)[] { ("changes", changes), ("kind", kind), ("import", import) }
            .FirstOrDefault(field => Blank(field.Value) is not null).Name;

        JsonElement stepList = default;
        JsonElement graphObject = default;
        var hasSteps = Blank(steps) is not null;
        var hasGraph = Blank(graph) is not null;
        var refused =
            notYet is not null ? $"`{notYet}` is not proposed yet: propose the whole `steps`."
            : scopeName is null ? "a workflow proposal names its scope: the repository or workspace it applies to."
            : Word(scopeName, "a scope")
              ?? (workflowId is null ? "a workflow proposal names its workflow, by its id."
                 : !WorkflowIdShape().IsMatch(workflowId) || workflowId.Length > 40
                    ? $"`{workflowId}` is not a workflow's id — lower-case letters, digits and dashes, at most 40, such as `docs-to-pr`."
                 : workflowId == "current" ? "`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it."
                 : baseVersion is < 1 ? "the base version is a whole number from 1."
                 : workflowName is not null && baseVersion is not null ? "an edit keeps its workflow's name, so `name` is left out: renaming is not proposed."
                 : workflowName is { Length: > 60 } ? "`name` is a short line, at most 60 characters."
                 : routeName is not null && RouteProblem(routeName) is { } routeProblem ? routeProblem
                 : hasSteps && hasGraph ? "a workflow proposal carries its `steps` or its `graph`, never both."
                 : hasGraph ? Graph(graph, out graphObject)
                 : !hasSteps && routeName is not null ? null
                 : Steps(steps, out stepList));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "workflow");
            writer.WriteString("scope", scopeName);
            writer.WriteString("workflow", workflowId);
            if (workflowName is not null) writer.WriteString("name", workflowName);
            if (baseVersion is { } baseNumber) writer.WriteNumber("base", baseNumber);
            else writer.WriteNull("base");
            writer.WriteBoolean("choose", choose);
            // FABRIC-PIPELINE1i: a version is its steps or a verification pipeline's graph, and a route with neither chooses alone.
            if (routeName is not null) writer.WriteString("route", routeName);
            if (hasSteps)
            {
                writer.WritePropertyName("steps");
                stepList.WriteTo(writer);
            }
            else if (hasGraph)
            {
                writer.WritePropertyName("graph");
                graphObject.WriteTo(writer);
            }
        }, why, session, at);
    }

    /// <summary>
    /// Why a route is none a proposal names (FABRIC-PIPELINE1i), or null: <c>default</c>, <c>landed</c>, or <c>kind:&lt;id&gt;</c>, a kind's id
    /// as the driver takes it: lower-case letters, digits and dashes, at most 24.
    /// </summary>
    private static string? RouteProblem(string route) =>
        route is "default" or "landed"
        || (route.StartsWith("kind:", StringComparison.Ordinal) && route[5..] is { Length: > 0 and <= 24 } kind && WorkflowIdShape().IsMatch(kind))
            ? null
            : $"`{route}` is not a route: `default`, `landed` for the work that lands, or `kind:<id>` for one of the scope's kinds, such as `kind:docs`.";

    /// <summary>Why <c>graph</c> is not a JSON object, or null with the object; the driver's graph reader judges the rest.</summary>
    private static string? Graph(string? text, out JsonElement graph)
    {
        graph = default;
        try
        {
            using var document = JsonDocument.Parse(text!);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return "`graph` is the graph's JSON object, its nodes and wires.";
            graph = document.RootElement.Clone();
            return null;
        }
        catch (JsonException)
        {
            return "`graph` is not valid JSON: send it as the graph's JSON object.";
        }
    }

    /// <summary>Why <c>steps</c> is not a non-empty JSON list of objects, or null with the list.</summary>
    private static string? Steps(string? text, out JsonElement list)
    {
        list = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "a workflow proposal carries its `steps`, a JSON list of step objects, or a verification pipeline's `graph`; or, with "
                + "neither, the `route` it chooses a saved workflow for.";
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array) return "`steps` is a JSON list of step objects.";
            if (root.GetArrayLength() == 0) return "`steps` is a list of at least one step.";
            if (root.EnumerateArray().Any(step => step.ValueKind != JsonValueKind.Object)) return "each of `steps` is a step, a JSON object — not a bare value.";
            list = root.Clone();
            return null;
        }
        catch (JsonException)
        {
            return "`steps` is not valid JSON: send it as a JSON list of step objects.";
        }
    }
}
